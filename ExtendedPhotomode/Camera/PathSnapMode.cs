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
        [Systems.EnumOption("", "", Visible = false)]
        None = 0,

        /// <summary>The point lands exactly where the cursor is.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/SnapFree.svg",
                            "No snapping — the point lands where you click.")]
        Free = 1,

        /// <summary>Rounds to a grid, for paths that need to run square to the city.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/SnapGrid.svg",
                            "Round to a fixed grid, for paths that run square to the city.")]
        Grid = 2,

        /// <summary>Constrains the direction from the previous point to a fixed step, keeping distance.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/SnapAngle.svg",
                            "Fix the heading from the previous point to a step, keeping the distance you reached.")]
        Angle = 3,

        /// <summary>Pulls onto a nearby existing point, which is how a loop is closed cleanly.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/SnapPoint.svg",
                            "Land exactly on a point already placed — how a closed loop meets itself with no gap.")]
        Point = 4,

        /// <summary>Pulls onto the centreline of the road under the cursor.</summary>
        [Systems.EnumOption("coui://extendedphotomode/Camera_Icons/SnapNetwork.svg",
                            "Follow the centreline of the road under the cursor, not where the click hit its surface.")]
        Network = 5,
    }
}
