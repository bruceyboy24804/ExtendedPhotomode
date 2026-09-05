namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;

    using Colossal.UI.Binding;

    using Game.CinematicCamera;
    using Game.Rendering;
    using Game.UI.InGame;

    using ModsCommon.Extensions;
    using ModsCommon.Systems;
    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>Bounds-checked replacements for vanilla's two keyframe-editing calls.</summary>
    /// <remarks>
    /// <para>
    /// The timeline panel used to write through <c>cinematicCamera/moveKeyFrame</c> and
    /// <c>removeKeyFrame</c> directly. Both of those index without checking, and both are reachable
    /// with an index that has since gone stale — which throws inside the binding callback rather than
    /// failing quietly, and takes the UI (and reportedly the game) with it.
    /// </para>
    /// <para>
    /// There are two distinct ways to get there, and the second is the one that actually bites:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <c>CinematicCameraSequence.MoveKeyframe</c> reads <c>curve[index]</c> with no range check — it
    /// guards a null curve and nothing else. The panel holds a snapshot of the curves from the last
    /// binding push, so any edit that lands between the push and the write leaves it addressing a key
    /// the curve no longer has.
    /// </description></item>
    /// <item><description>
    /// Modifiers are addressed by their POSITION in <c>activeSequence.modifiers</c>, and that list
    /// changes length during ordinary editing: <c>RemoveModifierKey</c> calls <c>RemoveModifier</c>
    /// when a curve loses its last key, dropping the whole entry. Delete the only key on, say, Time of
    /// Day — which double-clicking it on the graph does — and every modifier below it shifts up one.
    /// The panel's stored positions are now all off by one, so the next drag writes into the WRONG
    /// curve, and the last lane's position runs off the end of the array and throws.
    /// </description></item>
    /// </list>
    /// <para>
    /// So the fix is not only a bounds check. Curves are addressed here by <em>id</em> and the index
    /// is resolved against the live sequence at the moment of the write, which is the same lesson the
    /// path library learned: a position in a list that anything can reorder is not an address.
    /// Transforms keep their numeric index because <c>TransformCurveKey</c> is a fixed five-slot enum
    /// that nothing removes from — but note vanilla's <c>GetTransformCurves</c> COMPACTS null curves
    /// out of what the UI receives, so even there the panel maps the id back to its canonical slot
    /// rather than trusting the array position it arrived at.
    /// </para>
    /// <para>
    /// Everything else is vanilla's own handler, step for step, so playback, saving and the vanilla
    /// curve editor are unaffected: mutate, push the matching binding, re-evaluate at the playhead.
    /// </para>
    /// </remarks>
    public partial class EPM_TimelineEditSystem : CommonUISystemBase {
        public const string kMoveKeyBinding = "timelineMoveKey";

        public const string kRemoveKeyBinding = "timelineRemoveKey";

        /// <summary>Vanilla's private playhead time on the cinematic UI system.</summary>
        private const string kTimeField = "t";

        private CinematicCameraUISystem m_Cinematic;

        private EPM_ShotSequenceSystem m_Shots;

        private PhotoModeRenderSystem m_PhotoMode;

        private CameraUpdateSystem m_Cameras;

        protected override string ModId => Mod.Instance.Id;

        protected override void OnCreate() {
            base.OnCreate();

            m_Cinematic = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_Shots     = World.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>();
            m_PhotoMode = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_Cameras   = World.GetOrCreateSystemManaged<CameraUpdateSystem>();

            // Raw AddBinding rather than the CommonUISystemBase helpers: those top out at four
            // arguments and cannot return a value, and this has to be a call so the panel can learn
            // the index a moved key ended up at. `Keyframe` reads straight off the wire — vanilla's
            // own moveKeyFrame is declared with exactly this shape.
            AddBinding(new CallBinding<string, int, Keyframe, int>(ModId, kMoveKeyBinding, MoveKey));
            AddBinding(new TriggerBinding<string, int>(ModId, kRemoveKeyBinding, RemoveKey));
        }

        /// <summary>Moves a key, and reports the index it ended up at, or -1 if nothing was written.</summary>
        /// <param name="curveId">The curve's id: a <c>TransformCurveKey</c> name, or a modifier id.</param>
        /// <param name="index">The key's index within that curve.</param>
        /// <param name="keyframe">The key as it should end up.</param>
        private int MoveKey(string curveId, int index, Keyframe keyframe) {
            if (!TryResolve(curveId, out CinematicCameraSequence sequence, out bool transform, out int curveIndex)) {
                return -1;
            }

            CinematicCameraSequence.CinematicCameraCurveModifier modifier =
                transform ? sequence.transforms[curveIndex] : sequence.modifiers[curveIndex];

            if (modifier.curve == null || index < 0 || index >= modifier.curve.length) {
                m_Log.Warn($"Ignoring a move of key {index} on '{curveId}', which has "
                         + $"{modifier.curve?.length ?? 0} keys. The panel is a push behind.");
                return -1;
            }

            int moved;

            try {
                moved = sequence.MoveKeyframe(modifier, index, keyframe);
            } catch (Exception error) {
                // The bounds are checked above, but MoveKeyframe also runs EnsureLoop and
                // PatchRotations, and a throw from either would otherwise escape into the binding
                // callback — which is the failure this whole system exists to stop.
                m_Log.Error($"Moving key {index} on '{curveId}' failed: {error}");
                return -1;
            }

            Push(sequence, transform);
            return moved;
        }

        /// <summary>Removes a key.</summary>
        /// <param name="curveId">The curve's id: a <c>TransformCurveKey</c> name, or a modifier id.</param>
        /// <param name="index">The key's index within that curve.</param>
        private void RemoveKey(string curveId, int index) {
            if (!TryResolve(curveId, out CinematicCameraSequence sequence, out bool transform, out int curveIndex)) {
                return;
            }

            try {
                if (transform) {
                    // Already bounds-checked inside, and silently does nothing when it is out of
                    // range — the one vanilla path here that was never a hazard.
                    sequence.RemoveCameraTransform(curveIndex, index);
                } else {
                    // By id, which is what RemoveModifierKey wants anyway. Its own index check drops
                    // an out-of-range removal rather than throwing, so this only has to get the curve
                    // right. Removing the last key drops the modifier from the list entirely, which is
                    // exactly why nothing here may hold a position across a call.
                    sequence.RemoveModifierKey(sequence.modifiers[curveIndex].id, index);
                }
            } catch (Exception error) {
                m_Log.Error($"Removing key {index} on '{curveId}' failed: {error}");
                return;
            }

            Push(sequence, transform);
        }

        /// <summary>Finds a curve by id in the live sequence.</summary>
        /// <param name="curveId">A <c>TransformCurveKey</c> member name, or a modifier id.</param>
        /// <param name="sequence">The active sequence.</param>
        /// <param name="transform">Whether the curve is one of the five camera transforms.</param>
        /// <param name="curveIndex">Its index in the matching collection, resolved just now.</param>
        /// <returns>Whether a curve was found.</returns>
        private bool TryResolve(string curveId, out CinematicCameraSequence sequence, out bool transform,
                                out int curveIndex) {
            transform  = false;
            curveIndex = -1;
            sequence   = m_Cinematic?.activeSequence;

            if (sequence == null) {
                m_Log.Warn($"No active sequence; dropping an edit to '{curveId}'.");
                return false;
            }

            if (string.IsNullOrEmpty(curveId)) {
                m_Log.Warn("An edit arrived with no curve id.");
                return false;
            }

            if (Enum.TryParse(curveId, out CinematicCameraSequence.TransformCurveKey key)
             && (int)key >= 0 && (int)key < sequence.transforms.Length) {
                transform  = true;
                curveIndex = (int)key;
                return true;
            }

            curveIndex = sequence.modifiers.FindIndex(candidate => candidate.id == curveId);

            if (curveIndex < 0) {
                // Not an error: deleting a modifier's last key removes the modifier, so a second edit
                // from the same drag can legitimately arrive after its curve has gone.
                m_Log.Debug($"No curve '{curveId}' on the sequence; dropping the edit.");
                return false;
            }

            return true;
        }

        /// <summary>Pushes the changed curves to the UI and re-evaluates at the playhead.</summary>
        /// <remarks>
        /// What vanilla's own handlers do after a mutation, and it has to happen here too: the curve
        /// bindings are plain <c>ValueBinding</c>s that only reach the UI when something calls Update,
        /// and the Refresh is what moves the live camera onto the edited curve.
        /// </remarks>
        /// <param name="sequence">The sequence that was changed.</param>
        /// <param name="transform">Whether a transform curve changed, rather than a modifier.</param>
        private void Push(CinematicCameraSequence sequence, bool transform) {
            if (transform) {
                m_Shots.RefreshTransformCurveBinding();
            } else {
                m_Shots.RefreshModifierCurveBinding();
            }

            // Skipped rather than defaulted to zero on a failed read: a wrong time here would jump the
            // playhead, which is worse than not re-evaluating.
            if (m_Cinematic.GetMemberValue(kTimeField) is float at) {
                sequence.Refresh(at, m_PhotoMode.photoModeProperties, m_Cameras.activeCameraController);
            }
        }
    }
}
