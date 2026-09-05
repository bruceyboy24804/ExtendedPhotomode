import { trigger } from "cs2/api";
import { OneWayBinding } from "utils/onewayBinding";
import { TwoWayBinding } from "utils/bidirectionalBinding";
import triggers from "utils/trigger";

/**
 * Every binding and trigger the path tool's two panels share.
 *
 * The tool's UI is split across the vanilla tool options panel (what a click does, and the selected
 * point's properties) and a floating panel (the saved-path library). Both read the same state, so
 * declaring it in either one would leave the other importing from a component file.
 */

export type PathLibraryEntry = {
    index: number;

    name: string;

    points: number;
};

export type PathPointEntry = {
    index: number;

    x: number;

    y: number;

    z: number;

    dwell: number;

    pitch: number;

    pitchOverridden: boolean;

    broken: boolean;

    speed: number;

    fov: number;

    fovOverridden: boolean;

    timeOfDay: number;

    timeOfDayOverridden: boolean;

    lookAtSet: boolean;
};

/** Mirrors ExtendedPhotomode.Camera.PathEditMode. */
export const EditMode = { Points: 0, Curves: 1, LookAt: 2, Network: 3 } as const;

export const savedPathsBinding = new OneWayBinding<PathLibraryEntry[]>("savedPaths", []);

// Get/set pairs, where the binding and its trigger share one id. One-way reads use OneWayBinding.
export const toolActiveBinding = new TwoWayBinding<boolean>("pathToolActive", false);
export const panelOpenBinding = new TwoWayBinding<boolean>("pathPanelOpen", false);
export const editModeBinding = new TwoWayBinding<number>("pathEditMode", 0);

export const pointCountBinding = new OneWayBinding<number>("pathPointCount", 0);

/** Mirrors ExtendedPhotomode.Camera.PathSnapMode. Free is 1; 0 is the dropdown's sacrificial member. */
export const SnapMode = { Free: 1, Grid: 2, Angle: 3, Point: 4, Network: 5 } as const;

export const snapModeBinding = new TwoWayBinding<number>("pathSnapMode", SnapMode.Free);
export const closedBinding = new TwoWayBinding<boolean>("pathClosed", false);

/** Whichever number the active snap mode uses — grid size, angle step or snap radius. */
export const snapValueBinding = new TwoWayBinding<number>("pathSnapValue", 0);

/** Whether a preview flight is running. Setting it either way toggles. */
export const previewBinding = new TwoWayBinding<boolean>("pathPreviewing", false);

export type PathMetrics = {
    points: number;
    length: number;
    duration: number;
    /** Average camera speed in km/h. */
    speed: number;
    canUndo: boolean;
    canRedo: boolean;
    selected: number;
    frustums: boolean;
    railPoints: number;
};

/** Mirrors ExtendedPhotomode.Camera.PathTarget. */
export const EditTarget = { Camera: 0, Rail: 1 } as const;

export const editTargetBinding = new TwoWayBinding<number>("pathEditTarget", EditTarget.Camera);

/** Mirrors ExtendedPhotomode.Camera.PathTerrainMode and PathClearanceMode. */
export const TerrainMode = { Free: 1, Floor: 2, Follow: 3 } as const;
export const ObstacleMode = { Off: 1, Warn: 2, Lift: 3 } as const;

/**
 * The tool's loose numbers, as one binding.
 *
 * Three of these had no UI at all and were stuck on their defaults, because each needed its own
 * binding, trigger and row. One keyed setter makes the next one a field and a row.
 */
export type PathNumbers = {
    traceLength: number;
    traceSpacing: number;
    simplifyTolerance: number;
    nudgeStep: number;
    railOffset: number;
    obstacleClearance: number;
    terrainClearance: number;
    terrainMode: number;
    obstacleMode: number;
    shotType: number;
    hasSubject: boolean;

    orbitRadius: number;
    orbitEndRadius: number;
    orbitHeight: number;
    orbitEndHeight: number;
    orbitSweep: number;
    orbitSweepEase: number;
    orbitDuration: number;
    orbitSpacing: number;
    orbitLookAt: boolean;
    orbitPreview: boolean;

    pathDuration: number;
    pathSpacing: number;
    pathPitch: number;
    pathLook: number;
    pathLookAhead: number;
    pathEase: number;

    framing: number;
    framingHold: boolean;
    framingLens: number;
    focus: number;
    focusDepth: number;
    focusEase: number;
    rig: number;
    rigStrength: number;
    rigSeed: number;
    follow: number;

    dollyStart: number;
    dollyEnd: number;
    dollyDuration: number;
    dollyKeys: number;

    hasClipboard: boolean;
};

/** Mirrors ExtendedPhotomode.Camera.ShotType. */
export const ShotTypes = { Orbit: 1, DollyZoom: 2, Path: 3 } as const;

/** Mirrors the shot-look enums. Each has a sacrificial 0 that the panel never offers. */
export const LookMode = { Forward: 1, Fixed: 2, Target: 3, Rail: 4 } as const;
export const Framing = { Off: 0, Centre: 1, LeftThird: 2, RightThird: 3, Headroom: 4 } as const;
export const Focus = { Off: 1, Track: 2, Rack: 3 } as const;
export const Rig = { Free: 1, Crane: 2, Drone: 3, Handheld: 4 } as const;
export const Follow = { Off: 1, Aim: 2, Ride: 3 } as const;

export const numbersBinding = new OneWayBinding<PathNumbers>("pathNumbers", {
    traceLength: 500,
    traceSpacing: 40,
    simplifyTolerance: 5,
    nudgeStep: 5,
    railOffset: 60,
    obstacleClearance: 15,
    terrainClearance: 20,
    terrainMode: TerrainMode.Free,
    obstacleMode: ObstacleMode.Warn,
    shotType: 3,
    hasSubject: false,

    orbitRadius: 150,
    orbitEndRadius: 150,
    orbitHeight: 60,
    orbitEndHeight: 60,
    orbitSweep: 360,
    orbitSweepEase: 0,
    orbitDuration: 30,
    orbitSpacing: 30,
    orbitLookAt: true,
    orbitPreview: true,

    pathDuration: 30,
    pathSpacing: 25,
    pathPitch: 15,
    pathLook: 1,
    pathLookAhead: 0,
    pathEase: 0,

    framing: 0,
    framingHold: false,
    framingLens: 50,
    focus: 1,
    focusDepth: 0.5,
    focusEase: 0.6,
    rig: 1,
    rigStrength: 0.6,
    rigSeed: 1,
    follow: 1,

    dollyStart: 120,
    dollyEnd: 40,
    dollyDuration: 12,
    dollyKeys: 24,

    hasClipboard: false,
});

export const editAllBinding = new TwoWayBinding<boolean>("pathEditAll", false);

export const metricsBinding = new OneWayBinding<PathMetrics>("pathMetrics", {
    points: 0,
    length: 0,
    duration: 0,
    speed: 0,
    canUndo: false,
    canRedo: false,
    selected: 0,
    frustums: true,
    railPoints: 0,
});

/** The library path being edited, or -1 when the drawn path is scratch work. */
export const loadedPathBinding = new OneWayBinding<number>("pathLoadedId", -1);
export const placementHeightBinding = new OneWayBinding<number>("pathPlacementHeight", 0);

export const selectedPointBinding = new OneWayBinding<PathPointEntry>("pathSelectedPoint", {
    index: -1,
    x: 0,
    y: 0,
    z: 0,
    dwell: 0,
    pitch: 0,
    pitchOverridden: false,
    broken: false,
    speed: 1,
    fov: 0,
    fovOverridden: false,
    timeOfDay: 0,
    timeOfDayOverridden: false,
    lookAtSet: false,
});

/** The vanilla click sound, which is its own binding group rather than one of ours. */
export const click = (): void => trigger("audio", "playSound", "select-item", 1);

export const editPoint = triggers.create<[string, number]>("editPathPoint");
export const selectPoint = triggers.create<[number]>("selectPathPoint");
export const generateShot = triggers.create<[]>("generatePathShot");
export const newPath = triggers.create<[]>("newPath");
export const importFromSequence = triggers.create<[]>("importPathFromSequence");

/** Whole-path operations, keyed by name the way editPoint keys its fields. */
export const transformPath = triggers.create<[string]>("transformPath");
export const setNumber = triggers.create<[string, number]>("setPathNumber");
export const clipboard = triggers.create<[string]>("pathClipboard");
export const nudge = triggers.create<[number, number]>("nudgePathPoints");
export const deletePath = triggers.create<[number]>("deletePath");
export const renamePath = triggers.create<[number, string]>("renamePath");

const rawSave = triggers.create<[string]>("savePath");
const rawLoad = triggers.create<[number]>("loadPath");

export const setToolActive = (active: boolean): void => toolActiveBinding.set(active);
export const setEditMode = (mode: number): void => editModeBinding.set(mode);
export const setSnapMode = (mode: number): void => snapModeBinding.set(mode);
export const setEditTarget = (target: number): void => editTargetBinding.set(target);
export const setEditAll = (all: boolean): void => editAllBinding.set(all);
export const setClosed = (closed: boolean): void => closedBinding.set(closed);
export const setSnapValue = (value: number): void => snapValueBinding.set(Math.round(value));
export const closePanel = (): void => panelOpenBinding.set(false);
export const togglePreview = (): void => previewBinding.set(!previewBinding.binding.value);

export function savePath(name: string): void {
    click();
    rawSave(name);
}

export function loadPath(id: number): void {
    click();
    rawLoad(id);
}
