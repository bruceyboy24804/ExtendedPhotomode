namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Collections;
    using Colossal.Mathematics;

    using Game.Common;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>Finds how high the world stands under each point of a path.</summary>
    /// <remarks>
    /// Queries the game's own static object quadtree rather than raycasting. A raycast answers "is
    /// there something along this line", which is the wrong question: a camera path needs to know how
    /// FAR to rise, and a tree of bounds answers that directly by handing back the tallest thing near
    /// each sample. It is also one traversal for the whole path instead of a cast per sample.
    /// <para>
    /// Bounds, not meshes. A building's quadtree bounds are its bounding box, so clearance is measured
    /// against a box that is at worst slightly larger than the building — which errs towards flying a
    /// little higher than strictly necessary, and that is the right way for this to be wrong.
    /// </para>
    /// </remarks>
    public static class PathObstacles {
        /// <summary>How far either side of a sample counts as "in the way", in metres.</summary>
        private const float kCorridor = 12f;

        /// <summary>Collects the height of the tallest obstruction near each point.</summary>
        /// <param name="entities">The world's entity manager, used to reach the search system.</param>
        /// <param name="world">The world holding <c>Game.Objects.SearchSystem</c>.</param>
        /// <param name="points">The path samples to test.</param>
        /// <param name="heights">
        /// Per-point obstruction height, or <see cref="float.MinValue"/> where nothing is near.
        /// </param>
        public static void Measure(World world, IReadOnlyList<Vector3> points, List<float> heights) {
            heights.Clear();

            for (int i = 0; i < points.Count; i++) {
                heights.Add(float.MinValue);
            }

            var search = world.GetExistingSystemManaged<Game.Objects.SearchSystem>();

            if (search == null || points.Count == 0) {
                return;
            }

            NativeQuadTree<Entity, QuadTreeBoundsXZ> tree =
                search.GetStaticSearchTree(true, out JobHandle dependencies);

            // The tree is written by jobs that may still be in flight. Reading it without completing
            // them is a race that shows up as garbage bounds rather than as a crash.
            dependencies.Complete();

            for (int i = 0; i < points.Count; i++) {
                Vector3 point = points[i];

                var iterator = new TallestIterator {
                    m_Area = new Bounds3(new float3(point.x - kCorridor, -10000f, point.z - kCorridor),
                                         new float3(point.x + kCorridor, 10000f, point.z + kCorridor)),
                    m_Top = float.MinValue,
                };

                tree.Iterate(ref iterator);

                heights[i] = iterator.m_Top;
            }
        }

        /// <summary>Keeps the highest top surface of anything overlapping a column of world.</summary>
        private struct TallestIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>,
                                         IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
            public Bounds3 m_Area;

            public float m_Top;

            public bool Intersect(QuadTreeBoundsXZ bounds) {
                return MathUtils.Intersect(bounds.m_Bounds, m_Area);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity item) {
                if (MathUtils.Intersect(bounds.m_Bounds, m_Area)) {
                    m_Top = math.max(m_Top, bounds.m_Bounds.max.y);
                }
            }
        }
    }
}
