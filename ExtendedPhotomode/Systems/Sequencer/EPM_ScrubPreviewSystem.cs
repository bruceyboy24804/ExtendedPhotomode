namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using Game;
    using Game.Rendering;
    using Game.UI.InGame;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine.Rendering;

    #endregion

    /// <summary>Makes lens and environment values visible while scrubbing outside photo mode.</summary>
    /// <remarks>
    /// <para>
    /// Scrubbing already applies them. <c>CinematicCameraSequence.Refresh</c> evaluates every modifier
    /// curve and calls the property's setter, and those setters write overrides onto photo mode's
    /// <c>CinematicControlVolume</c>. The problem is that the volume only contributes to the render
    /// when its weight is above zero, and <c>PhotoModeRenderSystem.OnUpdate</c> drives the weight to
    /// 0 the moment photo mode is not active. So the writes land and nothing is seen: the camera
    /// moves, because that is written straight to the controller, while focal length, focus and time
    /// of day do not.
    /// </para>
    /// <para>
    /// This holds the weight at 1 while the mod's timeline is open and photo mode is not, so a scrub
    /// in gameplay shows what the shot will actually look like. Vanilla never needs this because its
    /// own editor exists only inside photo mode.
    /// </para>
    /// <para>
    /// The ordering works out without a patch. When photo mode goes inactive that system sets the
    /// weight to 0 and then sets <c>Enabled = false</c> on ITSELF, so it stops running — nothing
    /// fights the value back down afterwards, and there is no per-frame tug of war. It resumes when
    /// <c>Enable(true)</c> re-enables it, which is also when it starts driving the weight itself
    /// again, so this system stands down at exactly the right moment.
    /// </para>
    /// <para>
    /// KNOWN COST, and it is the one to watch. That volume is priority 2000 and outranks the climate
    /// volume for every parameter it overrides. Raising its weight in gameplay therefore also applies
    /// any WEATHER override left on it — fog, clouds, sky — which is the same mechanism that makes
    /// photo mode stomp Weather Anarchy. It only bites if something has actually set an override
    /// state on those parameters, which vanilla does when a weather slider is touched and
    /// <see cref="EPM_WeatherSyncSystem"/> deliberately does not. The weight is restored the instant
    /// the panel closes, so the exposure is bounded by how long the timeline is open.
    /// </para>
    /// <para>
    /// THE VOLUME IS ONLY HALF OF IT. The lens properties — focal length, sensor size, aperture,
    /// focus distance — never touch that volume at all. Each is an <c>OverridableLensProperty</c>,
    /// and its <c>Apply</c> writes to <c>CameraUpdateSystem.cinematicCameraController</c>
    /// unconditionally, never to whichever controller is actually active. Outside photo mode the
    /// active controller is the gameplay one, so those writes land on a controller nothing renders
    /// from and the values silently do nothing. Weight cannot fix that; only making the cinematic
    /// controller active can, which is what <see cref="Engage"/> does.
    /// </para>
    /// <para>
    /// That swap is one line lifted from <c>PhotoModeUISystem.Activate</c>, and deliberately only
    /// that line. Entering photo mode properly would also block the tool barrier and force the
    /// default tool — kicking the user out of the path tool, which lives in gameplay precisely
    /// because photo mode blocks the barrier — and its exit calls
    /// <c>DisableAllCameraProperties</c>, which would clear every override the moment previewing
    /// stopped, including ones set by hand.
    /// </para>
    /// <para>
    /// The swap is taken lazily, on the first scrub or play rather than when the panel opens, so
    /// merely having the timeline up does not cost normal camera control. It is then held until the
    /// panel closes rather than released per drag: the cinematic controller is the only one that
    /// stores a pose at all (<c>CameraController.position</c> has an empty setter), so handing the
    /// camera back on mouse-up would snap the view off the frame just scrubbed to.
    /// </para>
    /// </remarks>
    public partial class EPM_ScrubPreviewSystem : GameSystemBase {
        private const string kVolumeField = "m_CameraControlVolume";

        private const string kActiveField = "m_Active";

        private const string kPlayingField = "m_Playing";

        private const string kTimeMember = "t";

        private PhotoModeRenderSystem   m_PhotoMode;
        private EPM_ShotListSystem      m_ShotList;
        private CinematicCameraUISystem m_Cinematic;
        private CameraUpdateSystem      m_Cameras;
        private PrefixedLogger          m_Log;

        /// <summary>The controller that was active before the cinematic one was swapped in.</summary>
        private IGameCameraController m_Previous;

        /// <summary>Whether this system swapped the cinematic controller in and owes a restore.</summary>
        private bool m_Engaged;

        /// <summary>Last seen playhead time, so a scrub can be spotted without a UI signal.</summary>
        /// <remarks>
        /// There is no binding that says "the user is dragging the playhead" — the drag lives
        /// entirely in the panel. But any scrub moves vanilla's own <c>t</c>, so watching it for
        /// change detects a scrub from either side of the UI, including vanilla's own timeline.
        /// </remarks>
        private float m_LastTime;

        /// <summary>Whether this system is the one currently holding the weight up.</summary>
        /// <remarks>
        /// Tracked so the weight is only ever pushed back down by the system that raised it. Without
        /// it, closing the panel while photo mode happened to be open would zero a weight photo mode
        /// owns and blank its own overrides.
        /// </remarks>
        private bool m_Holding;

        protected override void OnCreate() {
            base.OnCreate();

            m_Log       = new PrefixedLogger(nameof(EPM_ScrubPreviewSystem));
            m_PhotoMode = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_ShotList  = World.GetOrCreateSystemManaged<EPM_ShotListSystem>();
            m_Cinematic = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_Cameras   = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
        }

        protected override void OnUpdate() {
            if (!Mod.Instance.Settings.PreviewOutsidePhotoMode) {
                Release();
                Disengage();
                return;
            }

            bool photoMode = m_PhotoMode.GetMemberValue(kActiveField) is bool active && active;
            bool wanted    = m_ShotList.TimelineOpen && !photoMode;

            if (wanted != m_Holding) {
                if (wanted) {
                    Hold();
                } else {
                    Release();
                }
            }

            // The camera swap is scoped to the same window as the weight, but taken lazily inside it
            // — so opening the panel costs nothing until the timeline is actually touched.
            if (!wanted) {
                Disengage();
            } else if (!m_Engaged && IsScrubbing()) {
                Engage();
            }
        }

        /// <summary>Whether the playhead is being played or has just moved.</summary>
        private bool IsScrubbing() {
            if (m_Cinematic.GetMemberValue(kPlayingField) is bool playing && playing) {
                return true;
            }

            if (!(m_Cinematic.GetMemberValue(kTimeMember) is float time)) {
                return false;
            }

            bool moved = time != m_LastTime;

            m_LastTime = time;

            return moved;
        }

        /// <summary>Makes the cinematic controller active so lens values and poses actually land.</summary>
        private void Engage() {
            CinematicCameraController cinematic = m_Cameras.cinematicCameraController;

            if (cinematic == null) {
                m_Log.Warn("No cinematic camera controller; lens values will not preview outside "
                           + "photo mode.");
                return;
            }

            // ReferenceEquals, not ==: the left side is the IGameCameraController interface, so ==
            // would bind to object's operator and skip the Unity null-check overload anyway. Identity
            // is what is actually being asked here, so say so.
            if (ReferenceEquals(m_Cameras.activeCameraController, cinematic)) {
                return;
            }

            m_Previous = m_Cameras.activeCameraController;

            // Without the match the view would cut to wherever the cinematic controller was left,
            // which on a first scrub is usually the end of the last shot.
            cinematic.TryMatchPosition(m_Previous);

            m_Cameras.activeCameraController = cinematic;
            m_Engaged                        = true;
        }

        /// <summary>Hands the camera back to whatever held it before.</summary>
        private void Disengage() {
            if (!m_Engaged) {
                return;
            }

            m_Engaged = false;

            // Restored rather than assumed to be the gameplay controller — the timeline can be opened
            // from the orbit camera too. And only when the cinematic controller is still the active
            // one: if something else has taken the camera in the meantime (photo mode entry, a path
            // preview), it owns it now and must not be overwritten.
            if (m_Previous != null
                && ReferenceEquals(m_Cameras.activeCameraController,
                                   m_Cameras.cinematicCameraController)) {
                m_Cameras.activeCameraController = m_Previous;
            }

            m_Previous = null;
        }

        private void Hold() {
            if (!(m_PhotoMode.GetMemberValue(kVolumeField) is Volume volume)) {
                m_Log.Warn($"{kVolumeField} not found; lens and environment values will not preview "
                           + "outside photo mode.");
                return;
            }

            volume.weight = 1f;
            m_Holding     = true;
        }

        private void Release() {
            if (!m_Holding) {
                return;
            }

            m_Holding = false;

            // Only ever back to zero, and only from here. Photo mode drives the weight itself while
            // it is active, so if it has taken over in the meantime this must not overwrite it —
            // which is what the m_Holding flag is guarding.
            if (m_PhotoMode.GetMemberValue(kActiveField) is bool active && active) {
                return;
            }

            if (m_PhotoMode.GetMemberValue(kVolumeField) is Volume volume) {
                volume.weight = 0f;
            }
        }

        protected override void OnDestroy() {
            Release();
            Disengage();

            base.OnDestroy();
        }
    }
}
