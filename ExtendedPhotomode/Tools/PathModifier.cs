namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using UnityEngine.InputSystem;

    #endregion

    /// <summary>
    /// Reads which modifier keys are held, so a click on the path tool can mean different things.
    /// </summary>
    /// <remarks>
    /// Read straight from the keyboard rather than through <c>ProxyAction</c>, which is how
    /// IntersectionMarkingTool does the same job. These are state tested at the moment of a click,
    /// not actions that fire, and binding the combinations as separate actions would collide with
    /// vanilla shortcuts.
    /// <para>
    /// This does not remove the need for <c>ModifierOptions.Ignore</c> on the apply action. That is a
    /// separate problem: an action left at the default <c>Allow</c> gets a
    /// <c>ProhibitionModifierProcessor</c> for every modifier it does not itself list, so the click
    /// never arrives at all while one is held. Ignore is what lets the click through; this is what
    /// tells us which modifier came with it.
    /// </para>
    /// </remarks>
    public static class PathModifier {
        /// <summary>Gets whether Ctrl is held, which forces curve shaping.</summary>
        public static bool Ctrl => Keyboard.current?.ctrlKey.isPressed ?? false;

        /// <summary>Gets whether Shift is held, which forces moving the point.</summary>
        public static bool Shift => Keyboard.current?.shiftKey.isPressed ?? false;

        /// <summary>Gets whether Alt is held.</summary>
        public static bool Alt => Keyboard.current?.altKey.isPressed ?? false;
    }
}
