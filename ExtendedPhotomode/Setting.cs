using Colossal.IO.AssetDatabase;
using Game.UI;

namespace ExtendedPhotomode {
    #region Using Statements

    using ExtendedPhotomode.Camera;

    using Game.Input;
    using Game.Modding;
    using Game.Settings;

    #endregion

    /// <summary>
    /// How the saved cinematic shots are ordered in the game's own list.
    /// </summary>
    /// <remarks>
    /// Unlike the enums shown in the photo mode panel, this one appears in the settings menu, where
    /// dropdowns are built with an items accessor and render every member — so it needs no throwaway
    /// leading entry.
    /// </remarks>
    public enum ShotSortOrder {
        /// <summary>Leave the database's own order alone, as vanilla does.</summary>
        Default,

        /// <summary>Alphabetical by name, A to Z, ignoring case.</summary>
        NameAscending,

        /// <summary>Reverse alphabetical by name, Z to A, ignoring case.</summary>
        NameDescending,

        /// <summary>Most recently saved first.</summary>
        Newest,

        /// <summary>Oldest saved first.</summary>
        Oldest,
    }

    /// <summary>
    /// Mod settings for ExtendedPhotomode.
    /// </summary>
    [FileLocation(nameof(ExtendedPhotomode))]
    [SettingsUIGroupOrder(kRenderingGroup, kKeybindingGroup)]
    [SettingsUIShowGroupName(kRenderingGroup, kKeybindingGroup)]
    [SettingsUIKeyboardAction(Mod.kApplyOrbitActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kPathToolActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kGeneratePathActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIMouseAction(Mod.kPathApplyActionName, ActionType.Button,
        modifierOptions: ModifierOptions.Ignore, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kPathRaiseActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kPathLowerActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kPathReverseActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kPathBreakTangentActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kTimelineActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    [SettingsUIKeyboardAction(Mod.kHideUIActionName, ActionType.Button, usages: new string[] { Usages.kDefaultUsage })]
    public class Setting : ModSetting {
        /// <summary>Id of the mod's only settings tab.</summary>
        public const string kSection = "Main";

        /// <summary>Group holding rendering fixes.</summary>
        public const string kRenderingGroup = "Rendering";

        /// <summary>Group holding key bindings.</summary>
        public const string kKeybindingGroup = "KeyBinding";

        #region Defaults

        // Shared by the property initialisers, SetDefaults, and the reset button on each photo mode
        // row, so all three can never drift apart.

        /// <summary>Default for <see cref="OrbitRadius"/>.</summary>
        public const int kDefaultOrbitRadius = 150;

        /// <summary>Default for <see cref="OrbitEndRadius"/>.</summary>
        /// <remarks>
        /// Matches <see cref="kDefaultOrbitRadius"/>, so an untouched orbit is a plain circle and the
        /// spiral is something you opt into by moving this row.
        /// </remarks>
        public const int kDefaultOrbitEndRadius = kDefaultOrbitRadius;

        /// <summary>Default for <see cref="OrbitHeight"/>.</summary>
        public const int kDefaultOrbitHeight = 60;

        /// <summary>Default for <see cref="OrbitEndHeight"/>.</summary>
        public const int kDefaultOrbitEndHeight = kDefaultOrbitHeight;

        /// <summary>Default for <see cref="OrbitSweepEase"/>.</summary>
        public const float kDefaultOrbitSweepEase = 0f;

        /// <summary>Default for <see cref="OrbitSweep"/>.</summary>
        public const int kDefaultOrbitSweep = 360;

        /// <summary>Default for <see cref="OrbitDuration"/>.</summary>
        public const int kDefaultOrbitDuration = 30;

        /// <summary>Default for <see cref="OrbitDegreesPerKey"/>.</summary>
        public const int kDefaultOrbitDegreesPerKey = (int)OrbitShot.kDefaultDegreesPerKey;

        /// <summary>Default for <see cref="OrbitLookAtTarget"/>.</summary>
        public const bool kDefaultOrbitLookAtTarget = true;

        /// <summary>Default for <see cref="OrbitReplacesSequence"/>.</summary>
        public const bool kDefaultOrbitReplacesSequence = true;

        /// <summary>Default for <see cref="ShowOrbitPreview"/>.</summary>
        public const bool kDefaultShowOrbitPreview = true;

        /// <summary>Default for <see cref="ConstantSpeed"/>.</summary>
        /// <remarks>
        /// Off, so curves keep their rounded corners by default. Turning it on is the right call for a
        /// move that should read as mechanical, and the wrong one for an orbit at coarse keyframe
        /// spacing, where straight chords between keys draw a polygon instead of a circle.
        /// </remarks>
        public const bool kDefaultConstantSpeed = false;

        /// <summary>Default for <see cref="AnimateTimeOfDay"/>.</summary>
        /// <remarks>
        /// Off, because switching it on takes over the Time of Day property for the whole shot. That
        /// is a big enough side effect on someone's lighting to be a deliberate choice.
        /// </remarks>
        public const bool kDefaultAnimateTimeOfDay = false;

        /// <summary>Default for <see cref="StartTimeOfDay"/> — an hour after sunrise.</summary>
        public const float kDefaultStartTimeOfDay = 7f;

        /// <summary>Default for <see cref="EndTimeOfDay"/> — golden hour.</summary>
        public const float kDefaultEndTimeOfDay = 18f;

        /// <summary>Default for <see cref="TimeOfDayPerKeyframe"/>.</summary>
        /// <remarks>
        /// On, because the ramp it draws is identical either way — the extra keys cost nothing visually
        /// and are what make the sun's pacing editable afterwards.
        /// </remarks>
        public const bool kDefaultTimeOfDayPerKeyframe = true;

        /// <summary>Default for <see cref="TimeOfDayEase"/>.</summary>
        /// <remarks>
        /// Zero, so the ramp stays exactly linear unless asked otherwise — the shaping is a look, and
        /// nobody's existing shot should change pacing because they updated the mod.
        /// </remarks>
        public const float kDefaultTimeOfDayEase = 0f;

        /// <summary>Default for <see cref="TimeOfDayRange"/>.</summary>
        public const TimeOfDayPreset kDefaultTimeOfDayRange = TimeOfDayPreset.Custom;

        /// <summary>Default for <see cref="Shot"/>.</summary>
        public const ShotType kDefaultShot = ShotType.Orbit;

        /// <summary>Default for <see cref="Follow"/>.</summary>
        public const FollowMode kDefaultFollow = FollowMode.Off;




        /// <summary>Default for <see cref="SmoothCameraRotation"/>.</summary>
        public const bool kDefaultSmoothCameraRotation = true;

        /// <summary>Default for <see cref="RestoreTimeAndWeatherOnExit"/>.</summary>
        public const bool kDefaultRestoreTimeAndWeatherOnExit = true;

        /// <summary>Default for <see cref="PathDuration"/>.</summary>
        public const int kDefaultPathDuration = 30;

        /// <summary>Default for <see cref="PathMetresPerKey"/>.</summary>
        public const int kDefaultPathMetresPerKey = (int)CameraPath.kDefaultMetresPerKey;

        /// <summary>Default for <see cref="PathPitch"/>.</summary>
        public const int kDefaultPathPitch = 15;

        /// <summary>Default for <see cref="PathLookAhead"/> — off, matching the old aim exactly.</summary>
        public const int kDefaultPathLookAhead = 0;

        /// <summary>Default for <see cref="PathEase"/>.</summary>
        public const float kDefaultPathEase = 0f;

        /// <summary>Default for <see cref="PathPointHeight"/>.</summary>
        public const int kDefaultPathPointHeight = 40;

        /// <summary>Default for <see cref="PathHeightStep"/>.</summary>
        public const int kDefaultPathHeightStep = 5;

        /// <summary>Default for <see cref="PathLook"/>.</summary>
        public const PathLookMode kDefaultPathLook = PathLookMode.Forward;

        /// <summary>Default for <see cref="PathClosed"/>.</summary>
        public const bool kDefaultPathClosed = false;

        /// <summary>Default for <see cref="PathTerrain"/>.</summary>
        public const PathTerrainMode kDefaultPathTerrain = PathTerrainMode.Free;

        /// <summary>Default for <see cref="PathClearance"/>.</summary>
        public const int kDefaultPathClearance = 20;

        /// <summary>Default for <see cref="PathSnap"/>.</summary>
        public const PathSnapMode kDefaultPathSnap = PathSnapMode.Free;

        /// <summary>Default for <see cref="PathGridSize"/>.</summary>
        public const int kDefaultPathGridSize = 8;

        /// <summary>Default for <see cref="PathAngleStep"/>.</summary>
        public const int kDefaultPathAngleStep = 15;

        /// <summary>Default for <see cref="PathSnapRadius"/>.</summary>
        public const int kDefaultPathSnapRadius = 12;

        /// <summary>Default for <see cref="PathShowFrustums"/>.</summary>
        public const bool kDefaultPathShowFrustums = true;

        /// <summary>Default for <see cref="PathSimplifyTolerance"/>.</summary>
        public const int kDefaultPathSimplifyTolerance = 5;

        /// <summary>Default for <see cref="PathTraceLength"/>.</summary>
        public const int kDefaultPathTraceLength = 500;

        /// <summary>Default for <see cref="PathTraceSpacing"/>.</summary>
        public const int kDefaultPathTraceSpacing = 40;

        /// <summary>Default for <see cref="PathClearanceMode"/>.</summary>
        /// <remarks>
        /// Warn rather than Lift, so an obstructed path is pointed out but never silently reshaped —
        /// a path that quietly moved itself would be worse than one that flew through a tower.
        /// </remarks>
        public const PathClearanceMode kDefaultPathClearanceMode = Camera.PathClearanceMode.Warn;

        /// <summary>Default for <see cref="PathObstacleClearance"/>.</summary>
        public const int kDefaultPathObstacleClearance = 15;

        /// <summary>Default for <see cref="PathNudgeStep"/>.</summary>
        public const int kDefaultPathNudgeStep = 5;

        /// <summary>Default for <see cref="PathRailOffset"/>.</summary>
        public const int kDefaultPathRailOffset = 60;

        /// <summary>Default for <see cref="TransitionSeconds"/>.</summary>
        /// <remarks>Zero, so a sequence cuts unless a move between setups is asked for.</remarks>
        public const float kDefaultTransitionSeconds = 0f;

        /// <summary>Default for <see cref="TransitionEase"/>.</summary>
        public const float kDefaultTransitionEase = 0.85f;

        /// <summary>Default for <see cref="Rig"/>.</summary>
        public const CameraRig kDefaultRig = CameraRig.Free;

        /// <summary>Default for <see cref="RigStrength"/>.</summary>
        public const float kDefaultRigStrength = 0.6f;

        /// <summary>Default for <see cref="RigSeed"/>.</summary>
        public const int kDefaultRigSeed = 1;

        /// <summary>Default for <see cref="Focus"/>.</summary>
        public const FocusMode kDefaultFocus = FocusMode.Off;

        /// <summary>Default for <see cref="FocusDepth"/>.</summary>
        public const float kDefaultFocusDepth = 0.5f;

        /// <summary>Default for <see cref="FocusEase"/>.</summary>
        public const float kDefaultFocusEase = 0.6f;

        /// <summary>Default for <see cref="Framing"/>.</summary>
        public const FramingRule kDefaultFraming = FramingRule.None;

        /// <summary>Default for <see cref="FramingHoldSize"/>.</summary>
        public const bool kDefaultFramingHoldSize = false;

        /// <summary>Default for <see cref="FramingFocalLength"/>.</summary>
        public const float kDefaultFramingFocalLength = 50f;

        /// <summary>Default for <see cref="DollyStartDistance"/>.</summary>
        public const int kDefaultDollyStartDistance = 60;

        /// <summary>Default for <see cref="DollyEndDistance"/>.</summary>
        public const int kDefaultDollyEndDistance = 220;

        /// <summary>Default for <see cref="DollyDuration"/>.</summary>
        public const int kDefaultDollyDuration = 12;

        /// <summary>Default for <see cref="DollyKeys"/>.</summary>
        public const int kDefaultDollyKeys = 24;

        /// <summary>Default for <see cref="ShotTransition"/>.</summary>
        public const int kDefaultShotTransition = 2;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="Setting"/> class.
        /// </summary>
        /// <param name="mod">The owning mod.</param>
        public Setting(IMod mod) : base(mod) { }

        /// <summary>
        /// Gets or sets a value indicating whether photo mode keeps the post-process quality tier
        /// configured for the game, rather than falling back to HDRP defaults. See
        /// EPM_PhotoModeQualitySystem for what this actually changes.
        /// </summary>
        [SettingsUISection(kSection, kRenderingGroup)]
        public bool RestorePostProcessQuality { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether lens and environment values are shown while
        /// scrubbing the mod's timeline outside photo mode. See EPM_ScrubPreviewSystem.
        /// </summary>
        /// <remarks>
        /// Switchable because it has a real cost. It works by holding photo mode's own control volume
        /// at full weight, and that volume is priority 2000 — so any weather override sitting on it
        /// also applies while the timeline is open, the same way photo mode outranks Weather Anarchy.
        /// Turn it off to keep the timeline strictly an editor and judge the picture in photo mode.
        /// </remarks>
        [SettingsUISection(kSection, kRenderingGroup)]
        public bool PreviewOutsidePhotoMode { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the weather on screen is carried into photo mode,
        /// so climate-driving mods such as Time & Weather Anarchy keep showing what they set.
        /// See EPM_WeatherSyncSystem for the volume-priority conflict this works around.
        /// </summary>
        [SettingsUISection(kSection, kRenderingGroup)]
        public bool SyncWeatherIntoPhotoMode { get; set; } = true;

        /// <summary>
        /// Gets or sets how the game's list of saved cinematic shots is ordered.
        /// </summary>
        /// <remarks>
        /// Vanilla applies no ordering at all, so the list arrives in asset-database order — neither
        /// alphabetical nor chronological, and not guaranteed to be the same next session.
        /// </remarks>
        [SettingsUISection(kSection, kRenderingGroup)]
        public ShotSortOrder ShotSort { get; set; } = ShotSortOrder.NameAscending;

        /// <summary>
        /// Gets or sets a value indicating whether the mouse pointer is hidden while a cinematic shot
        /// plays back, so it stays out of recordings.
        /// </summary>
        [SettingsUISection(kSection, kRenderingGroup)]
        public bool HideCursorDuringPlayback { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether camera rotation is kept at a constant speed.
        /// See CinematicCameraSequencePatchRotationsPatch for what vanilla does without it.
        /// </summary>
        [SettingsUISection(kSection, kRenderingGroup)]
        public bool SmoothCameraRotation { get; set; } = kDefaultSmoothCameraRotation;

        /// <summary>
        /// Gets or sets a value indicating whether the time and weather overrides in force before
        /// photo mode are put back when it closes. See EPM_WeatherSyncSystem for why they are lost.
        /// </summary>
        [SettingsUISection(kSection, kRenderingGroup)]
        public bool RestoreTimeAndWeatherOnExit { get; set; } = kDefaultRestoreTimeAndWeatherOnExit;

        /// <summary>
        /// Gets or sets a value indicating whether the orbit that would be generated is drawn in the
        /// world while the shot tool is open.
        /// </summary>
        /// <remarks>
        /// Authoring feedback only. This used to drive a second ring drawn over photo mode as well,
        /// which showed up for anyone who opened photo mode whatever they were shooting; that system
        /// is gone, and the only ring left is the one you get while actually placing an orbit.
        /// Hidden from the options screen because its toggle lives on the tool's own toolbar, next to
        /// the thing it affects.
        /// </remarks>
        [SettingsUIHidden]
        public bool ShowOrbitPreview { get; set; } = kDefaultShowOrbitPreview;

        /// <summary>
        /// Gets or sets how long a generated path flight takes, in seconds.
        /// </summary>
        [SettingsUIHidden]
        public int PathDuration { get; set; } = kDefaultPathDuration;

        /// <summary>
        /// Gets or sets the metres of travel between generated path keyframes.
        /// </summary>
        [SettingsUIHidden]
        public int PathMetresPerKey { get; set; } = kDefaultPathMetresPerKey;

        /// <summary>
        /// Gets or sets the downward tilt held while flying a path, in degrees.
        /// </summary>
        [SettingsUIHidden]
        public int PathPitch { get; set; } = kDefaultPathPitch;

        /// <summary>Gets or sets how far ahead along the path the camera aims, in metres.</summary>
        [SettingsUIHidden]
        public int PathLookAhead { get; set; } = kDefaultPathLookAhead;

        /// <summary>Gets or sets how strongly the whole path move eases in and out, 0 to 1.</summary>
        [SettingsUIHidden]
        public float PathEase { get; set; } = kDefaultPathEase;

        /// <summary>
        /// Gets or sets the height above terrain at which new path points are placed, in metres.
        /// </summary>
        [SettingsUIHidden]
        public int PathPointHeight { get; set; } = kDefaultPathPointHeight;

        /// <summary>
        /// Gets or sets how fast a path point rises or falls while a height key is held, in metres.
        /// </summary>
        [SettingsUIHidden]
        public int PathHeightStep { get; set; } = kDefaultPathHeightStep;

        /// <summary>
        /// Gets or sets how the camera is aimed while it flies the path.
        /// </summary>
        /// <remarks>
        /// <see cref="PathLookMode.Target"/> aims at the orbit's pinned centre, reusing that point
        /// deliberately: it already has a checkbox and a pin-to-selection button, and a second way to
        /// nominate a place to look at would be two things to keep in sync for no gain.
        /// </remarks>
        [SettingsUIHidden]
        public PathLookMode PathLook { get; set; } = kDefaultPathLook;

        /// <summary>Gets or sets whether the path's last point joins back to its first.</summary>
        [SettingsUIHidden]
        public bool PathClosed { get; set; } = kDefaultPathClosed;

        /// <summary>Gets or sets how the path's sampled heights relate to the ground.</summary>
        [SettingsUIHidden]
        public PathTerrainMode PathTerrain { get; set; } = kDefaultPathTerrain;

        /// <summary>Gets or sets the height above ground the terrain modes hold, in metres.</summary>
        [SettingsUIHidden]
        public int PathClearance { get; set; } = kDefaultPathClearance;

        /// <summary>Gets or sets what the cursor snaps to while placing a path point.</summary>
        [SettingsUIHidden]
        public PathSnapMode PathSnap { get; set; } = kDefaultPathSnap;

        /// <summary>Gets or sets the grid spacing used by grid snapping, in metres.</summary>
        [SettingsUIHidden]
        public int PathGridSize { get; set; } = kDefaultPathGridSize;

        /// <summary>Gets or sets the heading step used by angle snapping, in degrees.</summary>
        [SettingsUIHidden]
        public int PathAngleStep { get; set; } = kDefaultPathAngleStep;

        /// <summary>Gets or sets how close the cursor must be to snap onto an existing point.</summary>
        [SettingsUIHidden]
        public int PathSnapRadius { get; set; } = kDefaultPathSnapRadius;

        /// <summary>Gets or sets whether a camera view cone is drawn at every path point.</summary>
        [SettingsUIHidden]
        public bool PathShowFrustums { get; set; } = kDefaultPathShowFrustums;

        /// <summary>Gets or sets how far a point may sit off the line before Simplify keeps it.</summary>
        [SettingsUIHidden]
        public int PathSimplifyTolerance { get; set; } = kDefaultPathSimplifyTolerance;

        /// <summary>Gets or sets how far along a road tracing follows it, in metres.</summary>
        [SettingsUIHidden]
        public int PathTraceLength { get; set; } = kDefaultPathTraceLength;

        /// <summary>Gets or sets how far apart tracing places its points, in metres.</summary>
        [SettingsUIHidden]
        public int PathTraceSpacing { get; set; } = kDefaultPathTraceSpacing;

        /// <summary>Gets or sets what the path does about objects standing in its way.</summary>
        [SettingsUIHidden]
        public PathClearanceMode PathClearanceMode { get; set; } = kDefaultPathClearanceMode;

        /// <summary>Gets or sets how far above an obstruction the path is lifted, in metres.</summary>
        [SettingsUIHidden]
        public int PathObstacleClearance { get; set; } = kDefaultPathObstacleClearance;

        /// <summary>Gets or sets how far one nudge moves the selected points, in metres.</summary>
        [SettingsUIHidden]
        public int PathNudgeStep { get; set; } = kDefaultPathNudgeStep;

        /// <summary>Gets or sets how far to one side a rail built from the path sits, in metres.</summary>
        [SettingsUIHidden]
        public int PathRailOffset { get; set; } = kDefaultPathRailOffset;

        /// <summary>Gets or sets how long the move into a shot takes when it is added to the list.</summary>
        [SettingsUIHidden]
        public float TransitionSeconds { get; set; } = kDefaultTransitionSeconds;

        /// <summary>Gets or sets how strongly a transition eases in and out, 0 to 1.</summary>
        [SettingsUIHidden]
        public float TransitionEase { get; set; } = kDefaultTransitionEase;

        /// <summary>Gets or sets the physical camera support the shot imitates.</summary>
        [SettingsUIHidden]
        public CameraRig Rig { get; set; } = kDefaultRig;

        /// <summary>Gets or sets how strongly the rig's characteristics are applied, 0 to 1.</summary>
        [SettingsUIHidden]
        public float RigStrength { get; set; } = kDefaultRigStrength;

        /// <summary>Gets or sets which unsteadiness pattern the rig uses, so a take is repeatable.</summary>
        [SettingsUIHidden]
        public int RigSeed { get; set; } = kDefaultRigSeed;

        /// <summary>Gets or sets how focus distance is driven across the shot.</summary>
        [SettingsUIHidden]
        public FocusMode Focus { get; set; } = kDefaultFocus;

        /// <summary>Gets or sets how shallow the depth of field is, 0 to 1.</summary>
        [SettingsUIHidden]
        public float FocusDepth { get; set; } = kDefaultFocusDepth;

        /// <summary>Gets or sets how strongly a rack focus eases in and out, 0 to 1.</summary>
        [SettingsUIHidden]
        public float FocusEase { get; set; } = kDefaultFocusEase;

        /// <summary>Gets or sets where in frame the pinned subject is held.</summary>
        [SettingsUIHidden]
        public FramingRule Framing { get; set; } = kDefaultFraming;

        /// <summary>Gets or sets whether the lens is solved to keep the subject a constant size.</summary>
        [SettingsUIHidden]
        public bool FramingHoldSize { get; set; } = kDefaultFramingHoldSize;

        /// <summary>Gets or sets the lens the framing solver works from, in millimetres.</summary>
        [SettingsUIHidden]
        public float FramingFocalLength { get; set; } = kDefaultFramingFocalLength;

        /// <summary>
        /// Gets or sets the distance from the subject at the start of a dolly zoom, in metres.
        /// </summary>
        [SettingsUIHidden]
        public int DollyStartDistance { get; set; } = kDefaultDollyStartDistance;

        /// <summary>
        /// Gets or sets the distance from the subject at the end of a dolly zoom, in metres.
        /// </summary>
        [SettingsUIHidden]
        public int DollyEndDistance { get; set; } = kDefaultDollyEndDistance;

        /// <summary>
        /// Gets or sets how long a dolly zoom takes, in seconds.
        /// </summary>
        [SettingsUIHidden]
        public int DollyDuration { get; set; } = kDefaultDollyDuration;

        /// <summary>
        /// Gets or sets how many keys a dolly zoom generates.
        /// </summary>
        [SettingsUIHidden]
        public int DollyKeys { get; set; } = kDefaultDollyKeys;

        /// <summary>
        /// Gets or sets the blend time inserted between chained shots, in seconds.
        /// </summary>
        [SettingsUIHidden]
        public int ShotTransition { get; set; } = kDefaultShotTransition;

        // The orbit values below are hidden from the options page on purpose: they are edited in the
        // photo mode panel's Orbit Shot section, where the shot is actually being composed, and the
        // options page would only be a second place to change the same numbers. They stay real
        // settings properties because that is what persists them across sessions —
        // SettingsUIHidden is a UI marker only and does not affect serialisation.

        /// <summary>
        /// Gets or sets the horizontal distance from the orbit target, in metres.
        /// </summary>
        [SettingsUIHidden]
        public int OrbitRadius { get; set; } = kDefaultOrbitRadius;

        /// <summary>
        /// Gets or sets the horizontal distance at the end of the orbit, in metres. Differing from
        /// <see cref="OrbitRadius"/> spirals the camera in or out across the sweep.
        /// </summary>
        [SettingsUIHidden]
        public int OrbitEndRadius { get; set; } = kDefaultOrbitEndRadius;

        /// <summary>
        /// Gets or sets the camera height above the orbit target, in metres.
        /// </summary>
        [SettingsUIHidden]
        public int OrbitHeight { get; set; } = kDefaultOrbitHeight;

        /// <summary>Gets or sets the height a generated orbit ends at, which makes it a helix.</summary>
        [SettingsUIHidden]
        public int OrbitEndHeight { get; set; } = kDefaultOrbitEndHeight;

        /// <summary>Gets or sets how strongly a generated orbit eases its sweep, from 0 to 1.</summary>
        [SettingsUIHidden]
        public float OrbitSweepEase { get; set; } = kDefaultOrbitSweepEase;

        /// <summary>
        /// Gets or sets the arc the orbit travels, in degrees. 360 is one full turn; negative values
        /// orbit the other way, and values beyond 360 make multiple turns.
        /// </summary>
        [SettingsUIHidden]
        public int OrbitSweep { get; set; } = kDefaultOrbitSweep;

        /// <summary>
        /// Gets or sets the length of a generated orbit, in seconds.
        /// </summary>
        [SettingsUIHidden]
        public int OrbitDuration { get; set; } = kDefaultOrbitDuration;

        /// <summary>
        /// Gets or sets the arc between generated orbit keyframes, in degrees. Smaller is smoother
        /// but puts more keys on the timeline.
        /// </summary>
        [SettingsUIHidden]
        public int OrbitDegreesPerKey { get; set; } = kDefaultOrbitDegreesPerKey;

        /// <summary>
        /// Gets or sets a value indicating whether generated shots hold a dead constant speed, with no
        /// easing at any keyframe including the first and last.
        /// </summary>
        [SettingsUIHidden]
        public bool ConstantSpeed { get; set; } = kDefaultConstantSpeed;

        /// <summary>
        /// Gets or sets a value indicating whether generated shots also animate the time of day.
        /// </summary>
        [SettingsUIHidden]
        public bool AnimateTimeOfDay { get; set; } = kDefaultAnimateTimeOfDay;

        /// <summary>
        /// Gets or sets the hour a generated shot opens at, 0–24.
        /// </summary>
        [SettingsUIHidden]
        public float StartTimeOfDay { get; set; } = kDefaultStartTimeOfDay;

        /// <summary>
        /// Gets or sets the hour a generated shot ends at, 0–24.
        /// </summary>
        /// <remarks>
        /// May be lower than <see cref="StartTimeOfDay"/>; the ramp simply runs backwards. It does not
        /// wrap through midnight — a shot from 22 to 2 rewinds through the day rather than crossing
        /// into the next one, because the underlying property is a plain 0–24 float with no notion of
        /// which day it is on.
        /// </remarks>
        [SettingsUIHidden]
        public float EndTimeOfDay { get; set; } = kDefaultEndTimeOfDay;

        /// <summary>
        /// Gets or sets a value indicating whether the time of day ramp gets a key at every camera
        /// keyframe rather than one at each end.
        /// </summary>
        /// <remarks>
        /// Both settings draw the same straight ramp; the difference is what can be edited afterwards.
        /// With a key per camera keyframe, each one is a handle in the curve editor, so the sun can be
        /// made to dwell on golden hour and hurry through the night by dragging keys instead of adding
        /// them by hand. Regenerating the shot rewrites them all, so shape the ramp last.
        /// </remarks>
        [SettingsUIHidden]
        public bool TimeOfDayPerKeyframe { get; set; } = kDefaultTimeOfDayPerKeyframe;

        /// <summary>
        /// Gets or sets the named span of the day the hours were filled from, or
        /// <see cref="TimeOfDayPreset.Custom"/> once they are set by hand.
        /// </summary>
        /// <remarks>
        /// Stored rather than derived so the dropdown keeps showing what was picked. Editing either
        /// hour drops it back to Custom, because the pair no longer describes that span.
        /// </remarks>
        [SettingsUIHidden]
        public TimeOfDayPreset TimeOfDayRange { get; set; } = kDefaultTimeOfDayRange;

        /// <summary>Gets or sets which generator the Generate button runs.</summary>
        [SettingsUIHidden]
        public ShotType Shot { get; set; } = kDefaultShot;

        /// <summary>Gets or sets whether playback tracks the pinned subject as it moves.</summary>
        [SettingsUIHidden]
        public FollowMode Follow { get; set; } = kDefaultFollow;




        /// <summary>
        /// Gets or sets how strongly the time of day ramp slows down at the hours it starts and ends
        /// on, from 0 (linear) to 1.
        /// </summary>
        /// <remarks>
        /// A ramp that is linear in hours is not linear in what you see: nearly all of a day's visible
        /// change is packed around sunrise and sunset, so an even sweep races through those and then
        /// sits on flat daylight. Raising this spends more of the shot near the chosen endpoints and
        /// less on the middle of the range.
        /// </remarks>
        [SettingsUIHidden]
        public float TimeOfDayEase { get; set; } = kDefaultTimeOfDayEase;

        /// <summary>
        /// Gets or sets a value indicating whether generated orbits aim at the target. When off, the
        /// camera holds its opening heading and dollies around while facing outward.
        /// </summary>
        [SettingsUIHidden]
        public bool OrbitLookAtTarget { get; set; } = kDefaultOrbitLookAtTarget;

        /// <summary>
        /// Gets or sets a value indicating whether applying an orbit clears the existing sequence
        /// first. When off, the orbit is appended to whatever is already on the timeline.
        /// </summary>
        [SettingsUIHidden]
        public bool OrbitReplacesSequence { get; set; } = kDefaultOrbitReplacesSequence;

        /// <summary>
        /// Gets or sets the binding that generates an orbit ahead of the camera.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.O, Mod.kApplyOrbitActionName, ctrl: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding ApplyOrbitBinding { get; set; }

        /// <summary>
        /// Gets or sets the binding that opens and closes the camera path tool.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.P, Mod.kPathToolActionName, ctrl: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding PathToolBinding { get; set; }

        /// <summary>
        /// Gets or sets the binding that writes the drawn path to the cinematic timeline.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.P, Mod.kGeneratePathActionName, ctrl: true, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding GeneratePathBinding { get; set; }

        /// <summary>
        /// Gets or sets the click that places a path point. Declared separately from vanilla Apply so
        /// held modifiers cannot suppress it.
        /// </summary>
        [SettingsUIMouseBinding(BindingMouse.Left, Mod.kPathApplyActionName)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding PathApplyBinding { get; set; }

        /// <summary>
        /// Gets or sets the key that raises the hovered path point.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.PageUp, Mod.kPathRaiseActionName)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding PathRaiseBinding { get; set; }

        /// <summary>
        /// Gets or sets the key that lowers the hovered path point.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.PageDown, Mod.kPathLowerActionName)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding PathLowerBinding { get; set; }

        /// <summary>
        /// Gets or sets the key that reverses the drawn path.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.R, Mod.kPathReverseActionName, ctrl: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding PathReverseBinding { get; set; }

        /// <summary>
        /// Gets or sets the key that breaks or smooths the hovered tangent.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.B, Mod.kPathBreakTangentActionName)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding PathBreakTangentBinding { get; set; }

        // The shot list has no binding of its own. It is reached by the Shots button in the timeline
        // header, which writes the same shotListOpen state a keybind would have toggled — so a second
        // way in would only be a shortcut that could disagree with the button's own arrow.

        /// <summary>Gets or sets the binding that opens the curve timeline.</summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.K, Mod.kTimelineActionName, ctrl: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding TimelineBinding { get; set; }

        /// <summary>Gets or sets the binding that hides the panels and the world overlays.</summary>
        /// <remarks>
        /// A key binding and not only a button, necessarily: hiding the panels hides the button that
        /// would bring them back. Anything that can turn the UI off has to have a route back in that
        /// does not depend on the UI.
        /// </remarks>
        [SettingsUIKeyboardBinding(BindingKeyboard.H, Mod.kHideUIActionName, ctrl: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding HideUIBinding { get; set; }

        /// <summary>
        /// Gets or sets a value that, when set, restores every key binding to its default.
        /// </summary>
        [SettingsUISection(kSection, kKeybindingGroup)]
        public bool ResetBindings {
            set { ResetKeyBindings(); }
        }

        /// <inheritdoc/>
        public override void SetDefaults() {
            RestorePostProcessQuality = true;
            SyncWeatherIntoPhotoMode  = true;
            SmoothCameraRotation      = kDefaultSmoothCameraRotation;
            RestoreTimeAndWeatherOnExit = kDefaultRestoreTimeAndWeatherOnExit;
            ShowOrbitPreview          = kDefaultShowOrbitPreview;
            PathDuration              = kDefaultPathDuration;
            PathMetresPerKey          = kDefaultPathMetresPerKey;
            PathPitch                 = kDefaultPathPitch;
            PathLookAhead             = kDefaultPathLookAhead;
            PathEase                  = kDefaultPathEase;
            PathPointHeight           = kDefaultPathPointHeight;
            PathHeightStep            = kDefaultPathHeightStep;
            PathLook                  = kDefaultPathLook;
            DollyStartDistance        = kDefaultDollyStartDistance;
            DollyEndDistance          = kDefaultDollyEndDistance;
            DollyDuration             = kDefaultDollyDuration;
            DollyKeys                 = kDefaultDollyKeys;
            ShotTransition            = kDefaultShotTransition;
            OrbitRadius           = kDefaultOrbitRadius;
            OrbitEndRadius        = kDefaultOrbitEndRadius;
            OrbitHeight           = kDefaultOrbitHeight;
            OrbitEndHeight        = kDefaultOrbitEndHeight;
            OrbitSweepEase        = kDefaultOrbitSweepEase;
            OrbitSweep            = kDefaultOrbitSweep;
            OrbitDuration         = kDefaultOrbitDuration;
            OrbitDegreesPerKey    = kDefaultOrbitDegreesPerKey;
            OrbitLookAtTarget     = kDefaultOrbitLookAtTarget;
            OrbitReplacesSequence = kDefaultOrbitReplacesSequence;
            ConstantSpeed         = kDefaultConstantSpeed;
            AnimateTimeOfDay      = kDefaultAnimateTimeOfDay;
            StartTimeOfDay        = kDefaultStartTimeOfDay;
            EndTimeOfDay          = kDefaultEndTimeOfDay;
            TimeOfDayPerKeyframe  = kDefaultTimeOfDayPerKeyframe;
            TimeOfDayEase         = kDefaultTimeOfDayEase;
            TimeOfDayRange        = kDefaultTimeOfDayRange;
            Shot                  = kDefaultShot;
            Follow                = kDefaultFollow;
            PathClosed            = kDefaultPathClosed;
            PathTerrain           = kDefaultPathTerrain;
            PathClearance         = kDefaultPathClearance;
            PathSnap              = kDefaultPathSnap;
            PathGridSize          = kDefaultPathGridSize;
            PathAngleStep         = kDefaultPathAngleStep;
            PathSnapRadius        = kDefaultPathSnapRadius;
            PathShowFrustums      = kDefaultPathShowFrustums;
            PathSimplifyTolerance = kDefaultPathSimplifyTolerance;
            PathTraceLength       = kDefaultPathTraceLength;
            PathTraceSpacing      = kDefaultPathTraceSpacing;
            PathNudgeStep         = kDefaultPathNudgeStep;
            PathRailOffset        = kDefaultPathRailOffset;
            TransitionSeconds     = kDefaultTransitionSeconds;
            TransitionEase        = kDefaultTransitionEase;
            Rig                   = kDefaultRig;
            RigStrength           = kDefaultRigStrength;
            RigSeed               = kDefaultRigSeed;
            Focus                 = kDefaultFocus;
            FocusDepth            = kDefaultFocusDepth;
            FocusEase             = kDefaultFocusEase;
            PathClearanceMode     = kDefaultPathClearanceMode;
            PathObstacleClearance = kDefaultPathObstacleClearance;
            Framing               = kDefaultFraming;
            FramingHoldSize       = kDefaultFramingHoldSize;
            FramingFocalLength    = kDefaultFramingFocalLength;
        }
    }
}
