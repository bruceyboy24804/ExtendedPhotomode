namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;

    #endregion

    /// <summary>Marks an enum member as a choice the tool UI offers, with the icon and text it shows.</summary>
    /// <remarks>
    /// <para>
    /// Nothing in C# reads this. It is an input to the code generator, which turns annotated enums
    /// into the TypeScript the panel builds its button rows from — the same arrangement the Network
    /// Tools mod uses, where <c>[EnumOption]</c> exists purely so its codegen can see it.
    /// </para>
    /// <para>
    /// The point is that an enum member and the row that selects it stop being two separate edits.
    /// The panel currently keeps ten hand-written tables mirroring ten C# enums; add a member and
    /// nothing tells you the table is short one entry, and renumber one and every icon after it
    /// silently shifts to the wrong choice. Neither mistake can survive generation.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EnumOptionAttribute : Attribute {
        /// <summary>The icon shown on the button, as a <c>coui://</c> path.</summary>
        public string Icon { get; }

        /// <summary>What the button's tooltip says.</summary>
        public string Tooltip { get; }

        /// <summary>Whether the option is offered at all; false keeps the member out of the UI.</summary>
        /// <remarks>
        /// For members that exist for the type system rather than for the player — the zero-valued
        /// <c>None</c> that the photo mode dropdown trap requires every enum to carry, which is there
        /// to be eaten by the widget and must never appear as a choice.
        /// </remarks>
        public bool Visible { get; set; } = true;

        public EnumOptionAttribute(string icon, string tooltip) {
            Icon    = icon;
            Tooltip = tooltip;
        }
    }
}
