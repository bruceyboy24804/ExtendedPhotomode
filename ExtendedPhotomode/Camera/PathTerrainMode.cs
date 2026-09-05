namespace ExtendedPhotomode.Camera {
    /// <summary>How a path's height relates to the ground underneath it.</summary>
    /// <remarks>
    /// Points are placed at a height above the terrain, but only the points are — the curve between
    /// them is an arc through absolute space, so a path drawn across a ridge flies straight through
    /// the hill and a path across a valley sails level over it. This clamps the sampled positions
    /// rather than the control points, which is the only place the shape between points can be seen.
    /// <para>
    /// <c>None</c> is first and deliberately meaningless; see <see cref="ShotType"/>.
    /// </para>
    /// </remarks>
    public enum PathTerrainMode {
        None = 0,

        /// <summary>Heights are exactly as authored, terrain ignored.</summary>
        Free = 1,

        /// <summary>Authored heights, but never closer to the ground than the clearance.</summary>
        Floor = 2,

        /// <summary>A constant height above the ground for the whole path; authored heights ignored.</summary>
        Follow = 3,
    }
}
