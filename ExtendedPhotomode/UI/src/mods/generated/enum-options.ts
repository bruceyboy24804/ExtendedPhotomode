// GENERATED FILE — do not edit.
//
// Produced by Tools/ExtendedPhotomode.Codegen from the [EnumOption] attributes on the
// mod's own enums. Change an icon or a tooltip in the C# and re-run it; editing this file
// instead means the next run silently discards your change.
//
//   dotnet run --project Tools/ExtendedPhotomode.Codegen -- \
//       ExtendedPhotomode ExtendedPhotomode/UI/src/mods/generated/enum-options.ts

/** One selectable option, as the panel's button rows consume it. */
export type EnumOption = {
    readonly mode: number;
    readonly src: string;
    readonly tooltip: string;
};

export const CameraRig = {
    Free: 1,
    Crane: 2,
    Drone: 3,
    Handheld: 4,
} as const;

export const CameraRigOptions: readonly EnumOption[] = [
    { mode: CameraRig.Free, src: "coui://extendedphotomode/Camera_Icons/RigFree.svg", tooltip: "No rig — the move plays exactly as solved, which is mathematically perfect and reads as computer generated." },
    { mode: CameraRig.Crane, src: "coui://extendedphotomode/Camera_Icons/RigCrane.svg", tooltip: "A heavy crane: slow to start, slow to stop, utterly smooth." },
    { mode: CameraRig.Drone, src: "coui://extendedphotomode/Camera_Icons/RigDrone.svg", tooltip: "A drone: quick but never instant, drifting a little on the wind." },
    { mode: CameraRig.Handheld, src: "coui://extendedphotomode/Camera_Icons/RigHandheld.svg", tooltip: "Handheld: follows the action closely and is never quite still." },
];

export const FocusMode = {
    Off: 1,
    Track: 2,
    Rack: 3,
} as const;

export const FocusModeOptions: readonly EnumOption[] = [
    { mode: FocusMode.Off, src: "coui://extendedphotomode/Camera_Icons/FocusOff.svg", tooltip: "Leave focus exactly as the panel has it." },
    { mode: FocusMode.Track, src: "coui://extendedphotomode/Camera_Icons/FocusTrack.svg", tooltip: "Keep the pinned subject sharp however far the camera travels." },
    { mode: FocusMode.Rack, src: "coui://extendedphotomode/Camera_Icons/FocusRack.svg", tooltip: "Ramp focus from the subject to a second point across the shot." },
];

export const FollowMode = {
    Off: 1,
    Aim: 2,
    Ride: 3,
} as const;

export const FollowModeOptions: readonly EnumOption[] = [
    { mode: FollowMode.Off, src: "coui://extendedphotomode/Camera_Icons/FollowOff.svg", tooltip: "The shot plays exactly as generated." },
    { mode: FollowMode.Aim, src: "coui://extendedphotomode/Camera_Icons/FollowAim.svg", tooltip: "Keyframed position, but the camera turns to hold a moving subject in frame." },
    { mode: FollowMode.Ride, src: "coui://extendedphotomode/Camera_Icons/FollowRide.svg", tooltip: "The whole shot travels with the subject, and aims at it." },
];

export const FramingRule = {
    Centre: 1,
    LeftThird: 2,
    RightThird: 3,
    Headroom: 4,
} as const;

export const FramingRuleOptions: readonly EnumOption[] = [
    { mode: FramingRule.Centre, src: "coui://extendedphotomode/Camera_Icons/FramingCentre.svg", tooltip: "Hold the subject dead centre." },
    { mode: FramingRule.LeftThird, src: "coui://extendedphotomode/Camera_Icons/FramingLeftThird.svg", tooltip: "Hold the subject on the left third, looking into the space on the right." },
    { mode: FramingRule.RightThird, src: "coui://extendedphotomode/Camera_Icons/FramingRightThird.svg", tooltip: "Hold the subject on the right third, looking into the space on the left." },
    { mode: FramingRule.Headroom, src: "coui://extendedphotomode/Camera_Icons/FramingHeadroom.svg", tooltip: "Centre the subject horizontally and sit it low, with headroom above." },
];

export const PathClearanceMode = {
    Off: 1,
    Warn: 2,
    Lift: 3,
} as const;

export const PathClearanceModeOptions: readonly EnumOption[] = [
    { mode: PathClearanceMode.Off, src: "coui://extendedphotomode/Camera_Icons/ObstacleOff.svg", tooltip: "Ignore buildings and other objects entirely." },
    { mode: PathClearanceMode.Warn, src: "coui://extendedphotomode/Camera_Icons/ObstacleWarn.svg", tooltip: "Draw obstructed stretches red and change nothing, leaving the fix to you." },
    { mode: PathClearanceMode.Lift, src: "coui://extendedphotomode/Camera_Icons/ObstacleLift.svg", tooltip: "Raise the camera over what it hits when the shot is generated, easing the climb into the run-up either side." },
];

export const PathLookMode = {
    Forward: 1,
    Fixed: 2,
    Target: 3,
    Rail: 4,
} as const;

export const PathLookModeOptions: readonly EnumOption[] = [
    { mode: PathLookMode.Forward, src: "coui://extendedphotomode/Camera_Icons/AimForward.svg", tooltip: "Look along the path's own direction of travel." },
    { mode: PathLookMode.Fixed, src: "coui://extendedphotomode/Camera_Icons/AimFixed.svg", tooltip: "Hold one compass heading for the whole move." },
    { mode: PathLookMode.Target, src: "coui://extendedphotomode/Camera_Icons/AimTarget.svg", tooltip: "Keep the pinned subject framed, solving pitch for every keyframe." },
    { mode: PathLookMode.Rail, src: "coui://extendedphotomode/Camera_Icons/AimRail.svg", tooltip: "Look at the matching point on the aim rail — the second drawn path." },
];

export const PathSnapMode = {
    Free: 1,
    Grid: 2,
    Angle: 3,
    Point: 4,
    Network: 5,
} as const;

export const PathSnapModeOptions: readonly EnumOption[] = [
    { mode: PathSnapMode.Free, src: "coui://extendedphotomode/Camera_Icons/SnapFree.svg", tooltip: "No snapping — the point lands where you click." },
    { mode: PathSnapMode.Grid, src: "coui://extendedphotomode/Camera_Icons/SnapGrid.svg", tooltip: "Round to a fixed grid, for paths that run square to the city." },
    { mode: PathSnapMode.Angle, src: "coui://extendedphotomode/Camera_Icons/SnapAngle.svg", tooltip: "Fix the heading from the previous point to a step, keeping the distance you reached." },
    { mode: PathSnapMode.Point, src: "coui://extendedphotomode/Camera_Icons/SnapPoint.svg", tooltip: "Land exactly on a point already placed — how a closed loop meets itself with no gap." },
    { mode: PathSnapMode.Network, src: "coui://extendedphotomode/Camera_Icons/SnapNetwork.svg", tooltip: "Follow the centreline of the road under the cursor, not where the click hit its surface." },
];

export const PathTerrainMode = {
    Free: 1,
    Floor: 2,
    Follow: 3,
} as const;

export const PathTerrainModeOptions: readonly EnumOption[] = [
    { mode: PathTerrainMode.Free, src: "coui://extendedphotomode/Camera_Icons/TerrainFree.svg", tooltip: "Use the heights you placed, ignoring the ground." },
    { mode: PathTerrainMode.Floor, src: "coui://extendedphotomode/Camera_Icons/TerrainFloor.svg", tooltip: "Keep your heights, but never let the path get closer to the ground than the clearance." },
    { mode: PathTerrainMode.Follow, src: "coui://extendedphotomode/Camera_Icons/TerrainFollow.svg", tooltip: "Hold one altitude above the ground for the whole path, ignoring your heights — the drone shot." },
];

export const ShotType = {
    Orbit: 1,
    DollyZoom: 2,
    Path: 3,
} as const;

export const ShotTypeOptions: readonly EnumOption[] = [
    { mode: ShotType.Orbit, src: "coui://extendedphotomode/Camera_Icons/OrbitTool.svg", tooltip: "Orbit — circle a subject. Drag its centre and the two ends of the sweep." },
    { mode: ShotType.DollyZoom, src: "coui://extendedphotomode/Camera_Icons/DollyTool.svg", tooltip: "Dolly zoom — travel towards or away from a subject while the lens counter-zooms. Drag the subject and the two ends of the track." },
    { mode: ShotType.Path, src: "coui://extendedphotomode/Camera_Icons/PathTool.svg", tooltip: "Drawn path — click the ground to place points and fly the curve through them." },
];
