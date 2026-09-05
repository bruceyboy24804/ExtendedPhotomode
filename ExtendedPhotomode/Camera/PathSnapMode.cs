namespace ExtendedPhotomode.Camera {
    /// <summary>What the cursor snaps to while a path point is being placed.</summary>
    /// <remarks>
    /// One mode at a time rather than a set of flags, because the panel row is a dropdown and because
    /// two snaps competing for the same cursor is worse than either alone — a grid and a road pulling
    /// in different directions produces a point at neither.
    /// <para>
    /// <c>None</c> is first and deliberately meaningless; see <see cref="ShotType"/>.
    /// </para>
    /// </remarks>
    public enum PathSnapMode {
        None = 0,

        /// <summary>The point lands exactly where the cursor is.</summary>
        Free = 1,

        /// <summary>Rounds to a grid, for paths that need to run square to the city.</summary>
        Grid = 2,

        /// <summary>Constrains the direction from the previous point to a fixed step, keeping distance.</summary>
        Angle = 3,

        /// <summary>Pulls onto a nearby existing point, which is how a loop is closed cleanly.</summary>
        Point = 4,

        /// <summary>Pulls onto the centreline of the road under the cursor.</summary>
        Network = 5,
    }
}
