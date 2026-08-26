namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Systems;

    using Game;
    using Game.Tools;

    using ModsCommon.Utils;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Watches the path tool's hotkeys and switches <see cref="EPM_PathToolSystem"/> in and out of
    /// being the active tool.
    /// </summary>
    /// <remarks>
    /// A separate system is needed because a <see cref="ToolBaseSystem"/> only updates while it is
    /// the active tool — it cannot observe the hotkey that would activate it.
    /// </remarks>
    public partial class EPM_PathToolToggleSystem : GameSystemBase {
        private EPM_PathToolSystem m_PathTool;
        private EPM_ShotSequenceSystem m_ShotSystem;
        private EPM_ShotSubjectSystem  m_Subject;
        private ToolSystem         m_ToolSystem;
        private PrefixedLogger     m_Log;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log        = new PrefixedLogger(nameof(EPM_PathToolToggleSystem));
            m_PathTool   = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
            m_ShotSystem = World.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>();
            m_Subject    = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        protected override void OnUpdate() {
            HandleToggle();
            HandleGenerate();
        }

        private void HandleToggle() {
            var toggle = Mod.PathToolAction;

            if (toggle == null || !toggle.WasPressedThisFrame()) {
                return;
            }

            if (m_ToolSystem.activeTool == m_PathTool) {
                m_PathTool.RequestDisable();
            } else {
                m_PathTool.RequestEnable();
            }
        }

        private void HandleGenerate() {
            var generate = Mod.GeneratePathAction;

            if (generate == null || !generate.WasPressedThisFrame()) {
                return;
            }

            GeneratePath();
        }

        public bool GeneratePath() {
            CameraPath path = m_PathTool.Path;
            path.RefreshAutoTangents();

            if (!path.IsValid) {
                m_Log.Warn("Path needs at least two points before it can be generated.");
                return false;
            }

            Setting settings = Mod.Instance.Settings;
            path.Duration     = settings.PathDuration;
            path.MetresPerKey = settings.PathMetresPerKey;
            path.Pitch        = settings.PathPitch;
            path.LookMode = settings.PathLook;

            if (settings.PathLook == PathLookMode.Target) {
                if (m_Subject.PinnedTarget.HasValue) {
                    path.Target = m_Subject.PinnedTarget.Value;
                } else {
                    m_Log.Warn("Aim is set to Target but no centre is pinned; facing along the path instead. " +
                               "Pin one with the Pin centre checkbox or the orbit selection button.");
                    path.LookMode = PathLookMode.Forward;
                }
            }

            bool replace = settings.OrbitReplacesSequence;

            return m_ShotSystem.ApplySamples(path.Solve(), m_ShotSystem.NextStartTime(replace), replace,
                                             $"path of {path.Nodes.Count} points, {path.MeasureLength():0}m");
        }
    }
}
