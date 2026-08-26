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
