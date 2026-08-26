namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using ExtendedPhotomode.Camera;

    using Game;
    using Game.Rendering;
    using Game.Tools;

    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Holds what the shot is aimed at: the pinned centre, or a point derived from the live camera.
    /// </summary>
    /// <remarks>
    /// Split out of the orbit system because the subject is not an orbit concern. Every centre-aimed
    /// generator needs it — the dolly zoom aims at exactly the same point an orbit does, and the path
    /// tool's Look at target mode reads the same pin — so leaving it on the orbit system forced those
    /// callers to build a throwaway <see cref="OrbitShot"/> just to read a position out of it.
    /// </remarks>
    public partial class EPM_ShotSubjectSystem : GameSystemBase {
        private CameraUpdateSystem    m_CameraUpdateSystem;
        private PhotoModeRenderSystem m_PhotoModeRenderSystem;
        private ToolSystem            m_ToolSystem;
        private PrefixedLogger        m_Log;
        private bool                  m_SessionStarted;

        public Vector3? PinnedTarget { get; set; }

        public float? PinnedStartAngle { get; set; }

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                   = new PrefixedLogger(nameof(EPM_ShotSubjectSystem));
            m_CameraUpdateSystem    = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_PhotoModeRenderSystem = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_ToolSystem            = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        protected override void OnUpdate() {
            bool isActive = m_PhotoModeRenderSystem.Enabled;

            if (!isActive) {
                m_SessionStarted = false;
                return;
            }

            if (m_SessionStarted) {
                return;
            }

            m_SessionStarted = true;

            if (PinnedTarget.HasValue) {
                return;
            }

            if (TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                PinnedTarget = orbit.Target;
                m_Log.Debug($"Placed shot centre at {orbit.Target} for this photo mode session.");
            }
        }

        public bool TryPinToSelection() {
            Entity selected = m_ToolSystem.selected;

            if (selected == Entity.Null || !EntityManager.Exists(selected)) {
                m_Log.Warn("Nothing selected; cannot pin the shot to an object.");
                return false;
            }

            if (!EntityManager.HasComponent<Game.Objects.Transform>(selected)) {
                m_Log.Warn($"Selected entity {selected.Index} has no Transform; cannot aim at it.");
                return false;
            }

            PinnedTarget = EntityManager.GetComponentData<Game.Objects.Transform>(selected).m_Position;

            PinnedStartAngle = null;

            m_Log.Info($"Pinned shot to selected entity {selected.Index} at {PinnedTarget}");
            return true;
        }

        public OrbitShot OrbitFromCurrentCamera(Vector3 target) {
            var controller = m_CameraUpdateSystem?.activeCameraController;
            Vector3 position = (controller != null) ? controller.position : target + new Vector3(0f, 50f, -100f);
            return OrbitShot.FromCamera(position, target);
        }

        public bool TryBuildOrbitFromSettings(out OrbitShot orbit) {
            Setting settings = Mod.Instance.Settings;
            orbit = default;

            if (PinnedTarget.HasValue) {
                orbit               = OrbitShot.FromCamera(CameraPositionFor(PinnedTarget.Value, settings),
                                                          PinnedTarget.Value);
                orbit.Sweep         = settings.OrbitSweep;
                orbit.Duration      = settings.OrbitDuration;
                orbit.DegreesPerKey = settings.OrbitDegreesPerKey;
                orbit.LookAtTarget  = settings.OrbitLookAtTarget;
                orbit.EndRadius     = settings.OrbitEndRadius;
                orbit.StartAngle    = PinnedStartAngle ?? orbit.StartAngle;
                return true;
            }

            if (!TryGetOrbitTarget(settings.OrbitRadius, settings.OrbitHeight, out Vector3 target)) {
                return false;
            }

            orbit               = OrbitFromCurrentCamera(target);
            orbit.Sweep         = settings.OrbitSweep;
            orbit.Duration      = settings.OrbitDuration;
            orbit.DegreesPerKey = settings.OrbitDegreesPerKey;
            orbit.LookAtTarget  = settings.OrbitLookAtTarget;

            orbit.EndRadius = settings.OrbitEndRadius;
            return true;
        }

        private Vector3 CameraPositionFor(Vector3 target, Setting settings) {
            var controller = m_CameraUpdateSystem?.activeCameraController;
            float yaw = (controller != null) ? controller.rotation.y * Mathf.Deg2Rad : 0f;

            var offset = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw)) * settings.OrbitRadius;
            return target + offset + new Vector3(0f, settings.OrbitHeight, 0f);
        }

        private bool TryGetOrbitTarget(float radius, float height, out Vector3 target) {
            var controller = m_CameraUpdateSystem?.activeCameraController;
            if (controller == null) {
                target = Vector3.zero;
                return false;
            }

            float yaw     = controller.rotation.y * Mathf.Deg2Rad;
            var   heading = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

            target   = controller.position + heading * radius;
            target.y = controller.position.y - height;
            return true;
        }
    }
}
