namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Components;
    using ExtendedPhotomode.Tools;

    using Colossal.UI.Binding;

    using Game.CinematicCamera;
    using Game.Rendering.CinematicCamera;
    using Game.Tools;
    using Game.UI.InGame;

    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>One entry in the saved path library, as the UI sees it.</summary>
    public struct PathLibraryEntry {
        public int index;

        public string name;

        public int points;
    }

    /// <summary>What the panel reports about the path as a whole.</summary>
    public struct PathMetrics {
        public int points;

        public float length;

        public float duration;

        /// <summary>Average camera speed in km/h, which is the unit people have intuition for.</summary>
        public float speed;

        public bool canUndo;

        public bool canRedo;

        public int selected;

        public bool frustums;

        /// <summary>How many points the aim rail has, so the panel can say when it is empty.</summary>
        public int railPoints;
    }

    /// <summary>The selected path point, as the inspector needs it.</summary>
    /// <remarks>
    /// <c>index</c> is -1 when nothing is selected, which is how the panel decides to hide the
    /// inspector. <c>pitch</c> carries the path's own pitch when the point does not override it, so
    /// the field always shows the value actually in force rather than an empty box.
    /// </remarks>
    public struct PathPointEntry {
        public int index;

        public float x;

        public float y;

        public float z;

        public float dwell;

        public float pitch;

        public bool pitchOverridden;

        public bool broken;

        public float speed;

        public float fov;

        public bool fovOverridden;

        public float timeOfDay;

        public bool timeOfDayOverridden;

        public bool lookAtSet;
    }

    /// <summary>Keeps a library of named camera paths in the city save, and exposes it to the UI.</summary>
    /// <remarks>
    /// Separate from <see cref="EPM_PathStoreSystem"/>, which persists the one path currently being
    /// drawn. That is the working copy and survives a reload on its own; this is the shelf you
    /// deliberately put a finished path on so it can be brought back later.
    /// </remarks>
    public partial class EPM_PathLibrarySystem : CommonUISystemBase {
        /// <summary>How much one press of grow or shrink changes the path's size.</summary>
        private const float kScaleStep = 0.1f;

        public const string kPathsBinding = "savedPaths";

        public const string kSaveTrigger = "savePath";

        public const string kLoadTrigger = "loadPath";

        public const string kDeleteTrigger = "deletePath";

        public const string kRenameTrigger = "renamePath";

        public const string kToolActiveBinding = "pathToolActive";

        /// <summary>Binding key reporting whether the path panel is open.</summary>
        public const string kPanelOpenBinding = "pathPanelOpen";

        /// <summary>Binding key reporting how many points the drawn path has.</summary>
        public const string kPointCountBinding = "pathPointCount";

        /// <summary>Trigger key that opens or closes the panel.</summary>
        /// <remarks>
        /// Deliberately the same id as the binding it writes. <c>TwoWayBinding</c> in
        /// <c>Common/ui/utils</c> derives both <c>BINDING:id</c> and <c>TRIGGER:id</c> from one name,
        /// so a get/set pair sharing an id collapses to a single declaration on the UI side.
        /// </remarks>
        public const string kSetPanelOpenTrigger = kPanelOpenBinding;

        /// <summary>Trigger key that starts or stops drawing.</summary>
        public const string kSetToolActiveTrigger = kToolActiveBinding;

        /// <summary>Trigger key that writes the drawn path to the timeline.</summary>
        public const string kGenerateTrigger = "generatePathShot";

        /// <summary>Binding key reporting what a click in the tool acts on.</summary>
        public const string kEditModeBinding = "pathEditMode";

        /// <summary>Binding key reporting the height new points are placed at.</summary>
        public const string kPlacementHeightBinding = "pathPlacementHeight";

        /// <summary>Trigger key that chooses what a click acts on.</summary>
        public const string kSetEditModeTrigger = kEditModeBinding;

        /// <summary>Binding key carrying the selected point, or null when nothing is selected.</summary>
        public const string kSelectedPointBinding = "pathSelectedPoint";

        /// <summary>Trigger key that selects a point by index.</summary>
        public const string kSelectPointTrigger = "selectPathPoint";

        /// <summary>Trigger key that edits one field of the selected point.</summary>
        public const string kEditPointTrigger = "editPathPoint";

        /// <summary>Binding key carrying the id of the loaded library path, or -1 when scratch.</summary>
        public const string kLoadedPathBinding = "pathLoadedId";

        /// <summary>Trigger key that throws the drawn path away and starts a new one.</summary>
        public const string kNewPathTrigger = "newPath";

        /// <summary>Trigger key that rebuilds the drawn path from the timeline's camera keyframes.</summary>
        public const string kImportTrigger = "importPathFromSequence";

        /// <summary>Trigger key that moves, turns, resizes or flips the whole path.</summary>
        /// <remarks>
        /// One trigger taking the operation as a string rather than six of them, matching how
        /// <see cref="kEditPointTrigger"/> already handles the per-point fields. Adding an operation is
        /// then a case here and a button there, with no binding plumbing in between.
        /// </remarks>
        public const string kTransformTrigger = "transformPath";

        /// <summary>Binding key reporting whether a preview is flying, and its trigger.</summary>
        public const string kPreviewBinding = "pathPreviewing";

        /// <summary>Binding key carrying the path's length, duration and implied camera speed.</summary>
        public const string kMetricsBinding = "pathMetrics";

        /// <summary>Binding key reporting which path the tool edits, and its trigger.</summary>
        public const string kEditTargetBinding = "pathEditTarget";

        /// <summary>Binding key carrying the tool's loose numeric settings, and its setter.</summary>
        /// <remarks>
        /// One binding and one keyed setter rather than a pair per number, matching how
        /// <see cref="kEditPointTrigger"/> already handles the per-point fields. Three settings reached
        /// the game with no UI at all because each needed its own binding, its own trigger and its own
        /// row; this makes adding the next one a field here and a row there.
        /// </remarks>
        public const string kNumbersBinding = "pathNumbers";

        public const string kSetNumberTrigger = "setPathNumber";

        /// <summary>Binding key reporting whether edits apply to the whole selection.</summary>
        public const string kEditAllBinding = "pathEditAll";

        /// <summary>Trigger key that copies or stamps a point's properties.</summary>
        public const string kClipboardTrigger = "pathClipboard";

        /// <summary>Trigger key that shifts the selected points by a step.</summary>
        public const string kNudgeTrigger = "nudgePathPoints";

        /// <summary>Binding key reporting what the cursor snaps to, and its trigger.</summary>
        public const string kSnapModeBinding = "pathSnapMode";

        /// <summary>Binding key reporting whether the path closes on itself, and its trigger.</summary>
        public const string kClosedBinding = "pathClosed";

        /// <summary>Binding key carrying the number the active snap mode uses, and its trigger.</summary>
        /// <remarks>
        /// One field rather than three. Grid size, angle step and snap radius are never in force at
        /// the same time, so a row per setting would leave two of them permanently inert next to the
        /// one that matters — the tool options panel is narrow and every row costs.
        /// </remarks>
        public const string kSnapValueBinding = "pathSnapValue";

        private EPM_PathToolSystem                    m_PathTool;
        private ToolSystem                            m_ToolSystem;
        private EntityQuery                           m_Query;
        private GetterValueBinding<PathLibraryEntry[]> m_PathsBinding;
        private EPM_PathToolToggleSystem              m_Toggle;
        private CinematicCameraUISystem               m_CinematicUISystem;
        private EPM_PathPreviewSystem                 m_Preview;

        protected override string ModId => Mod.Instance.Id;

        protected override void OnCreate() {
            base.OnCreate();
            m_PathTool    = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
            m_ToolSystem  = World.GetOrCreateSystemManaged<ToolSystem>();
            m_Query       = GetEntityQuery(ComponentType.ReadOnly<EPM_SavedPath>());

            m_Toggle      = World.GetOrCreateSystemManaged<EPM_PathToolToggleSystem>();
            m_CinematicUISystem = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_Preview           = World.GetOrCreateSystemManaged<EPM_PathPreviewSystem>();

            CreateBinding(kToolActiveBinding, () => m_ToolSystem.activeTool == m_PathTool);
            CreateBinding(kPanelOpenBinding, () => PanelOpen);
            CreateBinding(kPointCountBinding, () => m_PathTool.Path.Nodes.Count);
            CreateTrigger<bool>(kSetPanelOpenTrigger, SetPanelOpen);
            CreateTrigger<bool>(kSetToolActiveTrigger, SetToolActive);
            // Stages the shot rather than writing it to the timeline, exactly as the photo mode
            // panel's Generate does — they are two buttons for one action and have to agree.
            //
            // This is the SECOND generate trigger in the mod: EPM_OrbitUISystem registers its own for
            // the photo mode panel. Changing one and not the other is why pressing Generate here
            // still put the move straight on the timeline while the generated list stayed empty.
            CreateTrigger(kGenerateTrigger,
                          () => World.GetOrCreateSystemManaged<EPM_ShotListSystem>()
                                     .AddShot(string.Empty));
            CreateBinding(kEditModeBinding, () => (int)m_PathTool.EditMode);
            CreateBinding(kPlacementHeightBinding, () => Mod.Instance.Settings.PathPointHeight);
            CreateTrigger<int>(kSetEditModeTrigger, SetEditMode);

            // Auto-updating, unlike the saved list: this changes while a point is being dragged.
            CreateBinding(kSelectedPointBinding, BuildSelectedPoint);
            CreateTrigger<int>(kSelectPointTrigger, index => m_PathTool.SelectedPoint = index);
            CreateTrigger<string, float>(kEditPointTrigger, EditPoint);
            CreateBinding(kLoadedPathBinding, () => LoadedPathId);
            CreateTrigger(kNewPathTrigger, NewShot);
            CreateTrigger(kImportTrigger, () => ImportFromSequence());
            CreateTrigger<string>(kTransformTrigger, TransformPath);

            CreateBinding(kMetricsBinding, BuildMetrics);
            CreateBinding(kNumbersBinding, BuildNumbers);
            CreateTrigger<string, float>(kSetNumberTrigger, SetNumber);
            CreateBinding(kEditAllBinding, () => m_PathTool.EditAllSelected);
            CreateTrigger<bool>(kEditAllBinding, all => m_PathTool.EditAllSelected = all);
            CreateTrigger<string>(kClipboardTrigger, Clipboard);
            CreateTrigger<float, float>(kNudgeTrigger, Nudge);

            CreateBinding(kEditTargetBinding, () => (int)m_PathTool.EditTarget);
            CreateTrigger<int>(kEditTargetBinding,
                               target => m_PathTool.EditTarget = (PathTarget)Mathf.Clamp(target, 0, 1));
            CreateBinding(kPreviewBinding, () => m_Preview.Playing);
            CreateTrigger<bool>(kPreviewBinding, _ => m_Preview.Toggle());

            CreateBinding(kSnapModeBinding, () => (int)Mod.Instance.Settings.PathSnap);
            CreateTrigger<int>(kSnapModeBinding, SetSnapMode);
            CreateBinding(kClosedBinding, () => Mod.Instance.Settings.PathClosed);
            CreateTrigger<bool>(kClosedBinding, SetClosed);
            CreateBinding(kSnapValueBinding, GetSnapValue);
            CreateTrigger<int>(kSnapValueBinding, SetSnapValue);

            m_PathsBinding = CreateBinding(kPathsBinding, BuildList, false);
            CreateTrigger<string>(kSaveTrigger, SavePath);
            CreateTrigger<int>(kLoadTrigger, LoadPath);
            CreateTrigger<int>(kDeleteTrigger, DeletePath);
            CreateTrigger<int, string>(kRenameTrigger, RenamePath);
        }

        /// <summary>
        /// Gets or sets whether the path panel is showing. Opening it does not start drawing.
        /// </summary>
        /// <remarks>
        /// The panel is the entry point and the tool is one action inside it, which is how Network
        /// Tools structures the same problem. A hotkey that goes straight into a tool leaves no room
        /// for the shot settings, the library or Generate, and gives no way to look at what you have
        /// without also being able to edit it by accident.
        /// </remarks>
        public bool PanelOpen { get; private set; }

        /// <summary>Opens or closes the panel, stopping any drawing when it closes.</summary>
        /// <param name="open">Whether the panel should show.</param>
        public void SetPanelOpen(bool open) {
            PanelOpen = open;

            if (!open) {
                SetToolActive(false);
            }
        }

        /// <summary>Toggles the panel.</summary>
        public void TogglePanel() { SetPanelOpen(!PanelOpen); }

        /// <summary>
        /// Gets the library path currently being edited, or -1 when the drawn path is scratch work.
        /// </summary>
        /// <remarks>
        /// Stopping the tool deliberately does not clear the drawn path — Ctrl+Shift+P is meant to
        /// work after putting the tool away for a clean view, and clearing on stop would throw away
        /// unsaved work every time. So "start again" is an explicit action instead, and this is what
        /// tells Save whether it is updating a named path or creating one.
        /// </remarks>
        public int LoadedPathId { get; private set; } = -1;

        /// <summary>Throws the drawn path away and starts a new one.</summary>
        /// <summary>Starts a fresh shot of whichever type is selected.</summary>
        /// <remarks>
        /// "New" means the same thing for all three — throw away what is there and start again — but
        /// what is there differs. A path is its points; an orbit or a dolly is a subject plus a handful
        /// of numbers, so clearing one means unpinning the subject and putting those numbers back to
        /// their defaults. Leaving the old radius and sweep behind would make New a subject-clearer
        /// rather than a new shot.
        /// </remarks>
        public void NewShot() {
            Setting settings = Mod.Instance.Settings;

            switch (settings.Shot) {
                case ShotType.Orbit:
                    ClearSubject();

                    settings.OrbitRadius        = Setting.kDefaultOrbitRadius;
                    settings.OrbitEndRadius     = Setting.kDefaultOrbitEndRadius;
                    settings.OrbitHeight        = Setting.kDefaultOrbitHeight;
                    settings.OrbitEndHeight     = Setting.kDefaultOrbitEndHeight;
                    settings.OrbitSweep         = Setting.kDefaultOrbitSweep;
                    settings.OrbitSweepEase     = Setting.kDefaultOrbitSweepEase;
                    settings.OrbitDuration      = Setting.kDefaultOrbitDuration;
                    settings.OrbitDegreesPerKey = Setting.kDefaultOrbitDegreesPerKey;
                    settings.OrbitLookAtTarget  = Setting.kDefaultOrbitLookAtTarget;

                    settings.ApplyAndSave();
                    return;

                case ShotType.DollyZoom:
                    ClearSubject();

                    settings.DollyStartDistance = Setting.kDefaultDollyStartDistance;
                    settings.DollyEndDistance   = Setting.kDefaultDollyEndDistance;
                    settings.DollyDuration      = Setting.kDefaultDollyDuration;
                    settings.DollyKeys          = Setting.kDefaultDollyKeys;
                    settings.OrbitHeight        = Setting.kDefaultOrbitHeight;

                    settings.ApplyAndSave();
                    return;

                default:
                    m_PathTool.Path.Clear();
                    m_PathTool.History.Clear();
                    m_PathTool.SelectedPoint = -1;
                    LoadedPathId             = -1;

                    return;
            }
        }

        /// <summary>Unpins the subject, so the next click in the world places a new one.</summary>
        private void ClearSubject() {
            Subject.PinnedTarget     = null;
            Subject.PinnedStartAngle = null;
            Subject.PinnedEntity     = Entity.Null;
        }

        /// <summary>Length, duration and the speed those two imply.</summary>
        /// <remarks>
        /// Speed is the number worth surfacing. Two hundred metres in five seconds is 144km/h, which
        /// looks absurd on screen, and until now nothing told you that before you generated the shot
        /// and watched it.
        /// </remarks>
        private PathMetrics BuildMetrics() {
            CameraPath path = m_PathTool.Path;

            return new PathMetrics {
                points   = path.Nodes.Count,
                length   = path.IsValid ? path.MeasureLength() : 0f,
                duration = Mod.Instance.Settings.PathDuration,
                speed    = path.IsValid ? path.AverageSpeed * 3.6f : 0f,
                canUndo  = m_PathTool.History.CanUndo,
                canRedo  = m_PathTool.History.CanRedo,
                selected = m_PathTool.Selection.Count,
                railPoints = m_PathTool.RailPath.Nodes.Count,
                frustums = Mod.Instance.Settings.PathShowFrustums,
            };
        }

        private void SetSnapMode(int mode) {
            Mod.Instance.Settings.PathSnap = (PathSnapMode)Mathf.Clamp(mode, (int)PathSnapMode.Free,
                                                                      (int)PathSnapMode.Network);

            Mod.Instance.Settings.ApplyAndSave();
        }

        private void SetClosed(bool closed) {
            Mod.Instance.Settings.PathClosed = closed;
            Mod.Instance.Settings.ApplyAndSave();

            // The tool syncs this itself, but only while it is running — and the panel is reachable
            // with drawing stopped, where nothing else would re-run the tangents.
            m_PathTool.Path.Closed = closed;
            m_PathTool.Path.RefreshAutoTangents();
        }

        private int GetSnapValue() {
            Setting settings = Mod.Instance.Settings;

            switch (settings.PathSnap) {
                case PathSnapMode.Grid:  return settings.PathGridSize;
                case PathSnapMode.Angle: return settings.PathAngleStep;
                case PathSnapMode.Point: return settings.PathSnapRadius;
                default:                 return 0;
            }
        }

        private void SetSnapValue(int value) {
            Setting settings = Mod.Instance.Settings;

            switch (settings.PathSnap) {
                case PathSnapMode.Grid:
                    settings.PathGridSize = Mathf.Clamp(value, 1, 100);
                    break;

                case PathSnapMode.Angle:
                    settings.PathAngleStep = Mathf.Clamp(value, 1, 90);
                    break;

                case PathSnapMode.Point:
                    settings.PathSnapRadius = Mathf.Clamp(value, 1, 100);
                    break;

                default:
                    return;
            }

            settings.ApplyAndSave();
        }

        /// <summary>Moves, turns, resizes or flips the whole path as one object.</summary>
        /// <remarks>
        /// The point of these is the library: a saved path is stored in world coordinates, so without
        /// them loading one only ever puts the same shot back in the same place. With them a saved move
        /// becomes a shape you can reuse anywhere in the city.
        /// <para>
        /// Rotation reuses the snap angle step and scaling a fixed tenth, so repeated presses land on
        /// round numbers rather than drifting — twelve presses of rotate right is exactly half a turn.
        /// </para>
        /// </remarks>
        private void TransformPath(string operation) {
            CameraPath path     = m_PathTool.Path;
            Setting    settings = Mod.Instance.Settings;

            if (path.Nodes.Count == 0) {
                m_Log.Warn("Nothing to transform — draw or load a path first.");
                return;
            }

            // Recorded once here rather than in each case, so every whole-path operation is undoable
            // by construction — including any added later, which is the point of a single dispatch.
            // Undo and redo move through the history rather than adding to it; recording first would
            // make every undo push the state it is about to leave and the stack would never drain.
            if (operation == "undo" || operation == "redo") {
                bool moved = (operation == "undo") ? m_PathTool.History.Undo(path)
                                                   : m_PathTool.History.Redo(path);

                if (moved) {
                    m_PathTool.ClearSelection();
                    path.RefreshAutoTangents();
                }

                return;
            }

            // Recorded once here rather than in each case, so every whole-path operation is undoable
            // by construction — including any added later, which is the point of a single dispatch.
            m_PathTool.RecordUndo();

            switch (operation) {
                case "subdivide":   path.Subdivide(); break;
                case "respace":     path.Respace(); break;
                case "selectAll":   m_PathTool.SelectAll(); break;
                case "selectNone":  m_PathTool.ClearSelection(); break;

                case "simplify":
                    path.Simplify(settings.PathSimplifyTolerance);
                    break;

                case "rotateLeft":  path.Rotate(-settings.PathAngleStep); break;
                case "rotateRight": path.Rotate(settings.PathAngleStep); break;
                case "grow":        path.Scale(1f + kScaleStep); break;
                case "shrink":      path.Scale(1f / (1f + kScaleStep)); break;
                case "mirrorX":     path.Mirror(true); break;
                case "mirrorZ":     path.Mirror(false); break;
                case "raise":       m_PathTool.RaisePath(settings.PathHeightStep); break;
                case "lower":       m_PathTool.RaisePath(-settings.PathHeightStep); break;
                case "moveHere":    m_PathTool.MovePathToCursor(); break;
                case "railFromPath": RailFromPath(); break;
                case "frustums":
                    settings.PathShowFrustums = !settings.PathShowFrustums;
                    settings.ApplyAndSave();
                    break;

                default:
                    m_Log.Warn($"Unknown path transform \"{operation}\".");
                    return;
            }

            // Mirroring and rotating change what each point's neighbours are relative to it, and an
            // auto tangent is derived from exactly that.
            path.RefreshAutoTangents();
        }

        /// <summary>Rebuilds the drawn path from the cinematic timeline's own camera keyframes.</summary>
        /// <returns>False when the open sequence has no camera keys to read.</returns>
        /// <remarks>
        /// Everything else in the mod runs one way — a path becomes keyframes — which leaves a shot
        /// unreachable by the tool the moment it is hand-authored or its keys are dragged in the curve
        /// editor. This is the way back.
        /// <para>
        /// One node per position keyframe, taken from the PositionX curve's key times and read out of
        /// all three curves at each. That is exact rather than a fit: the keys are the poses the author
        /// actually placed, and re-solving the path re-derives the smooth travel between them. Pitch
        /// comes across as a per-point override; yaw deliberately does not, because the path solves its
        /// own aim and a baked yaw would fight whichever aim mode is chosen.
        /// </para>
        /// </remarks>
        public bool ImportFromSequence() {
            CinematicCameraSequence sequence = m_CinematicUISystem?.activeSequence;

            CinematicCameraSequence.CinematicCameraCurveModifier[] transforms = sequence?.transforms;

            if (transforms == null || transforms.Length < 5 || transforms[0].curve == null ||
                transforms[0].curve.length == 0) {
                m_Log.Warn("The cinematic timeline has no camera keyframes to import.");
                return false;
            }

            AnimationCurve x     = transforms[(int)CinematicCameraSequence.TransformCurveKey.PositionX].curve;
            AnimationCurve y     = transforms[(int)CinematicCameraSequence.TransformCurveKey.PositionY].curve;
            AnimationCurve z     = transforms[(int)CinematicCameraSequence.TransformCurveKey.PositionZ].curve;
            AnimationCurve pitch = transforms[(int)CinematicCameraSequence.TransformCurveKey.RotationX].curve;

            m_PathTool.Path.Clear();

            for (int i = 0; i < x.length; i++) {
                float time = x[i].time;

                var node = new PathNode(new Vector3(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time)));

                if (pitch != null && pitch.length > 0) {
                    node.Pitch = pitch.Evaluate(time);
                }

                m_PathTool.Path.Nodes.Add(node);
            }

            m_PathTool.Path.Closed   = false;
            m_PathTool.SelectedPoint = -1;

            m_PathTool.Path.RefreshAutoTangents();

            // An imported path is not the saved one that may still be loaded, so Save must ask for a
            // name rather than quietly overwriting whatever was open before.
            LoadedPathId = -1;

            Mod.Instance.Settings.PathClosed = false;
            Mod.Instance.Settings.PathDuration =
                Mathf.Clamp(Mathf.RoundToInt(x[x.length - 1].time - x[0].time), 5, 300);

            Mod.Instance.Settings.ApplyAndSave();

            return true;
        }

        // Snapshots the selected point for the inspector.
        private PathPointEntry BuildSelectedPoint() {
            int index = m_PathTool.SelectedPoint;

            if (index < 0) {
                return new PathPointEntry { index = -1 };
            }

            PathNode node = m_PathTool.Path.Nodes[index];

            return new PathPointEntry {
                index           = index,
                x               = node.Position.x,
                y               = node.Position.y,
                z               = node.Position.z,
                dwell           = node.Dwell,
                pitch           = node.Pitch ?? Mod.Instance.Settings.PathPitch,
                pitchOverridden = node.Pitch.HasValue,
                broken          = node.Broken,

                speed               = node.Speed,
                fov                 = node.Fov ?? 0f,
                fovOverridden       = node.Fov.HasValue,
                timeOfDay           = node.TimeOfDay ?? 0f,
                timeOfDayOverridden = node.TimeOfDay.HasValue,
                lookAtSet           = node.LookAt.HasValue,
            };
        }

        /// <summary>Edits one field of the selected point.</summary>
        /// <param name="field">Which field to write.</param>
        /// <param name="value">The new value.</param>
        /// <remarks>
        /// One trigger keyed by field name rather than a trigger per property. Each of these is a
        /// single float on one object, and a trigger each would be a lot of near-identical plumbing
        /// for something the inspector already knows the name of.
        /// </remarks>
        /// <summary>Edits one field of the selected point, or of every selected point.</summary>
        /// <remarks>
        /// X and Z are deliberately excluded from applying across a selection. Every other field holds
        /// a value that several points can sensibly share — the same speed, the same lens, the same
        /// hour — but a shared X stacks the whole selection into one column, which is never what
        /// "apply to all" was meant to do. Height is included: setting a run of points to one altitude
        /// is a real thing to want.
        /// </remarks>
        private void EditPoint(string field, float value) {
            if (m_PathTool.SelectedPoint < 0) {
                return;
            }

            m_PathTool.RecordUndo();

            bool spread = m_PathTool.EditAllSelected && field != "x" && field != "z";

            if (spread) {
                foreach (int index in m_PathTool.Selection) {
                    if (index < m_PathTool.Path.Nodes.Count) {
                        EditNode(m_PathTool.Path.Nodes[index], field, value);
                    }
                }
            } else {
                EditNode(m_PathTool.Path.Nodes[m_PathTool.SelectedPoint], field, value);
            }

            // Moving a point reshapes its neighbours' auto tangents too.
            m_PathTool.Path.RefreshAutoTangents();
        }

        private void EditNode(PathNode node, string field, float value) {
            switch (field) {
                case "x":
                    node.Position = new Vector3(value, node.Position.y, node.Position.z);
                    break;

                case "y":
                    node.Position = new Vector3(node.Position.x, value, node.Position.z);
                    break;

                case "z":
                    node.Position = new Vector3(node.Position.x, node.Position.y, value);
                    break;

                case "dwell":
                    node.Dwell = Mathf.Max(value, 0f);
                    break;

                case "pitch":
                    node.Pitch = Mathf.Clamp(value, -89f, 89f);
                    break;

                case "clearPitch":
                    node.Pitch = null;
                    break;

                case "speed":
                    node.Speed = Mathf.Clamp(value, 0.05f, 20f);
                    break;

                // Clamped to the lens property's own bounds rather than invented ones. Vanilla's are
                // 0.11 to 1466mm, wider than any hand-picked range would have guessed, and
                // ApplyPointCurve clamps to them again when it writes the curve.
                case "fov": {
                    PhotoModeProperty lens =
                        World.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>().FindProperty("focalLength");

                    float low  = lens?.min?.Invoke() ?? 1f;
                    float high = lens?.max?.Invoke() ?? 1000f;

                    node.Fov = Mathf.Clamp(value, low, high);
                    break;
                }

                case "clearFov":
                    node.Fov = null;
                    break;

                case "timeOfDay":
                    node.TimeOfDay = Mathf.Clamp(value, 0f, 24f);
                    break;

                case "clearTimeOfDay":
                    node.TimeOfDay = null;
                    break;

                // Aims this point at whatever the shot subject is pinned to, which is the same point
                // the orbit and dolly zoom circle. Picking an arbitrary spot would need a whole
                // click-in-the-world mode; reusing the pin means the subject is already placeable by
                // selecting a building.
                case "lookAtPinned": {
                    Vector3? pinned = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>().PinnedTarget;

                    if (!pinned.HasValue) {
                        m_Log.Warn("No subject is pinned; nothing for this point to aim at.");
                        return;
                    }

                    node.LookAt = pinned.Value;
                    break;
                }

                case "clearLookAt":
                    node.LookAt = null;
                    break;

                case "broken":
                    node.Broken = value > 0.5f;

                    if (!node.Broken) {
                        node.SetHandleOut(node.HandleOut);
                    }

                    break;

                default:
                    m_Log.Warn($"Unknown path point field \"{field}\".");
                    break;
            }
        }

        /// <summary>Chooses what a click in the tool acts on, and starts drawing if it is not already.</summary>
        /// <param name="mode">The <see cref="PathEditMode"/> to switch to.</param>
        /// <remarks>
        /// Picking a mode implies wanting to use it, so this activates the tool. Without that, the
        /// buttons would appear to do nothing until Draw path was also pressed.
        /// </remarks>
        private void SetEditMode(int mode) {
            if (!Enum.IsDefined(typeof(PathEditMode), mode)) {
                m_Log.Warn($"Ignoring unknown path edit mode {mode}.");
                return;
            }

            m_PathTool.EditMode = (PathEditMode)mode;
            SetToolActive(true);
        }

        /// <summary>Starts or stops drawing, opening the panel first if it is closed.</summary>
        /// <param name="active">Whether the path tool should be the active tool.</param>
        public void SetToolActive(bool active) {
            if (active) {
                PanelOpen = true;
                m_PathTool.RequestEnable();
                return;
            }

            if (m_ToolSystem.activeTool == m_PathTool) {
                m_PathTool.RequestDisable();
            }
        }

        private PathLibraryEntry[] BuildList() {
            var entries = new List<PathLibraryEntry>();

            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    EPM_SavedPath saved = EntityManager.GetComponentData<EPM_SavedPath>(entity);

                    entries.Add(new PathLibraryEntry {
                        index  = saved.m_Id,
                        name   = saved.m_Name.ToString(),
                        points = EntityManager.GetBuffer<EPM_PathNodeData>(entity, true).Length,
                    });
                }
            }

            entries.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return entries.ToArray();
        }

        private void SavePath(string name) {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0) {
                m_Log.Warn("A saved path needs a name.");
                return;
            }

            if (m_PathTool.TravelPath.Nodes.Count < 2) {
                m_Log.Warn("Nothing to save — draw a path with at least two points first.");
                return;
            }

            Entity entity = FindByName(trimmed);

            if (entity == Entity.Null) {
                entity = EntityManager.CreateEntity();
                EntityManager.AddBuffer<EPM_PathNodeData>(entity);
                EntityManager.AddComponentData(entity, new EPM_SavedPath {
                    m_Id   = NextId(),
                    m_Name = new FixedString128Bytes(trimmed),
                });
            }

            // Rewritten every save, not only on create, so re-saving an existing path picks up a
            // change to whether it is closed.
            EPM_SavedPath header = EntityManager.GetComponentData<EPM_SavedPath>(entity);

            header.m_Closed = Mod.Instance.Settings.PathClosed;
            EntityManager.SetComponentData(entity, header);

            DynamicBuffer<EPM_PathNodeData> buffer = EntityManager.GetBuffer<EPM_PathNodeData>(entity);
            buffer.Clear();

            foreach (PathNode node in m_PathTool.TravelPath.Nodes) {
                buffer.Add(new EPM_PathNodeData {
                    m_Position   = node.Position,
                    m_TangentOut = node.TangentOut,
                    m_TangentIn  = node.TangentIn,
                    m_Auto       = node.Auto,
                    m_Broken     = node.Broken,
                    m_Dwell      = node.Dwell,
                    m_Pitch      = node.Pitch ?? 0f,
                    m_HasPitch   = node.Pitch.HasValue,

                    m_Speed        = node.Speed,
                    m_LookAt       = node.LookAt ?? default,
                    m_HasLookAt    = node.LookAt.HasValue,
                    m_Fov          = node.Fov ?? 0f,
                    m_HasFov       = node.Fov.HasValue,
                    m_TimeOfDay    = node.TimeOfDay ?? 0f,
                    m_HasTimeOfDay = node.TimeOfDay.HasValue,
                });
            }

            // The aim rail travels with the path it belongs to. A saved two-rail shot that came back
            // with only the camera move would aim at whatever rail happened to be drawn at the time.
            DynamicBuffer<EPM_RailNodeData> rail =
                EntityManager.HasBuffer<EPM_RailNodeData>(entity)
                    ? EntityManager.GetBuffer<EPM_RailNodeData>(entity)
                    : EntityManager.AddBuffer<EPM_RailNodeData>(entity);

            rail.Clear();

            foreach (PathNode node in m_PathTool.RailPath.Nodes) {
                rail.Add(new EPM_RailNodeData {
                    m_Position   = node.Position,
                    m_TangentOut = node.TangentOut,
                    m_TangentIn  = node.TangentIn,
                    m_Auto       = node.Auto,
                    m_Broken     = node.Broken,
                });
            }

            // Saving adopts the path, so a further Save updates it rather than making another copy.
            LoadedPathId = EntityManager.GetComponentData<EPM_SavedPath>(entity).m_Id;

            m_PathsBinding.Update();

        }

        private void LoadPath(int id) {
            Entity entity = FindById(id);

            if (entity == Entity.Null) {
                m_Log.Warn($"No saved path with id {id}.");
                return;
            }

            DynamicBuffer<EPM_PathNodeData> buffer = EntityManager.GetBuffer<EPM_PathNodeData>(entity, true);

            m_PathTool.TravelPath.Clear();
            m_PathTool.RailPath.Clear();

            for (int i = 0; i < buffer.Length; i++) {
                EPM_PathNodeData data = buffer[i];

                m_PathTool.TravelPath.Nodes.Add(new PathNode(data.m_Position) {
                    TangentOut = data.m_TangentOut,
                    TangentIn  = data.m_TangentIn,
                    Auto       = data.m_Auto,
                    Broken     = data.m_Broken,
                    Dwell      = data.m_Dwell,
                    Pitch      = data.m_HasPitch ? data.m_Pitch : (float?)null,

                    Speed     = data.m_Speed,
                    LookAt    = data.m_HasLookAt ? (Vector3)data.m_LookAt : (Vector3?)null,
                    Fov       = data.m_HasFov ? data.m_Fov : (float?)null,
                    TimeOfDay = data.m_HasTimeOfDay ? data.m_TimeOfDay : (float?)null,
                });
            }

            // Set before the tangents are refreshed, which reads it to decide whether the end nodes
            // have neighbours on both sides.
            bool closed = EntityManager.GetComponentData<EPM_SavedPath>(entity).m_Closed;

            Mod.Instance.Settings.PathClosed = closed;
            Mod.Instance.Settings.ApplyAndSave();
            m_PathTool.Path.Closed = closed;

            if (EntityManager.HasBuffer<EPM_RailNodeData>(entity)) {
                DynamicBuffer<EPM_RailNodeData> rail =
                    EntityManager.GetBuffer<EPM_RailNodeData>(entity, true);

                for (int i = 0; i < rail.Length; i++) {
                    EPM_RailNodeData data = rail[i];

                    m_PathTool.RailPath.Nodes.Add(new PathNode(data.m_Position) {
                        TangentOut = data.m_TangentOut,
                        TangentIn  = data.m_TangentIn,
                        Auto       = data.m_Auto,
                        Broken     = data.m_Broken,
                    });
                }

                m_PathTool.RailPath.RefreshAutoTangents();
            }

            m_PathTool.TravelPath.RefreshAutoTangents();

            // A loaded path is a different path, not an edit to the current one, so undoing across the
            // boundary would splice one path's nodes into the other's identity.
            m_PathTool.History.Clear();

            LoadedPathId          = id;
            m_PathTool.SelectedPoint = -1;

        }

        private void DeletePath(int id) {
            Entity entity = FindById(id);

            if (entity != Entity.Null) {
                EntityManager.DestroyEntity(entity);

                // The drawn path stays; it just stops being attached to a library entry that no
                // longer exists, so the next Save creates a new one instead of writing to a hole.
                if (LoadedPathId == id) {
                    LoadedPathId = -1;
                }

                m_PathsBinding.Update();
            }
        }

        private void RenamePath(int id, string name) {
            string trimmed = (name ?? string.Empty).Trim();
            Entity entity  = FindById(id);

            if (entity == Entity.Null || trimmed.Length == 0) {
                return;
            }

            EPM_SavedPath saved = EntityManager.GetComponentData<EPM_SavedPath>(entity);
            saved.m_Name        = new FixedString128Bytes(trimmed);

            EntityManager.SetComponentData(entity, saved);
            m_PathsBinding.Update();
        }

        private Entity FindById(int id) {
            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    if (EntityManager.GetComponentData<EPM_SavedPath>(entity).m_Id == id) {
                        return entity;
                    }
                }
            }

            return Entity.Null;
        }

        private Entity FindByName(string name) {
            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    EPM_SavedPath saved = EntityManager.GetComponentData<EPM_SavedPath>(entity);

                    if (string.Equals(saved.m_Name.ToString(), name, StringComparison.OrdinalIgnoreCase)) {
                        return entity;
                    }
                }
            }

            return Entity.Null;
        }

        private int NextId() {
            int highest = 0;

            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    highest = Math.Max(highest, EntityManager.GetComponentData<EPM_SavedPath>(entity).m_Id);
                }
            }

            return highest + 1;
        }
    }
}
