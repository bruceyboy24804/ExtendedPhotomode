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
        None = 0,

        /// <summary>Objects are ignored entirely.</summary>
        Off = 1,

        /// <summary>Obstructed stretches are drawn in warning colour, but the path is left alone.</summary>
        Warn = 2,

        /// <summary>Obstructed samples are lifted over what they hit, smoothly.</summary>
        Lift = 3,
    }
}
