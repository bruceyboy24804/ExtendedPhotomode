namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using UnityEngine;

    #endregion

    /// <summary>One control point on a camera path, with the tangent handles that shape the curve through it.</summary>
    /// <remarks>
    /// Tangents are stored as offsets from <see cref="Position"/>, so they are the handle positions
    /// the user drags rather than abstract direction vectors — moving the point moves its handles with
    /// it for free, and the cubic Bezier control points fall straight out.
    /// </remarks>
    public class PathNode {
        public Vector3 Position { get; set; }

        public Vector3 TangentOut { get; set; }

        public Vector3 TangentIn { get; set; }

        public bool Auto { get; set; } = true;

        public bool Broken { get; set; }

        /// <summary>Gets or sets how long the camera holds still here, in seconds.</summary>
        /// <remarks>
        /// Written as two identical keyframes that many seconds apart. The shot's Duration covers the
        /// travelling only, so adding dwell lengthens the shot rather than stealing time from the move.
        /// </remarks>
        public float Dwell { get; set; }

        /// <summary>Gets or sets how fast the camera travels through here, 1 being the path's own pace.</summary>
        /// <remarks>
        /// A weight on time, not a distance change: the path is unchanged and still takes Duration
        /// seconds overall, but a stretch marked 0.5 takes twice as long to cross and the rest of the
        /// shot speeds up to pay for it.
        /// </remarks>
        public float Speed { get; set; } = 1f;

        /// <summary>Gets or sets a point to aim at here, or null to aim the way the path does.</summary>
        /// <remarks>
        /// Interpolated between neighbouring nodes, so the camera swings from one subject to the next
        /// across the segment rather than snapping at the node.
        /// </remarks>
        public Vector3? LookAt { get; set; }

        /// <summary>Gets or sets the focal length to hold here, or null to leave the lens alone.</summary>
        public float? Fov { get; set; }

        /// <summary>Gets or sets the hour to hold here, or null to leave the light alone.</summary>
        public float? TimeOfDay { get; set; }

        /// <summary>Gets or sets the pitch to hold here, or null to use the path's own pitch.</summary>
        /// <remarks>
        /// Interpolated between neighbouring nodes, so two points with different pitches tilt smoothly
        /// between them rather than snapping at the node. Ignored while the path aims at a target,
        /// which solves pitch from the geometry instead.
        /// </remarks>
        public float? Pitch { get; set; }

        /// <summary>A deep copy, for the undo history to hold a snapshot that later edits cannot reach.</summary>
        /// <remarks>
        /// A node is a class, so a history that stored the list alone would store references — every
        /// "snapshot" would then track the live nodes and undo would restore the state it was undoing.
        /// </remarks>
        public PathNode Clone() {
            return new PathNode(Position) {
                TangentOut = TangentOut,
                TangentIn  = TangentIn,
                Auto       = Auto,
                Broken     = Broken,
                Dwell      = Dwell,
                Speed      = Speed,
                LookAt     = LookAt,
                Fov        = Fov,
                TimeOfDay  = TimeOfDay,
                Pitch      = Pitch,
            };
        }

        public Vector3 HandleOut => Position + TangentOut;

        public Vector3 HandleIn => Position + TangentIn;

        public PathNode(Vector3 position) { Position = position; }

        public void SetHandleOut(Vector3 handle) {
            TangentOut = handle - Position;
            Auto       = false;

            if (!Broken) {
                TangentIn = -TangentOut;
            }
        }

        public void SetHandleIn(Vector3 handle) {
            TangentIn = handle - Position;
            Auto      = false;

            if (!Broken) {
                TangentOut = -TangentIn;
            }
        }
    }
}
