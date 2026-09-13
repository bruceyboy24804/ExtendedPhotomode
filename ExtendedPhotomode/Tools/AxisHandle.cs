namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using Game.Rendering;

    using Unity.Mathematics;

    using UnityEngine;
    using UnityEngine.InputSystem;

    #endregion

    /// <summary>One value expressed as a grip sliding in and out along a fixed line.</summary>
    /// <remarks>
    /// <para>
    /// The tool's existing handles are POSITIONS: you drag them somewhere and the thing goes there.
    /// This is the other kind — a grip constrained to one axis, where the DISTANCE from an origin is
    /// the number being edited. That is what lets a radius, a focal length, a height or a key spacing
    /// be dragged at all: none of them is a place, and dragging them freely on a plane would mean
    /// inventing a meaning for the sideways component of the gesture.
    /// </para>
    /// <para>
    /// Modelled on the Network Tools mod's axis handle, which is the same idea: an origin, a
    /// constrained direction and a grip, drawn as a dot, a connector and a circle. Its proportions are
    /// copied deliberately in <see cref="Draw"/> — matching them is what makes a handle of ours read
    /// as the same class of object rather than as a lookalike.
    /// </para>
    /// </remarks>
    public struct AxisHandle {
        /// <summary>Identifies the handle to whatever produced it.</summary>
        public int Id;

        /// <summary>Where the axis starts. The grip's distance is measured from here.</summary>
        public Vector3 Origin;

        /// <summary>Unit direction the grip slides along.</summary>
        public Vector3 Axis;

        /// <summary>How far along the axis the grip currently sits.</summary>
        public float Distance;

        /// <summary>Locale key describing what dragging it does.</summary>
        public string Hint;

        /// <summary>Lift applied to the grip so it clears the surface it is annotating.</summary>
        /// <remarks>
        /// Network Tools' axis handle carries the same thing, and for the same reason: a grip sitting
        /// exactly on the line it measures is half-buried in it and hard to pick out from the line's
        /// own width. Lifting it is cheaper than drawing it larger, and it keeps the connector
        /// readable as a separate object.
        /// </remarks>
        public float Lift;

        /// <summary>Step the grip's distance is quantised to. Zero leaves it continuous.</summary>
        /// <remarks>
        /// <para>
        /// Network Tools' <c>HandleSnap</c> has two tiers: a world snap that runs the game's spatial
        /// snapping job against real geometry, and a cheap increment that quantises the value in the
        /// handle's own natural space when no world snap won. Only the second is meaningful here —
        /// a radius, a lens and a key spacing have nothing in the world to snap ONTO, so the spatial
        /// tier would have nothing to offer them.
        /// </para>
        /// <para>
        /// Snapping in the handle's natural space rather than on the cursor is what makes the value
        /// land on round numbers instead of the grip landing on round positions. Those are different
        /// things once the axis is not aligned to the world.
        /// </para>
        /// </remarks>
        public float Increment;

        /// <summary>Applies this handle's increment to a dragged distance.</summary>
        public float Snap(float distance) {
            return (Increment > 0.0001f)
                ? Colossal.Mathematics.MathUtils.Snap(distance, Increment)
                : distance;
        }

        /// <summary>Whether the grip follows the cursor freely instead of sliding on a fixed axis.</summary>
        /// <remarks>
        /// A constrained axis is right when the direction MEANS something — up is height, outward is
        /// radius — because the sideways part of the gesture would otherwise have to be invented.
        /// It is wrong when only the magnitude matters, which is how the timeline's easing handles
        /// behave: they take the whole distance to the cursor and follow it wherever it goes.
        /// Projecting onto one axis there makes perpendicular movement do nothing at all, and a grip
        /// that ignores most of your gesture reads as broken rather than as constrained.
        /// </remarks>
        public bool Free;

        /// <summary>Where the grip is drawn and picked.</summary>
        public Vector3 Position => Origin + (Axis * Distance) + (Vector3.up * Lift);

        // Network Tools' NT_Dimensions, to the value: a 1m origin dot, a 3m grip, 0.8 lines, and an
        // even 2/2 dash. Restating them here rather than sharing a constant because they belong to
        // that mod's visual language, and a future change to one of ours should not silently drift
        // away from it.
        private const float kOriginRadius = 1f;

        private const float kGripRadius = 3f;

        private const float kLineWidth = 0.8f;

        private const float kDash = 2f;

        private const float kGap = 2f;

        /// <summary>How steep an axis must be before it is measured against the ray, not the ground.</summary>
        /// <remarks>
        /// 0.7 is a hair over 45 degrees. Below it the axis has enough horizontal extent for a flat
        /// projection to be well conditioned; above it, the ground points the cursor sweeps out barely
        /// move along the axis at all and the answer becomes noise. Only the height grip is up there.
        /// </remarks>
        private const float kVerticalAxis = 0.7f;

        /// <summary>Distance along the axis the cursor currently points at.</summary>
        /// <param name="origin">The axis origin.</param>
        /// <param name="axis">Unit direction of the axis.</param>
        /// <param name="distance">Signed distance from the origin.</param>
        /// <returns>False when the view looks straight down the axis, which has no answer.</returns>
        /// <remarks>
        /// Closest approach between the mouse ray and the axis line. It genuinely has no answer when
        /// the two are parallel — every distance projects to the same pixel — so that returns false
        /// and the caller holds the value rather than letting it leap as the view crosses the axis.
        /// </remarks>
        public static bool TryDistanceAlong(Vector3 origin, Vector3 axis, out float distance) {
            distance = 0f;

            float3 unit = math.normalizesafe(axis);

            // A horizontal axis is measured on the GROUND, not against the ray.
            //
            // Network Tools' equivalent takes a world position and projects it flat:
            // dot(pos.xz - from.xz, axis). That is what makes its handles behave the same from every
            // camera — the cursor's ground point moves left when you drag left, always, so the
            // projection of it does too.
            //
            // Closest-approach between the mouse ray and a 3D line answers a different question, and
            // answers it badly at a shallow angle: as the view swings towards the axis the solution
            // grows enormously sensitive, and a pixel of mouse movement becomes tens of metres of
            // travel. Hence handles that felt fine from one side and unusable from another.
            //
            // Kept for the vertical case only, where there is no ground point that could express a
            // height and the ray really is the only information available.
            if (math.abs(unit.y) < kVerticalAxis) {
                if (!PathPicking.TryHitPlane(origin.y, out float3 ground)) {
                    return false;
                }

                float2 flat = math.normalizesafe(unit.xz);

                if (math.lengthsq(flat) < 0.5f) {
                    return false;
                }

                distance = math.dot(ground.xz - ((float3)(Vector3)origin).xz, flat);
                return true;
            }

            if (!PathPicking.TryGetMouseRay(out float3 rayOrigin, out float3 direction)) {
                return false;
            }

            // The ray direction is normalised, so the closest-approach denominator reduces to
            // 1 - (D·U)^2, which is zero exactly when the ray runs along the axis.
            float b           = math.dot(direction, unit);
            float denominator = 1f - (b * b);

            if (math.abs(denominator) < 0.0001f) {
                return false;
            }

            // From the RAY's origin to the axis's, and in that order.
            //
            // The closest-approach solution is stated for w = rayOrigin - lineOrigin. Building it the
            // other way round negates both dot products, and the result comes out as -t: the grip then
            // slides the opposite way to the cursor along its axis. That reads as "the handle only
            // works from some camera angles", because whether backwards LOOKS backwards depends on
            // which way the axis happens to point on screen — from the far side it appears correct.
            //
            // Concretely: axis along +X through the origin, cursor straight down from (5, 10, 0). The
            // nearest point on the axis is x = +5, and the flipped form answers -5.
            float3 w = rayOrigin - (float3)(Vector3)origin;
            float  d = math.dot(direction, w);
            float  e = math.dot(unit, w);

            distance = (e - (b * d)) / denominator;
            return true;
        }

        /// <summary>Draws the handle: an origin dot, a dashed connector, and the grip.</summary>
        /// <remarks>
        /// The connector is inset by both radii and drawn only when the two ends are further apart
        /// than that, which is Network Tools' rule. It stops the line poking into either circle, and
        /// it makes the connector disappear cleanly as the grip approaches the origin instead of
        /// degenerating into a smear inside the dot.
        /// </remarks>
        /// <param name="buffer">Overlay buffer to draw into.</param>
        /// <param name="fill">Grip fill, which carries the hover and drag state.</param>
        /// <param name="line">Colour of the connector and the origin dot.</param>
        public void Draw(ref OverlayRenderSystem.Buffer buffer, Color fill, Color line) {
            Vector3 grip = Position;

            buffer.DrawCircle(line, Origin, kOriginRadius * 2f);

            float span = Vector3.Distance(Origin, grip);

            if (span > kOriginRadius + kGripRadius) {
                Vector3 unit = (grip - Origin) / span;

                buffer.DrawDashedLine(line,
                                      new Colossal.Mathematics.Line3.Segment(Origin + (unit * kOriginRadius),
                                                                             grip - (unit * kGripRadius)),
                                      kLineWidth, kDash, kGap);
            }

            buffer.DrawCircle(fill, grip, kGripRadius * 2f);
        }

        /// <summary>Radius the grip is picked at. Generous — an axis grip has nothing crowding it.</summary>
        public const float kPickRadius = 4f;

        /// <summary>Whether an axis is too steep to measure against the ground.</summary>
        public static bool IsVertical(Vector3 axis) {
            return Mathf.Abs(math.normalizesafe(axis).y) >= kVerticalAxis;
        }

        /// <summary>How far along a steep axis the cursor has travelled since a drag began.</summary>
        /// <param name="origin">The axis origin.</param>
        /// <param name="axis">Unit direction of the axis.</param>
        /// <param name="grabScreen">Where the cursor was when the grip was taken, in pixels.</param>
        /// <param name="delta">World distance travelled along the axis since then.</param>
        /// <returns>False when the axis points at the camera and so has no direction on screen.</returns>
        /// <remarks>
        /// <para>
        /// Measured ON SCREEN, which is what makes a vertical grip behave like the horizontal ones.
        /// The axis is projected to pixels, the mouse travel is projected onto that, and the result is
        /// converted back through the axis's own on-screen length. Drag the way the axis appears to
        /// point and the value rises, from every camera — the rule every translate gizmo uses.
        /// </para>
        /// <para>
        /// The alternative for a vertical axis is closest-approach against the mouse ray, which is
        /// what this replaced. It is exact in principle and unusable in practice: looking level at a
        /// vertical line, the ray is nearly parallel to it and the solution's denominator approaches
        /// zero, so the grip bolts to the horizon on a pixel of movement. Screen space has no such
        /// singularity — the axis simply becomes short in pixels, and a short axis means a drag has
        /// to be longer to move it, which is the correct behaviour rather than a failure.
        /// </para>
        /// </remarks>
        public static bool TryScreenDelta(Vector3 origin, Vector3 axis, Vector2 grabScreen,
                                          out float delta) {
            delta = 0f;

            Camera camera = Camera.main;

            if (camera == null || Mouse.current == null) {
                return false;
            }

            Vector3 near = camera.WorldToScreenPoint(origin);
            Vector3 far  = camera.WorldToScreenPoint(origin + (Vector3)math.normalizesafe(axis));

            // Behind the camera, WorldToScreenPoint mirrors the point through the centre instead of
            // failing, so the projected axis would point the wrong way and the drag would invert.
            if (near.z <= 0f || far.z <= 0f) {
                return false;
            }

            Vector2 onScreen = (Vector2)far - (Vector2)near;
            float   pixels   = onScreen.magnitude;

            // Edge on. One world metre covers almost no pixels, so every answer here is noise.
            if (pixels < 0.5f) {
                return false;
            }

            Vector2 travel = Mouse.current.position.ReadValue() - grabScreen;

            // Divided by pixels TWICE, deliberately: once to normalise the axis direction, once to
            // convert the projected pixel travel into world metres at this zoom.
            delta = Vector2.Dot(travel, onScreen) / (pixels * pixels);
            return true;
        }

        /// <summary>Where a free grip should sit, and how far out it is, for the cursor's position.</summary>
        /// <param name="origin">The handle's origin.</param>
        /// <param name="direction">Horizontal direction from the origin to the cursor.</param>
        /// <param name="distance">How far the cursor is from the origin.</param>
        /// <returns>False when the cursor is not over the handle's plane, or sits on the origin.</returns>
        /// <remarks>
        /// Read on a level plane at the origin's own height, the rule the shot handles already use for
        /// anything off the ground. Taking the terrain hit instead would make the distance shrink as
        /// the ground rose under the gesture, so the value would drift with the landscape rather than
        /// with the drag.
        /// </remarks>
        public static bool TryFreeDrag(Vector3 origin, out Vector3 direction, out float distance) {
            direction = Vector3.forward;
            distance  = 0f;

            if (!PathPicking.TryHitPlane(origin.y, out float3 hit)) {
                return false;
            }

            Vector3 offset = (Vector3)(float3)hit - origin;

            offset.y = 0f;

            if (offset.sqrMagnitude < 0.0001f) {
                return false;
            }

            distance  = offset.magnitude;
            direction = offset / distance;
            return true;
        }
    }
}
