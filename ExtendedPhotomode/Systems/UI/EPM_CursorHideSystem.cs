namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using Game;
    using Game.Input;
    using Game.Rendering;
    using Game.UI.InGame;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    #endregion

    /// <summary>Hides the mouse pointer while a cinematic shot is playing back.</summary>
    /// <remarks>
    /// A pointer sitting in the middle of an otherwise clean frame is the one piece of UI photo mode's
    /// own hide-UI toggle leaves behind, and it lands in whatever screen recording is running.
    /// Driven through <see cref="InputManager.hideCursor"/> rather than <c>Cursor.visible</c> directly.
    /// The game recomputes visibility from that flag in <c>UpdateCursorVisibility</c> — factoring in
    /// the active control scheme, so a gamepad player is handled correctly — and writing
    /// <c>Cursor.visible</c> behind its back would simply be overwritten on the next recompute.
    /// </remarks>
    public partial class EPM_CursorHideSystem : GameSystemBase {
        private const string kPlayingMember = "playing";

        private CinematicCameraUISystem m_CinematicCameraUISystem;
        private PhotoModeRenderSystem   m_PhotoModeRenderSystem;
        private PrefixedLogger          m_Log;
        private bool                    m_Hiding;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                     = new PrefixedLogger(nameof(EPM_CursorHideSystem));
            m_CinematicCameraUISystem = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_PhotoModeRenderSystem   = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
        }

        protected override void OnUpdate() {
            bool wanted = Mod.Instance.Settings.HideCursorDuringPlayback && IsPlaying();

            if (wanted != m_Hiding) {
                Apply(wanted);
            }
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();
            Apply(false);
        }

        protected override void OnDestroy() {
            Apply(false);
            base.OnDestroy();
        }

        private bool IsPlaying() {
            if (!m_PhotoModeRenderSystem.Enabled) {
                return false;
            }

            return m_CinematicCameraUISystem.GetMemberValue(kPlayingMember) is bool playing && playing;
        }

        private void Apply(bool hide) {
            if (m_Hiding == hide) {
                return;
            }

            InputManager input = InputManager.instance;

            if (input == null) {
                return;
            }

            input.hideCursor = hide;
            m_Hiding         = hide;

            m_Log.Debug(hide ? "Hid the pointer for playback." : "Restored the pointer.");
        }
    }
}
