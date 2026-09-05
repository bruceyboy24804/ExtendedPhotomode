namespace ExtendedPhotomode.Tools {
    /// <summary>
    /// Locale keys for the contextual hints shown at the cursor by the path tool.
    /// </summary>
    /// <remarks>
    /// These are keys, not text. A <c>DisplayNameOverride</c>'s <c>displayName</c> becomes the hint's
    /// name, and the UI resolves that through <c>Common.ACTION[&lt;name&gt;]</c> — so handing it prose
    /// puts the prose inside the brackets and shows the raw key on screen. Network Tools passes keys
    /// like <c>NetworkTools.HintTooltip.AddNode.Hover</c> for the same reason.
    /// <para>
    /// Being keys, they cannot carry a live value. That is why the placement height is not in the Add
    /// point hint: it is shown in the tool options' New point row and in the panel's status line,
    /// both of which can hold a number.
    /// </para>
    /// </remarks>
    public static class PathHints {
        /// <summary>Prefix keeping these out of the way of the game's own action names.</summary>
        private const string kPrefix = "ExtendedPhotomode.Hint.";

        public const string AddPoint = kPrefix + "AddPoint";

        public const string InsertPoint = kPrefix + "InsertPoint";

        public const string MovePoint = kPrefix + "MovePoint";

        public const string ShapeCurve = kPrefix + "ShapeCurve";

        public const string PickHandle = kPrefix + "PickHandle";

        public const string SharpCorner = kPrefix + "SharpCorner";

        public const string SmoothCorner = kPrefix + "SmoothCorner";

        public const string Reverse = kPrefix + "Reverse";

        public const string Raise = kPrefix + "Raise";

        public const string Lower = kPrefix + "Lower";

        public const string DeletePoint = kPrefix + "DeletePoint";

        public const string StopDrawing = kPrefix + "StopDrawing";

        public const string SetLookAt = kPrefix + "SetLookAt";

        public const string SelectForLookAt = kPrefix + "SelectForLookAt";

        public const string TraceRoad = kPrefix + "TraceRoad";

        public const string PlaceSubject = kPrefix + "PlaceSubject";

        public const string MoveOrbitCentre = kPrefix + "MoveOrbitCentre";

        public const string DragOrbitStart = kPrefix + "DragOrbitStart";

        public const string DragOrbitEnd = kPrefix + "DragOrbitEnd";

        public const string DragDollyStart = kPrefix + "DragDollyStart";

        public const string DragDollyEnd = kPrefix + "DragDollyEnd";
    }
}
