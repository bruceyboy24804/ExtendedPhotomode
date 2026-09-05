import { useValue } from "cs2/api";
import { Scrollable, Tooltip } from "cs2/ui";
import { type ReactElement, useCallback, useState } from "react";
import { VC, VF, VT } from "vanilla/Components";
import {
    ShotListSortable,
    useShotDragActive,
    useShotDropZone,
    useShotSortable,
    useShotTrashZone,
} from "./shot-dnd";
import {
    type PathNumbers,
    ShotTypes,
    numbersBinding,
    setNumber,
} from "./path-bindings";
import { SequenceLibrary } from "./sequence-library";
import styles from "./shot-list-panel.module.scss";
import {
    type ShotListEntry,
    editShot,
    loadShot,
    setShotNumber,
    renameShot,
    shotsBinding,
    setTimeline,
    timelineBinding,
} from "./shot-bindings";

/** Unified Icon Library, which the mod already declares as a dependency in PublishConfiguration. */
const kPencil = "coui://uil/Standard/Pencil.svg";

/** A labelled row, so the timeline controls line up down the panel. */
function Row({
    label,
    children,
}: {
    readonly label: string;
    readonly children: ReactElement;
}): ReactElement {
    return (
        <div className={styles.row}>
            <span className={styles.rowLabel}>{label}</span>
            {children}
        </div>
    );
}

/**
 * An on/off control, drawn as the game's own checkbox.
 *
 * A checkbox rather than the toggle-image tool button it used to be. These rows are named settings
 * with a yes/no answer — "Chain onto existing", "Constant speed" — which is what a checkbox is for
 * and what the game uses everywhere else for the same question. The tool button read as an action
 * you press, and worse, it was carrying its own state in a swapped-out icon rather than in the
 * widget, so the on and off states looked like two different controls.
 *
 * `VC.Checkbox` comes from ModsCommon's vanilla resolver — the real
 * `game-ui/common/input/toggle/checkbox/checkbox.tsx`, not an imitation of it.
 */
function Toggle({
    on,
    tooltip,
    onSelect,
}: {
    readonly on: boolean;
    readonly tooltip: string;
    readonly onSelect: () => void;
}): ReactElement {
    return (
        <VC.Checkbox
            checked={on}
            tooltip={tooltip}
            focusKey={VF.FOCUS_DISABLED}
            className={VT.checkbox?.label}
            onChange={onSelect}
        />
    );
}

/**
 * A number with a step either side.
 *
 * Not a slider: these are exact values people know the number for — a two second gap, a thirty
 * second shot — and the panel is a floating window rather than the narrow tool options strip, so
 * there is room to show the figure plainly.
 */
function Stepper({
    value,
    suffix,
    min,
    max,
    step: amount = 1,
    onChange,
}: {
    readonly value: number;
    readonly suffix?: string;
    readonly min: number;
    readonly max: number;
    /** How much one press moves the value. Matches the tool options' step for the same setting, so
        a click means the same thing in both places. */
    readonly step?: number;
    readonly onChange: (value: number) => void;
}): ReactElement {
    const step = (delta: number): void =>
        onChange(Math.min(Math.max(value + delta * amount, min), max));

    // The same arrow-button-and-field arrangement the tool options point stepper uses, built from the
    // same vanilla widgets rather than imitated, so the two genuinely match instead of nearly doing.
    return (
        <div className={styles.stepper}>
            <VC.ToolButton
                src="coui://uil/Standard/ArrowLeft.svg"
                disabled={value <= min}
                tooltip="Less"
                focusKey={VF.FOCUS_DISABLED}
                className={VT.toolButton?.button}
                onSelect={() => step(-1)}
            />

            <div className={VT.mouseToolOptions?.numberField}>
                {`${Math.round(value * 10) / 10}${suffix ?? ""}`}
            </div>

            <VC.ToolButton
                src="coui://uil/Standard/ArrowRight.svg"
                disabled={value >= max}
                tooltip="More"
                focusKey={VF.FOCUS_DISABLED}
                className={VT.toolButton?.button}
                onSelect={() => step(1)}
            />
        </div>
    );
}

/**
 * How each shot type labels and bounds its key-density row.
 *
 * A dolly counts KEYS while the other two space them along something, so the label changes rather
 * than one wording being stretched to cover both. Ranges match the tool options' sliders — the same
 * setting behaving differently depending on which panel you reached it from is worse than not
 * offering it twice.
 */
const TIMING_FOR: Record<number, {
    duration: { min: number; max: number };
    spacing: { label: string; suffix?: string; min: number; max: number; step: number };
}> = {
    [ShotTypes.Orbit]: {
        duration: { min: 5, max: 300 },
        spacing: { label: "Key every", suffix: "°", min: 5, max: 90, step: 5 },
    },
    [ShotTypes.DollyZoom]: {
        duration: { min: 1, max: 120 },
        spacing: { label: "Keyframes", min: 2, max: 120, step: 1 },
    },
    [ShotTypes.Path]: {
        duration: { min: 5, max: 300 },
        spacing: { label: "Key every", suffix: "m", min: 5, max: 200, step: 5 },
    },
};

/**
 * The two settings that get changed most while building a cut: how long the shot runs, and how
 * densely it is keyed.
 *
 * Repeated here rather than only in the tool options because those live on a different panel, and
 * dialling a duration meant leaving the list, changing it, and coming back to regenerate. Both read
 * and write the same `pathNumbers` binding the tool options use, so the two panels cannot disagree.
 *
 * Ranges and steps are copied from the tool options deliberately — the same setting behaving
 * differently depending on which panel you reached it from is worse than not offering it twice.
 */
function ShotTiming({
    numbers,
    shot,
}: {
    readonly numbers: PathNumbers;
    /** The selected shot, when one is selected. */
    readonly shot?: ShotListEntry;
}): ReactElement | null {
    // With a shot selected the rows describe THAT shot and write back to it, so the timeline moves
    // as the value changes. With nothing selected they describe the next shot to be generated, which
    // is what the tool options edit. Same two rows either way — what differs is which shot they mean.
    if (shot) {
        const timing = TIMING_FOR[shot.type] ?? TIMING_FOR[ShotTypes.Path];

        return (
            <>
                <Row label="Shot duration">
                    <Stepper value={shot.duration} suffix="s"
                             min={timing.duration.min} max={timing.duration.max}
                             onChange={(v) => setShotNumber(shot.id, "duration", v)} />
                </Row>

                <Row label={timing.spacing.label}>
                    <Stepper value={shot.spacing} suffix={timing.spacing.suffix}
                             min={timing.spacing.min} max={timing.spacing.max}
                             step={timing.spacing.step}
                             onChange={(v) => setShotNumber(shot.id, "spacing", v)} />
                </Row>
            </>
        );
    }

    if (numbers.shotType === ShotTypes.Orbit) {
        return (
            <>
                <Row label="Shot duration">
                    <Stepper value={numbers.orbitDuration} suffix="s" min={5} max={300}
                             onChange={(v) => setNumber("orbitDuration", v)} />
                </Row>

                <Row label="Key every">
                    <Stepper value={numbers.orbitSpacing} suffix="°" min={5} max={90} step={5}
                             onChange={(v) => setNumber("orbitSpacing", v)} />
                </Row>
            </>
        );
    }

    if (numbers.shotType === ShotTypes.DollyZoom) {
        return (
            <>
                <Row label="Shot duration">
                    <Stepper value={numbers.dollyDuration} suffix="s" min={1} max={120}
                             onChange={(v) => setNumber("dollyDuration", v)} />
                </Row>

                {/* A dolly is keyed by COUNT, not by spacing — there is no distance to space along,
                    so the row says "Keyframes" rather than pretending to be the same control. */}
                <Row label="Keyframes">
                    <Stepper value={numbers.dollyKeys} min={2} max={120}
                             onChange={(v) => setNumber("dollyKeys", v)} />
                </Row>
            </>
        );
    }

    return (
        <>
            <Row label="Shot duration">
                <Stepper value={numbers.pathDuration} suffix="s" min={5} max={300}
                         onChange={(v) => setNumber("pathDuration", v)} />
            </Row>

            <Row label="Key every">
                <Stepper value={numbers.pathSpacing} suffix="m" min={5} max={200} step={5}
                         onChange={(v) => setNumber("pathSpacing", v)} />
            </Row>
        </>
    );
}

/**
 * One row in the list.
 *
 * Its own component because `useSortable` is a hook and cannot be called inside a map. Rows carry
 * their GLOBAL index — the list is globally ordered, and that is the position `dropShot` expects.
 */
function ShotRow({
    shot,
    index,
    selected,
    onSelect,
}: {
    readonly shot: ShotListEntry;
    readonly index: number;
    readonly selected: boolean;
    readonly onSelect: () => void;
}): ReactElement {
    const sortable = useShotSortable({
        shotId: shot.id,
        globalIndex: index,
        inSequence: shot.inSequence,
        label: shot.name,
        kind: "row",
    });

    // Renaming in place. The field only exists while editing, so the row reads as a row until you
    // ask it to be something else — no boxes sitting around looking editable.
    const [editing, setEditing] = useState(false);
    const [draft, setDraft] = useState(shot.name);

    const typeName = TYPE_NAMES[shot.type] ?? "Shot";

    const beginEdit = (event: React.MouseEvent) => {
        // Without this the row's own double-click handler loads the shot at the same time.
        event.stopPropagation();
        setDraft(shot.name);
        setEditing(true);
    };

    const commit = () => {
        const trimmed = draft.trim();

        // An empty name is a no-op on the C# side too, so clearing the field cancels rather than
        // leaving a nameless row. The type shows through as the placeholder while it is empty.
        if (trimmed.length > 0 && trimmed !== shot.name) {
            renameShot(shot.id, trimmed);
        }

        setEditing(false);
    };

    // useCallback with no deps so React runs it when the field mounts and not on every keystroke —
    // a plain inline ref would re-select the text each time a character was typed.
    const focusField = useCallback((field: HTMLInputElement | null) => {
        if (!field) {
            return;
        }

        field.focus();

        // Typing over the old name is the common case. Guarded because select() is one of the DOM
        // methods this engine does not always implement.
        try {
            field.select?.();
        } catch {
            /* Leaving the caret where it landed is fine. */
        }
    }, []);

    const classes = [
        styles.item,
        selected ? styles.itemSelected : "",
        shot.inSequence ? "" : styles.itemBinned,
        sortable.isDragging ? styles.dragging : "",
    ].filter(Boolean).join(" ");

    return (
        <div
            ref={sortable.ref}
            className={classes}
            style={sortable.style}
            onClick={onSelect}
            onDoubleClick={() => loadShot(shot.id)}
        >
            {/* The grip is the only part that starts a drag: on the whole row the drag listeners
                compete with selecting and with double-click to load.

                Six divs rather than a glyph. This was "⠿" (U+283F), which the game's font does not
                carry — it rendered as a tofu box, so the one draggable part of the row was invisible.
                Divs always render, and unlike SVG children they are hit-testable in Cohtml. */}
            <div className={styles.grip} {...sortable.attributes} {...sortable.listeners}>
                <span className={styles.dot} />
                <span className={styles.dot} />
                <span className={styles.dot} />
                <span className={styles.dot} />
                <span className={styles.dot} />
                <span className={styles.dot} />
            </div>

            <span className={styles.index}>{index + 1}</span>

            {/* Double-click the name to rename it; double-click anywhere else on the row to load
                the shot. The field is only rendered while editing — the point of the interaction is
                that the list stays a list until you ask a row to be editable. */}
            {editing ? (
                <input
                    ref={focusField}
                    className={styles.itemField}
                    value={draft}
                    placeholder={typeName}
                    onClick={(event: React.MouseEvent) => event.stopPropagation()}
                    onDoubleClick={(event: React.MouseEvent) => event.stopPropagation()}
                    onChange={(event: React.ChangeEvent<HTMLInputElement>) =>
                        setDraft(event.target.value)}
                    onBlur={commit}
                    onKeyDown={(event: React.KeyboardEvent) => {
                        if (event.key === "Enter") {
                            commit();
                        } else if (event.key === "Escape") {
                            setDraft(shot.name);
                            setEditing(false);
                        }

                        // Keeps typing out of whatever the row and the panel do with keys.
                        event.stopPropagation();
                    }}
                />
            ) : (
                <span className={styles.itemLabel} onDoubleClick={beginEdit}>
                    {shot.name.length > 0 ? shot.name : typeName}
                </span>
            )}

            {/* A shot in the bin has no time to report, so it says so rather than showing 0:00 —
                which would read as "starts at zero". */}
            <span className={styles.meta}>
                {shot.inSequence
                    ? `${TYPE_NAMES[shot.type] ?? "Shot"} · ${clock(shot.start)}`
                    : `${TYPE_NAMES[shot.type] ?? "Shot"} · bin`}
            </span>

            {/* An icon rather than an "Edit" label, because the row is already carrying a name, an
                index, a type, a time and the cut dot — a sixth word would push the name's ellipsis
                in. Tooltip via Tooltip, never Button's tooltipLabel, which silently shows nothing. */}
            <Tooltip
                tooltip={
                    `Edit this shot: load its settings, open the shot panel, `
                    + `and start the ${typeName.toLowerCase()} editor.`
                }
            >
                <button
                    className={styles.edit}
                    onClick={(event: React.MouseEvent) => {
                        event.stopPropagation();
                        editShot(shot.id);
                    }}
                >
                    {/* Masked, not an <img>: a mask reads alpha only, so the icon follows the text
                        colour instead of arriving in whatever colour it was drawn. Same approach as
                        the sort control's options. */}
                    <div className={styles.editIcon} style={{ maskImage: `url(${kPencil})` }} />
                </button>
            </Tooltip>

            {/* The cut dot used to sit here as a click route in and out of the sequence. Dragging
                has proven itself in this engine, and the row already says which side of the cut a
                shot is on — the meta column reads "bin", and a binned row is dimmed. */}
        </div>
    );
}

/** Mirrors ExtendedPhotomode.Camera.ShotType. */
const TYPE_NAMES: Record<number, string> = {
    1: "Orbit",
    2: "Dolly zoom",
    3: "Path",
};

/** Seconds to m:ss, because a shot list is read as a running time, not as a count of seconds. */
function clock(seconds: number): string {
    const whole = Math.max(0, Math.round(seconds));
    const mins = Math.floor(whole / 60);

    return `${mins}:${String(whole % 60).padStart(2, "0")}`;
}

/**
 * The shot list: an ordered set of shots assembled onto the cinematic timeline in one press.
 *
 * A pane rather than a window of its own. It used to float separately, but the shots and the curves
 * they assemble into are two ends of one pipeline, and the split had already started to cost: the
 * retiming and per-keyframe easing controls drifted onto this panel because there was nowhere better
 * for them, and ended up duplicating what the curve editor does directly.
 */
export function ShotListPane(): ReactElement {
    const shots = useValue(shotsBinding.binding);
    const timeline = useValue(timelineBinding.binding);
    const numbers = useValue(numbersBinding.binding);
    const [selected, setSelected] = useState(-1);

    const set = (field: string, value: number): void => setTimeline(field, value);
    const bin = useShotDropZone(false);
    const dragging = useShotDragActive();

    // Looked up fresh from the binding every render rather than held in state: the timing rows write
    // through it, and a copy taken when the row was clicked would keep showing the value from before
    // the edit. Undefined when the selection has been deleted or nothing is selected.
    const chosen = shots.find((shot: ShotListEntry) => shot.id === selected);

    // Only a shot that is IN the cut can be taken out of it. Dragging a binned row around the list is
    // a reorder, and offering to remove it from a sequence it is not in would read as a bug.
    const offering = dragging !== null && dragging.inSequence;
    const trash = useShotTrashZone();

    const inCut = shots.filter((shot: ShotListEntry) => shot.inSequence);
    const cutTotal = inCut.reduce((sum: number, shot: ShotListEntry) => sum + shot.duration, 0);

    return (
        <div className={styles.pane}>
            {/* The two counts are reported separately because they are now different questions:
                how much have I made, and how much is actually in the cut. One combined total hid
                the fact that a generated shot is not yet in the sequence. */}
            <div className={styles.status}>
                {shots.length === 0
                    ? "Nothing generated yet. Set a shot up, then press Generate shot."
                    : `${inCut.length} of ${shots.length} in sequence · ${clock(cutTotal)}.`}
            </div>

            <div className={styles.content}>
                <div className={styles.sectionLabel}>Generated shots</div>

                {/* The list already accepted a shot dragged off the timeline; nothing said so. The
                    hint appears the moment a cut shot is picked up, while the choice is still open,
                    rather than once the pointer is already over the list. */}
                {offering && (
                    <div className={styles.dropHint}>
                        {bin.isOver
                            ? "Release to take it out of the sequence"
                            : "Drop here to take it out of the sequence"}
                    </div>
                )}

                {/* stopPropagation so scrolling the list does not also zoom the camera underneath. */}
                {/* The whole list is the bin's drop zone, so a shot dragged anywhere over it comes
                    out of the cut — not only when it lands precisely on another row. */}
                <div
                    ref={bin.ref}
                    className={[
                        offering ? styles.binReady : "",
                        offering && bin.isOver ? styles.binOver : "",
                    ].filter(Boolean).join(" ")}
                    onWheel={(event) => event.stopPropagation()}
                >
                    <Scrollable vertical trackVisibility="scrollable" className={styles.list}>
                        <ShotListSortable shotIds={shots.map((s: ShotListEntry) => s.id)}>
                        {shots.map((shot: ShotListEntry, index: number) => (
                            <ShotRow
                                key={shot.id}
                                shot={shot}
                                index={index}
                                selected={shot.id === selected}
                                onSelect={() => setSelected(shot.id)}
                            />
                        ))}
                        </ShotListSortable>
                    </Scrollable>
                </div>

                {/* The timeline these shots assemble onto. Chaining, transitions and retiming are all
                    about how shots join, so they belong beside the order they apply to rather than on
                    a panel that shows none of it. */}
                <div className={styles.timeline}>
                    {/* The next shot's own timing, above the settings about how shots join. These
                        are what get changed between one Generate and the next, so having them here
                        is what stops the trip back to the tool options each time. */}
                    <ShotTiming numbers={numbers} shot={chosen} />

                    <Row label="Chain onto existing">
                        <Toggle
                            on={timeline.chain}
                            tooltip="Append generated shots to what is already on the timeline instead of replacing it."
                            onSelect={() => set("chain", timeline.chain ? 0 : 1)}
                        />
                    </Row>

                    {timeline.chain && (
                        <Row label="Gap between shots">
                            <Stepper value={timeline.gap} suffix="s" min={0} max={30}
                                     onChange={(v) => set("gap", v)} />
                        </Row>
                    )}

                    <Row label="Transition in">
                        <Stepper value={timeline.transitionSeconds} suffix="s" min={0} max={60}
                                 onChange={(v) => set("transitionSeconds", v)} />
                    </Row>

                    {/* Still a toggle, not a one-press action: it is also the preference future
                        generates are flattened by, so turning it into a button would lose that. */}
                    <Row label="Constant speed">
                        <Toggle
                            on={timeline.constantSpeed}
                            tooltip="Flatten every keyframe's tangents so the move runs at one unvarying speed."
                            onSelect={() => set("constantSpeed", timeline.constantSpeed ? 0 : 1)}
                        />
                    </Row>
                </div>

                {/* Edit and Delete used to sit here, acting on the selected row. Both are gone: the
                    pencil on each row does more than Edit did — it opens the tool as well as loading
                    the settings — and it needs no selection first, while Delete is the drop zone
                    below. Two ways to do one thing is how they drift apart. */}

                {/* The delete drop zone, filling whatever space is left below the controls.
                    It only appears while something is being dragged: an always-visible bin would be
                    a permanent invitation to destroy work, and empty space is worth more than a
                    target nobody is aiming at. */}
                <div
                    ref={trash.ref}
                    className={trash.isOver ? `${styles.trash} ${styles.trashOver}` : styles.trash}
                >
                    <span>{trash.isOver ? "Release to delete" : "Drag a shot here to delete it"}</span>
                </div>

                <SequenceLibrary />
            </div>
        </div>
    );
}
