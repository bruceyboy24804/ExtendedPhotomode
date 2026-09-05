import { useValue } from "cs2/api";
import { Button, Icon, Scrollable, Tooltip } from "cs2/ui";
import { type ReactElement, useEffect, useState } from "react";
import { VC, VF, VT } from "vanilla/Components";
import { closeButtonClass } from "./close-button";
import styles from "./path-library-panel.module.scss";
import {
    closePanel,
    deletePath,
    generateShot,
    loadPath,
    loadedPathBinding,
    newPath,
    importFromSequence,
    previewBinding,
    togglePreview,
    panelOpenBinding,
    placementHeightBinding,
    pointCountBinding,
    renamePath,
    savePath,
    savedPathsBinding,
    setToolActive,
    toolActiveBinding,
    ShotTypes,
    metricsBinding,
    numbersBinding,
} from "./path-bindings";

/**
 * What the panel calls itself, and what its tool button does, for each shot type.
 *
 * The panel serves all three shots now, and almost every word on it was written for a drawn path —
 * a title saying CAMERA PATH above an orbit's controls is worse than no title.
 */
const SHOT_LABELS: Record<
    number,
    { title: string; create: string; edit: string; stop: string }
> = {
    [ShotTypes.Path]: {
        title: "CAMERA PATH",
        create: "New path",
        edit: "Draw path",
        stop: "Stop drawing",
    },
    [ShotTypes.Orbit]: {
        title: "ORBIT SHOT",
        create: "New orbit",
        edit: "Draw orbit",
        stop: "Stop editing",
    },
    [ShotTypes.DollyZoom]: {
        title: "DOLLY ZOOM",
        create: "New dolly",
        edit: "Draw dolly",
        stop: "Stop editing",
    },
};

const MAX_NAME = 60;

const MIN_SEARCHABLE = 4;

function TextField({
    value,
    placeholder,
    maxLength,
    onChange,
    onBlur,
}: {
    readonly value: string;
    readonly placeholder: string;
    readonly maxLength?: number;
    readonly onChange: (value: string) => void;
    readonly onBlur?: () => void;
}): ReactElement {
    const handle = (event: React.ChangeEvent<HTMLInputElement>): void => onChange(event.target.value);

    // TextInput, not EllipsisTextInput: the latter keeps its real <input> at opacity 0 and paints the
    // visible text into a sibling label that only ever carries `value`, so a placeholder never shows.
    // TextInput renders a real field and honours it — this is what FindIt's search box uses.
    return VC.TextInput ? (
        <VC.TextInput
            multiline={1}
            type="text"
            value={value}
            placeholder={placeholder}
            maxLength={maxLength}
            disabled={false}
            focusKey={VF.FOCUS_DISABLED}
            className={`${VT.textInput?.input ?? ""} ${styles.field}`}
            onChange={handle}
            onBlur={onBlur}
        />
    ) : (
        <input
            className={styles.field}
            type="text"
            value={value}
            placeholder={placeholder}
            maxLength={maxLength}
            onChange={handle}
            onBlur={onBlur}
        />
    );
}

/**
 * The saved path library: the named paths, with save/load/delete controls under them.
 *
 * A React panel rather than more photo mode properties because the photo mode panel renders only a
 * fixed handful of widget types and a list is not one of them — the same wall that turned the
 * per-node editor into a point selector.
 */
export function PathLibraryPanel(): ReactElement | null {
    const paths = useValue(savedPathsBinding.binding);
    const toolActive = useValue(toolActiveBinding.binding);
    const panelOpen = useValue(panelOpenBinding.binding);
    const points = useValue(pointCountBinding.binding);
    const placementHeight = useValue(placementHeightBinding.binding);
    const loadedId = useValue(loadedPathBinding.binding);
    // Must sit above the `panelOpen` early return with the rest: a hook called conditionally changes
    // the hook count between renders, which React treats as a hard error.
    const previewing = useValue(previewBinding.binding);
    const numbers = useValue(numbersBinding.binding);
    const metrics = useValue(metricsBinding.binding);
    const [selected, setSelected] = useState(-1);
    const [name, setName] = useState("");
    const [search, setSearch] = useState("");

    const path = paths.find((entry) => entry.index === selected) ?? null;
    const loaded = paths.find((entry) => entry.index === loadedId) ?? null;

    useEffect(() => setName(path?.name ?? ""), [path?.name, path?.index]);

    if (!panelOpen) {
        return null;
    }

    const shot = numbers.shotType;
    const isPath = shot === ShotTypes.Path;
    const labels = SHOT_LABELS[shot] ?? SHOT_LABELS[ShotTypes.Path];

    // An orbit or dolly is ready the moment it has a subject; a path needs two points.
    const ready = isPath ? points >= 2 : numbers.hasSubject;

    const trimmed = name.trim();
    const searchable = paths.length > MIN_SEARCHABLE;
    const term = search.trim().toLowerCase();

    const shown =
        searchable && term ? paths.filter((entry) => entry.name.toLowerCase().includes(term)) : paths;

    const commitName = (): void => {
        if (path && trimmed && trimmed !== path.name) {
            renamePath(path.index, trimmed);
        }
    };

    return (
        <div className={styles.panel}>
            <div className={styles.header}>
                <span className={styles.title}>{labels.title}</span>
                {/* Vanilla's close button, so its hover and hit area come with it. See the same
                    change in timeline-panel for why a styled "×" was not equivalent. */}
                <Tooltip tooltip="Close the path library.">
                    <Button
                        variant="icon"
                        className={closeButtonClass(styles.close)}
                        focusKey={VF.FOCUS_DISABLED}
                        onSelect={closePanel}
                    >
                        <Icon src="Media/Glyphs/Close.svg" tinted className={styles.closeIcon} />
                    </Button>
                </Tooltip>
            </div>

            {/* Row order is the workflow: start a path, work on it, then commit it. Generate shot
                sits alone on the last row and so runs full width, which is right — it is the one
                button here that changes the timeline. */}
            <div className={styles.actions}>
                {/* New means the same for all three — start again — even though what gets cleared
                    differs: a path's points, or a shot's subject and its numbers. */}
                <Button
                    variant="flat"
                    disabled={isPath ? points === 0 : !numbers.hasSubject}
                    onSelect={newPath}
                >
                    {labels.create}
                </Button>

                {/* This one really is path-only: it reads camera keyframes back into control points,
                    and there is nothing in a timeline that reconstitutes into an orbit. */}
                {isPath && (
                    <Button variant="flat" onSelect={importFromSequence}>
                        From timeline
                    </Button>
                )}

                <Button
                    variant="flat"
                    className={toolActive ? styles.actionActive : ""}
                    onSelect={() => setToolActive(!toolActive)}
                >
                    {toolActive ? labels.stop : labels.edit}
                </Button>

                <Button
                    variant="flat"
                    disabled={!ready}
                    className={previewing ? styles.actionActive : ""}
                    onSelect={togglePreview}
                >
                    {previewing ? "Stop preview" : "Preview"}
                </Button>

                <Button variant="flat" disabled={!ready} onSelect={generateShot}>
                    Generate shot
                </Button>
            </div>

            <div className={styles.status}>
                {isPath
                    ? points === 0
                        ? "No path yet. Press Draw path, then click the ground to add points."
                        : points < 2
                          ? "1 point. Add at least one more before generating."
                          : `${points} points · ${Math.round(metrics.length)}m · ${Math.round(metrics.speed)}km/h.`
                    : numbers.hasSubject
                      ? shot === ShotTypes.Orbit
                          ? `${numbers.orbitRadius}m radius · ${numbers.orbitSweep}° · ${numbers.orbitDuration}s.`
                          : `${numbers.dollyStart}m to ${numbers.dollyEnd}m · ${numbers.dollyDuration}s.`
                      : "No subject yet. Press Edit, then click the ground or a building to place one."}
                {toolActive && isPath
                    ? ` New points land ${placementHeight}m up — Ctrl+wheel to change. Point settings are in the tool options.`
                    : ""}
                {toolActive && !isPath ? " Drag the handles in the world; numbers are in the tool options." : ""}
                {isPath && loaded ? ` Editing “${loaded.name}” — Save updates it.` : ""}
            </div>

            {/* The library holds drawn paths. An orbit or a dolly is a handful of numbers, and the
                place to keep one of those is the shot list, not here. */}
            {isPath && (
            <div className={styles.content}>
                {searchable && (
                    <div className={styles.search}>
                        <TextField value={search} placeholder="Search paths" onChange={setSearch} />
                    </div>
                )}

                {/* stopPropagation so scrolling the list does not also zoom the camera underneath. */}
                <div onWheel={(event) => event.stopPropagation()}>
                    <Scrollable vertical trackVisibility="scrollable" className={styles.list}>
                        {paths.length === 0 ? (
                            <div className={styles.empty}>Draw a path, name it below and press Save.</div>
                        ) : shown.length === 0 ? (
                            <div className={styles.empty}>No path matches “{search.trim()}”.</div>
                        ) : (
                            shown.map((entry) => (
                                <div
                                    key={entry.index}
                                    className={`${styles.item} ${
                                        entry.index === selected ? styles.itemSelected : ""
                                    }`}
                                    onClick={() => setSelected(entry.index)}
                                    onDoubleClick={() => loadPath(entry.index)}
                                >
                                    <span className={styles.itemLabel}>{entry.name}</span>
                                    <span className={styles.itemPoints}>{entry.points}</span>
                                </div>
                            ))
                        )}
                    </Scrollable>
                </div>

                <TextField
                    value={name}
                    maxLength={MAX_NAME}
                    placeholder="Harbour flythrough"
                    onChange={setName}
                    onBlur={commitName}
                />

                <div className={styles.buttonRow}>
                    {/* Saving under a name that already exists overwrites it, so this is both Save
                        and Save As depending on what is typed — hence one button, not two. */}
                    <Button variant="flat" disabled={!trimmed} onSelect={() => savePath(trimmed)}>
                        Save
                    </Button>

                    <Button variant="flat" disabled={!path} onSelect={() => path && loadPath(path.index)}>
                        Load
                    </Button>

                    <Button
                        variant="flat"
                        disabled={!path}
                        onSelect={() => {
                            if (path) {
                                deletePath(path.index);
                                setSelected(-1);
                            }
                        }}
                    >
                        Delete
                    </Button>
                </div>
            </div>
            )}
        </div>
    );
}
