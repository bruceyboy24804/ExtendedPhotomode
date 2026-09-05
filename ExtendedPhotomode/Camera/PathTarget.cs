namespace ExtendedPhotomode.Camera {
    /// <summary>Which of a shot's two paths the tool is currently editing.</summary>
    /// <remarks>
    /// A two-rail rig has a path for the camera body and a second for what it looks at, and both are
    /// drawn with the same editor. Rather than build a second one, the tool points its whole editing
    /// surface at whichever path this names — so every feature the camera path has (snapping, per-point
    /// properties, transforms, undo) is available on the aim rail for free.
    /// </remarks>
    public enum PathTarget {
        /// <summary>The path the camera travels.</summary>
        Camera = 0,

        /// <summary>The path the camera looks at as it travels.</summary>
        Rail = 1,
    }
}
