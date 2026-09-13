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

        /// <summary>Diameter of the tick marking one generated keyframe on the shot line.</summary>
        /// <remarks>
        /// Deliberately smaller than <c>kPointDiameter</c>, and drawn without the stem and ring that a
        /// <see cref="ShotHandle"/> gets. These are a readout, not something you can grab, and a marker
        /// that looks draggable and is not is worse than no marker at all.
        /// </remarks>
        private const float kKeyTickDiameter = 2f;

        /// <summary>Brighter than the shot line so the ticks read against it, and the same hue so they
        /// belong to it.</summary>
        private static readonly Color kKeyTickColor = new Color(1f, 1f, 1f, 0.75f);

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

            // Against last frame's preview: it is filled by DrawShotEditing at the end of this method,
            // so the alternative is solving the shot twice a frame to make a hover test one frame
            // fresher. Not worth it — the ticks have not moved in the meantime unless something else
            // changed the shot, and that redraws anyway.
            m_HoveredShotKey = FindHoveredShotKey(editor);

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
            // Ahead of the handles, and safe to be: FindHoveredShotKey already yields to them, so a
            // drag can only start here when the cursor is on a tick and on nothing else.
            if (m_DraggingShotKey) {
                if (!applyAction.IsPressed()) {
                    m_DraggingShotKey = false;
                    m_DraggedShotKey  = -1;
                    return;
                }

                // Against a level plane at the tick's own height, the rule the shot handles use for
                // anything not on the ground: an orbit key sits at the shot's height, and following
                // the terrain hit instead would read the bearing from a point somewhere below it.
                if (PathPicking.TryHitPlane(m_ShotPreview[Mathf.Min(m_DraggedShotKey,
                                                                   m_ShotPreview.Count - 1)].Position.y,
                                            out float3 hit)) {
                    editor.SpaceKeys(m_DraggedShotKey, hit);
                }

                return;
            }

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

            if (m_HoveredShotKey >= 1) {
                m_DraggedShotKey  = m_HoveredShotKey;
                m_DraggingShotKey = true;
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
                DrawShotKeys(ref buffer, editor);
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
        /// <summary>Marks every keyframe the shot will generate, on the line it will fly.</summary>
        /// <remarks>
        /// <para>
        /// The keys were always there — <see cref="m_ShotPreview"/> IS the solved keyframe list, and
        /// the shot line is drawn by joining consecutive entries of it — but nothing marked them, so
        /// key density was the one shot parameter with no representation in the world at all. Changing
        /// "Key every" moved a number and altered nothing you could see: the line is identical either
        /// way, and the frustums are capped at <see cref="kShotFrustums"/> and decimated, so their
        /// spacing deliberately says nothing about the keys.
        /// </para>
        /// <para>
        /// Drawn as plain ticks rather than as handles. They are honest feedback about what will land
        /// on the timeline, and dense ticks reading as a near-solid line is the correct answer to a
        /// spacing that is too fine, not a rendering fault to hide.
        /// </para>
        /// </remarks>
        private void DrawShotKeys(ref OverlayRenderSystem.Buffer buffer, ShotEditorBase editor) {
            // Nothing is drawn while the preview is thinned, because then these samples are a drawing
            // of the shot rather than its keys — a tick each would report a key count the shot is not
            // going to generate. At that density (past kPreviewKeys) the ticks would merge into a
            // solid line and say nothing anyway, so there is no readout being given up.
            if (editor.PreviewThinned) {
                return;
            }

            // The span being divided, from the dragged key round to the end handle. Straight dashes
            // between consecutive keys rather than a dashed bezier: the shot line itself is already
            // drawn as chords between these same samples, so a smooth curve here would not lie along
            // the line it is annotating.
            if (m_DraggingShotKey && m_DraggedShotKey >= 1) {
                for (int i = Mathf.Min(m_DraggedShotKey, m_ShotPreview.Count - 1);
                     i < m_ShotPreview.Count - 1; i++) {
                    buffer.DrawDashedLine(kKeyGuideColor,
                                          new Line3.Segment(m_ShotPreview[i].Position,
                                                            m_ShotPreview[i + 1].Position),
                                          kKeyGuideWidth, kKeyGuideDash, kKeyGuideGap);
                }
            }

            for (int i = 0; i < m_ShotPreview.Count; i++) {
                bool hot = editor.HasKeySpacing &&
                           (i == m_HoveredShotKey || (m_DraggingShotKey && i == m_DraggedShotKey));

                DrawMarker(ref buffer, hot ? kHoverColor : kKeyTickColor, m_ShotPreview[i].Position,
                           hot ? kKeyTickDiameter * 2f : kKeyTickDiameter);
            }
        }

        /// <summary>Which key tick on the shot line the cursor is over, or -1.</summary>
        /// <remarks>
        /// Offered only when the ticks are truthful and the editor has a spacing to set. Yields to the
        /// shot's own handles: the first key sits exactly under the start handle, and moving the shot
        /// always outranks retuning its key density.
        /// </remarks>
        private int FindHoveredShotKey(ShotEditorBase editor) {
            if (editor == null || !editor.HasKeySpacing || editor.PreviewThinned ||
                m_HoveredHandleId >= 0 || m_ShotPreview.Count < 3 ||
                !PathPicking.TryGetMouseRay(out float3 origin, out float3 direction)) {
                return -1;
            }

            int   best    = -1;
            float nearest = float.MaxValue;

            for (int i = 1; i < m_ShotPreview.Count; i++) {
                if (PathPicking.TryHitSphere(origin, direction, m_ShotPreview[i].Position,
                                             kKeyPickRadius, out float t) && t < nearest) {
                    nearest = t;
                    best    = i;
                }
            }

            return best;
        }

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

            if (m_HoveredShotKey >= 1) {
                return PathHints.SpaceKeys;
            }

            return PathHints.PlaceSubject;
        }
    }
}
