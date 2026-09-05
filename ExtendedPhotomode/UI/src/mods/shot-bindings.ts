import { OneWayBinding } from "utils/onewayBinding";
import { TwoWayBinding } from "utils/bidirectionalBinding";
import triggers from "utils/trigger";

/** Everything the shot list panel reads and writes. */

export type ShotListEntry = {
    id: number;

    name: string;

    /** Numeric ShotType, so the panel can label it. */
    type: number;

    duration: number;

    /**
     * How densely the shot is keyed, in whatever unit its type counts in: degrees per key for an
     * orbit, metres per key for a path, and a key COUNT for a dolly.
     */
    spacing: number;

    /** Where this shot begins on the assembled timeline, or -1 when it is in the bin. */
    start: number;

    points: number;

    /** Whether the shot is in the cut, or waiting in the bin. */
    inSequence: boolean;
};

/** The timeline the shot list assembles onto. */
export type TimelineNumbers = {
    duration: number;
    keyframes: number;
    chain: boolean;
    gap: number;
    constantSpeed: boolean;
    transitionSeconds: number;
    transitionEase: number;
};

export const timelineBinding = new OneWayBinding<TimelineNumbers>("shotTimeline", {
    duration: 0,
    keyframes: 0,
    chain: false,
    gap: 2,
    constantSpeed: false,
    transitionSeconds: 0,
    transitionEase: 0.85,
});

export const setTimeline = triggers.create<[string, number]>("setShotTimeline");

/**
 * Whether the mod's panels are hidden so the shot can be judged on the picture alone.
 *
 * Driven from C#, because the key that toggles it is a real input action rather than a DOM handler —
 * the panels have to be dismissable by something that still works once they are gone.
 */
export const uiHiddenBinding = new OneWayBinding<boolean>("uiHidden", false);

export const shotsBinding = new OneWayBinding<ShotListEntry[]>("shotList", []);
export const shotListOpenBinding = new TwoWayBinding<boolean>("shotListOpen", false);

export const removeShot = triggers.create<[number]>("removeShot");
export const renameShot = triggers.create<[number, string]>("renameShot");
export const moveShot = triggers.create<[number, number]>("moveShot");

/** Drop a shot at an explicit position in the order. Both of these re-assemble on the C# side. */
export const reorderShot = triggers.create<[number, number]>("reorderShot");

/** Drag a shot into the cut, or back out to the bin. */
export const setShotInSequence = triggers.create<[number, boolean]>("setShotInSequence");

/**
 * A completed drag: land a shot at a position, in or out of the cut, rebuilding once.
 *
 * `position` indexes the WHOLE ordered list, not the cut, because that is what the binding is
 * ordered by — a track-relative index would mean something different depending on which shots
 * happened to be in the sequence at the time. Pass -1 to change membership without reordering.
 */
export const dropShot = triggers.create<[number, number, boolean]>("dropShot");

export const loadShot = triggers.create<[number]>("loadShot");

/** Load a shot AND open the panel its settings live on. Load on its own leaves you in the list. */
export const editShot = triggers.create<[number]>("editShot");

/**
 * Clear the timeline: every curve, and every shot's membership of the cut.
 *
 * What the timeline's Reset button calls. Vanilla's own `reset` clears only the curves, which leaves
 * an empty sequence sitting over a full track — and the next reorder assembles the whole cut back.
 * Nothing is destroyed: the shots return to the generated list, and undo restores the curves.
 */
export const resetTimeline = triggers.create<[]>("resetTimeline");

/**
 * Edit one number on an EXISTING shot: `"duration"` or `"spacing"`.
 *
 * Distinct from `setNumber`, which writes the settings that describe the NEXT shot to be generated.
 * Changing those leaves everything already on the timeline untouched, which is why editing a
 * duration appeared to do nothing until the shot was regenerated. This rebuilds the cut, so the
 * curves and the track blocks move as the value changes.
 */
export const setShotNumber = triggers.create<[number, string, number]>("setShotNumber");

export const closeShotList = (): void => shotListOpenBinding.set(false);

export const toggleShotList = (): void =>
    shotListOpenBinding.set(!shotListOpenBinding.binding.value);

// `addShot` and `assembleShots` used to live here. Both are gone from the UI: generating a shot
// stages it (C# calls AddShot itself), and every drop, reorder and membership change reassembles.
// The C# triggers are deliberately left registered — they are still the seam the panel used to
// drive, and cost nothing standing idle.
