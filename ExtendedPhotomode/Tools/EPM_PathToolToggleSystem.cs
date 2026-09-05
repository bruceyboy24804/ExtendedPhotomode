namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Systems;

    using Game;
    using Game.Tools;

    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine.InputSystem;

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
            HandleEscape();
        }

        /// <summary>Closes the panel on Escape, once drawing has already stopped.</summary>
        /// <remarks>
        /// The tool handles the first Escape itself and leaves the panel up; this is the second one.
        /// It is read straight off the keyboard because vanilla's Cancel action only reaches a system
        /// while that system is the active tool, and by this point it is not.
        /// <para>
        /// Deliberately narrow: only while the panel is open and no tool is running. Escape with no
        /// tool active also belongs to the pause menu, and nothing here can consume it — so the wider
        /// this reaches, the more often it fires alongside something else.
        /// </para>
        /// </remarks>
        private void HandleEscape() {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) {
                return;
            }

            var library = World.GetOrCreateSystemManaged<EPM_PathLibrarySystem>();

            if (library.PanelOpen && m_ToolSystem.activeTool != m_PathTool) {
                library.SetPanelOpen(false);
            }
        }

        /// <remarks>
        /// Opens the panel rather than the tool. Going straight into a tool on a keypress is what made
        /// this hard to discover: there was nowhere to see the library, the shot settings or Generate
        /// without also being live and one click away from editing the path. Network Tools solves the
        /// same problem the same way — the panel is the entry point, drawing is a button inside it.
        /// </remarks>
        private void HandleToggle() {
            var toggle = Mod.PathToolAction;

            if (toggle == null || !toggle.WasPressedThisFrame()) {
                return;
            }

            World.GetOrCreateSystemManaged<EPM_PathLibrarySystem>().TogglePanel();
        }

        private void HandleGenerate() {
            var generate = Mod.GeneratePathAction;

            if (generate == null || !generate.WasPressedThisFrame()) {
                return;
            }

            GeneratePath();
        }

        /// <summary>Copies the current settings onto the drawn path, ready to be solved.</summary>
        /// <remarks>
        /// Shared by generating and previewing, so the two cannot drift apart — a preview that solved
        /// the path with different settings from the generate that follows it would be worse than no
        /// preview at all.
        /// </remarks>
        public CameraPath PrepareForSolve() {
            CameraPath path     = m_PathTool.TravelPath;
            Setting    settings = Mod.Instance.Settings;

            // Set before the tangents are refreshed: closing the path changes what an end node's
            // neighbours are, and so what its auto tangent should be.
            path.Closed           = settings.PathClosed;
            path.TerrainMode      = settings.PathTerrain;
            path.TerrainClearance = settings.PathClearance;

            path.RefreshAutoTangents();

            path.Duration     = settings.PathDuration;
            path.MetresPerKey = settings.PathMetresPerKey;
            path.Pitch        = settings.PathPitch;
            path.LookAhead    = settings.PathLookAhead;
            path.Ease         = settings.PathEase;
            path.LookMode     = settings.PathLook;

            path.Rail = m_PathTool.RailPath;

            // A rail with fewer than two points cannot be aimed at, so the shot falls back rather than
            // generating with an aim that silently does nothing.
            if (settings.PathLook == PathLookMode.Rail && !path.Rail.IsValid) {
                m_Log.Warn("Aim is set to Rail but no aim rail is drawn; facing along the path instead. " +
                           "Switch the tool to Rail and draw at least two points.");

                path.LookMode = PathLookMode.Forward;
            }

            if (settings.PathLook == PathLookMode.Target) {
                if (m_Subject.PinnedTarget.HasValue) {
                    path.Target = m_Subject.PinnedTarget.Value;
                } else {
                    m_Log.Warn("Aim is set to Target but no centre is pinned; facing along the path instead. " +
                               "Pin one with the Pin centre checkbox or the orbit selection button.");
                    path.LookMode = PathLookMode.Forward;
                }
            }

            return path;
        }

        public bool GeneratePath() {
            CameraPath path = PrepareForSolve();

            if (!path.IsValid) {
                m_Log.Warn("Path needs at least two points before it can be generated.");
                return false;
            }

            Setting settings = Mod.Instance.Settings;

            bool  replace = settings.OrbitReplacesSequence;
            float start   = m_ShotSystem.NextStartTime(replace);

            List<CameraSample> samples = path.Solve(out List<float> focalLengths, out List<float> hours);

            m_ShotSystem.ApplyFraming(samples, focalLengths);
            m_ShotSystem.ApplyRig(samples);

            if (!m_ShotSystem.ApplySamples(samples, start, replace,
                                           $"path of {path.Nodes.Count} points, {path.MeasureLength():0}m")) {
                return false;
            }

            // Matched by substring: vanilla derives the lens property's id from an expression tree over
            // a captured field, so the exact string depends on compiler-generated naming.
            m_ShotSystem.ApplyPointCurve("focalLength", samples, focalLengths, start);
            m_ShotSystem.ApplyFocus(samples, start);
            m_ShotSystem.ApplyPointCurve(EPM_ShotSequenceSystem.kTimeOfDayPropertyId, samples, hours, start);

            return true;
        }
    }
}
