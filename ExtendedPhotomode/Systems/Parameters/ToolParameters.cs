namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;

    using UnityEngine;

    #endregion

    /// <summary>Every tool value the UI can write, declared in one place.</summary>
    /// <remarks>
    /// <para>
    /// This replaced a switch of forty-one hand-written cases, each carrying its own clamp. The
    /// clamps were the problem rather than the length: they existed only here, so the panel rows in
    /// TypeScript restated the same ranges and the world handles had to invent a third copy. Three
    /// sets of bounds free to disagree, and nothing to make them agree again.
    /// </para>
    /// <para>
    /// Declaration order is not meaningful and lookup is by key, so a parameter can be added anywhere
    /// in the table without disturbing anything else — unlike the photo mode tab's section list, which
    /// is aligned to its widgets by index.
    /// </para>
    /// </remarks>
    public static class ToolParameters {
        private static Dictionary<string, ToolParameter> s_ByKey;

        /// <summary>The table, built on first use.</summary>
        public static IReadOnlyDictionary<string, ToolParameter> All => s_ByKey ??= Build();

        /// <summary>Finds a parameter by the key the UI addresses it with.</summary>
        public static bool TryGet(string key, out ToolParameter parameter) {
            return All.TryGetValue(key, out parameter);
        }

        private static Dictionary<string, ToolParameter> Build() {
            Setting s = Mod.Instance.Settings;

            var table = new List<ToolParameter> {
                // --- Path shaping -------------------------------------------------------------
                new("traceLength",       () => s.PathTraceLength,       v => s.PathTraceLength = (int)v,       50, 5000),
                new("traceSpacing",      () => s.PathTraceSpacing,      v => s.PathTraceSpacing = (int)v,       5, 200),
                new("simplifyTolerance", () => s.PathSimplifyTolerance, v => s.PathSimplifyTolerance = (int)v,  1, 100),
                new("nudgeStep",         () => s.PathNudgeStep,         v => s.PathNudgeStep = (int)v,          1, 100),
                new("railOffset",        () => s.PathRailOffset,        v => s.PathRailOffset = (int)v,      -500, 500),
                new("obstacleClearance", () => s.PathObstacleClearance, v => s.PathObstacleClearance = (int)v,  0, 200),
                new("terrainClearance",  () => s.PathClearance,         v => s.PathClearance = (int)v,          0, 500),

                new("terrainMode", () => (int)s.PathTerrain, v => s.PathTerrain = (PathTerrainMode)v,
                    (int)PathTerrainMode.Free, (int)PathTerrainMode.Follow),

                new("obstacleMode", () => (int)s.PathClearanceMode,
                    v => s.PathClearanceMode = (PathClearanceMode)v,
                    (int)PathClearanceMode.Off, (int)PathClearanceMode.Lift),

                // The shot type belongs here as much as any of the numbers do: it is chosen while
                // authoring, and its dropdown lives in photo mode, where the tool cannot run.
                new("shotType", () => (int)s.Shot, v => s.Shot = (ShotType)v,
                    (int)ShotType.Orbit, (int)ShotType.Path),

                // --- Orbit --------------------------------------------------------------------
                new("orbitRadius",    () => s.OrbitRadius,       v => s.OrbitRadius = (int)v,        10, 1000, modes: ShotType.Orbit),
                new("orbitEndRadius", () => s.OrbitEndRadius,    v => s.OrbitEndRadius = (int)v,     10, 1000, modes: ShotType.Orbit),
                new("orbitHeight",    () => s.OrbitHeight,       v => s.OrbitHeight = (int)v,      -100,  500, modes: ShotType.Orbit),
                new("orbitEndHeight", () => s.OrbitEndHeight,    v => s.OrbitEndHeight = (int)v,   -100,  500, modes: ShotType.Orbit),
                new("orbitSweep",     () => s.OrbitSweep,        v => s.OrbitSweep = (int)v,       -720,  720, modes: ShotType.Orbit),
                new("orbitDuration",  () => s.OrbitDuration,     v => s.OrbitDuration = (int)v,       5,  300, modes: ShotType.Orbit),
                new("orbitSpacing",   () => s.OrbitDegreesPerKey, v => s.OrbitDegreesPerKey = (int)v, 5,   90, modes: ShotType.Orbit),

                new("orbitLookAt",  () => s.OrbitLookAtTarget ? 1f : 0f, v => s.OrbitLookAtTarget = v > 0.5f, 0, 1, modes: ShotType.Orbit),
                new("orbitPreview", () => s.ShowOrbitPreview ? 1f : 0f,  v => s.ShowOrbitPreview = v > 0.5f,  0, 1),

                // The mode switch itself, so the panel can flip it without a trip to the options menu.
                new("advanced", () => s.Advanced ? 1f : 0f, v => s.Advanced = v > 0.5f, 0, 1),

                // Not whole: the eases are the only fractional values here, and rounding them would
                // leave a 0-to-1 control with exactly two positions.
                new("orbitSweepEase", () => s.OrbitSweepEase, v => s.OrbitSweepEase = v, 0f, 1f,
                    whole: false, modes: ShotType.Orbit),

                // --- Path shot ----------------------------------------------------------------
                new("pathDuration",  () => s.PathDuration,     v => s.PathDuration = (int)v,      5, 300, modes: ShotType.Path),
                new("pathSpacing",   () => s.PathMetresPerKey, v => s.PathMetresPerKey = (int)v,  5, 200, modes: ShotType.Path),
                new("pathPitch",     () => s.PathPitch,        v => s.PathPitch = (int)v,       -80,  80, modes: ShotType.Path),
                new("pathLookAhead", () => s.PathLookAhead,    v => s.PathLookAhead = (int)v,     0, 500, modes: ShotType.Path),

                new("pathEase", () => s.PathEase, v => s.PathEase = v, 0f, 1f,
                    whole: false, modes: ShotType.Path),

                new("pathLook", () => (int)s.PathLook, v => s.PathLook = (PathLookMode)v,
                    (int)PathLookMode.Forward, (int)PathLookMode.Rail, modes: ShotType.Path),

                // --- Framing, focus and rig ---------------------------------------------------
                new("framingHold", () => s.FramingHoldSize ? 1f : 0f, v => s.FramingHoldSize = v > 0.5f, 0, 1),

                // The lens's real range, not an invented one: 0.11mm to 1466mm is what the game's own
                // focal length property accepts, and clamping tighter here silently refuses lenses the
                // engine is happy with.
                new("framingLens", () => s.FramingFocalLength, v => s.FramingFocalLength = v,
                    0.11f, 1466f, whole: false),

                new("focusDepth",  () => s.FocusDepth,  v => s.FocusDepth = v,  0f, 1f, whole: false),
                new("focusEase",   () => s.FocusEase,   v => s.FocusEase = v,   0f, 1f, whole: false),
                new("rigStrength", () => s.RigStrength, v => s.RigStrength = v, 0f, 1f, whole: false),
                new("rigSeed",     () => s.RigSeed,     v => s.RigSeed = (int)v, 1, 999),

                new("framing", () => (int)s.Framing, v => s.Framing = (FramingRule)v,
                    (int)FramingRule.None, (int)FramingRule.Headroom),

                new("focus", () => (int)s.Focus, v => s.Focus = (FocusMode)v,
                    (int)FocusMode.Off, (int)FocusMode.Rack),

                new("rig", () => (int)s.Rig, v => s.Rig = (CameraRig)v,
                    (int)CameraRig.Free, (int)CameraRig.Handheld),

                new("follow", () => (int)s.Follow, v => s.Follow = (FollowMode)v,
                    (int)FollowMode.Off, (int)FollowMode.Ride),

                // --- Dolly zoom ---------------------------------------------------------------
                new("dollyStart",    () => s.DollyStartDistance, v => s.DollyStartDistance = (int)v, 5, 1000, modes: ShotType.DollyZoom),
                new("dollyEnd",      () => s.DollyEndDistance,   v => s.DollyEndDistance = (int)v,   5, 1000, modes: ShotType.DollyZoom),
                new("dollyDuration", () => s.DollyDuration,      v => s.DollyDuration = (int)v,      1,  120, modes: ShotType.DollyZoom),
                new("dollyKeys",     () => s.DollyKeys,          v => s.DollyKeys = (int)v,          2,  120, modes: ShotType.DollyZoom),
            };

            var byKey = new Dictionary<string, ToolParameter>(table.Count);

            foreach (ToolParameter parameter in table) {
                byKey[parameter.Key] = parameter;
            }

            return byKey;
        }
    }
}
