namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using UnityEngine;

    #endregion

    /// <summary>Turns a camera position and a point to look at into the euler rotation a controller wants.</summary>
    /// <remarks>
    /// Shared because the same solve is needed in two unrelated places: baking a keyframe's rotation
    /// at generate time, and re-solving rotation live for a moving subject. Both feed
    /// <c>IGameCameraController.rotation</c>, whose x is pitch positive-downwards and whose y is a
    /// compass yaw, so a plain <c>Quaternion.LookRotation</c> is not interchangeable with this.
    /// </remarks>
    public static class CameraAim {
        /// <summary>Solves the rotation that points a camera at <paramref name="lookAt"/>.</summary>
        /// <param name="from">Where the camera is.</param>
        /// <param name="lookAt">The point to frame.</param>
        /// <param name="fallback">Rotation returned when the two points coincide and no aim exists.</param>
        public static Vector3 Euler(Vector3 from, Vector3 lookAt, Vector3 fallback) {
            Vector3 toTarget = lookAt - from;
            float   flat     = new Vector2(toTarget.x, toTarget.z).magnitude;

            if (flat < 0.0001f && Mathf.Abs(toTarget.y) < 0.0001f) {
                return fallback;
            }

            float yaw   = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(toTarget.y, Mathf.Max(flat, 0.0001f)) * Mathf.Rad2Deg;

            return new Vector3(pitch, yaw, 0f);
        }
    }
}
