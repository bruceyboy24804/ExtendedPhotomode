namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Tools;

    using Game;
    using Game.Rendering;

    using ModsCommon.Utils;

    using UnityEngine;
    using UnityEngine.InputSystem;

    #endregion

    /// <summary>Flies the drawn path in normal gameplay, without writing anything to the timeline.</summary>
    /// <remarks>
    /// The authoring loop was draw, generate, leave the tool, open photo mode, play, come back. Every
    /// one of those steps is a chance to lose your place, and the last one throws away the tool state
    /// you were in. This closes it: press once and the camera flies what you have.
    /// <para>
    /// It has to borrow the cinematic camera controller to do it. <c>Game.CameraController.position</c>
    /// has an EMPTY setter, so the gameplay controller accepts a pose and silently discards it — a
    /// preview driven through it would run for its full duration from a motionless camera and report
    /// success. Only <see cref="CinematicCameraController"/> stores what it is given, so the preview
    /// swaps <c>activeCameraController</c> the way <c>CinematicCameraUISystem</c> does and puts the old
    /// one back when it stops.
    /// </para>
    /// <para>
    /// Nothing here touches the sequence. A preview is deliberately not a generate — the point is to
    /// look before committing, and writing keys as a side effect of looking would clear whatever is
    /// already on the timeline.
    /// </para>
    /// </remarks>
    public partial class EPM_PathPreviewSystem : GameSystemBase {
        private EPM_PathToolSystem     m_PathTool;
        private EPM_PathToolToggleSystem m_Toggle;
        private CameraUpdateSystem     m_CameraUpdateSystem;
        private PrefixedLogger         m_Log;

        private List<CameraSample>  m_Samples = new List<CameraSample>();
        private IGameCameraController m_Previous;
        private float               m_Time;

        /// <summary>Whether a preview is running right now.</summary>
        public bool Playing { get; private set; }

        /// <summary>How far through the preview we are, 0 to 1.</summary>
        public float Progress {
            get {
                float length = Length;
                return (length > 0f) ? Mathf.Clamp01(m_Time / length) : 0f;
            }
        }

        private float Length => (m_Samples.Count > 0) ? m_Samples[m_Samples.Count - 1].Time : 0f;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                = new PrefixedLogger(nameof(EPM_PathPreviewSystem));
            m_PathTool           = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
            m_Toggle             = World.GetOrCreateSystemManaged<EPM_PathToolToggleSystem>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
        }

        /// <summary>Starts or stops the preview.</summary>
        public void Toggle() {
            if (Playing) {
                Stop();
                return;
            }

            Start();
        }

        /// <summary>Solves the drawn path and starts flying it.</summary>
        /// <returns>False when there is nothing to fly, or no cinematic controller to fly it with.</returns>
        public bool Start() {
            if (Playing) {
                return true;
            }

            // Whichever shot type is selected, solved the same way the generator or the editor would
            // — so what you preview is what you would get, including easing, dwell, per-point speed
            // and the terrain clamp on a path, and the real spiral on an orbit.
            if (!m_PathTool.TrySolveActiveShot(out m_Samples)) {
                m_Log.Warn("Nothing to preview — the shot needs a subject, or a path of two points.");
                return false;
            }

            CinematicCameraController cinematic = m_CameraUpdateSystem.cinematicCameraController;

            if (cinematic == null) {
                m_Log.Warn("No cinematic camera controller is available; cannot preview.");
                return false;
            }

            m_Previous = m_CameraUpdateSystem.activeCameraController;

            m_CameraUpdateSystem.activeCameraController = cinematic;

            m_Time   = 0f;
            Playing  = true;

            return true;
        }

        /// <summary>Ends the preview and hands the camera back.</summary>
        public void Stop() {
            if (!Playing) {
                return;
            }

            Playing = false;

            // Restored rather than assumed to be the gameplay controller: the preview can be started
            // from the orbit camera too, and dropping the player somewhere they did not choose is the
            // one thing a preview must not do.
            if (m_Previous != null) {
                m_CameraUpdateSystem.activeCameraController = m_Previous;
            }

            m_Previous = null;
            m_Log.Debug("Preview stopped.");
        }

        protected override void OnUpdate() {
            if (!Playing) {
                return;
            }

            // Escape cancels. Read from the keyboard directly because the tool's own Cancel action only
            // reaches a system while that system is the active tool, and the preview runs whether the
            // tool is up or not.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                Stop();
                return;
            }

            // Unscaled, so a preview runs at real-world speed regardless of simulation speed or the
            // game being paused. A shot's duration is a duration in seconds of footage.
            m_Time += UnityEngine.Time.unscaledDeltaTime;

            if (m_Time >= Length) {
                Sample(Length);
                Stop();
                return;
            }

            Sample(m_Time);
        }

        /// <summary>Writes the pose for a moment in the shot onto the camera.</summary>
        /// <remarks>
        /// Straight-line interpolation between the solved keys, not the timeline's smoothed curves —
        /// close enough to judge framing and pacing, which is what a preview is for. Rotation lerps
        /// safely because <c>CameraPath.Solve</c> already unwraps yaw, so consecutive keys never differ
        /// by more than the turn actually taken and there is no 359-to-1 jump to trip over.
        /// </remarks>
        private void Sample(float time) {
            var controller = m_CameraUpdateSystem.activeCameraController as CinematicCameraController;

            if (controller == null) {
                // Something else took the camera mid-flight — photo mode opening, most likely. Stopping
                // is right: fighting for it would leave two systems writing the same transform.
                Stop();
                return;
            }

            int index = 0;

            while (index < m_Samples.Count - 2 && m_Samples[index + 1].Time < time) {
                index++;
            }

            CameraSample from = m_Samples[index];
            CameraSample to   = m_Samples[index + 1];

            float span = to.Time - from.Time;
            float t    = (span > 0.0001f) ? Mathf.Clamp01((time - from.Time) / span) : 0f;

            controller.position = Vector3.Lerp(from.Position, to.Position, t);
            controller.rotation = Vector3.Lerp(from.Rotation, to.Rotation, t);
        }
    }
}
