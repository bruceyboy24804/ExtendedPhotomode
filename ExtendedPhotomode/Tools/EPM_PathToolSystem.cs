namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using Colossal.Mathematics;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Rendering;

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
        private const float kPathWidth = 3f;

        private const float kTubeDiameter = 4f;

        private const float kPointDiameter = 5f;

        private const float kCursorDiameter = 4f;

        private const float kGroundLift = 0.5f;

        private const float kShadowWidth = 1.5f;

        private const float kStemWidth = 0.4f;

        private const float kHoverRadius = 25f;

        private const float kInsertRadius = 15f;

        private const float kRenderSpacing = 4f;

        private const float kHandleHoverRadius = 12f;

        private const float kHandleDiameter = 3.5f;

        private const float kHandleRingThickness = 0.25f;

        private const float kHandleArmWidth = 0.6f;

        private const float kDashLength = 2f;

        private const float kGapLength = 1.5f;

        private const float kHeightStepsPerSecond = 12f;

        private static readonly Color kPathColor   = new Color(0.35f, 0.7f, 1f, 1f);
        private static readonly Color kPointColor  = new Color(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color kCursorColor = new Color(0.4f, 1f, 0.5f, 0.9f);
        private static readonly Color kShadowColor = new Color(0.1f, 0.35f, 0.55f, 0.5f);
        private static readonly Color kStemColor   = new Color(1f, 0.85f, 0.2f, 0.25f);
        private static readonly Color kHoverColor  = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color kHandleColor = new Color(0.6f, 1f, 0.8f, 0.85f);
        private static readonly Color kBrokenColor = new Color(1f, 0.5f, 0.35f, 0.9f);

        private OverlayRenderSystem m_OverlayRenderSystem;
        private TerrainSystem       m_TerrainSystem;
        private ToolOutputBarrier   m_ToolOutputBarrier;
        private DefaultToolSystem   m_DefaultToolSystem;
        private PrefixedLogger      m_Log;

        private bool    m_HasCursorPosition;
        private Vector3 m_CursorPosition;
        private int     m_HoveredPoint = -1;
        private int     m_DraggedPoint = -1;
        private int     m_HoveredHandle = -1;
        private bool    m_DraggingHandle;
        private bool    m_DraggingOutHandle;

        private readonly EPM_TubeGizmo m_Tube = new EPM_TubeGizmo();

        public override string toolID => "EPM_PathTool";

        public CameraPath Path { get; } = new CameraPath();

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                 = new PrefixedLogger(nameof(EPM_PathToolSystem));
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_TerrainSystem       = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_ToolOutputBarrier   = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_DefaultToolSystem   = World.GetOrCreateSystemManaged<DefaultToolSystem>();

            m_ToolSystem.tools.Remove(this);
            m_ToolSystem.tools.Insert(0, this);
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();
            m_HasCursorPosition = false;

            applyActionOverride = Mod.PathApplyAction;

            applyAction.shouldBeEnabled  = true;
            cancelAction.shouldBeEnabled = true;

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

            m_ToolRaycastSystem.typeMask        = TypeMask.Terrain;
            m_ToolRaycastSystem.collisionMask   = CollisionMask.OnGround | CollisionMask.Overground;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
        }

        public override PrefabBase GetPrefab() { return null; }

        public override bool TrySetPrefab(PrefabBase prefab) { return false; }

        public void RequestEnable() { m_ToolSystem.activeTool = this; }

        public void RequestDisable() { m_ToolSystem.activeTool = m_DefaultToolSystem; }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            m_HasCursorPosition = GetRaycastResult(out Entity _, out RaycastHit hit);

            if (m_HasCursorPosition) {
                m_CursorPosition = hit.m_HitPosition;
            }

            m_HoveredHandle = FindHoveredHandle();
            m_HoveredPoint  = FindHoveredPoint();
            HandleInput();
            Draw();

            return inputDeps;
        }

        public string DescribeApply() {
            if (m_HoveredHandle >= 0) {
                return "Shape curve";
            }

            if (m_HoveredPoint >= 0) {
                return "Move point";
            }

            return FindInsertIndex() >= 0 ? "Insert point" : "Add point";
        }

        public string DescribeBreakTangent() {
            int index = m_HoveredHandle >= 0 ? m_HoveredHandle : m_HoveredPoint;

            if (index < 0) {
                return null;
            }

            return Path.Nodes[index].Broken ? "Smooth corner" : "Sharp corner";
        }

        private void HandleInput() {
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

            if (cancelAction.WasPressedThisFrame()) {
                HandleCancel();
            }
        }

        private void HandleReverse() {
            if (Mod.PathReverseAction == null || !Mod.PathReverseAction.WasPressedThisFrame()) {
                return;
            }

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
                    m_DraggedPoint  = m_HoveredHandle;
                    m_DraggingHandle = true;
                    return true;
                }

                if (m_HoveredPoint < 0) {
                    return false;
                }

                m_DraggedPoint   = m_HoveredPoint;
                m_DraggingHandle = false;
                return true;
            }

            if (!applyAction.IsPressed()) {
                m_DraggedPoint   = -1;
                m_DraggingHandle = false;
                return true;
            }

            if (!m_HasCursorPosition || m_DraggedPoint >= Path.Nodes.Count) {
                return true;
            }

            PathNode node = Path.Nodes[m_DraggedPoint];

            if (m_DraggingHandle) {
                var handle = new Vector3(m_CursorPosition.x, node.Position.y, m_CursorPosition.z);

                if (m_DraggingOutHandle) {
                    node.SetHandleOut(handle);
                } else {
                    node.SetHandleIn(handle);
                }

                return true;
            }

            Vector3 point = node.Position;

            point.x       = m_CursorPosition.x;
            point.z       = m_CursorPosition.z;
            node.Position = point;

            Path.RefreshAutoTangents();
            return true;
        }

        private int FindHoveredHandle() {
            if (!m_HasCursorPosition) {
                return -1;
            }

            int   best         = -1;
            float bestDistance = kHandleHoverRadius * kHandleHoverRadius;

            for (int i = 0; i < Path.Nodes.Count; i++) {
                PathNode node = Path.Nodes[i];

                float outSq = SquaredHorizontalDistance(node.HandleOut, m_CursorPosition);
                float inSq  = SquaredHorizontalDistance(node.HandleIn, m_CursorPosition);

                if (outSq < bestDistance) {
                    bestDistance       = outSq;
                    best               = i;
                    m_DraggingOutHandle = true;
                }

                if (inSq < bestDistance) {
                    bestDistance       = inSq;
                    best               = i;
                    m_DraggingOutHandle = false;
                }
            }

            return best;
        }

        private static float SquaredHorizontalDistance(Vector3 a, Vector3 b) {
            float dx = a.x - b.x;
            float dz = a.z - b.z;

            return dx * dx + dz * dz;
        }

        private void HandleBreakTangent() {
            if (Mod.PathBreakTangentAction == null || !Mod.PathBreakTangentAction.WasPressedThisFrame()) {
                return;
            }

            int index = m_HoveredHandle >= 0 ? m_HoveredHandle : m_HoveredPoint;

            if (index < 0) {
                return;
            }

            PathNode node = Path.Nodes[index];
            node.Broken   = !node.Broken;

            if (!node.Broken) {
                node.SetHandleOut(node.HandleOut);
            }

            m_Log.Debug($"Path point {index + 1} tangent is now {(node.Broken ? "broken" : "smooth")}.");
        }

        private void HandleApply() {
            TerrainHeightData heights = m_TerrainSystem.GetHeightData();
            int               insert  = FindInsertIndex();

            if (insert >= 0) {
                Vector3 before = Path.Nodes[insert - 1].Position;
                Vector3 after   = Path.Nodes[insert].Position;
                var     point   = new Vector3(m_CursorPosition.x, (before.y + after.y) * 0.5f,
                                              m_CursorPosition.z);

                Path.Nodes.Insert(insert, new PathNode(point));
                m_Log.Debug($"Inserted path point at {insert + 1}, {Path.Nodes.Count} total.");
                return;
            }

            Vector3 appended = PlacementPosition(ref heights);
            Path.Nodes.Add(new PathNode(appended));
            m_Log.Debug($"Added path point {Path.Nodes.Count} at {appended}");
        }

        private void HandleCancel() {
            if (m_HoveredPoint >= 0) {
                Path.Nodes.RemoveAt(m_HoveredPoint);
                m_Log.Debug($"Deleted path point {m_HoveredPoint + 1}, {Path.Nodes.Count} remain.");
                m_HoveredPoint = -1;
                return;
            }

            if (Path.Nodes.Count > 0) {
                Path.Nodes.RemoveAt(Path.Nodes.Count - 1);
                m_Log.Debug($"Removed last path point, {Path.Nodes.Count} remain.");
                return;
            }

            RequestDisable();
        }

        private int FindInsertIndex() {
            if (!m_HasCursorPosition || m_HoveredPoint >= 0 || Path.Nodes.Count < 2) {
                return -1;
            }

            int   best         = -1;
            float bestDistance = kInsertRadius * kInsertRadius;

            for (int i = 1; i < Path.Nodes.Count; i++) {
                float sq = SquaredDistanceToSegment(Path.Nodes[i - 1].Position, Path.Nodes[i].Position,
                                                    m_CursorPosition);

                if (sq < bestDistance) {
                    bestDistance = sq;
                    best         = i;
                }
            }

            return best;
        }

        private static float SquaredDistanceToSegment(Vector3 from, Vector3 to, Vector3 position) {
            var a     = new Vector2(from.x, from.z);
            var b     = new Vector2(to.x, to.z);
            var p     = new Vector2(position.x, position.z);
            var delta = b - a;

            float lengthSq = delta.sqrMagnitude;

            if (lengthSq < 0.0001f) {
                return (p - a).sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, delta) / lengthSq);
            return (p - (a + delta * t)).sqrMagnitude;
        }

        private void Draw() {
            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete();

            TerrainHeightData heights = m_TerrainSystem.GetHeightData();

            if (Path.IsValid) {
                List<Vector3> positions = Path.SamplePositions(kRenderSpacing);

                m_Tube.Draw(positions, kPathColor, kTubeDiameter);

                DrawGroundShadow(ref buffer, ref heights);
            }

            for (int i = 0; i < Path.Nodes.Count; i++) {
                float3 point   = Path.Nodes[i].Position;
                bool   hovered = i == m_HoveredPoint;
                float3 ground  = Lift(ref heights, point);

                if (hovered) {
                    DrawTube(ref buffer, kStemColor, ground, point, kStemWidth);
                }

                DrawMarker(ref buffer, hovered ? kHoverColor : kPointColor, point, kPointDiameter);

                DrawHandles(ref buffer, Path.Nodes[i], i);
            }

            if (m_HasCursorPosition) {
                float3 placement = PlacementPosition(ref heights);

                DrawMarker(ref buffer, kCursorColor, placement, kCursorDiameter);
                DrawTube(ref buffer, kStemColor, Lift(ref heights, m_CursorPosition), placement,
                         kStemWidth);
            }
        }

        private void DrawGroundShadow(ref OverlayRenderSystem.Buffer buffer, ref TerrainHeightData heights) {
            int last = Path.Nodes.Count - 1;

            for (int i = 0; i < last; i++) {
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

        private void DrawHandles(ref OverlayRenderSystem.Buffer buffer, PathNode node, int index) {
            if (index != m_HoveredPoint && index != m_HoveredHandle) {
                return;
            }

            Color color = node.Broken ? kBrokenColor : kHandleColor;

            buffer.DrawDashedLine(color, new Line3.Segment(node.Position, node.HandleOut),
                                  kHandleArmWidth, kDashLength, kGapLength);
            buffer.DrawDashedLine(color, new Line3.Segment(node.Position, node.HandleIn),
                                  kHandleArmWidth, kDashLength, kGapLength);

            DrawHandleRing(ref buffer, color, node.HandleOut);
            DrawHandleRing(ref buffer, color, node.HandleIn);
        }

        private static void DrawHandleRing(ref OverlayRenderSystem.Buffer buffer, Color color,
                                           Vector3 position) {
            buffer.DrawCircle(color, Color.clear, kHandleDiameter * kHandleRingThickness,
                              0, new float2(0f, 1f), position, kHandleDiameter);
        }

        private static void DrawTube(ref OverlayRenderSystem.Buffer buffer, Color color,
                                     float3 from, float3 to, float width) {
            float3 delta  = to - from;
            float  length = math.length(delta);

            if (length < 0.01f) {
                return;
            }

            var rotation = Quaternion.FromToRotation(Vector3.up, (Vector3)(delta / length));

            buffer.DrawCustomMesh(color, from + delta * 0.5f, length, width,
                                  OverlayRenderSystem.CustomMeshType.Cylinder, rotation);
        }

        private static void DrawMarker(ref OverlayRenderSystem.Buffer buffer, Color color,
                                       float3 position, float diameter) {
            EPM_SphereGizmo.Draw(position, diameter, color);
        }

        private void HandleHeightInput() {
            float step = Mod.Instance.Settings.PathHeightStep * UnityEngine.Time.unscaledDeltaTime
                         * kHeightStepsPerSecond;

            if (Mod.PathRaiseAction != null && Mod.PathRaiseAction.IsPressed()) {
                AdjustHeight(step);
            }

            if (Mod.PathLowerAction != null && Mod.PathLowerAction.IsPressed()) {
                AdjustHeight(-step);
            }
        }

        private float3 PlacementPosition(ref TerrainHeightData heights) {
            float ground = TerrainUtils.SampleHeight(ref heights, m_CursorPosition);
            return new float3(m_CursorPosition.x, ground + Mod.Instance.Settings.PathPointHeight,
                              m_CursorPosition.z);
        }

        private int FindHoveredPoint() {
            if (!m_HasCursorPosition) {
                return -1;
            }

            int   best         = -1;
            float bestDistance = kHoverRadius * kHoverRadius;

            for (int i = 0; i < Path.Nodes.Count; i++) {
                Vector3 point = Path.Nodes[i].Position;
                float   dx    = point.x - m_CursorPosition.x;
                float   dz    = point.z - m_CursorPosition.z;
                float   sq    = dx * dx + dz * dz;

                if (sq < bestDistance) {
                    bestDistance = sq;
                    best         = i;
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
