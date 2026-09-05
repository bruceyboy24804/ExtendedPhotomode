namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using Colossal.Mathematics;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Systems;

    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;

    using System.Collections.Generic;

    using ModsCommon.Utils;

    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    using UnityEngine;
    using UnityEngine.InputSystem;

    #endregion

    /// <summary>Lets a camera path be drawn in the world by clicking points on the ground.</summary>
    /// <remarks>
    /// A gameplay tool, not a photo mode one: photo mode blocks the tool input barrier and forces the
    /// default tool, so a <see cref="ToolBaseSystem"/> cannot take clicks while it is open.
    ///
    /// The apply action is substituted rather than reusing vanilla's, which is declared
    /// <c>ModifierOptions.Allow</c> and so is silently suppressed whenever an unlisted modifier is
    /// held. Any interaction where a modifier changes what a click does needs its own action with
    /// <c>ModifierOptions.Ignore</c>.
    /// </remarks>
    public partial class EPM_PathToolSystem : ToolBaseSystem {
        // The width of the drawn path band, in metres — an overlay curve, so it is a flat ribbon on
        // the game's own renderer rather than geometry of ours.
        private const float kPathWidth = 3f;

        private const float kPointDiameter = 5f;

        // The selected point is drawn larger than the rest and ringed, so it can be picked out of a
        // line of thirty from across the city rather than only by hovering until the inspector jumps.
        private const float kSelectedDiameter = 6.5f;

        private const float kSelectedRingDiameter = 11f;

        private const float kLookAtDiameter = 8f;

        private const float kFrustumLength = 40f;

        private const float kFrustumWidth = 0.5f;

        // The lens a point with no override of its own is drawn at — near enough the game's normal
        // field of view that the cone reads as "nothing unusual here".
        private const float kDefaultFocalLength = 50f;

        // Vanilla's default sensor height in millimetres, which is what its own focal-length-to-FOV
        // conversion is fed. Using anything else makes the drawn cone disagree with the real lens.
        private const float kSensorHeight = 24f;

        // Enough ties to read the pairing, few enough not to become a hatched wall between the curves.
        private const int kRailTies = 12;

        private const float kTieWidth = 0.35f;

        private const float kCursorDiameter = 4f;

        private const float kGroundLift = 0.5f;

        private const float kShadowWidth = 1.5f;

        private const float kStemWidth = 0.4f;


        // A perpendicular distance from the mouse ray in world units, no longer a distance in XZ from
        // the terrain hit — so it wants to be about the width of the drawn tube, not a broad catchment.
        private const float kInsertRadius = 6f;

        private const float kRenderSpacing = 4f;

        private const float kHandlePickRadius = 4f;

        private const float kHandleGripDiameter = 2.5f;

        private const float kPointPickRadius = 5f;

        // Larger than a path point's: a shot has three handles rather than thirty, so there is nothing
        // nearby to mis-grab and an easy target matters more than precision.
        private const float kShotHandleRadius = 8f;

        private const float kHandleDiameter = 3.5f;

        private const float kHandleRingThickness = 0.25f;

        private const float kHandleArmWidth = 0.6f;

        private const float kDashLength = 2f;

        private const float kGapLength = 1.5f;

        private const float kHeightStepsPerSecond = 12f;

        // Enough that the highlight reads as a sleeve around the path rather than as a replacement for
        // it; any more and it stops looking like the same curve.
        private const float kHighlightSwell = 1.6f;

        private static readonly Color kPathColor   = new Color(0.35f, 0.7f, 1f, 1f);
        private static readonly Color kPointColor  = new Color(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color kCursorColor = new Color(0.4f, 1f, 0.5f, 0.9f);
        private static readonly Color kShadowColor = new Color(0.1f, 0.35f, 0.55f, 0.5f);
        private static readonly Color kStemColor   = new Color(1f, 0.85f, 0.2f, 0.25f);
        private static readonly Color kHoverColor  = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color kHandleColor = new Color(0.6f, 1f, 0.8f, 0.85f);
        private static readonly Color kBrokenColor = new Color(1f, 0.5f, 0.35f, 0.9f);

        // Distinct from the yellow of an ordinary point and from the white of a hovered one, so all
        // three states are told apart at a glance.
        private static readonly Color kSelectedColor = new Color(1f, 0.45f, 0.75f, 0.95f);

        private static readonly Color kLookAtColor = new Color(1f, 0.75f, 0.35f, 0.8f);

        private static readonly Color kFrustumColor = new Color(0.55f, 0.85f, 1f, 0.55f);

        // Red, and only red, is reserved for "this stretch flies through something".
        private static readonly Color kBlockedColor = new Color(1f, 0.3f, 0.3f, 0.95f);

        // The rail reads as warm against the camera path's blue, which is the convention every rig
        // visualiser uses: the thing being looked at is not the thing doing the looking.
        private static readonly Color kRailColor     = new Color(1f, 0.6f, 0.3f, 0.9f);
        private static readonly Color kInactiveColor = new Color(0.35f, 0.7f, 1f, 0.35f);
        private static readonly Color kTieColor      = new Color(1f, 1f, 1f, 0.22f);

        // The same green as the free cursor marker, deliberately: both mean "a new point lands here",
        // and giving the snapped-to-curve case its own colour would suggest it does something else.
        private static readonly Color kInsertColor = kCursorColor;

        private OverlayRenderSystem m_OverlayRenderSystem;
        private TerrainSystem       m_TerrainSystem;
        private ToolOutputBarrier   m_ToolOutputBarrier;
        private DefaultToolSystem   m_DefaultToolSystem;
        private EPM_PathPreviewSystem m_Preview;
        private EPM_ShotSubjectSystem m_Subject;
        private PrefixedLogger      m_Log;

        private bool    m_HasCursorPosition;
        private Vector3 m_CursorPosition;
        private Entity  m_CursorEntity;
        private int     m_HoveredPoint = -1;
        private int     m_DraggedPoint = -1;
        private int     m_HoveredHandle = -1;
        private int     m_SelectedPoint = -1;

        // Points selected alongside the primary one. The primary stays a separate field because the
        // inspector edits exactly one point, while transforms act on the whole set.
        private readonly HashSet<int> m_Selection = new HashSet<int>();

        private PathTarget m_EditTarget = PathTarget.Camera;

        private readonly PathHistory m_CameraHistory = new PathHistory();
        private readonly PathHistory m_RailHistory   = new PathHistory();
        private bool    m_DraggingHandle;
        private bool    m_DraggingOutHandle;

        // The drawn curve, resampled once a frame and shared by picking and rendering. Hit-testing
        // against the samples rather than against the straight line between nodes is what makes the
        // insert preview land on the curve you can actually see.
        private List<Vector3> m_Samples         = new List<Vector3>();
        private List<float>   m_SampleGlobals   = new List<float>();
        private List<Vector3> m_SampleRotations = new List<Vector3>();
        private readonly List<int> m_NodeSamples = new List<int>();

        private readonly List<bool>  m_Obstructed   = new List<bool>();
        private readonly List<float> m_ObstacleTops = new List<float>();
        private int           m_InsertSegment = -1;
        private int           m_InsertIndex   = -1;
        private Vector3       m_InsertPoint;
        private Vector3       m_DragOrigin;

        public override string toolID => "EPM_PathTool";

        /// <summary>The path the camera travels.</summary>
        /// <remarks>Not named CameraPath: a property sharing its own type's name shadows the type
        /// inside this class, so a later <c>new CameraPath()</c> here would fail to resolve.</remarks>
        public CameraPath TravelPath { get; } = new CameraPath();

        /// <summary>The path the camera aims at, when the aim mode is Rail.</summary>
        public CameraPath RailPath { get; } = new CameraPath();

        /// <summary>Which of the two the tool is editing.</summary>
        /// <remarks>
        /// Switching clears the selection rather than carrying it over: the indices mean different
        /// points on the other path, so keeping them would leave a selection highlighting whatever
        /// happened to share those numbers.
        /// </remarks>
        public PathTarget EditTarget {
            get => m_EditTarget;

            set {
                if (m_EditTarget == value) {
                    return;
                }

                m_EditTarget = value;
                ClearSelection();
                m_DraggedPoint = -1;
            }
        }

        /// <summary>Whichever path is being edited. Everything in the editor works through this.</summary>
        /// <remarks>
        /// Redirecting one property is what lets the aim rail inherit the entire editor — snapping,
        /// per-point properties, transforms, undo, the frustums — with no second implementation of any
        /// of it. The alternative, a parallel set of rail-specific methods, would have doubled the tool
        /// and guaranteed the two drifted.
        /// </remarks>
        public CameraPath Path => (m_EditTarget == PathTarget.Rail) ? RailPath : TravelPath;

        /// <summary>Undo and redo for the path being edited. Each path keeps its own history.</summary>
        public PathHistory History =>
            (m_EditTarget == PathTarget.Rail) ? m_RailHistory : m_CameraHistory;

        /// <summary>Every selected point, including the primary one.</summary>
        public IReadOnlyCollection<int> Selection => m_Selection;

        /// <summary>Whether editing a point's property applies to the whole selection.</summary>
        /// <remarks>
        /// Off by default. Multi-select exists mainly to drag a group, and having a slider silently
        /// rewrite eight points because they happened to still be selected is the more damaging of the
        /// two mistakes — so this is opt-in, and visible in the panel while it is on.
        /// </remarks>
        public bool EditAllSelected { get; set; }

        /// <summary>Records the path before an edit, so the edit can be undone.</summary>
        public void RecordUndo() { History.Record(Path); }

        /// <summary>Gets or sets what a click acts on, chosen from the path panel.</summary>
        public PathEditMode EditMode { get; set; } = PathEditMode.Points;

        /// <summary>Gets or sets the point the panel's inspector is editing, or -1 for none.</summary>
        /// <remarks>
        /// Kept separate from the hovered point, which follows the cursor and would make the inspector
        /// unusable — the value you were about to change would swap the moment you moved towards it.
        /// </remarks>
        public int SelectedPoint {
            get => (m_SelectedPoint < Path.Nodes.Count) ? m_SelectedPoint : -1;

            set {
                m_SelectedPoint = (value >= 0 && value < Path.Nodes.Count) ? value : -1;

                // Plain selection replaces the set. Extending it is a separate call, so that stepping
                // through points with the panel arrows cannot silently accumulate a selection you did
                // not make and then transform all of it.
                m_Selection.Clear();

                if (m_SelectedPoint >= 0) {
                    m_Selection.Add(m_SelectedPoint);
                }
            }
        }

        /// <summary>Adds or removes a point from the selection, leaving the rest alone.</summary>
        public void ToggleSelected(int index) {
            if (index < 0 || index >= Path.Nodes.Count) {
                return;
            }

            if (!m_Selection.Remove(index)) {
                m_Selection.Add(index);
                m_SelectedPoint = index;
                return;
            }

            // Removing the primary hands the role to whatever is left, so the inspector never points
            // at something that is no longer selected.
            if (m_SelectedPoint != index) {
                return;
            }

            m_SelectedPoint = -1;

            foreach (int remaining in m_Selection) {
                m_SelectedPoint = remaining;
                break;
            }
        }

        /// <summary>Selects every point.</summary>
        public void SelectAll() {
            m_Selection.Clear();

            for (int i = 0; i < Path.Nodes.Count; i++) {
                m_Selection.Add(i);
            }

            m_SelectedPoint = (Path.Nodes.Count > 0) ? 0 : -1;
        }

        /// <summary>Clears the selection entirely.</summary>
        public void ClearSelection() {
            m_Selection.Clear();
            m_SelectedPoint = -1;
        }

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                 = new PrefixedLogger(nameof(EPM_PathToolSystem));
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_TerrainSystem       = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_ToolOutputBarrier   = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_DefaultToolSystem   = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_Preview             = World.GetOrCreateSystemManaged<EPM_PathPreviewSystem>();
            m_Subject             = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();

            m_ToolSystem.tools.Remove(this);
            m_ToolSystem.tools.Insert(0, this);

            // CameraPath is deliberately free of ECS so it can be solved anywhere, which leaves reading
            // terrain the one thing it cannot do for itself. GetHeightData only hands back a view over
            // memory the terrain system already owns, so calling it per sample is cheap.
            System.Func<Vector3, float> ground = position => {
                TerrainHeightData heights = m_TerrainSystem.GetHeightData();
                return TerrainUtils.SampleHeight(ref heights, position);
            };

            TravelPath.GroundHeight = ground;
            RailPath.GroundHeight   = ground;

            // Only the travel path avoids obstacles. The rail is a line of sight, not somewhere the
            // camera goes, so lifting it over a building would aim the shot at the sky.
            TravelPath.ObstacleHeights =
                (points, heights) => PathObstacles.Measure(World, points, heights);
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();
            m_HasCursorPosition = false;

            applyActionOverride = Mod.PathApplyAction;

            applyAction.shouldBeEnabled          = true;
            cancelAction.shouldBeEnabled         = true;
            secondaryApplyAction.shouldBeEnabled = true;

            m_Log.Debug($"Path tool active with {Path.Nodes.Count} points.");
        }

        protected override void OnStopRunning() {
            m_HasCursorPosition = false;
            m_DraggedPoint      = -1;
            m_HoveredHandle     = -1;
            m_DraggingHandle    = false;
            base.OnStopRunning();
        }

        public override void InitializeRaycast() {
            base.InitializeRaycast();

            // Roads are only raycast when something is going to snap to them. Leaving nets in the mask
            // the rest of the time would change what the cursor reports over every bridge and flyover,
            // for a feature that is switched off.
            bool nets = Mod.Instance.Settings.PathSnap == PathSnapMode.Network ||
                        EditMode == PathEditMode.Network;

            m_ToolRaycastSystem.typeMask      = nets ? TypeMask.Terrain | TypeMask.Net : TypeMask.Terrain;
            m_ToolRaycastSystem.netLayerMask  = nets ? Layer.Road | Layer.PublicTransportRoad
                                                     : Layer.None;
            m_ToolRaycastSystem.collisionMask   = CollisionMask.OnGround | CollisionMask.Overground;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
        }

        public override PrefabBase GetPrefab() { return null; }

        public override bool TrySetPrefab(PrefabBase prefab) { return false; }

        public void RequestEnable() { m_ToolSystem.activeTool = this; }

        public void RequestDisable() { m_ToolSystem.activeTool = m_DefaultToolSystem; }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            m_HasCursorPosition = GetRaycastResult(out Entity hovered, out RaycastHit hit);

            if (m_HasCursorPosition) {
                m_CursorPosition = hit.m_HitPosition;
                m_CursorEntity   = hovered;
            }

            // The tool serves every shot type. A drawn path keeps its own pipeline; orbit and dolly
            // run through the editor abstraction, which is why the branch is here rather than threaded
            // through each step below. Nothing after this point applies to a shot that is not a path.
            if (EditingShot) {
                UpdateShotEditing();
                return inputDeps;
            }

            SyncPathFromSettings();
            RefreshSamples();

            m_HoveredHandle = FindHoveredHandle();
            m_HoveredPoint  = FindHoveredPoint();

            // Solved once here rather than on demand. Drawing the highlight, the hint text and the
            // click itself all need the same answer, and re-running the search for each would let them
            // disagree within a frame.
            m_InsertIndex = FindInsertIndex();
            HandleInput();
            Draw();

            return inputDeps;
        }

        /// <summary>Keeps the drawn path in step with the settings that change its shape.</summary>
        /// <remarks>
        /// Only the three that alter geometry, so what is drawn is what will be generated. Closing the
        /// path has to re-run the tangents: an end node's auto tangent is half length, and once the ends
        /// have neighbours on both sides they need a full one or the join shows a kink.
        /// </remarks>
        private void SyncPathFromSettings() {
            Setting settings = Mod.Instance.Settings;

            if (Path.Closed != settings.PathClosed) {
                Path.Closed = settings.PathClosed;
                Path.RefreshAutoTangents();
            }

            Path.TerrainMode      = settings.PathTerrain;
            Path.TerrainClearance = settings.PathClearance;

            TravelPath.ClearanceMode     = settings.PathClearanceMode;
            TravelPath.ObstacleClearance = settings.PathObstacleClearance;

            // The aim settings go on the TRAVEL path specifically, not the one being edited: aim is a
            // property of the camera's move, and the rail is only ever a shape that move looks at.
            // Set here rather than by calling the generator's own preparation, which warns when an aim
            // mode has nothing to aim at — once a frame, that would be a log full of the same line.
            TravelPath.Pitch        = settings.PathPitch;
            TravelPath.LookAhead    = settings.PathLookAhead;
            TravelPath.MetresPerKey = settings.PathMetresPerKey;
            TravelPath.Duration     = settings.PathDuration;
            TravelPath.Ease         = settings.PathEase;
            TravelPath.Rail         = RailPath;

            Vector3?     pinned = m_Subject.PinnedTarget;
            PathLookMode look   = settings.PathLook;

            // Falls back silently for the gizmos, the same way generating falls back loudly. Drawing
            // frustums for an aim mode that cannot resolve would show a shot that will not happen.
            if ((look == PathLookMode.Target && !pinned.HasValue) ||
                (look == PathLookMode.Rail && !RailPath.IsValid)) {
                look = PathLookMode.Forward;
            }

            TravelPath.LookMode = look;

            if (pinned.HasValue) {
                TravelPath.Target = pinned.Value;
            }
        }

        /// <remarks>Returns a locale key, not text. See <see cref="PathHints"/>.</remarks>
        public string DescribeApply() {
            if (EditingShot) {
                return DescribeShotApply();
            }

            if (EditMode == PathEditMode.Network) {
                return PathHints.TraceRoad;
            }

            if (EditMode == PathEditMode.LookAt) {
                return SelectedPoint >= 0 ? PathHints.SetLookAt : PathHints.SelectForLookAt;
            }

            if (m_HoveredHandle >= 0) {
                return PathHints.ShapeCurve;
            }

            if (EditMode == PathEditMode.Curves) {
                return PathHints.PickHandle;
            }

            if (m_HoveredPoint >= 0) {
                return PathHints.MovePoint;
            }

            return m_InsertIndex >= 0 ? PathHints.InsertPoint : PathHints.AddPoint;
        }

        public string DescribeBreakTangent() {
            int index = m_HoveredHandle >= 0 ? m_HoveredHandle : m_HoveredPoint;

            if (index < 0) {
                return null;
            }

            return Path.Nodes[index].Broken ? PathHints.SmoothCorner : PathHints.SharpCorner;
        }

        /// <summary>What secondary apply would do right now, or null when it would do nothing.</summary>
        /// <remarks>Returns a locale key, not text. See <see cref="PathHints"/>.</remarks>
        public string DescribeDelete() {
            return m_HoveredPoint >= 0 ? PathHints.DeletePoint : null;
        }

        /// <summary>What Escape would do right now.</summary>
        /// <remarks>Returns a locale key, not text. See <see cref="PathHints"/>.</remarks>
        public string DescribeCancel() { return PathHints.StopDrawing; }

        private void HandleInput() {
            // The preview owns the camera and Escape while it flies. Letting the tool keep taking
            // clicks would mean the click that stops a preview also drops a point in the world.
            if (m_Preview != null && m_Preview.Playing) {
                return;
            }

            HandleUndoRedo();
            HandleHeightInput();
            HandleReverse();
            HandleBreakTangent();

            if (HandleDrag()) {
                return;
            }

            if (m_HasCursorPosition && applyAction.WasPressedThisFrame()) {
                HandleApply();
                return;
            }

            if (secondaryApplyAction.WasPressedThisFrame()) {
                HandleDelete();
                return;
            }

            if (cancelAction.WasPressedThisFrame()) {
                HandleCancel();
            }
        }

        /// <summary>Ctrl+Z and Ctrl+Y, read straight off the keyboard.</summary>
        /// <remarks>
        /// Not bound actions. A binding for a modifier combination collides with vanilla shortcuts,
        /// and these only need to work while this tool is the active one — which is exactly when this
        /// method runs.
        /// </remarks>
        private void HandleUndoRedo() {
            if (Keyboard.current == null || !PathModifier.Ctrl) {
                return;
            }

            bool undo = Keyboard.current.zKey.wasPressedThisFrame;
            bool redo = Keyboard.current.yKey.wasPressedThisFrame;

            if (!undo && !redo) {
                return;
            }

            if (undo ? History.Undo(Path) : History.Redo(Path)) {
                // The restored path may be shorter than the one that was on screen, so anything
                // pointing into it by index has to be dropped rather than clamped.
                ClearSelection();
                m_DraggedPoint = -1;
                Path.RefreshAutoTangents();

                m_Log.Debug(undo ? "Undid a path edit." : "Redid a path edit.");
            }
        }

        private void HandleReverse() {
            if (Mod.PathReverseAction == null || !Mod.PathReverseAction.WasPressedThisFrame()) {
                return;
            }

            RecordUndo();
            Path.Nodes.Reverse();

            foreach (PathNode node in Path.Nodes) {
                Vector3 tangentOut = node.TangentOut;

                node.TangentOut = node.TangentIn;
                node.TangentIn  = tangentOut;
            }

            m_Log.Debug($"Reversed path direction, {Path.Nodes.Count} points.");
        }

        private bool HandleDrag() {
            if (m_DraggedPoint < 0) {
                if (!applyAction.WasPressedThisFrame()) {
                    return false;
                }

                if (m_HoveredHandle >= 0) {
                    // Recorded at the grab, not per frame: a drag is one edit, and a snapshot every
                    // frame would fill the whole history with a single mouse movement.
                    RecordUndo();

                    m_DraggedPoint  = m_HoveredHandle;
                    m_DraggingHandle = true;
                    return true;
                }

                if (m_HoveredPoint < 0) {
                    return false;
                }

                RecordUndo();

                m_DraggedPoint   = m_HoveredPoint;
                m_DraggingHandle = false;

                // Shift extends the selection instead of replacing it, and a click on something
                // already selected keeps the set so the whole group can be dragged together.
                if (PathModifier.Shift) {
                    ToggleSelected(m_HoveredPoint);
                } else if (!m_Selection.Contains(m_HoveredPoint)) {
                    SelectedPoint = m_HoveredPoint;
                } else {
                    m_SelectedPoint = m_HoveredPoint;
                }

                m_DragOrigin = Path.Nodes[m_HoveredPoint].Position;
                return true;
            }

            if (!applyAction.IsPressed()) {
                m_DraggedPoint   = -1;
                m_DraggingHandle = false;
                return true;
            }

            if (m_DraggedPoint >= Path.Nodes.Count) {
                return true;
            }

            PathNode node = Path.Nodes[m_DraggedPoint];

            if (!PathPicking.TryHitPlane(node.Position.y, out float3 drag)) {
                return true;
            }

            if (m_DraggingHandle) {
                var handle = new Vector3(drag.x, node.Position.y, drag.z);

                if (m_DraggingOutHandle) {
                    node.SetHandleOut(handle);
                } else {
                    node.SetHandleIn(handle);
                }

                return true;
            }

            Vector3 moved = ConstrainDrag(new Vector3(drag.x, node.Position.y, drag.z), m_DragOrigin);
            Vector3 delta = moved - node.Position;

            // Everything selected moves by the same delta, so a group keeps its shape. Dragging the
            // primary and having the rest stay put is the thing that makes multi-select pointless.
            foreach (int index in m_Selection) {
                if (index >= Path.Nodes.Count) {
                    continue;
                }

                Path.Nodes[index].Position += delta;
            }

            if (!m_Selection.Contains(m_DraggedPoint)) {
                node.Position = moved;
            }

            Path.RefreshAutoTangents();
            return true;
        }

        /// <summary>Locks a drag to one world axis while a modifier is held.</summary>
        /// <remarks>
        /// Measured from where the drag started rather than from the point's current position, so the
        /// constraint holds for the whole gesture — comparing against the live position would let the
        /// locked axis creep a little every frame and end up unlocked.
        /// </remarks>
        private Vector3 ConstrainDrag(Vector3 position, Vector3 origin) {
            if (PathModifier.Ctrl) {
                return new Vector3(position.x, position.y, origin.z);
            }

            if (PathModifier.Alt) {
                return new Vector3(origin.x, position.y, position.z);
            }

            return position;
        }

        private int FindHoveredHandle() {
            if (EditMode != PathEditMode.Curves ||
                !PathPicking.TryGetMouseRay(out float3 origin, out float3 direction)) {
                return -1;
            }

            int   best    = -1;
            float radius  = kHandlePickRadius;
            float nearest = float.MaxValue;

            for (int i = 0; i < Path.Nodes.Count; i++) {
                PathNode node = Path.Nodes[i];

                if (node.TangentOut.sqrMagnitude < 0.01f && node.TangentIn.sqrMagnitude < 0.01f) {
                    continue;
                }

                if (PathPicking.TryHitSphere(origin, direction, node.HandleOut, radius, out float outT) &&
                    outT < nearest) {
                    nearest             = outT;
                    best                = i;
                    m_DraggingOutHandle = true;
                }

                if (PathPicking.TryHitSphere(origin, direction, node.HandleIn, radius, out float inT) &&
                    inT < nearest) {
                    nearest             = inT;
                    best                = i;
                    m_DraggingOutHandle = false;
                }
            }

            return best;
        }

        private void HandleBreakTangent() {
            if (Mod.PathBreakTangentAction == null || !Mod.PathBreakTangentAction.WasPressedThisFrame()) {
                return;
            }

            int index = m_HoveredHandle >= 0 ? m_HoveredHandle : m_HoveredPoint;

            if (index < 0) {
                return;
            }

            RecordUndo();

            PathNode node = Path.Nodes[index];
            node.Broken   = !node.Broken;

            if (!node.Broken) {
                node.SetHandleOut(node.HandleOut);
            }

            m_Log.Debug($"Path point {index + 1} tangent is now {(node.Broken ? "broken" : "smooth")}.");
        }

        private void HandleApply() {
            if (EditMode == PathEditMode.Network) {
                HandleTraceRoad();
                return;
            }

            if (EditMode == PathEditMode.LookAt) {
                HandleSetLookAt();
                return;
            }

            TerrainHeightData heights = m_TerrainSystem.GetHeightData();
            int               insert  = m_InsertIndex;

            if (insert >= 0) {
                // Exactly the point the preview marker sits on, which is a point already on the curve —
                // so inserting splits the path without moving it. Taking the cursor's XZ and the mean of
                // the two neighbouring heights, as this used to, tugged the curve towards the ground and
                // sideways off its own line every time a point was added to an existing path.
                RecordUndo();
                Path.Nodes.Insert(insert, new PathNode(m_InsertPoint));

                // A new node arrives with zero tangents and Auto set, so without this the curve reads
                // as a hard corner at the point you just placed until something else happens to refresh
                // it — dragging a point, or nudging a height. Adding and deleting were the two edits
                // that never did.
                Path.RefreshAutoTangents();

                SelectedPoint = insert;
                m_Log.Debug($"Inserted path point at {insert + 1}, {Path.Nodes.Count} total.");
                return;
            }

            RecordUndo();

            Vector3 appended = PlacementPosition(ref heights);
            Path.Nodes.Add(new PathNode(appended));
            Path.RefreshAutoTangents();

            SelectedPoint = Path.Nodes.Count - 1;
            m_Log.Debug($"Added path point {Path.Nodes.Count} at {appended}");
        }

        /// <summary>Turns the road under the cursor into a path.</summary>
        /// <remarks>
        /// Replaces the path rather than appending to it. Tracing produces a whole move in one action,
        /// and grafting that onto whatever was already drawn is almost never what was meant — the undo
        /// stack is what makes replacing safe to offer.
        /// <para>
        /// Heights come from the terrain plus the placement height, not from the road's own geometry.
        /// A camera at road level is a driving shot, which is a different thing from following a road,
        /// and the placement height is already the control for that.
        /// </para>
        /// </remarks>
        private void HandleTraceRoad() {
            Setting settings = Mod.Instance.Settings;

            if (!PathNetworkTracer.TryTrace(EntityManager, m_CursorEntity, m_CursorPosition,
                                            settings.PathTraceLength, settings.PathTraceSpacing,
                                            out List<Vector3> traced)) {
                m_Log.Warn("Nothing to trace here — hover a road, rail or tram line and click it.");
                return;
            }

            RecordUndo();

            TerrainHeightData heights = m_TerrainSystem.GetHeightData();

            Path.Clear();

            foreach (Vector3 point in traced) {
                float ground = TerrainUtils.SampleHeight(ref heights, point);

                Path.Nodes.Add(new PathNode(new Vector3(point.x, ground + settings.PathPointHeight,
                                                        point.z)));
            }

            Path.RefreshAutoTangents();
            ClearSelection();

        }

        /// <summary>Aims the selected point at whatever was clicked in the world.</summary>
        /// <remarks>
        /// Clicking a point selects it instead, so a target can be set for several points in a row
        /// without leaving the mode — otherwise choosing which point to aim would mean switching back
        /// to Points, picking it, and switching here again for every single one.
        /// </remarks>
        private void HandleSetLookAt() {
            if (m_HoveredPoint >= 0) {
                SelectedPoint = m_HoveredPoint;
                return;
            }

            if (SelectedPoint < 0) {
                m_Log.Warn("Select a path point first, then click what it should aim at.");
                return;
            }

            if (!m_HasCursorPosition) {
                return;
            }

            RecordUndo();
            Path.Nodes[SelectedPoint].LookAt = m_CursorPosition;
            m_Log.Debug($"Path point {SelectedPoint + 1} now aims at {m_CursorPosition}.");
        }

        /// <remarks>
        /// Deleting is bound to secondary apply alone, so the two are never confused: right-click
        /// removes things, Escape backs out. Escape used to do both, which made it destructive —
        /// a press with the cursor slightly off a point silently shortened the path instead.
        /// </remarks>
        private void HandleDelete() {
            if (m_HoveredPoint < 0) {
                return;
            }

            RecordUndo();
            Path.Nodes.RemoveAt(m_HoveredPoint);
            Path.RefreshAutoTangents();

            m_Log.Debug($"Deleted path point {m_HoveredPoint + 1}, {Path.Nodes.Count} remain.");

            // Indices above the hole all shift down, so a stored selection would silently start
            // pointing at the next point along. Rebuilt through the property, which keeps the
            // selection set in step with the primary point.
            if (m_SelectedPoint == m_HoveredPoint) {
                ClearSelection();
            } else {
                SelectedPoint = (m_SelectedPoint > m_HoveredPoint) ? m_SelectedPoint - 1 : m_SelectedPoint;
            }

            m_HoveredPoint = -1;
        }

        // Escape only ever backs out of the tool now; the panel stays open for the second press.
        private void HandleCancel() { RequestDisable(); }

        /// <summary>Resamples the curve for this frame, for both picking and drawing.</summary>
        private void RefreshSamples() {
            if (!Path.IsValid) {
                m_Samples.Clear();
                m_SampleGlobals.Clear();
                m_SampleRotations.Clear();
                m_NodeSamples.Clear();
                return;
            }

            m_Samples         = Path.SamplePositions(kRenderSpacing, out m_SampleGlobals);
            m_SampleRotations = Path.SolveRotations(m_Samples, m_SampleGlobals);

            // Obstruction is measured against the drawn samples every frame so the warning follows the
            // path as it is dragged, rather than only appearing once a shot is generated.
            MeasureObstruction();

            FindNodeSamples();
        }

        /// <summary>Flags the drawn samples that have something standing in them.</summary>
        /// <remarks>
        /// Deliberately reports rather than lifts, whatever the mode is. The lift belongs to the solve,
        /// where it can be spread smoothly across the run-up; doing it here as well would move the
        /// drawn path away from the points the user placed and make dragging feel like a fight.
        /// </remarks>
        private void MeasureObstruction() {
            m_Obstructed.Clear();

            Setting settings = Mod.Instance.Settings;

            if (settings.PathClearanceMode == PathClearanceMode.Off ||
                settings.PathClearanceMode == PathClearanceMode.None || m_Samples.Count == 0) {
                return;
            }

            PathObstacles.Measure(World, m_Samples, m_ObstacleTops);

            for (int i = 0; i < m_Samples.Count && i < m_ObstacleTops.Count; i++) {
                m_Obstructed.Add(m_ObstacleTops[i] > float.MinValue &&
                                 m_Samples[i].y < m_ObstacleTops[i] + settings.PathObstacleClearance);
            }
        }

        /// <summary>Whether the segment about to be drawn runs through anything.</summary>
        private bool SegmentObstructed(int segment) {
            if (m_Obstructed.Count == 0) {
                return false;
            }

            for (int i = 0; i < m_SampleGlobals.Count && i < m_Obstructed.Count; i++) {
                if (m_Obstructed[i] && (int)m_SampleGlobals[i] == segment) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Which sample sits closest to each node, so a node can borrow the solved aim there.</summary>
        /// <remarks>
        /// Samples are spaced by arc length and so do not land on nodes; there is no sample index that
        /// simply *is* node three. This is the same lookup the dwell solver does, for the same reason.
        /// </remarks>
        private void FindNodeSamples() {
            m_NodeSamples.Clear();

            for (int node = 0; node < Path.Nodes.Count; node++) {
                int   best     = -1;
                float distance = float.MaxValue;

                for (int i = 0; i < m_SampleGlobals.Count; i++) {
                    float delta = Mathf.Abs(m_SampleGlobals[i] - node);

                    if (delta < distance) {
                        distance = delta;
                        best     = i;
                    }
                }

                m_NodeSamples.Add(best);
            }
        }

        /// <summary>Finds the curve under the cursor, and exactly where on it a new point would land.</summary>
        /// <remarks>
        /// Tested against the sampled curve under the mouse ray, not against the straight line between
        /// nodes in XZ. The straight-line version was wrong in three ways that all showed: it missed
        /// wherever the curve bows away from the chord between its nodes, it ignored height so a path
        /// fifty metres up was picked by the ground beneath it, and its tolerance was a fixed world
        /// distance — generous up close and unusable zoomed out.
        /// <para>
        /// Sampling covers a closed path's joining segment like any other, so the wrap needs no special
        /// case: a global parameter in the last segment yields an insert index of <c>Nodes.Count</c>,
        /// which appends.
        /// </para>
        /// </remarks>
        private int FindInsertIndex() {
            m_InsertSegment = -1;

            if (EditMode != PathEditMode.Points || m_HoveredPoint >= 0 || m_Samples.Count < 2 ||
                !PathPicking.TryGetMouseRay(out float3 origin, out float3 direction)) {
                return -1;
            }

            int   best     = -1;
            float nearest  = kInsertRadius;

            for (int i = 0; i < m_Samples.Count; i++) {
                float distance = PathPicking.DistanceToRay(origin, direction, m_Samples[i]);

                if (distance < nearest) {
                    nearest = distance;
                    best    = i;
                }
            }

            if (best < 0) {
                return -1;
            }

            int segment = Mathf.Clamp(Mathf.FloorToInt(m_SampleGlobals[best]), 0, Path.SegmentCount - 1);

            // A sample sitting on a node is that node, not a place to insert beside it — the point
            // picker owns those, and inserting there would stack two points in the same spot.
            if (Mathf.Abs(m_SampleGlobals[best] - Mathf.Round(m_SampleGlobals[best])) < 0.02f) {
                return -1;
            }

            m_InsertSegment = segment;
            m_InsertPoint   = m_Samples[best];

            return segment + 1;
        }

        private void Draw() {
            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete();

            TerrainHeightData heights = m_TerrainSystem.GetHeightData();

            // The path NOT being edited is drawn too, dimmed and without its points. Editing a rail
            // blind to where the camera goes — or the reverse — is the one thing a two-rail rig has to
            // avoid, since the whole point is the relationship between them.
            DrawInactivePath(ref buffer);

            if (Path.IsValid) {
                DrawPath(ref buffer);
                DrawGroundShadow(ref buffer, ref heights);
            }

            DrawRailTies(ref buffer);

            for (int i = 0; i < Path.Nodes.Count; i++) {
                float3 point    = Path.Nodes[i].Position;
                bool   hovered  = i == m_HoveredPoint;
                bool   selected = i == SelectedPoint;

                // A stem for both, because both are points you are currently thinking about and the
                // riser is what tells you where one actually sits over the ground.
                if (hovered || selected) {
                    DrawStem(ref buffer, Lift(ref heights, point), point);
                }

                // Hover wins the fill colour where a point is both: hover answers "what will this
                // click do", which is the more urgent question. The ring stays either way, so the
                // selected point never stops being findable just because the cursor crossed it.
                DrawMarker(ref buffer, hovered ? kHoverColor : selected ? kSelectedColor : kPointColor,
                           point, selected ? kSelectedDiameter : kPointDiameter);

                if (selected) {
                    DrawRing(ref buffer, kSelectedColor, point, kSelectedRingDiameter);
                }

                DrawHandles(ref buffer, Path.Nodes[i], i);
                DrawLookAt(ref buffer, Path.Nodes[i], i);
                DrawFrustum(ref buffer, i);
            }

            // Over the curve, the cursor marker sits ON it rather than at the height a new point would
            // otherwise be placed at — that is where the point is about to go, so showing it anywhere
            // else is a lie about what the click will do.
            if (m_InsertIndex >= 0) {
                DrawMarker(ref buffer, kInsertColor, m_InsertPoint, kPointDiameter);
                DrawStem(ref buffer, Lift(ref heights, m_InsertPoint), m_InsertPoint);
                return;
            }

            if (m_HasCursorPosition && m_HoveredPoint < 0 && m_HoveredHandle < 0) {
                float3 placement = PlacementPosition(ref heights);

                DrawMarker(ref buffer, kCursorColor, placement, kCursorDiameter);
                DrawStem(ref buffer, Lift(ref heights, m_CursorPosition), placement);
            }
        }

        /// <summary>Draws the path a segment at a time, highlighting the one a click would split.</summary>
        /// <remarks>
        /// One <c>DrawCurve</c> per segment, handed the segment's own Bezier. The overlay renderer
        /// takes a curve rather than a polyline, so nothing has to be resampled to draw it — what you
        /// see is the exact curve the solver will fly, not an approximation of it.
        /// <para>
        /// Drawing per segment is also what makes the hover highlight free: the hovered stretch is
        /// simply the one segment drawn in another colour, with no second mesh and no splitting.
        /// </para>
        /// <para>
        /// Note the overlay measures a curve by its length <em>in XZ</em> and skips anything under a
        /// centimetre, so a perfectly vertical segment draws nothing. Only reachable by stacking two
        /// points at the same spot at different heights.
        /// </para>
        /// </remarks>
        private void DrawPath(ref OverlayRenderSystem.Buffer buffer) {
            for (int i = 0; i < Path.SegmentCount; i++) {
                bool hot = i == m_InsertSegment;

                Color plain = (m_EditTarget == PathTarget.Rail) ? kRailColor : kPathColor;

                // Obstruction beats the ordinary colour but not the insert highlight: the highlight
                // answers "what will this click do", which is the more immediate question.
                if (!hot && SegmentObstructed(i)) {
                    plain = kBlockedColor;
                }

                buffer.DrawCurve(hot ? kInsertColor : plain, SegmentCurve(i),
                                 hot ? kPathWidth + kHighlightSwell : kPathWidth);
            }
        }

        /// <summary>Draws the other path faintly, so both are visible while either is edited.</summary>
        private void DrawInactivePath(ref OverlayRenderSystem.Buffer buffer) {
            CameraPath other = (m_EditTarget == PathTarget.Rail) ? TravelPath : RailPath;

            if (!other.IsValid) {
                return;
            }

            Color color = (m_EditTarget == PathTarget.Rail) ? kInactiveColor : kRailColor;

            for (int i = 0; i < other.SegmentCount; i++) {
                (Vector3 a, Vector3 b, Vector3 c, Vector3 d) = other.GetSegment(i);

                buffer.DrawCurve(color, new Bezier4x3 { a = a, b = b, c = c, d = d }, kPathWidth * 0.6f);
            }
        }

        /// <summary>Draws the sightlines between the travel path and the aim rail.</summary>
        /// <remarks>
        /// The ties are the whole readable content of a two-rail rig: two curves on their own say
        /// nothing about which point on one belongs to which point on the other, and that pairing is
        /// exactly what you are authoring. Drawn only when the aim mode is actually Rail, so they do
        /// not imply a relationship the shot will not use.
        /// </remarks>
        private void DrawRailTies(ref OverlayRenderSystem.Buffer buffer) {
            if (Mod.Instance.Settings.PathLook != PathLookMode.Rail ||
                !TravelPath.IsValid || !RailPath.IsValid) {
                return;
            }

            for (int i = 0; i <= kRailTies; i++) {
                float progress = (float)i / kRailTies;

                Vector3 from = TravelPath.PositionAtProgress(progress);
                Vector3 to   = RailPath.PositionAtProgress(progress);

                buffer.DrawDashedLine(kTieColor, new Line3.Segment(from, to), kTieWidth,
                                      kDashLength, kGapLength);
            }
        }

        private Bezier4x3 SegmentCurve(int segment) {
            (Vector3 a, Vector3 b, Vector3 c, Vector3 d) = Path.GetSegment(segment);

            return new Bezier4x3 { a = a, b = b, c = c, d = d };
        }

        private void DrawGroundShadow(ref OverlayRenderSystem.Buffer buffer, ref TerrainHeightData heights) {
            for (int i = 0; i < Path.SegmentCount; i++) {
                (Vector3 a, Vector3 b, Vector3 c, Vector3 d) = Path.GetSegment(i);

                var curve = new Bezier4x3 {
                    a = Lift(ref heights, a),
                    b = Lift(ref heights, b),
                    c = Lift(ref heights, c),
                    d = Lift(ref heights, d),
                };

                buffer.DrawCurve(kShadowColor, curve, kShadowWidth);
            }
        }

        /// <summary>Draws the camera's view cone at a point, so its aim is visible without flying it.</summary>
        /// <remarks>
        /// A dot says where the camera is and nothing about what it sees, which is the wrong primitive
        /// for a camera path — it is why judging a shot previously meant generating it and watching.
        /// Every professional motion-path tool draws the camera gizmo along the curve for exactly this
        /// reason.
        /// <para>
        /// The aim comes from the solver, so what is drawn is the rotation the shot will actually use,
        /// including look-ahead, per-point pitch, target aiming and the look-at overrides. The cone's
        /// half-angle comes from the point's own focal length where it sets one, so a long lens visibly
        /// narrows — which is the other half of judging framing.
        /// </para>
        /// </remarks>
        private void DrawFrustum(ref OverlayRenderSystem.Buffer buffer, int index) {
            if (!Mod.Instance.Settings.PathShowFrustums || m_SampleRotations == null ||
                index >= m_NodeSamples.Count) {
                return;
            }

            int sample = m_NodeSamples[index];

            if (sample < 0 || sample >= m_SampleRotations.Count) {
                return;
            }

            Vector3 origin = Path.Nodes[index].Position;
            Vector3 euler  = m_SampleRotations[sample];

            float half = HalfAngleFor(Path.Nodes[index].Fov);

            Color color = (index == SelectedPoint) ? kSelectedColor : kFrustumColor;

            DrawFrustumEdge(ref buffer, color, origin, euler, -half);
            DrawFrustumEdge(ref buffer, color, origin, euler, half);

            // The centre line is what actually reads at a distance; the edges only give it width.
            DrawFrustumEdge(ref buffer, color, origin, euler, 0f);
        }

        private static void DrawFrustumEdge(ref OverlayRenderSystem.Buffer buffer, Color color,
                                            Vector3 origin, Vector3 euler, float yawOffset) {
            Vector3 direction = Quaternion.Euler(euler.x, euler.y + yawOffset, 0f) * Vector3.forward;

            buffer.DrawLine(color, new Line3.Segment(origin, origin + direction * kFrustumLength),
                            kFrustumWidth, true);
        }

        /// <summary>Half the horizontal view angle for a focal length, or the default lens.</summary>
        /// <remarks>
        /// Uses Unity's own conversion against the game's default sensor height, so the cone widens and
        /// narrows the way the real lens does rather than by an invented curve. A point with no lens
        /// override draws at a plain 50mm, which is close enough to the game's normal view to read as
        /// "unchanged".
        /// </remarks>
        private static float HalfAngleFor(float? focalLength) {
            float millimetres = Mathf.Max(focalLength ?? kDefaultFocalLength, 0.0001f);

            return Mathf.Clamp(UnityEngine.Camera.FocalLengthToFieldOfView(millimetres, kSensorHeight),
                               1f, 179f) * 0.5f;
        }

        /// <summary>Draws what a point aims at, when it aims at something of its own.</summary>
        /// <remarks>
        /// Every target while the look-at mode is open, otherwise only the selected point's. Drawing
        /// them all the time turns a path with a dozen targets into a starburst that hides the path
        /// itself; drawing none leaves the one property with no visible state at all.
        /// </remarks>
        private void DrawLookAt(ref OverlayRenderSystem.Buffer buffer, PathNode node, int index) {
            if (!node.LookAt.HasValue) {
                return;
            }

            if (EditMode != PathEditMode.LookAt && index != SelectedPoint) {
                return;
            }

            Color color = index == SelectedPoint ? kSelectedColor : kLookAtColor;

            buffer.DrawDashedLine(color, new Line3.Segment(node.Position, node.LookAt.Value),
                                  kHandleArmWidth, kDashLength, kGapLength);

            DrawRing(ref buffer, color, node.LookAt.Value, kLookAtDiameter);
        }

        private void DrawHandles(ref OverlayRenderSystem.Buffer buffer, PathNode node, int index) {
            if (EditMode != PathEditMode.Curves) {
                return;
            }

            if (node.TangentOut.sqrMagnitude < 0.01f && node.TangentIn.sqrMagnitude < 0.01f) {
                return;
            }

            Color color   = node.Broken ? kBrokenColor : kHandleColor;
            bool  onThis  = index == m_HoveredHandle;
            bool  outHot  = onThis && m_DraggingOutHandle;
            bool  inHot   = onThis && !m_DraggingOutHandle;

            buffer.DrawDashedLine(color, new Line3.Segment(node.Position, node.HandleOut),
                                  kHandleArmWidth, kDashLength, kGapLength);
            buffer.DrawDashedLine(color, new Line3.Segment(node.Position, node.HandleIn),
                                  kHandleArmWidth, kDashLength, kGapLength);

            DrawHandleGrip(ref buffer, outHot ? kHoverColor : color, node.HandleOut, outHot);
            DrawHandleGrip(ref buffer, inHot ? kHoverColor : color, node.HandleIn, inHot);
        }

        private void DrawHandleGrip(ref OverlayRenderSystem.Buffer buffer, Color color,
                                    Vector3 position, bool hot) {
            buffer.DrawCircle(color, position, hot ? kHandleGripDiameter * 1.4f : kHandleGripDiameter);
            DrawHandleRing(ref buffer, color, position);
        }

        private static void DrawHandleRing(ref OverlayRenderSystem.Buffer buffer, Color color,
                                           Vector3 position) {
            DrawRing(ref buffer, color, position, kHandleDiameter);
        }

        /// <summary>An unfilled circle around a point.</summary>
        /// <remarks>
        /// The fill goes to <see cref="Color.clear"/> and a non-zero outline width carries the shape —
        /// that is how an overlay circle becomes a ring rather than a disc. The <c>float2</c> is a
        /// world-Y slab, not a direction, despite the parameter name: it has to bracket the point's own
        /// height or the shape is projected into empty space and silently draws nothing.
        /// </remarks>
        private static void DrawRing(ref OverlayRenderSystem.Buffer buffer, Color color,
                                     Vector3 position, float diameter) {
            buffer.DrawCircle(color, Color.clear, diameter * kHandleRingThickness,
                              0, new float2(position.y - diameter, position.y + diameter),
                              position, diameter);
        }

        /// <summary>The dotted riser tying a point in the air to the ground below it.</summary>
        /// <remarks>
        /// A camera-facing line, which the overlay supports directly. A flat one would vanish when
        /// looked at edge-on, and that is precisely the view you are in while judging a point's height.
        /// </remarks>
        private static void DrawStem(ref OverlayRenderSystem.Buffer buffer, float3 ground, float3 point) {
            if (math.distance(ground, point) < 0.01f) {
                return;
            }

            buffer.DrawLine(kStemColor, new Line3.Segment(ground, point), kStemWidth, true);
        }

        /// <summary>A point marker, drawn with the game's own overlay disc.</summary>
        /// <remarks>
        /// Flat and horizontal rather than a sphere, which is what every other marker in the game is —
        /// route stops, tool cursors, net node handles. It reads correctly from the angle a city is
        /// actually viewed from, and it costs no mesh of ours to build or keep in sync.
        /// </remarks>
        private static void DrawMarker(ref OverlayRenderSystem.Buffer buffer, Color color,
                                       float3 position, float diameter) {
            buffer.DrawCircle(color, position, diameter);
        }

        private void HandleHeightInput() {
            HandlePlacementHeight();

            float step = Mod.Instance.Settings.PathHeightStep * UnityEngine.Time.unscaledDeltaTime
                         * kHeightStepsPerSecond;

            if (Mod.PathRaiseAction != null && Mod.PathRaiseAction.IsPressed()) {
                AdjustHeight(step);
            }

            if (Mod.PathLowerAction != null && Mod.PathLowerAction.IsPressed()) {
                AdjustHeight(-step);
            }
        }

        /// <remarks>
        /// Ctrl plus the wheel sets how high the next point will be placed, which is the one thing you
        /// want to change while lining a point up rather than after placing it.
        /// <para>
        /// The wheel is read from <c>Mouse.current</c> instead of through a bound action, for the same
        /// reason the modifier is: binding wheel-plus-Ctrl would collide with the camera. It does not
        /// fight vanilla zoom either, and that is the modifier trap working in our favour — zoom is
        /// bound at the default <c>ModifierOptions.Allow</c>, so holding an unlisted modifier attaches
        /// a <c>ProhibitionModifierProcessor</c> and suppresses it. The wheel is ours while Ctrl is
        /// down.
        /// </para>
        /// </remarks>
        private void HandlePlacementHeight() {
            if (!PathModifier.Ctrl || Mouse.current == null) {
                return;
            }

            float wheel = Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(wheel) < 0.01f) {
                return;
            }

            Setting settings = Mod.Instance.Settings;

            // Uncapped, like every other height the world lets you drag. A camera a kilometre up is a
            // real shot, and one below ground level is how you fly a tunnel or a canyon — neither has
            // any business being decided by a constant here.
            settings.PathPointHeight += (wheel > 0f) ? settings.PathHeightStep
                                                     : -settings.PathHeightStep;
        }

        private float3 PlacementPosition(ref TerrainHeightData heights) {
            // A point snap is the only one that carries a height with it: it lands exactly on an
            // existing point, which is what makes closing a loop land on the first point rather than
            // near it. The rest steer the cursor and leave the height rule alone.
            if (TrySnapToPoint(out Vector3 exact)) {
                return exact;
            }

            Vector3 cursor = SnapCursor(m_CursorPosition);
            float   ground = TerrainUtils.SampleHeight(ref heights, cursor);

            return new float3(cursor.x, ground + Mod.Instance.Settings.PathPointHeight, cursor.z);
        }

        /// <summary>Pulls the cursor onto an existing point, so a click can land exactly on one.</summary>
        /// <remarks>
        /// The first point is what this is for. Closing a loop by eye leaves the join a metre or two
        /// out, which the curve then has to travel — snapping makes the last point coincide with the
        /// first exactly, and <c>Closed</c> turns that into a real segment.
        /// </remarks>
        private bool TrySnapToPoint(out Vector3 position) {
            position = default;

            if (Mod.Instance.Settings.PathSnap != PathSnapMode.Point) {
                return false;
            }

            float best     = Mod.Instance.Settings.PathSnapRadius;
            int   bestNode = -1;

            best *= best;

            for (int i = 0; i < Path.Nodes.Count; i++) {
                if (i == m_DraggedPoint) {
                    continue;
                }

                Vector3 node = Path.Nodes[i].Position;
                float   sq   = new Vector2(node.x - m_CursorPosition.x,
                                           node.z - m_CursorPosition.z).sqrMagnitude;

                if (sq < best) {
                    best     = sq;
                    bestNode = i;
                }
            }

            if (bestNode < 0) {
                return false;
            }

            position = Path.Nodes[bestNode].Position;
            return true;
        }

        /// <summary>Applies whichever cursor snap is selected, in the XZ plane.</summary>
        private Vector3 SnapCursor(Vector3 cursor) {
            Setting settings = Mod.Instance.Settings;

            switch (settings.PathSnap) {
                case PathSnapMode.Grid: {
                    float size = Mathf.Max(settings.PathGridSize, 1f);

                    return new Vector3(Mathf.Round(cursor.x / size) * size, cursor.y,
                                       Mathf.Round(cursor.z / size) * size);
                }

                case PathSnapMode.Angle:
                    return SnapToAngle(cursor, settings.PathAngleStep);

                case PathSnapMode.Network:
                    return SnapToNetwork(cursor);

                default:
                    return cursor;
            }
        }

        /// <summary>Constrains the heading from the previous point to a fixed step, keeping the distance.</summary>
        /// <remarks>
        /// Distance is preserved rather than projecting onto the snapped ray, so the point stays where
        /// the cursor reaches and only the direction is corrected — projecting makes the point slide
        /// towards the anchor as the cursor swings off the step, which reads as the tool fighting you.
        /// </remarks>
        private Vector3 SnapToAngle(Vector3 cursor, float step) {
            if (Path.Nodes.Count == 0 || step <= 0f) {
                return cursor;
            }

            Vector3 anchor = Path.Nodes[Path.Nodes.Count - 1].Position;
            var     flat   = new Vector2(cursor.x - anchor.x, cursor.z - anchor.z);

            if (flat.sqrMagnitude < 0.01f) {
                return cursor;
            }

            float angle    = Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
            float snapped  = Mathf.Round(angle / step) * step * Mathf.Deg2Rad;
            float distance = flat.magnitude;

            return new Vector3(anchor.x + Mathf.Sin(snapped) * distance, cursor.y,
                               anchor.z + Mathf.Cos(snapped) * distance);
        }

        /// <summary>Pulls the cursor onto the centreline of the road it is over.</summary>
        /// <remarks>
        /// The raycast hit lands on the road surface, which is not the same as the road's line — a
        /// path built from surface hits wanders across the carriageway. <c>Game.Net.Curve</c> carries
        /// the actual centreline, so the closest point on that bezier is what a drive-along shot wants.
        /// </remarks>
        private Vector3 SnapToNetwork(Vector3 cursor) {
            if (m_CursorEntity == Entity.Null || !EntityManager.HasComponent<Curve>(m_CursorEntity)) {
                return cursor;
            }

            Bezier4x3 curve = EntityManager.GetComponentData<Curve>(m_CursorEntity).m_Bezier;

            MathUtils.Distance(curve, cursor, out float t);

            float3 point = MathUtils.Position(curve, t);
            return new Vector3(point.x, cursor.y, point.z);
        }

        /// <summary>Moves the whole path so its centre lands under the cursor.</summary>
        /// <remarks>
        /// Height is carried across as a difference in ground level, not copied outright: a saved path
        /// holds absolute world heights, so dropping one on a hillside without this puts the whole shot
        /// underground. Matching the terrain delta keeps every point the same distance above the ground
        /// it was authored over, which is what the heights actually meant.
        /// </remarks>
        public bool MovePathToCursor() {
            if (!m_HasCursorPosition || Path.Nodes.Count == 0) {
                return false;
            }

            TerrainHeightData heights = m_TerrainSystem.GetHeightData();

            Vector3 centre = Path.Centre;
            Vector3 target = SnapCursor(m_CursorPosition);

            float from = TerrainUtils.SampleHeight(ref heights, centre);
            float to   = TerrainUtils.SampleHeight(ref heights, target);

            Path.Translate(new Vector3(target.x - centre.x, to - from, target.z - centre.z));
            m_Log.Debug($"Moved path of {Path.Nodes.Count} points to {target}.");
            return true;
        }

        /// <summary>Raises or lowers every point by the same amount.</summary>
        public void RaisePath(float delta) {
            Path.Translate(new Vector3(0f, delta, 0f));
        }

        private int FindHoveredPoint() {
            // Pickable in look-at mode as well as in points mode: clicking a point there is how you
            // choose which one to aim, so gating this on Points alone made the mode unusable.
            if (EditMode == PathEditMode.Curves ||
                !PathPicking.TryGetMouseRay(out float3 origin, out float3 direction)) {
                return -1;
            }

            int   best    = -1;
            float radius  = kPointPickRadius;
            float nearest = float.MaxValue;

            for (int i = 0; i < Path.Nodes.Count; i++) {
                if (PathPicking.TryHitSphere(origin, direction, Path.Nodes[i].Position, radius,
                                             out float t) && t < nearest) {
                    nearest = t;
                    best    = i;
                }
            }

            return best;
        }

        private void AdjustHeight(float delta) {
            int index = m_HoveredPoint >= 0 ? m_HoveredPoint : Path.Nodes.Count - 1;

            if (index < 0) {
                return;
            }

            PathNode node = Path.Nodes[index];
            node.Position = node.Position + new Vector3(0f, delta, 0f);
            Path.RefreshAutoTangents();
        }

        private static float3 Lift(ref TerrainHeightData heights, Vector3 position) {
            return new float3(position.x,
                              TerrainUtils.SampleHeight(ref heights, position) + kGroundLift,
                              position.z);
        }
    }
}
