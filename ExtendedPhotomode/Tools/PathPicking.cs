namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using Unity.Mathematics;

    using UnityEngine;
    using UnityEngine.InputSystem;

    #endregion

    /// <summary>
    /// Picks path handles and points by casting the mouse ray at them, the way Network Tools does.
    /// </summary>
    /// <remarks>
    /// The path tool originally compared each handle to the terrain raycast hit in XZ only, which is
    /// why handles were so hard to grab. Three things were wrong with it: the cursor had to be over
    /// terrain at all, height was ignored entirely so a handle lifted 50m up was matched by the ground
    /// beneath it, and the tolerance was a fixed world distance — perfectly usable up close and
    /// sub-pixel once you zoomed out.
    /// <para>
    /// Testing the mouse ray against a sphere fixes all three: it works wherever the camera is
    /// pointing, it respects height, and picking the smallest <c>t</c> resolves overlapping handles to
    /// whichever is actually nearest the camera rather than whichever the loop reached first.
    /// </para>
    /// </remarks>
    public static class PathPicking {
        /// <summary>Builds the ray under the mouse cursor.</summary>
        /// <param name="origin">Ray origin, in world space.</param>
        /// <param name="direction">Ray direction, normalised by Unity.</param>
        /// <returns><c>false</c> when there is no main camera or no mouse.</returns>
        public static bool TryGetMouseRay(out float3 origin, out float3 direction) {
            origin    = float3.zero;
            direction = float3.zero;

            Camera camera = Camera.main;

            if (camera == null || Mouse.current == null) {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            origin    = ray.origin;
            direction = ray.direction;
            return true;
        }

        /// <summary>Intersects a ray with a sphere.</summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="direction">Ray direction.</param>
        /// <param name="centre">Sphere centre.</param>
        /// <param name="radius">Sphere radius.</param>
        /// <param name="distance">Distance along the ray to the near hit.</param>
        /// <returns><c>true</c> when the ray hits in front of the origin.</returns>
        public static bool TryHitSphere(float3 origin, float3 direction, float3 centre, float radius,
                                        out float distance) {
            distance = float.MaxValue;

            float3 toCentre = origin - centre;
            float  a        = math.dot(direction, direction);
            float  b        = 2f * math.dot(toCentre, direction);
            float  c        = math.dot(toCentre, toCentre) - radius * radius;
            float  d        = b * b - 4f * a * c;

            if (d < 0f) {
                return false;
            }

            distance = (-b - math.sqrt(d)) / (2f * a);
            return distance >= 0f;
        }

        /// <summary>How far a point lies from a ray, measured perpendicular to it.</summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="direction">Ray direction, normalised.</param>
        /// <param name="point">The point to measure.</param>
        /// <remarks>
        /// For picking something that has no radius to intersect against — a sampled point on a curve,
        /// where a sphere per sample would be both arbitrary and slow. Points behind the camera measure
        /// from the origin instead of from the ray's line, or a curve running away behind the viewer
        /// would report as a near miss.
        /// </remarks>
        public static float DistanceToRay(float3 origin, float3 direction, float3 point) {
            float3 delta = point - origin;
            float  along = math.dot(delta, direction);

            if (along <= 0f) {
                return math.length(delta);
            }

            return math.length(delta - direction * along);
        }

        /// <summary>Intersects the mouse ray with a horizontal plane at <paramref name="planeY"/>.</summary>
        /// <param name="planeY">World height of the plane.</param>
        /// <param name="hit">Where the ray crosses it.</param>
        /// <returns><c>false</c> when the ray is parallel to the plane or points away from it.</returns>
        /// <remarks>
        /// Dragging reads from this rather than from the terrain hit, so a handle keeps tracking the
        /// cursor when it is dragged out over water, off the map, or above a hill that the old
        /// terrain-relative drag would have snapped it onto.
        /// </remarks>
        public static bool TryHitPlane(float planeY, out float3 hit) {
            hit = float3.zero;

            if (!TryGetMouseRay(out float3 origin, out float3 direction)) {
                return false;
            }

            if (math.abs(direction.y) < 0.0001f) {
                return false;
            }

            float t = (planeY - origin.y) / direction.y;

            if (t < 0f) {
                return false;
            }

            hit = origin + direction * t;
            return true;
        }
    }
}
