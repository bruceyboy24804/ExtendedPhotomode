namespace ExtendedPhotomode.Camera {
    /// <summary>
    /// What clicking in the path tool acts on.
    /// </summary>
    /// <remarks>
    /// A mode chosen from the panel rather than from a held modifier. Points and their curve handles
    /// sit close enough together that hit-testing alone cannot reliably tell which one was meant, and
    /// a modifier only fixes that while it is held — there is nothing on screen saying which of the
    /// two a click would hit. A button leaves the current mode visible, which is what the handles
    /// being drawn or not now follows.
    /// </remarks>
    public enum PathEditMode {
        /// <summary>Clicking adds, inserts and moves points; curve handles are not pickable.</summary>
        Points = 0,

        /// <summary>Every handle is shown and pickable; points are not.</summary>
        Curves = 1,

        /// <summary>Clicking sets what the selected point aims at.</summary>
        /// <remarks>
        /// A mode rather than a one-shot arm, because targets are usually set several in a row — and
        /// because it is what makes the aiming lines worth drawing: they would be clutter in the other
        /// two modes and are the entire point of this one.
        /// </remarks>
        LookAt = 2,

        /// <summary>Clicking a road, rail or tram line traces it into a path.</summary>
        Network = 3,
    }
}
