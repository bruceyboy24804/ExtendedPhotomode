namespace ExtendedPhotomode.Camera {
    /// <summary>How the camera behaves as it arrives at and leaves a single keyframe.</summary>
    /// <remarks>
    /// A keyframe holds two tangents; a tangent of zero means the value is momentarily stationary, so
    /// easing is one or both tangents flattened rather than a separate curve type.
    ///
    /// Shaped like <c>UnityEngine.Camera.GateFitMode</c>: real options from 1, plus a zero-valued
    /// <see cref="None"/> that sorts first. The photo mode dropdown DISCARDS the first option of any
    /// list it is given — vanilla only escapes it because the option it loses is a meaningless None.
    /// </remarks>
    public enum KeyframeEase {
        None = 0,

        Linear = 1,

        Smooth = 2,

        In = 3,

        Out = 4,

        InOut = 5,
    }
}
