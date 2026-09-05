namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using ExtendedPhotomode.Camera;

    using Game;
    using Game.Rendering;

    using ModsCommon.Utils;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Re-solves the camera every frame so a shot can track a subject that moves while it plays.
    /// </summary>
    /// <remarks>
    /// This is the one shot feature that cannot be baked. A <c>CinematicCameraSequence</c> holds five
    /// curves sampled by time — three for position, two for rotation — so the only thing it can express
    /// is where the camera was told to be at <c>t</c>. "Wherever the tram is at <c>t</c>" has no
    /// representation in it at all.
    /// <para>
    /// So following runs at playback instead, from a postfix over <c>CinematicCameraSequence.Refresh</c>
    /// — the single funnel every play, scrub and slider drag goes through, which is why one patch covers
    /// all of them. The postfix lands after <c>SampleTransform</c> has already written the curve values
    /// onto the controller, so overwriting them here wins without touching the sequence.
    /// </para>
    /// <para>
    /// Two consequences to expect rather than debug. A followed shot saved to a cinematic asset replays
    /// as an ordinary fixed shot in a later session, because the pin is an <c>Entity</c> and entities
    /// are recreated on load. And following only does anything while the world is actually simulating —
    /// with the game paused the subject does not move, so the shot plays exactly as generated.
    /// </para>
    /// </remarks>
    public partial class EPM_FollowSubjectSystem : GameSystemBase {
        private EPM_ShotSubjectSystem m_Subject;
        private PrefixedLogger        m_Log;
        private bool                  m_WarnedNoSubject;

        /// <summary>The live instance, for the Harmony postfix to reach — patches are static.</summary>
        public static EPM_FollowSubjectSystem Instance { get; private set; }

        protected override void OnCreate() {
            base.OnCreate();
            m_Log     = new PrefixedLogger(nameof(EPM_FollowSubjectSystem));
            m_Subject = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
            Instance  = this;
        }

        protected override void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }

            base.OnDestroy();
        }

        /// <summary>Nothing per-frame; the work is driven by playback through <see cref="Apply"/>.</summary>
        protected override void OnUpdate() { }

        /// <summary>Overrides the pose vanilla just wrote, so the shot tracks the pinned subject.</summary>
        /// <param name="controller">The controller vanilla's <c>Refresh</c> was given.</param>
        public void Apply(IGameCameraController controller) {
            FollowMode mode = Mod.Instance.Settings.Follow;

            if (mode != FollowMode.Aim && mode != FollowMode.Ride) {
                m_WarnedNoSubject = false;
                return;
            }

            // Game.CameraController.position has an empty setter, so writing a pose to the gameplay
            // controller succeeds silently and does nothing. Only the cinematic one stores it.
            if (controller is not CinematicCameraController) {
                return;
            }

            if (!m_Subject.TryGetLiveSubject(out Vector3 subject)) {
                if (!m_WarnedNoSubject) {
                    m_WarnedNoSubject = true;
                    m_Log.Warn("Follow is on but nothing is pinned to an entity, or the pinned one is " +
                               "gone. Select an object and use the orbit pin button, then generate again.");
                }

                return;
            }

            m_WarnedNoSubject = false;

            Vector3 position = controller.position;

            if (mode == FollowMode.Ride) {
                // The anchor is where the subject was when it was pinned, which is the point every
                // generator built the shot around — so the offset the keyframes encode stays intact
                // and the whole rig simply travels with the subject.
                Vector3 anchor = m_Subject.PinnedTarget ?? subject;
                position      += subject - anchor;
                controller.position = position;
            }

            controller.rotation = CameraAim.Euler(position, subject, controller.rotation);
        }
    }
}
