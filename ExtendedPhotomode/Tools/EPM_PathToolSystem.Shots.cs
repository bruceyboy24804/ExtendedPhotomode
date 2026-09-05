namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Mathematics;

    using ExtendedPhotomode.Camera;

    using Game.Rendering;
    using Game.Simulation;

    using Unity.Jobs;
    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>Editing for the shot types that are not drawn paths.</summary>
    /// <remarks>
    /// <para>
    /// The tool used to be the path tool. It is now the tool for every shot type, because everything
    /// it had built up — ray picking, gizmos, hint tooltips, the tool options panel — was equally
    /// useful to an orbit, and an orbit had none of it. Dialling a radius by typing a number, pressing
    /// generate, watching, and going back to change it was the loop this removes.
    /// </para>
    /// <para>
    /// Dispatch is by <c>Settings.Shot</c> and lives at the top of the update, so a drawn path keeps
    /// running through exactly the code it always did. That asymmetry is deliberate: the path pipeline
    /// is long, subtle and verified, and folding it into the editor abstraction would have risked all
    /// of it to make the file look tidier. The editors own the shot types that had nothing to lose.
    /// </para>
    /// </remarks>
    public partial class EPM_PathToolSystem {
        private Dictionary<ShotType, ShotEditorBase> m_Editors;

        private readonly List<ShotHandle> m_Handles = new List<ShotHandle>();

        /// <summary>The solved shot, resolved once a frame and shared by every part of the drawing.</summary>
        private readonly List<CameraSample> m_ShotPreview = new List<CameraSample>();

        /// <summary>Roughly how many view cones to draw along a shot, whatever its keyframe count.</summary>
        private const int kShotFrustums = 10;

        private int m_HoveredHandleId = -1;

        private int m_DraggedHandleId = -1;

        /// <summary>Whether the tool is editing a shot rather than drawing a path.</summary>
        public bool EditingShot => Mod.Instance.Settings.Shot != ShotType.Path;

        private ShotEditorBase ActiveEditor {
            get {
                m_Editors ??= ShotEditorBase.Discover(World);

                return m_Editors.TryGetValue(Mod.Instance.Settings.Shot, out ShotEditorBase editor)
                           ? editor
                           : null;
            }
        }

        /// <summary>Solves whichever shot is selected, for previewing it without generating.</summary>
        /// <param name="samples">The solved keyframes, or an empty list.</param>
        /// <returns>False when the shot is not complete enough to solve.</returns>
        /// <remarks>
        /// One entry point for all three types, so the preview flight does not need to know which is
        /// selected. A path goes through the same preparation the generator uses; the others go
        /// through their editor, which is already solving them every frame to draw the ring or track.
        /// </remarks>
        public bool TrySolveActiveShot(out List<CameraSample> samples) {
            if (!EditingShot) {
                CameraPath path = World.GetOrCreateSystemManaged<EPM_PathToolToggleSystem>()
                                       .PrepareForSolve();

                samples = path.IsValid ? path.Solve() : new List<CameraSample>();
                return samples.Count >= 2;
            }

            samples = new List<CameraSample>();

            ShotEditorBase editor = ActiveEditor;

            return editor != null && editor.TryPreview(samples);
        }

        /// <summary>Runs a frame of shot editing, in place of the path pipeline.</summary>
        private void UpdateShotEditing() {
            ShotEditorBase editor = ActiveEditor;

            if (editor == null) {
                return;
            }

            m_Handles.Clear();
            editor.CollectHandles(m_Handles);

            m_HoveredHandleId = FindHoveredShotHandle();

            HandleShotHeight(editor);
            HandleShotInput(editor);
            DrawShotEditing(editor);
        }

        private int FindHoveredShotHandle() {
            if (!PathPicking.TryGetMouseRay(out float3 origin, out float3 direction)) {
                return -1;
            }

            int   best    = -1;
            float nearest = float.MaxValue;

            foreach (ShotHandle handle in m_Handles) {
                if (PathPicking.TryHitSphere(origin, direction, handle.Position, kShotHandleRadius,
                                             out float t) && t < nearest) {
                    nearest = t;
                    best    = handle.Id;
                }
            }

            return best;
        }

        private void HandleShotInput(ShotEditorBase editor) {
            if (m_DraggedHandleId >= 0) {
                if (!applyAction.IsPressed()) {
                    m_DraggedHandleId = -1;
                    return;
                }

                DragShotHandle(editor);
                return;
            }

            if (!applyAction.WasPressedThisFrame()) {
                return;
            }

            if (m_HoveredHandleId >= 0) {
                m_DraggedHandleId = m_HoveredHandleId;
                return;
            }

            // A click on empty ground places the subject. Without it there is no way to start a shot
            // from inside the tool at all — you would have to leave, select a building, and come back.
            if (m_HasCursorPosition) {
                editor.PlaceTarget(m_CursorPosition);
                m_Log.Debug($"Placed the shot subject at {m_CursorPosition}.");
            }
        }

        private void DragShotHandle(ShotEditorBase editor) {
            ShotHandle handle = default;
            bool       found  = false;

            foreach (ShotHandle candidate in m_Handles) {
                if (candidate.Id == m_DraggedHandleId) {
                    handle = candidate;
                    found  = true;
                    break;
                }
            }

            if (!found) {
                return;
            }

            // A grounded handle follows the terrain hit; one in the air follows a level plane at its
            // own height, so dragging a raised handle does not drop it to the ground on the first
            // frame — the same rule the path tool's own drag uses.
            if (handle.OnGround) {
                if (m_HasCursorPosition) {
                    editor.MoveHandle(handle.Id, m_CursorPosition);
                }

                return;
            }

            if (PathPicking.TryHitPlane(handle.Position.y, out float3 hit)) {
                editor.MoveHandle(handle.Id, hit);
            }
        }

        private void HandleShotHeight(ShotEditorBase editor) {
            if (m_HoveredHandleId < 0 && m_DraggedHandleId < 0) {
                return;
            }

            float step = Mod.Instance.Settings.PathHeightStep * UnityEngine.Time.unscaledDeltaTime
                         * kHeightStepsPerSecond;

            int target = (m_DraggedHandleId >= 0) ? m_DraggedHandleId : m_HoveredHandleId;

            if (Mod.PathRaiseAction != null && Mod.PathRaiseAction.IsPressed()) {
                editor.RaiseHandle(target, step);
            }

            if (Mod.PathLowerAction != null && Mod.PathLowerAction.IsPressed()) {
                editor.RaiseHandle(target, -step);
            }
        }

        /// <summary>Draws a shot the way a path is drawn, so both read the same.</summary>
        /// <remarks>
        /// The travelled line, its shadow on the ground and the camera frustums along it are the tool's
        /// vocabulary, not the path's — an orbit answers the same questions with them that a path does:
        /// where does the camera go, where is that over the ground, and what is it looking at. The
        /// editors supply geometry and the peculiar extras; everything shared happens here.
        /// </remarks>
        private void DrawShotEditing(ShotEditorBase editor) {
            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete();

            TerrainHeightData heights = m_TerrainSystem.GetHeightData();

            if (editor.TryPreview(m_ShotPreview)) {
                DrawShotLine(ref buffer, ref heights, editor.LineColor);
                DrawShotFrustums(ref buffer);
            }

            editor.Draw(ref buffer);

            foreach (ShotHandle handle in m_Handles) {
                bool hot = handle.Id == m_HoveredHandleId || handle.Id == m_DraggedHandleId;

                DrawStem(ref buffer, Lift(ref heights, handle.Position), handle.Position);
                DrawMarker(ref buffer, hot ? kHoverColor : kPointColor, handle.Position,
                           hot ? kSelectedDiameter : kPointDiameter);

                if (hot) {
                    DrawRing(ref buffer, kSelectedColor, handle.Position, kSelectedRingDiameter);
                }
            }
        }

        /// <summary>The line the camera travels, and its shadow on the ground beneath.</summary>
        /// <remarks>
        /// Camera-facing segments rather than overlay curves. A shot's travel is not a bezier the
        /// overlay renderer could take — an orbit is a spiral and a dolly is a straight run — and a
        /// flat ribbon would vanish edge-on, which is the view you are in while judging a height.
        /// <para>
        /// The shadow is a flat line on the terrain, because that IS a ground-plane shape and the
        /// overlay draws those properly. It answers where over the city the shot passes, which the
        /// airborne line alone cannot.
        /// </para>
        /// </remarks>
        private void DrawShotLine(ref OverlayRenderSystem.Buffer buffer, ref TerrainHeightData heights,
                                  Color color) {
            for (int i = 1; i < m_ShotPreview.Count; i++) {
                Vector3 from = m_ShotPreview[i - 1].Position;
                Vector3 to   = m_ShotPreview[i].Position;

                buffer.DrawLine(color, new Line3.Segment(from, to), kPathWidth * 0.5f, true);

                buffer.DrawLine(kShadowColor,
                                new Line3.Segment(Lift(ref heights, from), Lift(ref heights, to)),
                                kShadowWidth, false);
            }
        }

        /// <summary>View cones along the shot, at the same density whatever its keyframe count.</summary>
        /// <remarks>
        /// Stepped rather than drawn at every key. An orbit keyed every five degrees has seventy-two
        /// of them and a dolly has as many as you ask for, so drawing one per key turns the shot into
        /// a solid fan — the cones stop describing the aim and start hiding it.
        /// </remarks>
        private void DrawShotFrustums(ref OverlayRenderSystem.Buffer buffer) {
            if (!Mod.Instance.Settings.PathShowFrustums || m_ShotPreview.Count == 0) {
                return;
            }

            int step = Mathf.Max(1, Mathf.CeilToInt(m_ShotPreview.Count / (float)kShotFrustums));

            for (int i = 0; i < m_ShotPreview.Count; i += step) {
                Vector3 origin = m_ShotPreview[i].Position;
                Vector3 euler  = m_ShotPreview[i].Rotation;

                float half = HalfAngleFor(null);

                DrawFrustumEdge(ref buffer, kFrustumColor, origin, euler, -half);
                DrawFrustumEdge(ref buffer, kFrustumColor, origin, euler, half);
                DrawFrustumEdge(ref buffer, kFrustumColor, origin, euler, 0f);
            }
        }

        /// <remarks>Returns a locale key, not text. See <see cref="PathHints"/>.</remarks>
        private string DescribeShotApply() {
            foreach (ShotHandle handle in m_Handles) {
                if (handle.Id == m_HoveredHandleId) {
                    return handle.Hint;
                }
            }

            return PathHints.PlaceSubject;
        }
    }
}
