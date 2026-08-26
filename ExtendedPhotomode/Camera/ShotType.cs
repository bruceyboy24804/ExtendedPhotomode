namespace ExtendedPhotomode.Camera {
    /// <summary>
    /// Which generator the single Generate button runs.
    /// </summary>
    /// <remarks>
    /// One button plus this dropdown, rather than a button per shot type. The photo mode button row is
    /// a flex container that already overflows — every button added shrinks all of them — so a button
    /// per generator does not survive more than about three of them. Adding a shot type is now an
    /// entry here and a case in the dispatch, with no UI change at all.
    /// <para>
    /// <c>None</c> is first and deliberately meaningless: photo mode's <c>EnumField</c> drops the
    /// lowest-valued option on its first push, so every enum bound to a dropdown here needs a
    /// sacrificial entry at zero. See <c>Camera.GateFitMode</c>, which is shaped the same way.
    /// </para>
    /// </remarks>
    public enum ShotType {
        None = 0,

        Orbit = 1,

        DollyZoom = 2,

        Path = 3,
    }
}
