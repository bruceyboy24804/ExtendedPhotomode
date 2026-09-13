namespace ExtendedPhotomode.Camera {
    /// <summary>What a path does about buildings and other objects standing in its way.</summary>
    /// <remarks>
    /// The other half of <see cref="PathTerrainMode"/>. Clamping to terrain stops a path burrowing into
    /// a hill, but nothing stopped it flying through a tower — and a downtown flythrough is the shot
    /// this mod exists for.
    /// <para>
    /// <c>None</c> is first and deliberately meaningless; see <see cref="ShotType"/>.
    /// </para>
    /// </remarks>
    public enum PathClearanceMode {
        [Systems.EnumOption("", "", Visible = false)]
        None = 0,

        /// <summary>Objects are ignored entirely.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/ObstacleOff.svg",
                            "Ignore buildings and other objects entirely.")]
        Off = 1,

        /// <summary>Obstructed stretches are drawn in warning colour, but the path is left alone.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/ObstacleWarn.svg",
                            "Draw obstructed stretches red and change nothing, leaving the fix to you.")]
        Warn = 2,

        /// <summary>Obstructed samples are lifted over what they hit, smoothly.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/ObstacleLift.svg",
                            "Raise the camera over what it hits when the shot is generated, easing the climb into the run-up either side.")]
        Lift = 3,
    }
}
