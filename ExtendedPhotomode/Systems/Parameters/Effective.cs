namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using ExtendedPhotomode.Camera;

    #endregion

    /// <summary>The value a setting actually has, once simple mode has had its say.</summary>
    /// <remarks>
    /// <para>
    /// Simple mode is not fewer knobs; it is the mod deciding. Hiding a control only works if what
    /// it would have set is genuinely good without you, so every hidden setting has an answer here,
    /// and the solvers read THIS rather than the raw setting. The panel hides the row; this decides
    /// what the row would have said.
    /// </para>
    /// <para>
    /// One place, deliberately. The settings are copied onto a path in two places — the tool's
    /// per-frame preview and the generator's solve — and a default that lived in one and not the
    /// other would draw a shot that does not match the one it generates. Both read through here.
    /// </para>
    /// <para>
    /// Every value in the table is a judgement about what "automatic" should mean, and a wrong one
    /// is invisible to a simple-mode user by design. They were chosen by someone who shoots with the
    /// mod, and should be changed the same way — from a shot, not from a guess.
    /// </para>
    /// </remarks>
    public static class Effective {
        private static Setting S => Mod.Instance.Settings;

        /// <summary>Whether the player asked to see and drive everything.</summary>
        public static bool Advanced => S.Advanced;

        // --- The route ------------------------------------------------------------------------

        /// <summary>Never below ground: keeps your heights and lifts only where the shot would clip.</summary>
        public static PathTerrainMode PathTerrain => Advanced ? S.PathTerrain : PathTerrainMode.Floor;

        /// <summary>Lift over buildings, easing the climb into the run-up either side.</summary>
        public static PathClearanceMode PathClearanceMode =>
            Advanced ? S.PathClearanceMode : PathClearanceMode.Lift;

        /// <summary>Level. A fixed tilt is a choice; simple mode makes none.</summary>
        public static float PathPitch => Advanced ? S.PathPitch : 0f;

        /// <summary>Far enough down the path that a bend reads as a turn rather than a twitch.</summary>
        public static float PathLookAhead => Advanced ? S.PathLookAhead : 40f;

        /// <summary>A gentle ease at both ends, so the move neither jolts off nor stops dead.</summary>
        public static float PathEase => Advanced ? S.PathEase : 0.5f;

        /// <summary>Aim at the subject when there is one, otherwise along the path.</summary>
        /// <param name="hasSubject">Whether a subject is pinned.</param>
        public static PathLookMode PathLook(bool hasSubject) {
            if (Advanced) {
                return S.PathLook;
            }

            return hasSubject ? PathLookMode.Target : PathLookMode.Forward;
        }

        /// <summary>Keys tighten through bends, where the camera's direction actually changes.</summary>
        /// <remarks>
        /// Full weighting. Curvature-weighted spacing only ever ADDS keys, so it cannot make a
        /// simple-mode shot coarser than the plain spacing would — only smoother through corners.
        /// </remarks>
        public static float CurvatureBias => Advanced ? S.PathCurvatureBias : 1f;

        // --- The camera -----------------------------------------------------------------------

        /// <summary>A drone: quick but never instant, drifting a little. The least "computer" of the rigs.</summary>
        public static CameraRig Rig => Advanced ? S.Rig : CameraRig.Drone;

        /// <summary>Keep the subject sharp however far the camera travels.</summary>
        public static FocusMode Focus => Advanced ? S.Focus : FocusMode.Track;

        /// <summary>Dead centre, which is never wrong even when it is not the most interesting choice.</summary>
        public static FramingRule Framing => Advanced ? S.Framing : FramingRule.Centre;
    }
}
