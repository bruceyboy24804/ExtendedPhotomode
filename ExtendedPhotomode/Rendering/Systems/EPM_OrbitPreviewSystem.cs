namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Mathematics;

    using ExtendedPhotomode.Camera;

    using Colossal.UI.Binding;

    using Game;
    using Game.Rendering;
    using Game.UI.InGame;
    using Game.Simulation;

    using ModsCommon.Extensions;
    using ModsCommon.Rendering;

    using Game.Common;

    using ModsCommon.Utils;

    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Draws the orbit that would be generated right now, so the shot can be aimed before it is
    /// committed to the timeline.
    /// </summary>
    /// <remarks>
    /// Drawn from <see cref="OrbitShot.Solve"/> — the same samples that become keyframes — so the
    /// preview cannot drift from what is actually generated.
    /// This draws through <see cref="CustomOverlayRenderSystem"/> rather than the vanilla
    /// <c>OverlayRenderSystem</c>. Photo mode suppresses vanilla overlay rendering — proven by two
    /// captures from the same camera with the same draws running, where the ring and vanilla's own
    /// road name labels are both present outside photo mode and both gone inside it. Nothing in the
    /// source explains it, so the only reliable route is a renderer that does its own drawing.
    /// The preview is deliberately photo-mode-only. An orbit is composed from the photo mode camera,
    /// so that is where being unable to see the ring actually costs you; normal gameplay stays clean,
    /// and the path tool already covers authoring a move from outside. It still disappears when the
    /// hide-UI toggle is used: bypassing <c>hideOverlay</c> means that toggle no longer suppresses us
    /// automatically, so the hide state is read from the photo mode panel and honoured here instead.
    /// </remarks>
    public partial class EPM_OrbitPreviewSystem : GameSystemBase {
        private const float kPathWidth = 2f;

        private const float kTargetMarkerDiameter = 12f;

        private const float kStartMarkerDiameter = 8f;

        private const float kGuideWidth = 0.75f;

        private const float kRingThickness = 0.125f;

        private const float kProjectionDepth = 1f;

        private const float kGroundLift = 0.5f;

        private static readonly Color kPathColor   = new Color(0.25f, 0.75f, 1f, 0.9f);
        private static readonly Color kTargetColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color kStartColor  = new Color(0.4f, 1f, 0.5f, 0.9f);
        private static readonly Color kGuideColor  = new Color(1f, 0.85f, 0.2f, 0.35f);

        private const string kOverlayHiddenField = "m_OverlayHiddenBinding";

        private PhotoModeUISystem     m_PhotoModeUISystem;
        private PhotoModeRenderSystem m_PhotoModeRenderSystem;
        private CustomOverlayRenderSystem m_OverlayRenderSystem;
        private EPM_ShotSubjectSystem    m_Subject;
        private TerrainSystem         m_TerrainSystem;
        private PrefixedLogger        m_Log;
        private int                   m_DrawCount;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                   = new PrefixedLogger(nameof(EPM_OrbitPreviewSystem));
            m_PhotoModeRenderSystem = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_PhotoModeUISystem     = World.GetOrCreateSystemManaged<PhotoModeUISystem>();
            m_OverlayRenderSystem   = World.GetOrCreateSystemManaged<CustomOverlayRenderSystem>();
            m_Subject               = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
            m_TerrainSystem         = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override void OnUpdate() {
            if (!Mod.Instance.Settings.ShowOrbitPreview) {
                m_DrawCount = 0;
                return;
            }

            bool inPhotoMode = m_PhotoModeRenderSystem.Enabled;

            bool shouldDraw = inPhotoMode && !IsOverlayHiddenByUser();
            m_OverlayRenderSystem.IgnoreHideOverlay = shouldDraw;

            if (!shouldDraw) {
                m_DrawCount = 0;
                return;
            }

            if (!m_Subject.TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                return;
            }

            List<CameraSample> samples = orbit.Solve();
            if (samples.Count < 2) {
                return;
            }

            Draw(orbit, samples);

            m_DrawCount++;

            if (m_DrawCount == 1 || m_DrawCount % 120 == 0) {
                m_Log.Debug($"Orbit preview draw #{m_DrawCount}: {samples.Count} samples, centre {orbit.Target}, photoMode={inPhotoMode}");
            }
        }

        private bool IsOverlayHiddenByUser() {
            if (m_PhotoModeUISystem.GetMemberValue(kOverlayHiddenField) is ValueBinding<bool> binding) {
                return binding.value;
            }

            return true;
        }

        private void Draw(OrbitShot orbit, List<CameraSample> samples) {
            CustomOverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete();

            TerrainHeightData heights = m_TerrainSystem.GetHeightData();

            float3 target = OnGround(ref heights, orbit.Target);
            float3 start  = samples[0].Position;

            for (int i = 1; i < samples.Count; i++) {
                var segment = new Line3.Segment(OnGround(ref heights, samples[i - 1].Position, kGroundLift),
                                                OnGround(ref heights, samples[i].Position, kGroundLift));

                buffer.DrawLine(kPathColor, kPathColor, 0f, 0, segment, kPathWidth, new float2(1f, 1f));
            }

            buffer.DrawCircle(kTargetColor, Color.clear, kTargetMarkerDiameter * kRingThickness,
                              CustomOverlayRenderSystem.StyleFlags.Projected, ProjectionSlab(target),
                              target, kTargetMarkerDiameter);

            buffer.DrawLine(kGuideColor, new Line3.Segment(target, start), kGuideWidth, true);

            float3 startOnGround = OnGround(ref heights, start);
            buffer.DrawCircle(kStartColor, Color.clear, kStartMarkerDiameter * kRingThickness,
                              CustomOverlayRenderSystem.StyleFlags.Projected, ProjectionSlab(startOnGround),
                              startOnGround, kStartMarkerDiameter);
        }

        private static float3 OnGround(ref TerrainHeightData heights, float3 position, float lift = 0f) {
            return new float3(position.x, TerrainUtils.SampleHeight(ref heights, position) + lift, position.z);
        }

        private static float2 ProjectionSlab(float3 position) {
            return new float2(position.y - kProjectionDepth, position.y + kProjectionDepth);
        }
    }
}
