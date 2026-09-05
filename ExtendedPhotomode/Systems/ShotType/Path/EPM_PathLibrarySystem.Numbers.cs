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

        private void SetNumber(string field, float value) {
            Setting settings = Mod.Instance.Settings;
            int     rounded  = Mathf.RoundToInt(value);

            switch (field) {
                case "traceLength":       settings.PathTraceLength = Mathf.Clamp(rounded, 50, 5000); break;
                case "traceSpacing":      settings.PathTraceSpacing = Mathf.Clamp(rounded, 5, 200); break;
                case "simplifyTolerance": settings.PathSimplifyTolerance = Mathf.Clamp(rounded, 1, 100); break;
                case "nudgeStep":         settings.PathNudgeStep = Mathf.Clamp(rounded, 1, 100); break;
                case "railOffset":        settings.PathRailOffset = Mathf.Clamp(rounded, -500, 500); break;
                case "obstacleClearance": settings.PathObstacleClearance = Mathf.Clamp(rounded, 0, 200); break;
                case "terrainClearance":  settings.PathClearance = Mathf.Clamp(rounded, 0, 500); break;

                case "terrainMode":
                    settings.PathTerrain = (PathTerrainMode)Mathf.Clamp(rounded,
                                                                        (int)PathTerrainMode.Free,
                                                                        (int)PathTerrainMode.Follow);
                    break;

                // The shot type belongs here as much as any of the numbers do: it is chosen while
                // authoring, and its dropdown lives in photo mode, where the tool cannot run.
                case "shotType":
                    settings.Shot = (ShotType)Mathf.Clamp(rounded, (int)ShotType.Orbit,
                                                          (int)ShotType.Path);
                    break;

                case "obstacleMode":
                    settings.PathClearanceMode = (PathClearanceMode)Mathf.Clamp(
                        rounded, (int)PathClearanceMode.Off, (int)PathClearanceMode.Lift);
                    break;

                case "orbitRadius":    settings.OrbitRadius = Mathf.Clamp(rounded, 10, 1000); break;
                case "orbitEndRadius": settings.OrbitEndRadius = Mathf.Clamp(rounded, 10, 1000); break;
                case "orbitHeight":    settings.OrbitHeight = Mathf.Clamp(rounded, -100, 500); break;
                case "orbitEndHeight": settings.OrbitEndHeight = Mathf.Clamp(rounded, -100, 500); break;
                case "orbitSweep":     settings.OrbitSweep = Mathf.Clamp(rounded, -720, 720); break;
                case "orbitDuration":  settings.OrbitDuration = Mathf.Clamp(rounded, 5, 300); break;
                case "orbitSpacing":   settings.OrbitDegreesPerKey = Mathf.Clamp(rounded, 5, 90); break;
                case "orbitLookAt":    settings.OrbitLookAtTarget = value > 0.5f; break;
                case "orbitPreview":   settings.ShowOrbitPreview = value > 0.5f; break;

                // Not rounded: the eases are the only fractional values here, and truncating them to
                // whole numbers would leave a 0-to-1 control with exactly two positions.
                case "orbitSweepEase": settings.OrbitSweepEase = Mathf.Clamp01(value); break;

                case "pathDuration":  settings.PathDuration = Mathf.Clamp(rounded, 5, 300); break;
                case "pathSpacing":   settings.PathMetresPerKey = Mathf.Clamp(rounded, 5, 200); break;
                case "pathPitch":     settings.PathPitch = Mathf.Clamp(rounded, -80, 80); break;
                case "pathLookAhead": settings.PathLookAhead = Mathf.Clamp(rounded, 0, 500); break;
                case "pathEase":      settings.PathEase = Mathf.Clamp01(value); break;

                case "pathLook":
                    settings.PathLook = (PathLookMode)Mathf.Clamp(rounded, (int)PathLookMode.Forward,
                                                                  (int)PathLookMode.Rail);
                    break;

                case "framingHold": settings.FramingHoldSize = value > 0.5f; break;
                case "framingLens": settings.FramingFocalLength = Mathf.Clamp(value, 0.11f, 1466f); break;
                case "focusDepth":  settings.FocusDepth = Mathf.Clamp01(value); break;
                case "focusEase":   settings.FocusEase = Mathf.Clamp01(value); break;
                case "rigStrength": settings.RigStrength = Mathf.Clamp01(value); break;
                case "rigSeed":     settings.RigSeed = Mathf.Clamp(rounded, 1, 999); break;

                case "framing":
                    settings.Framing = (FramingRule)Mathf.Clamp(rounded, (int)FramingRule.None,
                                                                (int)FramingRule.Headroom);
                    break;

                case "focus":
                    settings.Focus = (FocusMode)Mathf.Clamp(rounded, (int)FocusMode.Off,
                                                            (int)FocusMode.Rack);
                    break;

                case "rig":
                    settings.Rig = (CameraRig)Mathf.Clamp(rounded, (int)CameraRig.Free,
                                                          (int)CameraRig.Handheld);
                    break;

                case "follow":
                    settings.Follow = (FollowMode)Mathf.Clamp(rounded, (int)FollowMode.Off,
                                                              (int)FollowMode.Ride);
                    break;

                case "dollyStart":    settings.DollyStartDistance = Mathf.Clamp(rounded, 5, 1000); break;
                case "dollyEnd":      settings.DollyEndDistance = Mathf.Clamp(rounded, 5, 1000); break;
                case "dollyDuration": settings.DollyDuration = Mathf.Clamp(rounded, 1, 120); break;
                case "dollyKeys":     settings.DollyKeys = Mathf.Clamp(rounded, 2, 120); break;

                // Clearing the pin is how you start a shot somewhere else: the next click on empty
                // ground places a new subject rather than being swallowed by an existing one.
                case "pinCentre":
                    if (value > 0.5f) {
                        Subject.TryPinToSelection();
                    } else {
                        Subject.PinnedTarget     = null;
                        Subject.PinnedStartAngle = null;
                        Subject.PinnedEntity     = Unity.Entities.Entity.Null;
                    }

                    return;

                default:
                    m_Log.Warn($"Unknown path number \"{field}\".");
                    return;
            }

            settings.ApplyAndSave();
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
