namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Mathematics;

    using Game.Net;

    using Unity.Entities;
    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>Builds a camera path along a road, rail or tram line already in the city.</summary>
    /// <remarks>
    /// A network is a far better source of camera moves than freehand drawing for anything that ought
    /// to follow the city rather than cut across it — a tram route, a river bridge, a highway sweep.
    /// Tracing one is exact where drawing one by eye is not, and it takes a click instead of thirty.
    /// <para>
    /// The game already models "this whole named road" as an <c>Aggregate</c>, with its member edges in
    /// an ordered <c>AggregateElement</c> buffer, so the traversal is a walk over that buffer rather
    /// than a graph search. The ordering is contiguous but not oriented: consecutive elements share a
    /// node, and either end of an edge may be the shared one, so each edge's direction has to be
    /// recovered by matching nodes as the walk proceeds. Sampling a reversed edge forwards is what
    /// makes a traced road zig-zag back on itself.
    /// </para>
    /// </remarks>
    public static class PathNetworkTracer {
        /// <summary>How finely each edge's curve is walked when measuring and sampling it.</summary>
        private const int kStepsPerEdge = 24;

        /// <summary>Traces the road an edge belongs to, producing evenly spaced points along it.</summary>
        /// <param name="entities">The world's entity manager.</param>
        /// <param name="edge">Any edge of the road, typically the one under the cursor.</param>
        /// <param name="from">Where on it to start, typically the cursor hit.</param>
        /// <param name="maxLength">How far to follow the road, in metres.</param>
        /// <param name="spacing">Roughly how far apart the produced points should be.</param>
        /// <param name="points">The traced centreline points, in travel order.</param>
        /// <returns>False when the entity is not part of an aggregated network.</returns>
        public static bool TryTrace(EntityManager entities, Entity edge, Vector3 from, float maxLength,
                                    float spacing, out List<Vector3> points) {
            points = new List<Vector3>();

            if (!TryGetChain(entities, edge, out List<Bezier4x3> chain, out int startIndex)) {
                return false;
            }

            // Start at the point on the clicked edge nearest the cursor rather than at that edge's
            // beginning, so a click halfway along a road starts the shot halfway along it.
            MathUtils.Distance(chain[startIndex], from, out float startT);

            float step     = Mathf.Max(spacing, 1f);
            float travelled = 0f;
            float budget    = Mathf.Max(maxLength, step);

            Vector3 previous = MathUtils.Position(chain[startIndex], startT);

            points.Add(previous);

            for (int i = startIndex; i < chain.Count && travelled < budget; i++) {
                float begin = (i == startIndex) ? startT : 0f;

                for (int s = 1; s <= kStepsPerEdge; s++) {
                    float t = Mathf.Lerp(begin, 1f, (float)s / kStepsPerEdge);

                    Vector3 current = MathUtils.Position(chain[i], t);
                    float   moved   = Vector3.Distance(previous, current);

                    travelled += moved;

                    // Points are placed by distance travelled, not per curve step, so the spacing is
                    // even whether the road is straight or a tight junction curve.
                    if (moved > 0f && travelled >= points.Count * step) {
                        points.Add(current);
                    }

                    previous = current;

                    if (travelled >= budget) {
                        break;
                    }
                }
            }

            return points.Count >= 2;
        }

        /// <summary>Collects the road's edges as curves in travel order, all pointing the same way.</summary>
        /// <param name="startIndex">Where in the chain the given edge sits.</param>
        private static bool TryGetChain(EntityManager entities, Entity edge, out List<Bezier4x3> chain,
                                        out int startIndex) {
            chain      = new List<Bezier4x3>();
            startIndex = 0;

            if (edge == Entity.Null || !entities.Exists(edge) ||
                !entities.HasComponent<Aggregated>(edge)) {
                return false;
            }

            Entity aggregate = entities.GetComponentData<Aggregated>(edge).m_Aggregate;

            if (aggregate == Entity.Null || !entities.HasBuffer<AggregateElement>(aggregate)) {
                return false;
            }

            DynamicBuffer<AggregateElement> elements =
                entities.GetBuffer<AggregateElement>(aggregate, true);

            // The shared node carried forward from the previous edge. Null until the first edge fixes
            // an orientation, which it does by simply being taken in its own direction.
            Entity carried = Entity.Null;

            for (int i = 0; i < elements.Length; i++) {
                Entity member = elements[i].m_Edge;

                if (!entities.HasComponent<Curve>(member) || !entities.HasComponent<Edge>(member)) {
                    continue;
                }

                Edge      ends  = entities.GetComponentData<Edge>(member);
                Bezier4x3 curve = entities.GetComponentData<Curve>(member).m_Bezier;

                bool reversed = carried != Entity.Null && ends.m_End == carried;

                if (reversed) {
                    curve = Reverse(curve);
                }

                carried = reversed ? ends.m_Start : ends.m_End;

                if (member == edge) {
                    startIndex = chain.Count;
                }

                chain.Add(curve);
            }

            return chain.Count > 0;
        }

        private static Bezier4x3 Reverse(Bezier4x3 curve) {
            return new Bezier4x3 { a = curve.d, b = curve.c, c = curve.b, d = curve.a };
        }
    }
}
