namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using ExtendedPhotomode.Camera;

    using UnityEngine;

    #endregion

    /// <summary>The tool's loose numeric settings, as one binding.</summary>
    /// <remarks>
    /// A struct rather than a binding each. Three of these reached the game with no UI at all — the
    /// road tracer's length and spacing, and Simplify's tolerance — because every one needed its own
    /// binding, trigger and row, and it was easy to stop after the C# side.
    /// </remarks>
    public struct PathNumbers {
        public int traceLength;

        public int traceSpacing;

        public int simplifyTolerance;

        public int nudgeStep;

        public int railOffset;

        public int obstacleClearance;

        public int terrainClearance;

        public int terrainMode;

        public int obstacleMode;

        /// <summary>Which shot type the tool is authoring.</summary>
        public int shotType;

        /// <summary>Whether a subject is pinned, without which orbit and dolly have no handles.</summary>
        public bool hasSubject;

        /// <summary>Whether every control is shown, or the mod is deciding the hidden ones.</summary>
        public bool advanced;

        #region Orbit

        public int orbitRadius;

        public int orbitEndRadius;

        public int orbitHeight;

        public int orbitEndHeight;

        public int orbitSweep;

        public float orbitSweepEase;

        public int orbitDuration;

        public int orbitSpacing;

        public bool orbitLookAt;

        public bool orbitPreview;

        #endregion

        #region Path shape

        public int pathDuration;

        public int pathSpacing;

        public int pathPitch;

        public int pathLook;

        public int pathLookAhead;

        public float pathEase;

        #endregion

        #region How the shot is shot

        public int framing;

        public bool framingHold;

        public float framingLens;

        public int focus;

        public float focusDepth;

        public float focusEase;

        public int rig;

        public float rigStrength;

        public int rigSeed;

        public int follow;

        #endregion

        #region Dolly zoom

        public int dollyStart;

        public int dollyEnd;

        public int dollyDuration;

        public int dollyKeys;

        #endregion

        /// <summary>Whether anything is on the point clipboard, so Paste can be disabled.</summary>
        public bool hasClipboard;
    }

    /// <summary>Numbers, clipboard and nudging for <see cref="EPM_PathLibrarySystem"/>.</summary>
    public partial class EPM_PathLibrarySystem {
        /// <summary>The point whose properties Paste will stamp, or null when nothing is copied.</summary>
        /// <remarks>
        /// A detached clone, not a reference into the path. Holding the live node would make Paste
        /// stamp whatever that point has become since — including nothing, if it was deleted.
        /// </remarks>
        private PathNode m_Clipboard;

        /// <summary>The pinned subject, which orbit and dolly are both built around.</summary>
        private EPM_ShotSubjectSystem Subject =>
            World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();

        private PathNumbers BuildNumbers() {
            Setting settings = Mod.Instance.Settings;

            return new PathNumbers {
                traceLength       = settings.PathTraceLength,
                traceSpacing      = settings.PathTraceSpacing,
                simplifyTolerance = settings.PathSimplifyTolerance,
                nudgeStep         = settings.PathNudgeStep,
                railOffset        = settings.PathRailOffset,
                obstacleClearance = settings.PathObstacleClearance,
                terrainClearance  = settings.PathClearance,
                terrainMode       = (int)settings.PathTerrain,
                obstacleMode      = (int)settings.PathClearanceMode,
                shotType          = (int)settings.Shot,
                hasSubject        = Subject.PinnedTarget.HasValue,
                advanced          = settings.Advanced,

                orbitRadius    = settings.OrbitRadius,
                orbitEndRadius = settings.OrbitEndRadius,
                orbitHeight    = settings.OrbitHeight,
                orbitEndHeight = settings.OrbitEndHeight,
                orbitSweep     = settings.OrbitSweep,
                orbitSweepEase = settings.OrbitSweepEase,
                orbitDuration  = settings.OrbitDuration,
                orbitSpacing   = settings.OrbitDegreesPerKey,
                orbitLookAt    = settings.OrbitLookAtTarget,
                orbitPreview   = settings.ShowOrbitPreview,

                pathDuration  = settings.PathDuration,
                pathSpacing   = settings.PathMetresPerKey,
                pathPitch     = settings.PathPitch,
                pathLook      = (int)settings.PathLook,
                pathLookAhead = settings.PathLookAhead,
                pathEase      = settings.PathEase,

                framing     = (int)settings.Framing,
                framingHold = settings.FramingHoldSize,
                framingLens = settings.FramingFocalLength,
                focus       = (int)settings.Focus,
                focusDepth  = settings.FocusDepth,
                focusEase   = settings.FocusEase,
                rig         = (int)settings.Rig,
                rigStrength = settings.RigStrength,
                rigSeed     = settings.RigSeed,
                follow      = (int)settings.Follow,

                dollyStart    = settings.DollyStartDistance,
                dollyEnd      = settings.DollyEndDistance,
                dollyDuration = settings.DollyDuration,
                dollyKeys     = settings.DollyKeys,

                hasClipboard      = m_Clipboard != null,
            };
        }

        /// <summary>Writes one tool value, addressed by the key the UI knows it as.</summary>
        /// <remarks>
        /// The forty-one hand-written cases this replaced each carried their own clamp, which made
        /// this file the only place a range was written down — so the panel rows restated the ranges
        /// in TypeScript and the world handles invented a third copy. They now come from
        /// <see cref="ToolParameters"/>, which is the single place a bound is declared.
        /// </remarks>
        private void SetNumber(string field, float value) {
            // Not a value, so not a parameter: pinning reaches into the subject system rather than
            // storing a number, and clearing the pin is how you start a shot somewhere else — the
            // next click on empty ground places a new subject instead of being swallowed by the
            // existing one.
            if (field == "pinCentre") {
                if (value > 0.5f) {
                    Subject.TryPinToSelection();
                } else {
                    Subject.PinnedTarget     = null;
                    Subject.PinnedStartAngle = null;
                    Subject.PinnedEntity     = Unity.Entities.Entity.Null;
                }

                return;
            }

            if (!ToolParameters.TryGet(field, out ToolParameter parameter)) {
                m_Log.Warn($"Unknown path number \"{field}\".");
                return;
            }

            parameter.Set(value);
            Mod.Instance.Settings.ApplyAndSave();
        }

        /// <summary>Copies the selected point's properties, or stamps them onto the selection.</summary>
        /// <remarks>
        /// Properties only — never position. A paste that moved points would be a different operation
        /// wearing the same name, and the one thing nobody wants from "apply this point's settings" is
        /// for their path to change shape.
        /// </remarks>
        private void Clipboard(string operation) {
            int index = m_PathTool.SelectedPoint;

            if (index < 0) {
                return;
            }

            if (operation == "copy") {
                m_Clipboard = m_PathTool.Path.Nodes[index].Clone();
                m_Log.Debug($"Copied the properties of path point {index + 1}.");
                return;
            }

            if (operation != "paste" || m_Clipboard == null) {
                return;
            }

            m_PathTool.RecordUndo();

            foreach (int target in m_PathTool.Selection) {
                if (target >= m_PathTool.Path.Nodes.Count) {
                    continue;
                }

                PathNode node = m_PathTool.Path.Nodes[target];

                node.Dwell     = m_Clipboard.Dwell;
                node.Speed     = m_Clipboard.Speed;
                node.Pitch     = m_Clipboard.Pitch;
                node.Fov       = m_Clipboard.Fov;
                node.TimeOfDay = m_Clipboard.TimeOfDay;
                node.LookAt    = m_Clipboard.LookAt;
                node.Broken    = m_Clipboard.Broken;
            }

            m_PathTool.Path.RefreshAutoTangents();
        }

        /// <summary>Shifts every selected point by whole steps in X and Z.</summary>
        /// <remarks>
        /// Buttons rather than the arrow keys. Arrows already drive the game camera, and a tool that
        /// quietly stole them would break panning the moment the path tool was open — the modifier
        /// traps in this codebase are all variations on that same lesson.
        /// </remarks>
        private void Nudge(float x, float z) {
            if (m_PathTool.Selection.Count == 0) {
                return;
            }

            m_PathTool.RecordUndo();

            float step = Mod.Instance.Settings.PathNudgeStep;
            var   delta = new Vector3(x * step, 0f, z * step);

            foreach (int index in m_PathTool.Selection) {
                if (index < m_PathTool.Path.Nodes.Count) {
                    m_PathTool.Path.Nodes[index].Position += delta;
                }
            }

            m_PathTool.Path.RefreshAutoTangents();
        }

        /// <summary>Copies the travel path sideways to seed an aim rail.</summary>
        /// <remarks>
        /// A rail drawn from scratch has to be eyeballed against a path you cannot see while drawing
        /// it. Offsetting the travel path gives a rail that already runs parallel to the move, which is
        /// the common rig setup, and leaves it as an ordinary path to adjust afterwards.
        /// <para>
        /// Offset is perpendicular to each point's own direction of travel, not along a world axis, so
        /// the rail follows a curving path at a constant distance instead of crossing it.
        /// </para>
        /// </remarks>
        private void RailFromPath() {
            CameraPath travel = m_PathTool.TravelPath;

            if (!travel.IsValid) {
                m_Log.Warn("Draw a camera path first; there is nothing to offset into a rail.");
                return;
            }

            m_PathTool.RailPath.Clear();

            float offset = Mod.Instance.Settings.PathRailOffset;
            int   count  = travel.Nodes.Count;

            for (int i = 0; i < count; i++) {
                Vector3 here = travel.Nodes[i].Position;

                Vector3 ahead  = travel.Nodes[Mathf.Min(i + 1, count - 1)].Position;
                Vector3 behind = travel.Nodes[Mathf.Max(i - 1, 0)].Position;

                var direction = new Vector3(ahead.x - behind.x, 0f, ahead.z - behind.z);

                if (direction.sqrMagnitude < 0.0001f) {
                    direction = Vector3.forward;
                }

                direction.Normalize();

                // Rotate the heading 90° about Y to get the sideways direction.
                var sideways = new Vector3(direction.z, 0f, -direction.x);

                m_PathTool.RailPath.Nodes.Add(new PathNode(here + sideways * offset));
            }

            m_PathTool.RailPath.RefreshAutoTangents();
        }
    }
}
