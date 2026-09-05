import {
    DndContext,
    DragOverlay,
    MouseSensor,
    pointerWithin,
    rectIntersection,
    useDndContext,
    useDroppable,
    useSensor,
    useSensors,
    type CollisionDetection,
    type DragEndEvent,
    type DragStartEvent,
} from "@dnd-kit/core";
import {
    SortableContext,
    horizontalListSortingStrategy,
    useSortable,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { type ReactElement, type ReactNode, useState } from "react";
import { createPortal } from "react-dom";
import styles from "./shot-dnd.module.scss";
import { dropShot, removeShot } from "./shot-bindings";

/**
 * Dragging shots between the bin and the timeline track.
 *
 * WHICH DND-KIT, AND WHY IT MATTERS
 *
 * This uses @dnd-kit/core v6 — the classic API — and NOT @dnd-kit/react, which is a ground-up
 * rewrite on @dnd-kit/dom. That is not a preference. Probed live, Cohtml 1.64 reports:
 *
 *   ResizeObserver        function      MutationObserver   function
 *   IntersectionObserver  undefined     PointerEvent       undefined
 *   Element.animate       undefined     setPointerCapture  undefined
 *
 * @dnd-kit/dom needs all four of the missing ones and throws while its module is still evaluating,
 * which takes the mod's whole UI registration down with it — the symptom is not "dragging does not
 * work", it is "the mod has no UI at all". v6 needs only the two that exist. Its one call into the
 * Web Animations API is the DragOverlay drop animation, so this file deliberately does not use
 * DragOverlay; the dragged row is styled in place instead.
 *
 * Precedent, which is how this was settled: ExtraLib (pdx mod 75724) ships @dnd-kit/core v6 and
 * works in this game today. If this ever stops working, check what ExtraLib does before rewriting.
 *
 * MouseSensor rather than the default PointerSensor for the same reason — `PointerEvent` does not
 * exist here, and mouse events are what the timeline scrub already drives itself with successfully.
 */

/** What a draggable row or block carries, so a drop knows where it landed. */
export type ShotDragData = {
    shotId: number;

    /** Index into the whole ordered list — what `dropShot` expects. */
    globalIndex: number;

    inSequence: boolean;

    /** Shown in the drag overlay, so it does not have to look the shot up mid-gesture. */
    label: string;

    /**
     * Which drawing of the shot this is — a list row or a track block.
     *
     * Part of the sortable id, and it has to be: a shot in the cut is drawn TWICE, once in the list
     * and once on the track. Keying both on the shot id alone put two sortables with the same id in
     * one context, which is exactly the thing dnd-kit cannot resolve — collisions land on whichever
     * it happened to register last.
     */
    kind: "row" | "block";
};

export const kCut = "cut";

export const kBin = "bin";

/** Dropping here deletes the shot. */
export const kTrash = "trash";

/**
 * Which droppable a drag is over.
 *
 * NOT closestCenter, which was the first thing tried and is wrong here. Both containers are
 * droppables too, and closestCenter measures centre-to-centre against every one of them — so the
 * list pane, being enormous, won over the small track block the pointer was actually on. The visible
 * symptom was that dragging one block onto another REMOVED it from the cut rather than reordering,
 * because the resolved target was the bin.
 *
 * So: what is under the pointer, and among those prefer a shot over a container. A container only
 * wins when the pointer is over its empty space, which is exactly when "just change sides" is meant.
 */
const collisionDetection: CollisionDetection = (args) => {
    const under = pointerWithin(args);
    const hits = under.length > 0 ? under : rectIntersection(args);
    const shots = hits.filter(
        (hit) => hit.id !== kCut && hit.id !== kBin && hit.id !== kTrash,
    );

    return shots.length > 0 ? shots : hits;
};

/** Ids have to be unique across the whole DndContext, and a cut shot is drawn in both places. */
const idOf = (shotId: number, kind: "row" | "block"): string => `shot-${shotId}-${kind}`;

/** The list's ids, in list order. */
export const rowIds = (shotIds: number[]): string[] => shotIds.map((id) => idOf(id, "row"));

/** The track's ids, in cut order. */
export const blockIds = (shotIds: number[]): string[] => shotIds.map((id) => idOf(id, "block"));

/**
 * Wraps the timeline window so the list pane and the track share one drag context.
 *
 * Everything resolves from the drop target's own data rather than from dnd-kit's internal ordering:
 * the library tracks an order of its own while dragging, and reading that back would mean trusting a
 * second source of truth about an order C# already owns.
 */
export function ShotDragContext({ children }: { readonly children: ReactNode }): ReactElement {
    // A small distance before a drag starts, so clicking a row to select it does not begin one.
    const sensors = useSensors(
        useSensor(MouseSensor, { activationConstraint: { distance: 4 } }),
    );

    // What the overlay draws. Held in state rather than read from the active draggable so the
    // overlay still has something to show during the frame the drag is being torn down.
    const [dragging, setDragging] = useState<ShotDragData | null>(null);

    const onDragStart = (event: DragStartEvent): void => {
        setDragging((event.active?.data?.current as ShotDragData | undefined) ?? null);
    };

    const onDragEnd = (event: DragEndEvent): void => {
        setDragging(null);

        const source = event.active?.data?.current as ShotDragData | undefined;

        if (!source || !event.over) {
            return;
        }

        // Deleting wins over every other reading of the drop: the trash is a container, so without
        // this it would fall through to the "dropped on empty space" branch and merely change sides.
        if (event.over.id === kTrash) {
            removeShot(source.shotId);
            return;
        }

        const target = event.over.data?.current as ShotDragData | undefined;

        // Dropped on a container's empty space rather than on another shot. The container carries
        // only its side, so membership changes and the order is left alone.
        if (!target) {
            const intoCut = event.over.id === kCut;

            if (intoCut !== source.inSequence) {
                dropShot(source.shotId, -1, intoCut);
            }

            return;
        }

        if (target.shotId === source.shotId) {
            return;
        }

        // Which CONTAINER the drop landed in decides membership, and the target's KIND is what names
        // it: a "row" is only ever drawn in the list, a "block" only ever on the track.
        //
        // This used to read target.inSequence, which is the target shot's own membership — a
        // different thing. The list draws every shot, cut ones included, so a block dragged off the
        // timeline onto one of those rows resolved to "still in the cut" and the shot could not be
        // taken out at all unless it happened to land on empty space or on an already-binned row.
        const intoCut = target.kind === "block";

        dropShot(source.shotId, target.globalIndex, intoCut);
    };

    return (
        <DndContext
            sensors={sensors}
            collisionDetection={collisionDetection}
            onDragStart={onDragStart}
            onDragCancel={() => setDragging(null)}
            onDragEnd={onDragEnd}
            // MUST stay off. Auto-scroll calls element.scrollBy() when a drag nears a scrollable
            // edge, and Cohtml implements none of scrollBy, scrollTo or scrollIntoView — only the
            // scrollTop property. Leaving it on throws "e.scrollBy is not a function" on every drag.
            // ExtraLib ships the same `autoScroll: false` for the same reason.
            autoScroll={false}
        >
            {children}

            {/* Portalled to the body, not rendered in place.
                The dragged row is transformed out of the list, and BOTH the list pane and the window
                clip it — which is why a shot dragged towards the track simply vanished. An overlay
                is the fix, but it has to escape the clip: the panel sets `backdrop-filter`, and that
                makes it the containing block for position:fixed, so even a fixed overlay would still
                be trapped inside it. Only a portal out of the subtree gets clear.

                dropAnimation={null} is required, not cosmetic. The drop animation is dnd-kit's one
                call into Element.animate, which does not exist in Cohtml; passing null returns from
                useDropAnimation before it is ever reached. */}
            {createPortal(
                <DragOverlay dropAnimation={null}>
                    {dragging ? (
                        <div className={styles.overlay}>{dragging.label}</div>
                    ) : null}
                </DragOverlay>,
                document.body,
            )}
        </DndContext>
    );
}

/**
 * The list's sortable region — vertical — and the track's — horizontal.
 *
 * Two regions rather than one, because the list runs down and the track runs across. A SortableContext
 * has ONE strategy, so whichever axis a single region was set to, the other container would compute
 * its gaps along the wrong one and shuffle items to positions that make no sense. Both sit inside the
 * same DndContext, which is what still allows dragging from one to the other.
 */
export function ShotListSortable({
    children,
    shotIds,
}: {
    readonly children: ReactNode;
    readonly shotIds: number[];
}): ReactElement {
    return (
        <SortableContext items={rowIds(shotIds)} strategy={verticalListSortingStrategy}>
            {children}
        </SortableContext>
    );
}

export function ShotTrackSortable({
    children,
    shotIds,
}: {
    readonly children: ReactNode;
    readonly shotIds: number[];
}): ReactElement {
    return (
        <SortableContext items={blockIds(shotIds)} strategy={horizontalListSortingStrategy}>
            {children}
        </SortableContext>
    );
}

/**
 * A draggable, sortable shot — a row in the list or a block on the track.
 *
 * Returns the transform separately from the ref on purpose. `transform` and `transition` have to be
 * applied as inline style or the dragged element never visibly moves: dnd-kit tracks the drag but
 * draws nothing itself, and without this the whole thing looks broken while working perfectly.
 *
 * `listeners` belong on a HANDLE, not the whole element. Spread over a row that also has its own
 * onClick and onDoubleClick, they compete with selecting and loading a shot.
 */
export function useShotSortable(data: ShotDragData) {
    const sortable = useSortable({ id: idOf(data.shotId, data.kind), data });

    return {
        ref: sortable.setNodeRef,
        listeners: sortable.listeners,
        attributes: sortable.attributes,
        isDragging: sortable.isDragging,
        style: {
            transform: sortable.transform
                ? `translate3d(${sortable.transform.x}px, ${sortable.transform.y}px, 0)`
                : undefined,
            transition: sortable.transition,
        } as React.CSSProperties,
    };
}

/** The delete zone. Its own hook because it is neither side of the cut. */
export function useShotTrashZone() {
    const droppable = useDroppable({ id: kTrash });

    return { ref: droppable.setNodeRef, isOver: droppable.isOver };
}

/**
 * The shot currently being dragged, or null when nothing is.
 *
 * A drop target knowing it is *hovered* is not enough to advertise itself: by the time the pointer
 * is over the track the player has already guessed that the track accepts shots. This is what lets
 * a target light up the moment a drag starts, while there is still a decision to make.
 */
export function useShotDragActive(): ShotDragData | null {
    const { active } = useDndContext();

    return (active?.data.current as ShotDragData | undefined) ?? null;
}

/** A container that accepts shots — the track, or the list pane. */
export function useShotDropZone(inSequence: boolean) {
    const droppable = useDroppable({ id: inSequence ? kCut : kBin });

    return { ref: droppable.setNodeRef, isOver: droppable.isOver };
}
