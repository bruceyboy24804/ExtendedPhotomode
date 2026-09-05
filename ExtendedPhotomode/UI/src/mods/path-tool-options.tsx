import { useValue } from "cs2/api";
import { type ModuleRegistryExtend } from "cs2/modding";
import { type ReactElement, useState } from "react";
import { VC, VF, VT } from "vanilla/Components";
import styles from "./path-tool-options.module.scss";
import {
    EditMode,
    SnapMode,
    closedBinding,
    editModeBinding,
    editPoint,
    placementHeightBinding,
    pointCountBinding,
    selectPoint,
    selectedPointBinding,
    setClosed,
    setEditMode,
    setSnapMode,
    setSnapValue,
    transformPath,
    metricsBinding,
    Focus,
    Follow,
    Framing,
    LookMode,
    ObstacleMode,
    type PathNumbers,
    Rig,
    ShotTypes,
    TerrainMode,
    clipboard,
    editAllBinding,
    numbersBinding,
    nudge,
    previewBinding,
    setEditAll,
    setNumber,
    togglePreview,
    EditTarget,
    editTargetBinding,
    setEditTarget,
    snapModeBinding,
    snapValueBinding,
    toolActiveBinding,
} from "./path-bindings";
import { uiHiddenBinding } from "./shot-bindings";

const i_points = "coui://uil/Standard/ArrowsMoveAll.svg";
// Verified against the installed Unified Icon Library. A name that does not exist there silently
// falls back to Media/Placeholder.svg rather than erroring, so these are worth checking on disk.
const i_curves = "coui://extendedphotomode/Camera_Icons/EditCurve.svg";
const i_reset = "coui://uil/Standard/Reset.svg";
// Shared by Look at, Subject and orbit Aim — all three ask "which thing in the world is the
// subject", so they get the marker. Hold size asks what the FRAME looks like and gets its own.
const i_target = "coui://extendedphotomode/Camera_Icons/SubjectPin.svg";
const i_holdSize = "coui://extendedphotomode/Camera_Icons/FramingHoldSize.svg";
const i_loop = "coui://uil/Standard/Circle.svg";
const i_lookAt = "coui://extendedphotomode/Camera_Icons/Trace.svg";

// Whole-path operations. These are what make a saved path reusable: it is stored in world
// coordinates, so without them loading one only ever puts the same shot back in the same place.
// Ghost-and-solid throughout: white for where the path was, blue for where it ends up. The mirror
// pair especially — they used to be ArrowsMoveLeftRight and ArrowsMoveUpDown, double-headed arrows
// that mean TRANSLATE, so anyone reading them as "nudge sideways" was reading them correctly.
const TRANSFORMS = [
    { op: "rotateLeft", src: "coui://extendedphotomode/Camera_Icons/PathRotateLeft.svg", tooltip: "Turn the whole path anticlockwise about its centre, by the snap angle step." },
    { op: "rotateRight", src: "coui://extendedphotomode/Camera_Icons/PathRotateRight.svg", tooltip: "Turn the whole path clockwise about its centre, by the snap angle step." },
    { op: "shrink", src: "coui://extendedphotomode/Camera_Icons/PathShrink.svg", tooltip: "Draw the path in towards its centre by a tenth. Heights are left alone." },
    { op: "grow", src: "coui://extendedphotomode/Camera_Icons/PathGrow.svg", tooltip: "Spread the path out from its centre by a tenth. Heights are left alone." },
    { op: "mirrorX", src: "coui://extendedphotomode/Camera_Icons/PathMirrorX.svg", tooltip: "Flip the path east to west about its centre." },
    { op: "mirrorZ", src: "coui://extendedphotomode/Camera_Icons/PathMirrorZ.svg", tooltip: "Flip the path north to south about its centre." },
];

const i_frustum = "coui://extendedphotomode/Camera_Icons/ShowFrustums.svg";
const i_travel = "coui://uil/Standard/VideoCamera.svg";
const i_rail = "coui://uil/Standard/MapMarker.svg";
const i_trace = "coui://uil/Standard/Network.svg";
// The Selection row draws its points in #ff73bf, kSelectedColor — the colour a selected point really
// is on the terrain — so the row reads as a legend for the world rather than three generic symbols.
const i_all = "coui://extendedphotomode/Camera_Icons/SelectionAll.svg";
const i_copy = "coui://extendedphotomode/Camera_Icons/SelectionCopy.svg";
const i_paste = "coui://extendedphotomode/Camera_Icons/SelectionPaste.svg";
const i_railFrom = "coui://extendedphotomode/Camera_Icons/RailFromPath.svg";
const i_preview = "coui://extendedphotomode/Camera_Icons/RailPreview.svg";

// Point-set utilities: change how many control points a path has, or where they sit, without
// changing the shape it flies.
// Every icon in this row draws the same curve, which is the row's whole promise: these change the
// points without changing the shape the camera flies. A bare plus and minus said nothing of the kind.
const UTILITIES = [
    { op: "subdivide", src: "coui://extendedphotomode/Camera_Icons/PointsSubdivide.svg", tooltip: "Add a point at the middle of every segment. The new points land on the existing curve, so the shape does not change — you just get more places to grab." },
    { op: "simplify", src: "coui://extendedphotomode/Camera_Icons/PointsSimplify.svg", tooltip: "Drop points the curve barely needs. Points carrying a dwell, speed, lens, hour, pitch or target of their own are always kept." },
    { op: "respace", src: "coui://extendedphotomode/Camera_Icons/PointsRespace.svg", tooltip: "Spread the points evenly along the curve by distance, keeping the shape and the count." },
    { op: "selectAll", src: "coui://extendedphotomode/Camera_Icons/PointsSelectAll.svg", tooltip: "Select every point, so a drag moves the whole set together." },
    { op: "selectNone", src: "coui://extendedphotomode/Camera_Icons/PointsSelectNone.svg", tooltip: "Clear the selection." },
];

// The shot type, mirrored out of the photo mode panel. Its dropdown lives there, and photo mode
// forces the default tool — so without this the tool could author three shot types but could not be
// told which one while it was open.
const SHOT_TYPES = [
    { mode: ShotTypes.Orbit, src: "coui://extendedphotomode/Camera_Icons/OrbitTool.svg", tooltip: "Orbit — circle a subject. Drag its centre and the two ends of the sweep." },
    { mode: ShotTypes.DollyZoom, src: "coui://extendedphotomode/Camera_Icons/DollyTool.svg", tooltip: "Dolly zoom — travel towards or away from a subject while the lens counter-zooms. Drag the subject and the two ends of the track." },
    { mode: ShotTypes.Path, src: "coui://extendedphotomode/Camera_Icons/PathTool.svg", tooltip: "Drawn path — click the ground to place points and fly the curve through them." },
];

const LOOK_MODES = [
    // One path with two cameras on it, four times over; only the cones move. Aim mode changes nothing
    // about where the camera goes, so nothing about the path should change between these four either.
    { mode: LookMode.Forward, src: "coui://extendedphotomode/Camera_Icons/AimForward.svg", tooltip: "Look along the path's own direction of travel." },
    { mode: LookMode.Fixed, src: "coui://extendedphotomode/Camera_Icons/AimFixed.svg", tooltip: "Hold one compass heading for the whole move." },
    { mode: LookMode.Target, src: "coui://extendedphotomode/Camera_Icons/AimTarget.svg", tooltip: "Keep the pinned subject framed, solving pitch for every keyframe." },
    { mode: LookMode.Rail, src: "coui://extendedphotomode/Camera_Icons/AimRail.svg", tooltip: "Look at the matching point on the aim rail — the second drawn path." },
];

const FRAMINGS = [
    // Viewfinders, not maps: framing is the one question about where the subject sits IN FRAME. The
    // arrows these replace pointed in a direction, which is not what a framing rule is.
    { mode: Framing.Off, src: "coui://extendedphotomode/Camera_Icons/FramingOff.svg", tooltip: "No framing rule; the shot aims however its own settings say." },
    { mode: Framing.Centre, src: "coui://extendedphotomode/Camera_Icons/FramingCentre.svg", tooltip: "Hold the subject dead centre." },
    { mode: Framing.LeftThird, src: "coui://extendedphotomode/Camera_Icons/FramingLeftThird.svg", tooltip: "Hold the subject on the left third, looking into the space on the right." },
    { mode: Framing.RightThird, src: "coui://extendedphotomode/Camera_Icons/FramingRightThird.svg", tooltip: "Hold the subject on the right third, looking into the space on the left." },
    { mode: Framing.Headroom, src: "coui://extendedphotomode/Camera_Icons/FramingHeadroom.svg", tooltip: "Centre the subject horizontally and sit it low, with headroom above." },
];

const FOCUS_MODES = [
    // Drawn along the optical axis with the focal plane as a blue bar, since focus is purely a
    // question of distance. Off has no bar, Track has one, Rack has two and a ramp.
    { mode: Focus.Off, src: "coui://extendedphotomode/Camera_Icons/FocusOff.svg", tooltip: "Leave focus exactly as the panel has it." },
    { mode: Focus.Track, src: "coui://extendedphotomode/Camera_Icons/FocusTrack.svg", tooltip: "Keep the pinned subject sharp however far the camera travels." },
    { mode: Focus.Rack, src: "coui://extendedphotomode/Camera_Icons/FocusRack.svg", tooltip: "Ramp focus from the subject to a second point across the shot." },
];

const RIGS = [
    // One trace, four amounts of character — clean, weighted, drifting, shaky. Rig is the only
    // setting that changes how a move FEELS rather than where it goes, and read left to right this
    // row is a scale, which the old pictograms could never have been.
    { mode: Rig.Free, src: "coui://extendedphotomode/Camera_Icons/RigFree.svg", tooltip: "No rig — the move plays exactly as solved, which is mathematically perfect and reads as computer generated." },
    { mode: Rig.Crane, src: "coui://extendedphotomode/Camera_Icons/RigCrane.svg", tooltip: "A heavy crane: slow to start, slow to stop, utterly smooth." },
    { mode: Rig.Drone, src: "coui://extendedphotomode/Camera_Icons/RigDrone.svg", tooltip: "A drone: quick but never instant, drifting a little on the wind." },
    { mode: Rig.Handheld, src: "coui://extendedphotomode/Camera_Icons/RigHandheld.svg", tooltip: "Handheld: follows the action closely and is never quite still." },
];

const FOLLOWS = [
    // One scene: a subject that moves, ghost to solid. Off leaves the cone behind, Aim swings the
    // cone across, Ride carries the camera too. The subject moving is the premise of the row.
    { mode: Follow.Off, src: "coui://extendedphotomode/Camera_Icons/FollowOff.svg", tooltip: "The shot plays exactly as generated." },
    { mode: Follow.Aim, src: "coui://extendedphotomode/Camera_Icons/FollowAim.svg", tooltip: "Keyframed position, but the camera turns to hold a moving subject in frame." },
    { mode: Follow.Ride, src: "coui://extendedphotomode/Camera_Icons/FollowRide.svg", tooltip: "The whole shot travels with the subject, and aims at it." },
];

// Drawn as a set: one ridge, three curves over it. The stock icons that were here — an ✕, an arrow
// and a grid — were three unrelated symbols, so nothing about them said they were the same choice.
// Now the difference IS the shape of the line: through the hill, lifted over it, or parallel to it.
const TERRAINS = [
    { mode: TerrainMode.Free, src: "coui://extendedphotomode/Camera_Icons/TerrainFree.svg", tooltip: "Use the heights you placed, ignoring the ground." },
    { mode: TerrainMode.Floor, src: "coui://extendedphotomode/Camera_Icons/TerrainFloor.svg", tooltip: "Keep your heights, but never let the path get closer to the ground than the clearance." },
    { mode: TerrainMode.Follow, src: "coui://extendedphotomode/Camera_Icons/TerrainFollow.svg", tooltip: "Hold one altitude above the ground for the whole path, ignoring your heights — the drone shot." },
];

// The same treatment as TERRAINS: one scene, three curves over it. A skyline rather than a ridge,
// because Terrain/Free and Obstacle/Off both mean "ignore it and fly straight" — drawn against the
// same scene the two rows would be indistinguishable, and the scene is what tells them apart.
const OBSTACLES = [
    { mode: ObstacleMode.Off, src: "coui://extendedphotomode/Camera_Icons/ObstacleOff.svg", tooltip: "Ignore buildings and other objects entirely." },
    { mode: ObstacleMode.Warn, src: "coui://extendedphotomode/Camera_Icons/ObstacleWarn.svg", tooltip: "Draw obstructed stretches red and change nothing, leaving the fix to you." },
    { mode: ObstacleMode.Lift, src: "coui://extendedphotomode/Camera_Icons/ObstacleLift.svg", tooltip: "Raise the camera over what it hits when the shot is generated, easing the climb into the run-up either side." },
];

// Each shot type gets its own pages: a drawn path has three groups of controls, an orbit and a dolly
// two, and the labels differ — so the pager is per-type rather than a fixed three.
//
// Icons stay distinct from the buttons on the pages they open; a pager icon that repeats a control's
// icon reads as that control rather than as a page.
const PAGE_SETS: Record<number, { label: string; icon: string }[]> = {
    [ShotTypes.Path]: [
        { label: "Editing and snapping", icon: "coui://extendedphotomode/Camera_Icons/EditSnap.svg" },
        { label: "The selected point", icon: "coui://extendedphotomode/Camera_Icons/SelectedPoint.svg" },
        { label: "The path as a whole", icon: "coui://extendedphotomode/Camera_Icons/WholePath.svg" },
        { label: "Shape and timing", icon: "coui://extendedphotomode/Camera_Icons/ShapeTiming.svg" },
        { label: "How it is shot", icon: "coui://uil/Standard/VideoCamera.svg" },
    ],
    [ShotTypes.Orbit]: [
        { label: "Shape of the orbit", icon: "coui://uil/Standard/Circle.svg" },
        { label: "Timing and keys", icon: "coui://uil/Standard/Clock.svg" },
        { label: "How it is shot", icon: "coui://uil/Standard/VideoCamera.svg" },
    ],
    [ShotTypes.DollyZoom]: [
        { label: "Shape of the move", icon: "coui://uil/Standard/ArrowsMoveLeftRight.svg" },
        { label: "Timing and keys", icon: "coui://uil/Standard/Clock.svg" },
        { label: "How it is shot", icon: "coui://uil/Standard/VideoCamera.svg" },
    ],
};

// Raise and lower are drawn side-on against a ground line, since this row is the only one about
// height; moveHere is plan view, since it is the only one about where on the map the path sits.
const MOVES = [
    { op: "lower", src: "coui://extendedphotomode/Camera_Icons/PlaceLower.svg", tooltip: "Lower every point by the height step." },
    { op: "raise", src: "coui://extendedphotomode/Camera_Icons/PlaceRaise.svg", tooltip: "Raise every point by the height step." },
    { op: "moveHere", src: "coui://extendedphotomode/Camera_Icons/PlaceHere.svg", tooltip: "Move the whole path so its centre lands under the cursor, keeping its height above the ground." },
];

// Plan view, where TERRAINS and OBSTACLES are cross-sections seen from the side. Snapping is about
// where a click lands on the ground, and the change of viewpoint is what keeps a third row of blue
// line over white scenery from reading as more of the same.
const SNAPS = [
    { mode: SnapMode.Free, src: "coui://extendedphotomode/Camera_Icons/SnapFree.svg", tooltip: "No snapping — the point lands where you click." },
    { mode: SnapMode.Grid, src: "coui://extendedphotomode/Camera_Icons/SnapGrid.svg", tooltip: "Round to a fixed grid, for paths that run square to the city." },
    { mode: SnapMode.Angle, src: "coui://extendedphotomode/Camera_Icons/SnapAngle.svg", tooltip: "Fix the heading from the previous point to a step, keeping the distance you reached." },
    { mode: SnapMode.Point, src: "coui://extendedphotomode/Camera_Icons/SnapPoint.svg", tooltip: "Land exactly on a point already placed — how a closed loop meets itself with no gap." },
    { mode: SnapMode.Network, src: "coui://extendedphotomode/Camera_Icons/SnapNetwork.svg", tooltip: "Follow the centreline of the road under the cursor, not where the click hit its surface." },
];

// Grid, angle and point each drive one number; the other two modes have none, which is what hides
// the field rather than showing a box that does nothing.
const SNAP_VALUE_LABEL: Record<number, { title: string; suffix: string; max: number } | undefined> = {
    [SnapMode.Grid]: { title: "Grid size", suffix: "m", max: 100 },
    [SnapMode.Angle]: { title: "Angle step", suffix: "°", max: 90 },
    [SnapMode.Point]: { title: "Snap radius", suffix: "m", max: 100 },
};

/**
 * Forces the vanilla tool options panel visible while the path tool is running.
 *
 * This extends a hook rather than a component, so it has to call through and OR with the vanilla
 * answer — returning only our own condition would hide the panel for every other tool.
 */
export const PathToolOptionsVisibility: ModuleRegistryExtend = (Component: any) => {
    return () => {
        // useValue, not binding.value: reading the binding directly does not subscribe, so React
        // never re-renders this hook's owner when the tool becomes active and our answer is never
        // observed. The vanilla call has to come first and unconditionally, or the hook count
        // changes between renders.
        const vanilla = Component();
        const active = useValue(toolActiveBinding.binding);

        return vanilla || active;
    };
};

/**
 * Puts what a click does, and the selected point's properties, into the game's own tool options.
 *
 * These are per-interaction settings and only mean anything while the tool is running, which is
 * exactly what the tool options panel is for — and it brings vanilla's styling, sizing and focus
 * handling with it, so each new per-point property is a row rather than a row plus its own CSS.
 *
 * The saved-path library deliberately stays a floating panel: it has to work with the tool switched
 * off, and tool options only render while a tool is active. Write Everywhere splits the same way,
 * keeping its hierarchy and appearance editors in their own windows.
 */
export const PathToolOptions: ModuleRegistryExtend = (Component: any) => {
    return (props: any) => {
        const result = Component(props);
        const active = useValue(toolActiveBinding.binding);

        // Hidden hides this too. Leaving the tool options up over an otherwise clean frame defeats
        // the point, and it is the same reasoning that takes the world gizmos with it.
        const hidden = useValue(uiHiddenBinding.binding);

        if (!active || hidden) {
            return result;
        }

        result.props.children ??= [];
        result.props.children.unshift(<PathToolOptionsPanel key="epm-path" />);

        return result;
    };
};

function PathToolOptionsPanel(): ReactElement {
    const editMode = useValue(editModeBinding.binding);
    const point = useValue(selectedPointBinding.binding);
    const points = useValue(pointCountBinding.binding);
    const placementHeight = useValue(placementHeightBinding.binding);
    const snapMode = useValue(snapModeBinding.binding);
    const snapValue = useValue(snapValueBinding.binding);
    const closed = useValue(closedBinding.binding);
    const metrics = useValue(metricsBinding.binding);
    const editTarget = useValue(editTargetBinding.binding);
    const numbers = useValue(numbersBinding.binding);
    const editAll = useValue(editAllBinding.binding);
    const previewing = useValue(previewBinding.binding);
    const [page, setPage] = useState(0);

    return (
        <>
            <VC.Section title="Shot">
                {SHOT_TYPES.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.shotType === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("shotType", mode)}
                    />
                ))}
            </VC.Section>

            {/* Which of the two paths a click edits. Above the pager because it changes the meaning
                of every control below it — the whole editor re-points at the other path. */}
            <VC.Section title="Editing">
                <VC.ToolButton
                    src={i_travel}
                    selected={editTarget === EditTarget.Camera}
                    tooltip="Edit the path the camera travels."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setEditTarget(EditTarget.Camera)}
                />

                <VC.ToolButton
                    src={i_rail}
                    selected={editTarget === EditTarget.Rail}
                    tooltip="Edit the aim rail — the second path the camera looks at as it travels. Set Aim to Rail on the photo mode panel to use it."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setEditTarget(EditTarget.Rail)}
                />

                <div className={styles.metrics}>
                    {editTarget === EditTarget.Rail
                        ? `Rail · ${metrics.railPoints}`
                        : `Camera · ${points}`}
                </div>
            </VC.Section>

            <Pager page={page} setPage={setPage} shotType={numbers.shotType} />

            {numbers.shotType !== ShotTypes.Path && (
                <SubjectSection numbers={numbers} />
            )}

            {numbers.shotType === ShotTypes.Orbit && page === 0 && (
                <OrbitShape numbers={numbers} />
            )}

            {numbers.shotType === ShotTypes.Orbit && page === 1 && (
                <OrbitTiming numbers={numbers} />
            )}

            {numbers.shotType === ShotTypes.DollyZoom && page === 0 && (
                <DollyShape numbers={numbers} />
            )}

            {numbers.shotType === ShotTypes.DollyZoom && page === 1 && (
                <DollyTiming numbers={numbers} />
            )}

            {numbers.shotType === ShotTypes.Path && page === 3 && <PathShape numbers={numbers} />}

            {/* The last page of every type is the same one: how the shot is shot is a property of the
                shot, not of how its geometry was arrived at. */}
            {((numbers.shotType === ShotTypes.Path && page === 4) ||
              (numbers.shotType !== ShotTypes.Path && page === 2)) && <ShotLook numbers={numbers} />}

            {page === 0 && numbers.shotType === ShotTypes.Path && (
                <>
            <VC.Section title="Click acts on">
                <VC.ToolButton
                    src={i_points}
                    selected={editMode === EditMode.Points}
                    tooltip="Add, insert and move points. Curve handles are hidden."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setEditMode(EditMode.Points)}
                />

                <VC.ToolButton
                    src={i_curves}
                    selected={editMode === EditMode.Curves}
                    tooltip="Show every curve handle and drag them to shape the path."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setEditMode(EditMode.Curves)}
                />

                <VC.ToolButton
                    src={i_trace}
                    selected={editMode === EditMode.Network}
                    tooltip="Click a road, rail or tram line to trace it into a path. Replaces whatever is drawn — Ctrl+Z brings it back."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setEditMode(EditMode.Network)}
                />

                <VC.ToolButton
                    src={i_lookAt}
                    selected={editMode === EditMode.LookAt}
                    tooltip="Click the world to aim the selected point at it; click a point to choose which one. Aiming lines are drawn for every point while this is on."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setEditMode(EditMode.LookAt)}
                />
            </VC.Section>

            {editMode === EditMode.Points && (
                <VC.Section title="New point">
                    <div className={`${VT.mouseToolOptions?.numberField} ${styles.numberBox}`}>
                        {placementHeight}m
                    </div>
                </VC.Section>
            )}

            {/* Snapping lives here rather than only on the photo mode panel because drawing happens
                in normal gameplay, where that panel is not on screen at all. */}
            <VC.Section title="Snap">
                {SNAPS.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={snapMode === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setSnapMode(mode)}
                    />
                ))}
            </VC.Section>

            {SNAP_VALUE_LABEL[snapMode] && (
                <SliderSection
                    title={SNAP_VALUE_LABEL[snapMode]!.title}
                    value={snapValue}
                    suffix={SNAP_VALUE_LABEL[snapMode]!.suffix}
                    min={1}
                    max={SNAP_VALUE_LABEL[snapMode]!.max}
                    step={1}
                    onChange={setSnapValue}
                />
            )}

            {/* Only meaningful in the tracing mode, and unreachable anywhere else until now. */}
            {editMode === EditMode.Network && (
                <>
                    <SliderSection title="Trace length" value={numbers.traceLength} suffix="m"
                                   min={50} max={3000} step={50}
                                   onChange={(v) => setNumber("traceLength", v)} />

                    <SliderSection title="Trace spacing" value={numbers.traceSpacing} suffix="m"
                                   min={5} max={200} step={5}
                                   onChange={(v) => setNumber("traceSpacing", v)} />
                </>
            )}

            {/* Terrain and obstacles both reshape what is drawn, so they belong where the path is
                visible rather than on the photo mode panel, where it is not. */}
            <VC.Section title="Terrain">
                {TERRAINS.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.terrainMode === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("terrainMode", mode)}
                    />
                ))}
            </VC.Section>

            {numbers.terrainMode !== TerrainMode.Free && (
                <SliderSection title="Ground clear" value={numbers.terrainClearance} suffix="m"
                               min={0} max={300} step={5}
                               onChange={(v) => setNumber("terrainClearance", v)} />
            )}

            <VC.Section title="Obstacles">
                {OBSTACLES.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.obstacleMode === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("obstacleMode", mode)}
                    />
                ))}
            </VC.Section>

            {numbers.obstacleMode !== ObstacleMode.Off && (
                <SliderSection title="Object clear" value={numbers.obstacleClearance} suffix="m"
                               min={0} max={150} step={5}
                               onChange={(v) => setNumber("obstacleClearance", v)} />
            )}

            <VC.Section title="Loop">
                <VC.ToolButton
                    src={i_loop}
                    selected={closed}
                    disabled={points < 3}
                    tooltip="Join the last point back to the first as a real curve segment, so the join is as smooth as any other corner. Needs at least three points."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setClosed(!closed)}
                />
            </VC.Section>
                </>
            )}

            {page === 1 && numbers.shotType === ShotTypes.Path && (
                <>
                    <VC.Section title="Selection">
                        <VC.ToolButton
                            src={i_all}
                            selected={editAll}
                            tooltip="Apply every property change below to all selected points, not just the one shown. Position across and along are left out — a shared X would stack the whole selection into one column."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => setEditAll(!editAll)}
                        />

                        <VC.ToolButton
                            src={i_copy}
                            disabled={point.index < 0}
                            tooltip="Copy this point's properties — dwell, speed, pitch, lens, hour, target and corner. Position is never copied."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => clipboard("copy")}
                        />

                        <VC.ToolButton
                            src={i_paste}
                            disabled={!numbers.hasClipboard || point.index < 0}
                            tooltip="Stamp the copied properties onto every selected point."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => clipboard("paste")}
                        />

                        <div className={styles.metrics}>{`${metrics.selected} sel`}</div>
                    </VC.Section>

                    {/* Buttons, not the arrow keys: those already drive the game camera, and a tool
                        that stole them would break panning whenever it was open. */}
                    <VC.Section title="Nudge">
                        <VC.ToolButton src="coui://uil/Standard/ArrowLeft.svg" disabled={metrics.selected === 0}
                                       tooltip="Move the selection west by one step"
                                       focusKey={VF.FOCUS_DISABLED} className={VT.toolButton?.button}
                                       onSelect={() => nudge(-1, 0)} />

                        <VC.ToolButton src="coui://uil/Standard/ArrowRight.svg" disabled={metrics.selected === 0}
                                       tooltip="Move the selection east by one step"
                                       focusKey={VF.FOCUS_DISABLED} className={VT.toolButton?.button}
                                       onSelect={() => nudge(1, 0)} />

                        <VC.ToolButton src="coui://uil/Standard/ArrowUp.svg" disabled={metrics.selected === 0}
                                       tooltip="Move the selection north by one step"
                                       focusKey={VF.FOCUS_DISABLED} className={VT.toolButton?.button}
                                       onSelect={() => nudge(0, 1)} />

                        <VC.ToolButton src="coui://uil/Standard/ArrowDown.svg" disabled={metrics.selected === 0}
                                       tooltip="Move the selection south by one step"
                                       focusKey={VF.FOCUS_DISABLED} className={VT.toolButton?.button}
                                       onSelect={() => nudge(0, -1)} />
                    </VC.Section>

                    <SliderSection title="Nudge step" value={numbers.nudgeStep} suffix="m"
                                   min={1} max={50} step={1}
                                   onChange={(v) => setNumber("nudgeStep", v)} />

            {point.index >= 0 && (
                <>
                    <VC.Section title="Point">
                        <VC.ToolButton
                            src="coui://uil/Standard/ArrowLeft.svg"
                            disabled={point.index <= 0}
                            tooltip="Select the previous point"
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => selectPoint(point.index - 1)}
                        />

                        <div className={`${VT.mouseToolOptions?.numberField} ${styles.numberBox}`}>
                            {point.index + 1} / {points}
                        </div>

                        <VC.ToolButton
                            src="coui://uil/Standard/ArrowRight.svg"
                            disabled={point.index >= points - 1}
                            tooltip="Select the next point"
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => selectPoint(point.index + 1)}
                        />
                    </VC.Section>

                    <NumberSection title="X" value={point.x} suffix="m"
                                   onCommit={(v) => editPoint("x", v)} />

                    <NumberSection title="Height" value={point.y} suffix="m"
                                   onCommit={(v) => editPoint("y", v)} />

                    <NumberSection title="Z" value={point.z} suffix="m"
                                   onCommit={(v) => editPoint("z", v)} />

                    <SliderSection title="Dwell" value={point.dwell} suffix="s"
                                   min={0} max={30} step={0.5}
                                   onChange={(v) => editPoint("dwell", v)} />

                    <SliderSection
                        title="Pitch"
                        value={point.pitch}
                        suffix="°"
                        min={-80}
                        max={80}
                        step={1}
                        onChange={(v) => editPoint("pitch", v)}
                        extra={
                            <VC.ToolButton
                                src={i_reset}
                                disabled={!point.pitchOverridden}
                                tooltip="Use the path's own pitch here again"
                                focusKey={VF.FOCUS_DISABLED}
                                className={VT.toolButton?.button}
                                onSelect={() => editPoint("clearPitch", 0)}
                            />
                        }
                    />

                    <SliderSection title="Speed" value={point.speed} suffix="x"
                                   min={0.1} max={4} step={0.1}
                                   onChange={(v) => editPoint("speed", v)} />

                    <NumberSection
                        title="Lens"
                        value={point.fov}
                        suffix="mm"
                        onCommit={(v) => editPoint("fov", v)}
                        extra={
                            <VC.ToolButton
                                src={i_reset}
                                disabled={!point.fovOverridden}
                                tooltip="Leave the lens alone at this point"
                                focusKey={VF.FOCUS_DISABLED}
                                className={VT.toolButton?.button}
                                onSelect={() => editPoint("clearFov", 0)}
                            />
                        }
                    />

                    <SliderSection
                        title="Hour"
                        value={point.timeOfDay}
                        suffix="h"
                        min={0}
                        max={24}
                        step={0.25}
                        onChange={(v) => editPoint("timeOfDay", v)}
                        extra={
                            <VC.ToolButton
                                src={i_reset}
                                disabled={!point.timeOfDayOverridden}
                                tooltip="Leave the light alone at this point"
                                focusKey={VF.FOCUS_DISABLED}
                                className={VT.toolButton?.button}
                                onSelect={() => editPoint("clearTimeOfDay", 0)}
                            />
                        }
                    />

                    <VC.Section title="Look at">
                        <VC.ToolButton
                            src={i_target}
                            selected={point.lookAtSet}
                            tooltip="Aim this point at the pinned subject, the same point an orbit circles."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => editPoint("lookAtPinned", 0)}
                        />

                        <VC.ToolButton
                            src={i_reset}
                            disabled={!point.lookAtSet}
                            tooltip="Aim the way the rest of the path does"
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => editPoint("clearLookAt", 0)}
                        />
                    </VC.Section>

                    <VC.Section title="Corner">
                        <VC.ToolButton
                            src="coui://uil/Standard/Dot.svg"
                            selected={point.broken}
                            tooltip={
                                point.broken
                                    ? "Sharp corner — the two handles move independently."
                                    : "Smooth corner — the handles stay opposite each other."
                            }
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => editPoint("broken", point.broken ? 0 : 1)}
                        />
                    </VC.Section>
                </>
            )}
                    {point.index < 0 && (
                        <VC.Section title="Point">
                            <div className={`${VT.mouseToolOptions?.numberField} ${styles.numberBox}`}>
                                None selected
                            </div>
                        </VC.Section>
                    )}
                </>
            )}

            {page === 2 && numbers.shotType === ShotTypes.Path && (
                <>
            <VC.Section title="Whole path">
                {TRANSFORMS.map(({ op, src, tooltip }) => (
                    <VC.ToolButton
                        key={op}
                        src={src}
                        disabled={points === 0}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => transformPath(op)}
                    />
                ))}
            </VC.Section>

            <VC.Section title="Place">
                {MOVES.map(({ op, src, tooltip }) => (
                    <VC.ToolButton
                        key={op}
                        src={src}
                        disabled={points === 0}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => transformPath(op)}
                    />
                ))}
            </VC.Section>

                    <VC.Section title="Points">
                        {UTILITIES.map(({ op, src, tooltip }) => (
                            <VC.ToolButton
                                key={op}
                                src={src}
                                disabled={points === 0}
                                tooltip={tooltip}
                                focusKey={VF.FOCUS_DISABLED}
                                className={VT.toolButton?.button}
                                onSelect={() => transformPath(op)}
                            />
                        ))}
                    </VC.Section>

                    <SliderSection title="Simplify by" value={numbers.simplifyTolerance} suffix="m"
                                   min={1} max={50} step={1}
                                   onChange={(v) => setNumber("simplifyTolerance", v)} />

                    <VC.Section title="Aim rail">
                        <VC.ToolButton
                            src={i_railFrom}
                            disabled={points < 2}
                            tooltip="Build an aim rail by copying the camera path sideways. The offset is perpendicular to the direction of travel, so the rail follows a curving path at a constant distance instead of crossing it."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => transformPath("railFromPath")}
                        />

                        <VC.ToolButton
                            src={i_preview}
                            selected={previewing}
                            disabled={points < 2}
                            tooltip="Fly the path now, without writing anything to the timeline. Escape stops it."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={togglePreview}
                        />
                    </VC.Section>

                    <SliderSection title="Rail offset" value={numbers.railOffset} suffix="m"
                                   min={-300} max={300} step={10}
                                   onChange={(v) => setNumber("railOffset", v)} />

                    <VC.Section title="History">
                        <VC.ToolButton
                            src="coui://extendedphotomode/Camera_Icons/HistoryUndo.svg"
                            disabled={!metrics.canUndo}
                            tooltip="Undo the last path edit. Ctrl+Z does the same while drawing."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => transformPath("undo")}
                        />

                        <VC.ToolButton
                            src="coui://extendedphotomode/Camera_Icons/HistoryRedo.svg"
                            disabled={!metrics.canRedo}
                            tooltip="Redo the edit you just undid. Ctrl+Y does the same while drawing."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => transformPath("redo")}
                        />

                        <VC.ToolButton
                            src={i_frustum}
                            selected={metrics.frustums}
                            tooltip="Draw a view cone at every point, showing where the camera looks and how wide its lens is."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => transformPath("frustums")}
                        />
                    </VC.Section>

                    {/* The speed is the number worth having here: 200m in 5s is 144km/h, which reads
                        as a mistake on screen and nothing else tells you before you generate. */}
                    <VC.Section title="Move">
                        {/* One interpolated string. Adjacent text nodes break onto separate lines
                            here whatever white-space says, and this row had six of them. */}
                        <div className={styles.metrics}>
                            {`${Math.round(metrics.length)}m · ${Math.round(metrics.duration)}s · ${Math.round(metrics.speed)}km/h`}
                        </div>
                    </VC.Section>
                </>
            )}
        </>
    );
}

/**
 * Page selector for the tool options, shaped after RoadBuilder's pager.
 *
 * The panel outgrew the screen: every per-point property, the snap modes, the whole-path transforms
 * and the utilities together run past the bottom of a 1080p viewport, and tool options do not
 * scroll. Paging is what keeps each group short enough to read at a glance.
 */
function Pager({
    page,
    setPage,
    shotType,
}: {
    readonly page: number;
    readonly setPage: (page: number) => void;
    readonly shotType: number;
}): ReactElement {
    const pages = PAGE_SETS[shotType] ?? PAGE_SETS[ShotTypes.Path];

    return (
        <VC.Section title="Page">
            {pages.map(({ label, icon }, index) => (
                <VC.ToolButton
                    key={label}
                    src={icon}
                    selected={page === index}
                    tooltip={label}
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setPage(index)}
                />
            ))}
        </VC.Section>
    );
}

/**
 * A number field in a vanilla `Section`, committing on Enter or blur and cancelling on Escape.
 *
 * The current value is the field's *placeholder*, and the value itself stays empty until you type.
 * That solves the problem this had before: the binding pushes a new value every frame while a point
 * is dragged, so anything driven from `value` fights what is being typed. With the live number in
 * the placeholder there is nothing to fight — the field shows the truth while idle, and shows only
 * your own input while editing.
 *
 * Not vanilla's own FloatInput: that is an editor widget (`editor: true`) built for the map editor's
 * light widget system, and inside the in-game tool options it renders as a white, left-aligned,
 * full-width box that ignores its own `label` prop.
 */
/**
 * A bounded value as a drag-able slider with its number beside it.
 *
 * Only for properties with real limits — pitch, speed, an hour of the day. World coordinates have
 * none, and the lens spans 0.11mm to 1466mm, where a slider gives no usable precision at the short
 * end; those stay text fields.
 *
 * The number is held locally while dragging and pushed on every change, so the drawn path follows
 * the drag. `useStepTransformer` quantises the drag itself rather than rounding afterwards, which is
 * what stops a value flickering between two steps as the thumb sits on a boundary.
 */
function SliderSection({
    title,
    value,
    min,
    max,
    step,
    suffix,
    onChange,
    extra,
}: {
    readonly title: string;
    readonly value: number;
    readonly min: number;
    readonly max: number;
    readonly step: number;
    readonly suffix?: string;
    readonly onChange: (value: number) => void;
    readonly extra?: ReactElement;
}): ReactElement {
    const transformer = VC.useStepTransformer(step);
    const [dragged, setDragged] = useState<number | null>(null);
    const shown = dragged ?? value;

    // The slider's range is a SOFT cap: a comfortable span for the control, not a limit on the value.
    // Some of these can be pushed past it by dragging a handle in the world, so the track is clamped
    // — a thumb at 3000 on a 1000 track sits outside its own groove — while the readout stays honest
    // and shows what the shot will actually use.
    const onTrack = Math.min(Math.max(shown, min), max);

    return (
        <VC.Section title={title}>
            {/* One interpolated string, not a value and a suffix side by side: two adjacent text
                nodes break between them here regardless of white-space, so "18.75" and "h" end up on
                separate lines even with room to spare. */}
            <div className={styles.sliderValue}>{`${Math.round(shown * 100) / 100}${suffix ?? ""}`}</div>

            <VC.Slider
                value={onTrack}
                start={min}
                end={max}
                gamepadStep={step}
                valueTransformer={transformer}
                className={styles.slider}
                theme={VT.slider}
                onChange={(next: number) => {
                    setDragged(next);
                    onChange(next);
                }}
                onDragEnd={() => setDragged(null)}
            />

            {extra ?? <></>}
        </VC.Section>
    );
}

function NumberSection({
    title,
    value,
    suffix,
    onCommit,
    extra,
}: {
    readonly title: string;
    readonly value: number;
    readonly suffix?: string;
    readonly onCommit: (value: number) => void;
    readonly extra?: ReactElement;
}): ReactElement {
    const [draft, setDraft] = useState("");

    const commit = (): void => {
        const parsed = parseFloat(draft);

        if (!isNaN(parsed)) {
            onCommit(parsed);
        }

        setDraft("");
    };

    return (
        <VC.Section title={title}>
            <VC.TextInput
                multiline={1}
                type="text"
                value={draft}
                placeholder={`${Math.round(value * 100) / 100}${suffix ?? ""}`}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                className={`${VT.textInput?.input ?? ""} ${styles.field}`}
                onChange={(event: React.ChangeEvent<HTMLInputElement>) => setDraft(event.target.value)}
                onBlur={commit}
                onKeyDownCapture={(event: React.KeyboardEvent) => {
                    if (event.key === "Enter") {
                        commit();
                        event.stopPropagation();
                    } else if (event.key === "Escape") {
                        setDraft("");
                        event.stopPropagation();
                    }
                }}
            />

            {extra ?? <></>}
        </VC.Section>
    );
}

/**
 * The pinned subject, which both orbit and dolly are built around.
 *
 * Shown for those two only. A drawn path can use a subject — the Target aim mode and per-point
 * look-ats both read it — but it is not what the shot is made of, so it does not belong at the top
 * of every page.
 */
function SubjectSection({ numbers }: { readonly numbers: PathNumbers }): ReactElement {
    return (
        <VC.Section title="Subject">
            <VC.ToolButton
                src={i_target}
                selected={numbers.hasSubject}
                tooltip="Pin the selected object as the subject. With nothing pinned, click empty ground in the world to place one."
                focusKey={VF.FOCUS_DISABLED}
                className={VT.toolButton?.button}
                onSelect={() => setNumber("pinCentre", 1)}
            />

            <VC.ToolButton
                src={i_reset}
                disabled={!numbers.hasSubject}
                tooltip="Unpin the subject, so the next click on the ground places a new one."
                focusKey={VF.FOCUS_DISABLED}
                className={VT.toolButton?.button}
                onSelect={() => setNumber("pinCentre", 0)}
            />

            <div className={styles.metrics}>{numbers.hasSubject ? "Pinned" : "None"}</div>
        </VC.Section>
    );
}

/**
 * The orbit's geometry.
 *
 * Start and end are separate rows rather than one: equal values are a plain circle, and differing
 * ones are the spiral and the helix, so showing both is what makes those two shots discoverable at
 * all. They are also the two handles you drag in the world, and this is the same pair as numbers.
 */
function OrbitShape({ numbers }: { readonly numbers: PathNumbers }): ReactElement {
    return (
        <>
            <SliderSection title="Radius" value={numbers.orbitRadius} suffix="m"
                           min={10} max={1000} step={5}
                           onChange={(v) => setNumber("orbitRadius", v)} />

            <SliderSection title="End radius" value={numbers.orbitEndRadius} suffix="m"
                           min={10} max={1000} step={5}
                           onChange={(v) => setNumber("orbitEndRadius", v)} />

            <SliderSection title="Height" value={numbers.orbitHeight} suffix="m"
                           min={-100} max={500} step={5}
                           onChange={(v) => setNumber("orbitHeight", v)} />

            <SliderSection title="End height" value={numbers.orbitEndHeight} suffix="m"
                           min={-100} max={500} step={5}
                           onChange={(v) => setNumber("orbitEndHeight", v)} />

            <SliderSection title="Sweep" value={numbers.orbitSweep} suffix="°"
                           min={-720} max={720} step={15}
                           onChange={(v) => setNumber("orbitSweep", v)} />

            <SliderSection title="Sweep ease" value={numbers.orbitSweepEase}
                           min={0} max={1} step={0.05}
                           onChange={(v) => setNumber("orbitSweepEase", v)} />
        </>
    );
}

function OrbitTiming({ numbers }: { readonly numbers: PathNumbers }): ReactElement {
    return (
        <>
            <SliderSection title="Duration" value={numbers.orbitDuration} suffix="s"
                           min={5} max={300} step={1}
                           onChange={(v) => setNumber("orbitDuration", v)} />

            <SliderSection title="Key every" value={numbers.orbitSpacing} suffix="°"
                           min={5} max={90} step={5}
                           onChange={(v) => setNumber("orbitSpacing", v)} />

            <VC.Section title="Aim">
                <VC.ToolButton
                    src={i_target}
                    selected={numbers.orbitLookAt}
                    tooltip="Keep the camera pointed at the subject all the way round. Off holds whatever heading it started on."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setNumber("orbitLookAt", numbers.orbitLookAt ? 0 : 1)}
                />

                <VC.ToolButton
                    src={i_frustum}
                    selected={numbers.orbitPreview}
                    tooltip="Draw the orbit ring in the world while the tool is open."
                    focusKey={VF.FOCUS_DISABLED}
                    className={VT.toolButton?.button}
                    onSelect={() => setNumber("orbitPreview", numbers.orbitPreview ? 0 : 1)}
                />
            </VC.Section>
        </>
    );
}

/**
 * The dolly zoom's geometry.
 *
 * Start further out than the end is the classic push-in; reversing them pulls out. Both are distances
 * from the subject along one line, which is why there is no bearing row — that comes from dragging
 * either handle in the world.
 */
function DollyShape({ numbers }: { readonly numbers: PathNumbers }): ReactElement {
    return (
        <>
            <SliderSection title="Start at" value={numbers.dollyStart} suffix="m"
                           min={5} max={1000} step={5}
                           onChange={(v) => setNumber("dollyStart", v)} />

            <SliderSection title="End at" value={numbers.dollyEnd} suffix="m"
                           min={5} max={1000} step={5}
                           onChange={(v) => setNumber("dollyEnd", v)} />

            <SliderSection title="Height" value={numbers.orbitHeight} suffix="m"
                           min={-100} max={500} step={5}
                           onChange={(v) => setNumber("orbitHeight", v)} />
        </>
    );
}

function DollyTiming({ numbers }: { readonly numbers: PathNumbers }): ReactElement {
    return (
        <>
            <SliderSection title="Duration" value={numbers.dollyDuration} suffix="s"
                           min={1} max={120} step={1}
                           onChange={(v) => setNumber("dollyDuration", v)} />

            <SliderSection title="Keyframes" value={numbers.dollyKeys}
                           min={2} max={120} step={1}
                           onChange={(v) => setNumber("dollyKeys", v)} />
        </>
    );
}

/**
 * The drawn path's own shape and timing.
 *
 * These used to sit on the photo mode panel, which cannot be open while the path is visible — and
 * pitch, aim and look-ahead all change what the camera cones draw, so setting them without seeing
 * the result was the whole problem.
 */
function PathShape({ numbers }: { readonly numbers: PathNumbers }): ReactElement {
    return (
        <>
            <SliderSection title="Duration" value={numbers.pathDuration} suffix="s"
                           min={5} max={300} step={1}
                           onChange={(v) => setNumber("pathDuration", v)} />

            <SliderSection title="Key every" value={numbers.pathSpacing} suffix="m"
                           min={5} max={200} step={5}
                           onChange={(v) => setNumber("pathSpacing", v)} />

            <SliderSection title="Pitch" value={numbers.pathPitch} suffix="°"
                           min={-80} max={80} step={1}
                           onChange={(v) => setNumber("pathPitch", v)} />

            <VC.Section title="Aim">
                {LOOK_MODES.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.pathLook === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("pathLook", mode)}
                    />
                ))}
            </VC.Section>

            {/* Only means anything when the path faces along its own direction; the other three aim
                modes have a target and do not need to look ahead to find one. */}
            {numbers.pathLook === LookMode.Forward && (
                <SliderSection title="Look ahead" value={numbers.pathLookAhead} suffix="m"
                               min={0} max={500} step={10}
                               onChange={(v) => setNumber("pathLookAhead", v)} />
            )}

            <SliderSection title="Ease" value={numbers.pathEase}
                           min={0} max={1} step={0.05}
                           onChange={(v) => setNumber("pathEase", v)} />
        </>
    );
}

/**
 * How the shot is shot, rather than where it goes — shared by all three types.
 *
 * The rows under each mode stay hidden until that mode is chosen. A panel full of controls that
 * quietly do nothing is worse than a short one, and most of these only apply to one setting of the
 * button row above them.
 */
function ShotLook({ numbers }: { readonly numbers: PathNumbers }): ReactElement {
    return (
        <>
            <VC.Section title="Framing">
                {FRAMINGS.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.framing === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("framing", mode)}
                    />
                ))}
            </VC.Section>

            {numbers.framing !== Framing.Off && (
                <>
                    <VC.Section title="Hold size">
                        <VC.ToolButton
                            src={i_holdSize}
                            selected={numbers.framingHold}
                            tooltip="Solve the lens at every keyframe so the subject stays the size it is at the start, however far the camera travels."
                            focusKey={VF.FOCUS_DISABLED}
                            className={VT.toolButton?.button}
                            onSelect={() => setNumber("framingHold", numbers.framingHold ? 0 : 1)}
                        />
                    </VC.Section>

                    <SliderSection title="Framing lens" value={numbers.framingLens} suffix="mm"
                                   min={10} max={300} step={5}
                                   onChange={(v) => setNumber("framingLens", v)} />
                </>
            )}

            <VC.Section title="Focus">
                {FOCUS_MODES.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.focus === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("focus", mode)}
                    />
                ))}
            </VC.Section>

            {numbers.focus !== Focus.Off && (
                <SliderSection title="Depth of field" value={numbers.focusDepth}
                               min={0} max={1} step={0.05}
                               onChange={(v) => setNumber("focusDepth", v)} />
            )}

            {numbers.focus === Focus.Rack && (
                <SliderSection title="Rack easing" value={numbers.focusEase}
                               min={0} max={1} step={0.05}
                               onChange={(v) => setNumber("focusEase", v)} />
            )}

            <VC.Section title="Rig">
                {RIGS.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.rig === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("rig", mode)}
                    />
                ))}
            </VC.Section>

            {numbers.rig !== Rig.Free && (
                <>
                    <SliderSection title="Rig amount" value={numbers.rigStrength}
                                   min={0} max={1} step={0.05}
                                   onChange={(v) => setNumber("rigStrength", v)} />

                    <SliderSection title="Rig seed" value={numbers.rigSeed}
                                   min={1} max={99} step={1}
                                   onChange={(v) => setNumber("rigSeed", v)} />
                </>
            )}

            <VC.Section title="Follow">
                {FOLLOWS.map(({ mode, src, tooltip }) => (
                    <VC.ToolButton
                        key={mode}
                        src={src}
                        selected={numbers.follow === mode}
                        tooltip={tooltip}
                        focusKey={VF.FOCUS_DISABLED}
                        className={VT.toolButton?.button}
                        onSelect={() => setNumber("follow", mode)}
                    />
                ))}
            </VC.Section>
        </>
    );
}
