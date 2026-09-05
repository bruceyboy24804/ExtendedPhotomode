namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Components;

    using Unity.Collections;
    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>List keeping for <see cref="EPM_ShotListSystem"/>: ordering, ids and the UI list.</summary>
    public partial class EPM_ShotListSystem {
        /// <summary>The shots in playing order.</summary>
        /// <remarks>
        /// Sorted by the stored order field, with the id as the tie-break so the result is stable. An
        /// unstable sort here would show the list in one arrangement and assemble it in another.
        /// </remarks>
        private List<Entity> Ordered() {
            var entities = new List<Entity>();

            using (NativeArray<Entity> all = m_Query.ToEntityArray(Allocator.Temp)) {
                entities.AddRange(all);
            }

            entities.Sort((a, b) => {
                EPM_Shot left  = EntityManager.GetComponentData<EPM_Shot>(a);
                EPM_Shot right = EntityManager.GetComponentData<EPM_Shot>(b);

                int order = left.m_Order.CompareTo(right.m_Order);

                return (order != 0) ? order : left.m_Id.CompareTo(right.m_Id);
            });

            return entities;
        }

        /// <summary>The shots that are actually in the cut, in order.</summary>
        /// <remarks>
        /// What <c>Assemble</c> builds from. Everything else — the list, reordering, renaming — works
        /// on <see cref="Ordered"/> and sees every shot, because a shot in the bin is still a shot you
        /// can rename, edit and drag back in.
        /// </remarks>
        private List<Entity> InSequence() {
            return Ordered().FindAll(e => EntityManager.GetComponentData<EPM_Shot>(e).m_InSequence);
        }

        private ShotListEntry[] BuildList() {
            List<Entity> ordered = Ordered();

            var entries = new ShotListEntry[ordered.Count];
            float start = 0f;

            for (int i = 0; i < ordered.Count; i++) {
                EPM_Shot shot = EntityManager.GetComponentData<EPM_Shot>(ordered[i]);

                entries[i] = new ShotListEntry {
                    id       = shot.m_Id,
                    name     = shot.m_Name.ToString(),
                    type     = shot.m_Type,
                    duration = shot.m_Duration,
                    spacing  = SpacingOf(shot),

                    inSequence = shot.m_InSequence,

                    // The measured start from the last assembly where there is one, and the running
                    // total only as a fallback before anything has been assembled. These positions
                    // place blocks on the track, so a predicted figure that drifts past the end of
                    // the view gets its block culled and the shot vanishes.
                    //
                    // A shot in the bin reports -1 rather than a time: it has no place on the
                    // timeline, and giving it the next slot would draw a block where nothing plays.
                    start = !shot.m_InSequence
                                ? -1f
                                : m_Starts.TryGetValue(shot.m_Id, out float measured)
                                    ? measured
                                    : start,
                    points = EntityManager.HasBuffer<EPM_PathNodeData>(ordered[i])
                                 ? EntityManager.GetBuffer<EPM_PathNodeData>(ordered[i], true).Length
                                 : 0,
                };

                // The shot's own approach counts towards where the next one begins; the chaining gap
                // does not, because it is a live setting rather than anything stored per shot. This
                // was already a rough readout and stays one — it is a label, not the assembler.
                if (shot.m_InSequence) {
                    start += shot.m_Duration + shot.m_TransitionIn;
                }
            }

            return entries;
        }

        private void RemoveShot(int id) {
            Entity entity = FindById(id);

            if (entity == Entity.Null) {
                return;
            }

            EntityManager.DestroyEntity(entity);
            Renumber();

            m_ShotsBinding.Update();
        }

        private void RenameShot(int id, string name) {
            Entity entity = FindById(id);
            string trimmed = (name ?? string.Empty).Trim();

            if (entity == Entity.Null || trimmed.Length == 0) {
                return;
            }

            EPM_Shot shot = EntityManager.GetComponentData<EPM_Shot>(entity);

            shot.m_Name = new FixedString128Bytes(trimmed);
            EntityManager.SetComponentData(entity, shot);

            m_ShotsBinding.Update();
        }

        /// <summary>Moves a shot up or down the list.</summary>
        /// <param name="delta">-1 to move earlier, 1 to move later.</param>
        /// <remarks>
        /// Implemented as a swap of the two order values rather than by rewriting the whole list,
        /// which keeps the operation correct even if the numbers have gaps in them.
        /// </remarks>
        private void MoveShot(int id, int delta) {
            List<Entity> ordered = Ordered();
            int          index   = ordered.FindIndex(e =>
                                       EntityManager.GetComponentData<EPM_Shot>(e).m_Id == id);

            if (index < 0) {
                return;
            }

            int target = index + Math.Sign(delta);

            if (target < 0 || target >= ordered.Count) {
                return;
            }

            EPM_Shot a = EntityManager.GetComponentData<EPM_Shot>(ordered[index]);
            EPM_Shot b = EntityManager.GetComponentData<EPM_Shot>(ordered[target]);

            (a.m_Order, b.m_Order) = (b.m_Order, a.m_Order);

            EntityManager.SetComponentData(ordered[index], a);
            EntityManager.SetComponentData(ordered[target], b);

            m_ShotsBinding.Update();
        }

        /// <summary>Puts a shot into the cut or takes it back out, and rebuilds the timeline.</summary>
        /// <remarks>
        /// Assembling here rather than leaving it to a button is the point of the whole arrangement:
        /// dropping a shot onto the timeline should put it on the timeline. Note that this rebuilds
        /// every curve, so any tangent pulled by hand in the curve editor is lost — which is why undo
        /// had to exist first. One press brings it back.
        /// </remarks>
        private void SetInSequence(int id, bool inSequence) {
            DropShot(id, -1, inSequence);
        }

        /// <summary>Lands a shot at a position, in or out of the cut, and rebuilds once.</summary>
        /// <remarks>
        /// One entry point for both halves of a drop, because a drag can change the order AND the
        /// membership at the same time — pulling a binned shot onto the track between two others is
        /// one gesture, and doing it as two triggers would assemble twice and flash the timeline
        /// through an arrangement the user never asked for.
        /// <para>
        /// <paramref name="position"/> is an index into the WHOLE ordered list, not into the cut. The
        /// caller knows the global index because the list binding is globally ordered; deriving it
        /// here from a cut-relative index would need the same lookup, done twice. Pass -1 to leave
        /// the order alone and change only membership.
        /// </para>
        /// </remarks>
        private void DropShot(int id, int position, bool inSequence) {
            Entity entity = FindById(id);

            if (entity == Entity.Null) {
                return;
            }

            EPM_Shot shot    = EntityManager.GetComponentData<EPM_Shot>(entity);
            bool     changed = false;

            if (shot.m_InSequence != inSequence) {
                shot.m_InSequence = inSequence;
                EntityManager.SetComponentData(entity, shot);
                changed = true;
            }

            if (position >= 0) {
                changed |= Reorder(id, position);
            }

            if (!changed) {
                return;
            }

            m_ShotsBinding.Update();
            Assemble();
        }

        /// <summary>Reads a shot's key density in whatever unit its own type counts in.</summary>
        private static int SpacingOf(EPM_Shot shot) {
            switch ((ShotType)shot.m_Type) {
                case ShotType.Orbit:     return shot.m_OrbitDegreesPerKey;
                case ShotType.DollyZoom: return shot.m_DollyKeys;
                default:                 return shot.m_PathMetresPerKey;
            }
        }

        /// <summary>Edits one number on an existing shot and rebuilds the timeline around it.</summary>
        /// <param name="id">The shot to change.</param>
        /// <param name="field">Either <c>duration</c> or <c>spacing</c>.</param>
        /// <param name="value">The new value.</param>
        /// <remarks>
        /// This writes onto the SHOT, not onto the settings. The settings describe the next shot to
        /// be generated, so changing them leaves everything already on the timeline as it was —
        /// which is why editing a duration used to appear to do nothing until the shot was made
        /// again.
        /// <para>
        /// Re-assembles immediately, so the curves and the track blocks move as the value changes.
        /// Assemble rewrites the whole cut rather than one shot, because a duration change moves
        /// every shot after it along the timeline anyway.
        /// </para>
        /// </remarks>
        private void SetShotNumber(int id, string field, float value) {
            Entity entity = FindById(id);

            if (entity == Entity.Null) {
                return;
            }

            EPM_Shot shot = EntityManager.GetComponentData<EPM_Shot>(entity);

            if (field == "duration") {
                if (Mathf.Approximately(shot.m_Duration, value)) {
                    return;
                }

                shot.m_Duration = value;
            } else if (field == "spacing") {
                int rounded = Mathf.RoundToInt(value);

                if (SpacingOf(shot) == rounded) {
                    return;
                }

                switch ((ShotType)shot.m_Type) {
                    case ShotType.Orbit:     shot.m_OrbitDegreesPerKey = rounded; break;
                    case ShotType.DollyZoom: shot.m_DollyKeys          = rounded; break;
                    default:                 shot.m_PathMetresPerKey   = rounded; break;
                }
            } else {
                m_Log.Warn($"Unknown shot field \"{field}\".");
                return;
            }

            EntityManager.SetComponentData(entity, shot);

            // Only worth rebuilding when the shot is actually on the timeline. A binned shot keeps
            // the new value and applies it whenever it is dragged in.
            if (shot.m_InSequence) {
                Assemble();
            }

            m_ShotsBinding.Update();
        }

        /// <summary>Empties the timeline and takes every shot back out of the cut.</summary>
        /// <remarks>
        /// What the Reset button does. Clearing the curves without clearing the cut left the two
        /// disagreeing — an empty timeline with a full track underneath it, and the next reorder
        /// would assemble the whole cut straight back.
        /// <para>
        /// Nothing is destroyed: the shots return to the generated list and can be dragged back in.
        /// Reset is Harmony-hooked for history too, so Ctrl+Z restores the curves.
        /// </para>
        /// <para>
        /// Membership is written directly rather than through <see cref="DropShot"/> per shot, which
        /// would re-assemble once per row and walk the timeline back through every intermediate cut.
        /// </para>
        /// </remarks>
        private void ResetTimeline() {
            foreach (Entity entity in Ordered()) {
                EPM_Shot shot = EntityManager.GetComponentData<EPM_Shot>(entity);

                if (!shot.m_InSequence) {
                    continue;
                }

                shot.m_InSequence = false;
                EntityManager.SetComponentData(entity, shot);
            }

            m_Starts.Clear();

            // After the membership, so the empty-cut branch of Assemble is what clears the curves —
            // one definition of "an empty cut means an empty timeline", used by both routes in.
            Assemble();

            m_ShotsBinding.Update();
        }

        /// <summary>Moves a shot to an explicit position in the order, and rebuilds the timeline.</summary>
        /// <remarks>
        /// The drag counterpart of <see cref="MoveShot"/>'s one-step nudge. It works in stored order
        /// values rather than list indices: the list arrives sorted, so an index is only meaningful
        /// against the arrangement the caller happened to be looking at, and it stops being true the
        /// moment anything moves. The path library learned this about rows, and the shot list has the
        /// same shape.
        /// </remarks>
        private void ReorderShot(int id, int position) {
            if (Reorder(id, position)) {
                m_ShotsBinding.Update();
                Assemble();
            }
        }

        /// <summary>Moves a shot to a position in the order. Returns whether anything changed.</summary>
        private bool Reorder(int id, int position) {
            List<Entity> ordered = Ordered();
            int          index   = ordered.FindIndex(e =>
                                       EntityManager.GetComponentData<EPM_Shot>(e).m_Id == id);

            if (index < 0) {
                return false;
            }

            int target = Math.Min(Math.Max(position, 0), ordered.Count - 1);

            if (target == index) {
                return false;
            }

            Entity moved = ordered[index];
            ordered.RemoveAt(index);
            ordered.Insert(target, moved);

            for (int i = 0; i < ordered.Count; i++) {
                EPM_Shot shot = EntityManager.GetComponentData<EPM_Shot>(ordered[i]);

                if (shot.m_Order != i) {
                    shot.m_Order = i;
                    EntityManager.SetComponentData(ordered[i], shot);
                }
            }

            return true;
        }

        /// <summary>Rewrites the order values to 0..n-1, closing any gaps.</summary>
        private void Renumber() {
            List<Entity> ordered = Ordered();

            for (int i = 0; i < ordered.Count; i++) {
                EPM_Shot shot = EntityManager.GetComponentData<EPM_Shot>(ordered[i]);

                if (shot.m_Order == i) {
                    continue;
                }

                shot.m_Order = i;
                EntityManager.SetComponentData(ordered[i], shot);
            }
        }

        private Entity FindById(int id) {
            using (NativeArray<Entity> all = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in all) {
                    if (EntityManager.GetComponentData<EPM_Shot>(entity).m_Id == id) {
                        return entity;
                    }
                }
            }

            return Entity.Null;
        }

        // Ids are handed out above the highest in use rather than by counting, so removing a shot
        // cannot make the next one reuse a live id.
        private int NextId() {
            int highest = 0;

            using (NativeArray<Entity> all = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in all) {
                    highest = Math.Max(highest, EntityManager.GetComponentData<EPM_Shot>(entity).m_Id);
                }
            }

            return highest + 1;
        }

        private int NextOrder() {
            int highest = -1;

            using (NativeArray<Entity> all = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in all) {
                    highest = Math.Max(highest,
                                       EntityManager.GetComponentData<EPM_Shot>(entity).m_Order);
                }
            }

            return highest + 1;
        }
    }
}
