import { useValue } from "cs2/api";
import type { ReactElement } from "react";
import styles from "./shot-track.module.scss";
import { type ShotListEntry, shotsBinding } from "./shot-bindings";
import {
    ShotTrackSortable,
    useShotDragActive,
    useShotDropZone,
    useShotSortable,
} from "./shot-dnd";

/**
 * The shots in the cut, drawn as blocks along the timeline.
 *
 * The track is a VIEW of the ordered shot list, never a second copy of it. `Assemble` regenerates
 * every curve from the ordered shots each time it runs, so the timeline is already a pure function
 * of that list — which means a block's position and width are derived here rather than stored, and
 * the track cannot drift out of step with what is actually on the timeline.
 *
 * That is also what makes dragging cheap: reordering blocks or pulling one off the track is a change
 * to the list plus a rebuild, never surgery on the curves.
 */
export function ShotTrack({
    view,
    width,
    playhead,
}: {
    /** The visible slice of the timeline, in seconds — the curve graphs' own zoom. */
    readonly view: { from: number; to: number };
    readonly width: number;
    /** Where the playhead sits, as a CSS percentage — the same one the graphs use. */
    readonly playhead: string;
}): ReactElement | null {
    const shots = useValue(shotsBinding.binding);
    const zone = useShotDropZone(true);
    const dragging = useShotDragActive();

    // A shot already in the cut is being reordered, not added, so the track outlines itself but does
    // not offer to take it — saying "drop here to add" about something already here reads as a bug.
    const offering = dragging !== null && !dragging.inSequence;

    // A shot in the bin reports start = -1 and has no place here. Filtering on the flag rather than
    // on the time keeps the two in agreement even if the readout is ever computed differently.
    const inCut = shots.filter((shot: ShotListEntry) => shot.inSequence);

    return (
        <div
            className={[
                styles.track,
                dragging ? styles.trackReady : "",
                zone.isOver ? styles.trackOver : "",
            ].filter(Boolean).join(" ")}
            ref={zone.ref}
        >
            {/* The empty-state hint is for when nothing is happening; it would fight the drag hint
                below, which is the more specific message at that moment. */}
            {inCut.length === 0 && !dragging && (
                <div className={styles.empty}>
                    No shots in the sequence. Drag one here from the list.
                </div>
            )}

            {/* Shown from the moment a drag starts rather than on hover: by the time the pointer is
                over the track the player has already worked out that it accepts shots. */}
            {offering && (
                <div className={styles.dropHint}>
                    {zone.isOver ? "Release to add to the sequence" : "Drop here to add to the sequence"}
                </div>
            )}

            <ShotTrackSortable shotIds={inCut.map((shot: ShotListEntry) => shot.id)}>
            {inCut.map((shot: ShotListEntry, index: number) => {
                const left = ((shot.start - view.from) / width) * 100;
                const span = (shot.duration / width) * 100;

                // Clipped rather than hidden when it runs off the left edge, so a shot that starts
                // before the zoom window still shows the part of itself that is in view.
                const from = Math.max(left, 0);
                const to = Math.min(left + span, 100);

                if (to <= 0 || from >= 100) {
                    return null;
                }

                return (
                    <TrackBlock
                        key={shot.id}
                        shot={shot}
                        index={index}
                        globalIndex={shots.findIndex((s: ShotListEntry) => s.id === shot.id)}
                        left={from}
                        width={Math.max(to - from, 0.5)}
                    />
                );
            })}
            </ShotTrackSortable>

            {/* The track shares the graphs' time axis, so it carries the playhead too — it is one of
                the timeline boxes, not a header. */}
            <div className={styles.playhead} style={{ left: playhead }} />
        </div>
    );
}

/**
 * One shot on the track.
 *
 * Split into its own component because `useSortable` is a hook and cannot be called inside the map.
 */
function TrackBlock({
    shot,
    index,
    globalIndex,
    left,
    width,
}: {
    readonly shot: ShotListEntry;
    readonly index: number;
    readonly globalIndex: number;
    readonly left: number;
    readonly width: number;
}): ReactElement {
    const sortable = useShotSortable({
        shotId: shot.id,
        globalIndex,
        inSequence: true,
        label: shot.name,
        kind: "block",
    });

    return (
        <div
            ref={sortable.ref}
            className={sortable.isDragging ? `${styles.block} ${styles.dragging}` : styles.block}
            // The whole block is the handle here — unlike a list row it has no click of its own to
            // compete with, and a grip inside a block that may be a few pixels wide is unusable.
            style={{ left: `${left}%`, width: `${width}%`, ...sortable.style }}
            title={`${shot.name} · ${Math.round(shot.duration)}s`}
            {...sortable.attributes}
            {...sortable.listeners}
        >
            <span className={styles.index}>{index + 1}</span>
            <span className={styles.name}>{shot.name}</span>
        </div>
    );
}
