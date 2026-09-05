namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Mathematics;

    using ExtendedPhotomode.Camera;

    using Game.Rendering;

    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>Authors an orbit by dragging its centre and the two ends of its sweep.</summary>
    /// <remarks>
    /// Two handles express the entire shot, including the spiral and helix cases. The start handle
    /// carries the opening bearing, radius and height; the end handle carries the closing radius and
    /// height, and the angle between them IS the sweep. Nothing else about an orbit is independent of
    /// those, which is why there are only three points to grab rather than a row per number.
    /// <para>
    /// Sweep is accumulated rather than recomputed from the handle's bearing. A bearing only says where
    /// the end handle is, and there is no way to tell 30° from 390° from that — so a shot that should
    /// go round twice would silently collapse to a quarter turn as soon as it was touched.
    /// </para>
    /// </remarks>
    public sealed class OrbitShotEditor : ShotEditorBase {
        private const int kCentre = 0;

        private const int kStart = 1;

        private const int kEnd = 2;

        /// <summary>The smallest radius a drag can produce, in metres.</summary>
        /// <remarks>
        /// A floor, not a cap. Nothing limits how far out a handle can be dragged — the tool options
        /// slider stops at 1000, but that is only a comfortable range for the control. This exists
        /// solely because a zero radius puts the camera inside its own subject, which is not a shot.
        /// </remarks>
        private const int kMinRadius = 1;

        /// <summary>How many samples the drawn ring is allowed, however long the sweep.</summary>
        private const int kPreviewKeys = 360;

        private static readonly Color kRingColor   = new Color(0.4f, 0.85f, 1f, 0.9f);
        private static readonly Color kSpokeColor  = new Color(0.4f, 0.85f, 1f, 0.3f);

        public override ShotType Type => ShotType.Orbit;

        public override void CollectHandles(List<ShotHandle> into) {
            if (!Subject.PinnedTarget.HasValue) {
                return;
            }

            Vector3 centre = Subject.PinnedTarget.Value;

            into.Add(new ShotHandle {
                Id = kCentre, Position = centre, OnGround = true, Hint = PathHints.MoveOrbitCentre,
            });

            into.Add(new ShotHandle {
                Id       = kStart,
                Position = Ring(centre, StartAngle, Settings.OrbitRadius, Settings.OrbitHeight),
                Hint     = PathHints.DragOrbitStart,
            });

            into.Add(new ShotHandle {
                Id       = kEnd,
                Position = Ring(centre, StartAngle + Settings.OrbitSweep, Settings.OrbitEndRadius,
                                Settings.OrbitEndHeight),
                Hint     = PathHints.DragOrbitEnd,
            });
        }

        public override void MoveHandle(int id, Vector3 world) {
            if (!Subject.PinnedTarget.HasValue) {
                return;
            }

            if (id == kCentre) {
                Subject.PinnedTarget = world;
                return;
            }

            Vector3 centre = Subject.PinnedTarget.Value;

            Polar(centre, world, out float bearing, out float radius);

            if (id == kStart) {
                // Turning the start handle turns the whole shot rather than resizing its sweep: the
                // end follows, because sweep is the angle BETWEEN them and neither end owns it alone.
                Subject.PinnedStartAngle = bearing;
                Settings.OrbitRadius     = ClampRadius(radius);
            } else {
                // Nearest equivalent of the dragged bearing to the sweep already set, so dragging past
                // north continues to 370° instead of snapping back to 10°.
                float current = StartAngle + Settings.OrbitSweep;
                float sweep   = Settings.OrbitSweep + Mathf.DeltaAngle(current, bearing);

                // Uncapped. Sweep accumulates as the handle is dragged round, so winding it past a
                // full turn keeps adding turns — which is the only way to author a multi-turn orbit
                // by hand, and there is no geometric reason to stop at any particular number.
                Settings.OrbitSweep = Mathf.RoundToInt(sweep);
                Settings.OrbitEndRadius = ClampRadius(radius);
            }

            Settings.ApplyAndSave();
        }

        /// <remarks>
        /// Unbounded in both directions, like the radius. The panel's own rows keep a comfortable
        /// range; the world does not, because a camera a kilometre up over a city is a real shot.
        /// </remarks>
        public override void RaiseHandle(int id, float delta) {
            if (id == kStart) {
                Settings.OrbitHeight = Mathf.RoundToInt(Settings.OrbitHeight + delta);
            } else if (id == kEnd) {
                Settings.OrbitEndHeight = Mathf.RoundToInt(Settings.OrbitEndHeight + delta);
            } else {
                return;
            }

            Settings.ApplyAndSave();
        }

        /// <summary>Draws the swept ring, plus a spoke at each end showing what is being framed.</summary>
        /// <remarks>
        /// Sampled from the same solver the shot uses, so the drawn ring shows the real spiral and
        /// helix rather than a circle at the opening radius. Drawn as short line segments because an
        /// overlay circle is flat and a spiralling orbit is not.
        /// </remarks>
        public override Color LineColor => kRingColor;

        /// <remarks>
        /// The preview is thinned for very long sweeps rather than solved in full. Key count is the
        /// sweep divided by the spacing, so an uncapped sweep at a fine spacing runs to thousands of
        /// samples — and this solves once per frame, while a handle is being dragged, which is exactly
        /// when the framerate matters. Only the DRAWING is coarsened; the generated shot still gets
        /// every key its settings ask for.
        /// </remarks>
        public override bool TryPreview(List<CameraSample> into) {
            into.Clear();

            if (!Settings.ShowOrbitPreview || !Subject.PinnedTarget.HasValue ||
                !Subject.TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                return false;
            }

            if (orbit.KeyCount > kPreviewKeys) {
                orbit.DegreesPerKey = Mathf.Abs(orbit.Sweep) / kPreviewKeys;
            }

            into.AddRange(orbit.Solve());
            return into.Count >= 2;
        }

        /// <summary>Spokes from the subject to each end of the sweep.</summary>
        /// <remarks>
        /// The ring alone says nothing about which way round it goes or what it is circling, and those
        /// are the two questions you have while dragging it. Two spokes answer both: the subject is
        /// where they meet, and the shot runs from the first to the second.
        /// <para>
        /// Drawn from the handle positions rather than by solving the orbit again. The two ends of a
        /// sweep ARE the two handles, so a second full solve every frame bought nothing — and on a
        /// long sweep it was the more expensive of the two.
        /// </para>
        /// </remarks>
        public override void Draw(ref OverlayRenderSystem.Buffer buffer) {
            if (!Subject.PinnedTarget.HasValue) {
                return;
            }

            Vector3 centre = Subject.PinnedTarget.Value;

            Vector3 from = Ring(centre, StartAngle, Settings.OrbitRadius, Settings.OrbitHeight);
            Vector3 to   = Ring(centre, StartAngle + Settings.OrbitSweep, Settings.OrbitEndRadius,
                                Settings.OrbitEndHeight);

            buffer.DrawDashedLine(kSpokeColor, new Line3.Segment(centre, from), 0.6f, 3f, 2f);
            buffer.DrawDashedLine(kSpokeColor, new Line3.Segment(centre, to), 0.6f, 3f, 2f);
        }

        private static int ClampRadius(float radius) {
            return Mathf.Max(Mathf.RoundToInt(radius), kMinRadius);
        }

        private float StartAngle {
            get {
                if (Subject.PinnedStartAngle.HasValue) {
                    return Subject.PinnedStartAngle.Value;
                }

                // Falls back to whatever the settings would produce, so the handles appear where the
                // generated shot would actually start rather than due north.
                return Subject.TryBuildOrbitFromSettings(out OrbitShot orbit) ? orbit.StartAngle : 0f;
            }
        }
    }
}
