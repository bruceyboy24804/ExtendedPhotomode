import { bindValue, call, trigger, type ValueBinding } from "cs2/api";
import { TwoWayBinding } from "utils/bidirectionalBinding";

/**
 * Direct bindings to vanilla's own cinematic camera group.
 *
 * Not through our mod's binding helpers, which prefix ids and assume our own group — these read the
 * game's bindings by their real names. Everything the vanilla curve editor knows is here, which is
 * what makes a replacement view possible without a line of C#: we read the same curves it reads and
 * write through the same calls it writes, so vanilla's Refresh still runs and playback, scrubbing
 * and saving are untouched.
 */
const kGroup = "cinematicCamera";

/** One keyframe as the game serialises it. */
export type CurveKey = {
    time: number;
    value: number;
    inTangent: number;
    outTangent: number;
    inWeight: number;
    outWeight: number;
    weightedMode: number;
};

/** One curve: the five transforms use fixed ids, the modifiers use their property id. */
export type CurveModifier = {
    id: string;
    curve: { keys: CurveKey[] };
    min: number;
    max: number;
};

const kNoCurves: CurveModifier[] = [];

/** PositionX, PositionY, PositionZ, RotationX, RotationY — in that order. */
export const transformCurves: ValueBinding<CurveModifier[]> = bindValue<CurveModifier[]>(
    kGroup,
    "transformAnimationCurves",
    kNoCurves,
);

/** Every property modifier the shot carries — time of day, focus, lens, and so on. */
export const modifierCurves: ValueBinding<CurveModifier[]> = bindValue<CurveModifier[]>(
    kGroup,
    "modifierAnimationCurves",
    kNoCurves,
);

export const timelinePosition: ValueBinding<number> = bindValue<number>(kGroup, "timelinePosition", 0);
export const timelineLength: ValueBinding<number> = bindValue<number>(kGroup, "timelineLength", 0);
export const playbackDuration: ValueBinding<number> = bindValue<number>(kGroup, "playbackDuration", 0);
export const playing: ValueBinding<boolean> = bindValue<boolean>(kGroup, "playing", false);

/** Scrub. The one write the panel makes on a plain click. */
export const setTimelinePosition = (time: number): void =>
    trigger(kGroup, "setTimelinePosition", time);

export const togglePlayback = (): void => trigger(kGroup, "togglePlayback");
export const stopPlayback = (): void => trigger(kGroup, "stopPlayback");

/**
 * Key edits go through the MOD's guarded pair, not vanilla's.
 *
 * Vanilla's `moveKeyFrame` and `removeKeyFrame` index without checking, in two places each — the
 * modifier array by the position the UI sends, and then the curve by the key index. Neither is
 * range-checked, and a throw inside a binding callback is not a caught error. Both are reachable in
 * ordinary use: the panel writes from a snapshot of the curves that can be a push behind, and
 * `RemoveModifierKey` deletes the whole modifier when its curve loses its last key, which shifts
 * every position after it.
 *
 * `EPM_TimelineEditSystem` takes the curve's ID instead, resolves it against the live sequence at
 * the moment of the write, bounds-checks, and then does exactly what vanilla's handler does. So this
 * also sidesteps vanilla's inconsistent discriminator, which was the other trap here: `removeKeyFrame`
 * tests `id == "Property"` and treats anything else as a transform, while `moveKeyFrame` goes through
 * `GetData`, which tests `id == "Position"` and treats anything else as a MODIFIER — opposite
 * defaults, on calls that look like a matching pair.
 */
const kModGroup = "ExtendedPhotomode";

/**
 * Removes a key.
 *
 * @param curveId A `TransformCurveKey` name for a camera channel, or the modifier's own id.
 */
export const removeKey = (curveId: string, index: number): void =>
    trigger(kModGroup, "timelineRemoveKey", curveId, index);

/**
 * Moves a key, and reports the index it ended up at, or -1 if the edit was dropped as stale.
 *
 * A CallBinding rather than a trigger, so it answers: `AnimationCurve` keeps its keys sorted by time,
 * and a key dragged past its neighbour comes back with a different index. The shape of the keyframe
 * is the game's own — the seven fields of `editor-types.ts` — and vanilla forces `weightedMode` to
 * Both on the way in, so whatever is passed there is overwritten.
 */
export const moveKey = (curveId: string, index: number, key: CurveKey): Promise<number> =>
    call<number>(kModGroup, "timelineMoveKey", curveId, index, {
        time: key.time,
        value: key.value,
        inTangent: key.inTangent,
        outTangent: key.outTangent,

        // A zero weight is promoted to a third on the way out, and this is a correctness fix rather
        // than a cosmetic one.
        //
        // MoveKeyframe forces `weightedMode = WeightedMode.Both` on every write. A key that arrives
        // with weight 0 therefore leaves as WEIGHTED with ZERO weights, and Unity evaluates that as
        // a degenerate curve — the control points collapse onto the endpoints and the segment
        // flattens. So merely nudging a key sideways silently changed how the shot played.
        //
        // Weight 0 is the normal state for anything not yet hand-edited: Unity's `AddKey` and
        // `new Keyframe(t, v)` both zero them, which covers every key this mod generates AND every
        // camera key vanilla's own AddCameraTransform writes. A third is the exact Hermite-to-bezier
        // equivalent of an unweighted key, so promoting it here makes the weighted evaluation that
        // MoveKeyframe imposes agree with the unweighted one the curve had before the edit.
        //
        // Done at this choke point rather than in the generators because every write from the editor
        // passes through here — including edits to keys the mod never created.
        inWeight: key.inWeight || 1 / 3,
        outWeight: key.outWeight || 1 / 3,

        weightedMode: key.weightedMode,
    });

/**
 * One saved cinematic, as vanilla's asset writer emits it.
 *
 * `guid` is what save, load and delete address an asset by — never the name, which is not unique and
 * which the user can change. `cloudTarget` says which database it lives in, and has to be passed
 * back on load and delete or the game looks in the wrong one.
 */
export type CinematicAsset = {
    name: string;
    guid: string;
    identifier: string;
    cloudTarget: string;
    readOnly: boolean;
};

const kNoAssets: CinematicAsset[] = [];

export const cinematicAssets: ValueBinding<CinematicAsset[]> =
    bindValue<CinematicAsset[]>(kGroup, "assets", kNoAssets);

/** The asset most recently loaded or saved, or null. Vanilla uses it to preselect the list row. */
export const lastLoaded: ValueBinding<CinematicAsset | null> =
    bindValue<CinematicAsset | null>(kGroup, "lastLoaded", null);

export const cloudTargets: ValueBinding<string[]> =
    bindValue<string[]>(kGroup, "availableCloudTargets", []);

export const selectedCloudTarget: ValueBinding<string> =
    bindValue<string>(kGroup, "selectedCloudTarget", "Local");

export const selectCloudTarget = (target: string): void =>
    trigger(kGroup, "selectCloudTarget", target);

/**
 * Saves the open sequence.
 *
 * Passing a guid overwrites that asset; passing an empty string creates a new one under `name`. Both
 * arguments are required even when one is empty — the trigger is registered as taking two strings.
 */
export const saveSequence = (name: string, guid: string): void =>
    trigger(kGroup, "save", name, guid);

export const loadSequence = (guid: string, cloudTarget: string): void =>
    trigger(kGroup, "load", guid, cloudTarget);

export const deleteSequence = (guid: string, cloudTarget: string): void =>
    trigger(kGroup, "delete", guid, cloudTarget);

// Vanilla's bare `reset` clears the curves and nothing else, which leaves the timeline and the shot
// list disagreeing — an empty sequence with a full track under it. The panel calls the mod's
// `resetTimeline` in shot-bindings instead, which clears both. Deliberately not exported as a second
// reset: two functions that nearly do the same thing is how they drift apart.

export const looping: ValueBinding<boolean> = bindValue<boolean>(kGroup, "loop", false);

/**
 * Turning loop on runs EnsureLoop, which forces the last key of every curve to the first key's
 * value — so it is a real edit to the curves, not just a playback flag.
 */
export const toggleLoop = (loop: boolean): void => trigger(kGroup, "toggleLoop", loop);

/**
 * Undo and redo for the whole sequence. Ours — vanilla has none.
 *
 * The `BINDING:` / `TRIGGER:` prefixes are mandatory: `EPM_TimelineHistorySystem` registers these
 * through `CommonUISystemBase`'s helpers, which prefix every key they create. Without them the names
 * simply never match and both buttons do nothing at all — no error on either side, which is how they
 * went unnoticed. Anything registered with a raw `AddBinding` carries no prefix, which is why the two
 * key calls above have none.
 */
export const canUndo: ValueBinding<boolean> =
    bindValue<boolean>(kModGroup, "BINDING:timelineCanUndo", false);

export const canRedo: ValueBinding<boolean> =
    bindValue<boolean>(kModGroup, "BINDING:timelineCanRedo", false);

export const undo = (): void => trigger(kModGroup, "TRIGGER:timelineUndo");

export const redo = (): void => trigger(kModGroup, "TRIGGER:timelineRedo");

/** Whether our timeline panel is showing. Ours, so it goes through the mod's own group. */
export const timelineOpenBinding = new TwoWayBinding<boolean>("timelinePanelOpen", false);

export const closeTimeline = (): void => timelineOpenBinding.set(false);
