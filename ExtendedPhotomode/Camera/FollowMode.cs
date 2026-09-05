namespace ExtendedPhotomode.Camera {
    /// <summary>
    /// What a generated shot does about a subject that moves while the shot plays.
    /// </summary>
    /// <remarks>
    /// Unlike every other setting on the panel this one is not baked into keyframes, and it cannot be:
    /// a <c>CinematicCameraSequence</c> stores rotation as two curves sampled by time, so there is
    /// nowhere in it to put "wherever the tram is at t". Following is therefore applied live, as a
    /// postfix over <c>CinematicCameraSequence.Refresh</c>, against the entity pinned as the shot
    /// centre. The consequence worth knowing is that a saved shot replays as an ordinary fixed shot
    /// on a later session — the pin is an <c>Entity</c>, and entities are recreated on load.
    /// <para>
    /// <c>None</c> is first and deliberately meaningless; photo mode's <c>EnumField</c> drops the
    /// lowest-valued option on its first push. See <see cref="ShotType"/>.
    /// </para>
    /// </remarks>
    public enum FollowMode {
        None = 0,

        /// <summary>The shot plays exactly as it was generated.</summary>
        Off = 1,

        /// <summary>Keyframed position, but rotation re-solved every frame to keep the subject framed.</summary>
        Aim = 2,

        /// <summary>The whole shot rides along with the subject, and aims at it.</summary>
        Ride = 3,
    }
}
