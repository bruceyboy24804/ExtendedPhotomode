namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Mathematics;

    using ExtendedPhotomode.Camera;

    using Game.Rendering;

    using UnityEngine;

    #endregion

    /// <summary>Authors a dolly zoom by dragging its subject and the two ends of the travel.</summary>
    /// <remarks>
    /// The shot is a straight run towards or away from a subject while the lens counter-zooms, so its
    /// handles are the subject and the two ends of that run. Both ends share one bearing — the camera
    /// travels along a line through the subject, not an arc — so dragging either end sets the bearing
    /// and the other end follows it round, which is what keeps the track straight.
    /// </remarks>
    public sealed class DollyShotEditor : ShotEditorBase {
        private const int kCentre = 0;

        private const int kStart = 1;

        private const int kEnd = 2;

        /// <summary>The closest a handle can be dragged to the subject, in metres.</summary>
        private const int kMinDistance = 1;

        private static readonly Color kTrackColor = new Color(1f, 0.8f, 0.4f, 0.9f);
        private static readonly Color kSightColor = new Color(1f, 0.8f, 0.4f, 0.28f);

        public override ShotType Type => ShotType.DollyZoom;

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
                Position = Ring(centre, Bearing, Settings.DollyStartDistance, Settings.OrbitHeight),
                Hint     = PathHints.DragDollyStart,
            });

            into.Add(new ShotHandle {
                Id       = kEnd,
                Position = Ring(centre, Bearing, Settings.DollyEndDistance, Settings.OrbitHeight),
                Hint     = PathHints.DragDollyEnd,
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

            // Both ends live on one line through the subject, so whichever end is dragged owns the
            // bearing and the other simply swings round to match. Letting each keep its own would
            // bend the track, and a dolly zoom that curves is an orbit.
            Subject.PinnedStartAngle = bearing;

            // Floored, never capped: how far a dolly starts from its subject is the shot, and the
            // panel's slider range is a convenience rather than a limit.
            int distance = Mathf.Max(Mathf.RoundToInt(radius), kMinDistance);

            if (id == kStart) {
                Settings.DollyStartDistance = distance;
            } else {
                Settings.DollyEndDistance = distance;
            }

            Settings.ApplyAndSave();
        }

        /// <remarks>
        /// Both ends share <c>OrbitHeight</c>, which is what the dolly generator reads, so raising
        /// either raises the track. Two independent heights would tilt it, and the shot has no way to
        /// express that.
        /// </remarks>
        public override void RaiseHandle(int id, float delta) {
            if (id == kCentre) {
                return;
            }

            Settings.OrbitHeight = Mathf.RoundToInt(Settings.OrbitHeight + delta);

            Settings.ApplyAndSave();
        }

        public override Color LineColor => kTrackColor;

        public override bool TryPreview(List<CameraSample> into) {
            into.Clear();

            if (!Subject.PinnedTarget.HasValue) {
                return false;
            }

            var shot = new DollyZoomShot {
                Target        = Subject.PinnedTarget.Value,
                Bearing       = Bearing,
                StartDistance = Settings.DollyStartDistance,
                EndDistance   = Settings.DollyEndDistance,
                Height        = Settings.OrbitHeight,
                Duration      = Settings.DollyDuration,
                Keys          = Settings.DollyKeys,
            };

            // The lens the shot opens on does not matter for the preview — only the positions and
            // aim are drawn — so the focal curve it produces is discarded.
            into.AddRange(shot.Solve(50f, out List<float> _));

            return into.Count >= 2;
        }

        /// <summary>Sightlines from the subject to both ends of the travel.</summary>
        /// <remarks>
        /// These are the point of the shot: the subject stays the same size between them while
        /// everything else changes, and seeing both makes that legible before generating.
        /// </remarks>
        public override void Draw(ref OverlayRenderSystem.Buffer buffer) {
            if (!Subject.PinnedTarget.HasValue) {
                return;
            }

            Vector3 centre = Subject.PinnedTarget.Value;

            Vector3 from = Ring(centre, Bearing, Settings.DollyStartDistance, Settings.OrbitHeight);
            Vector3 to   = Ring(centre, Bearing, Settings.DollyEndDistance, Settings.OrbitHeight);

            buffer.DrawDashedLine(kSightColor, new Line3.Segment(centre, from), 0.6f, 3f, 2f);
            buffer.DrawDashedLine(kSightColor, new Line3.Segment(centre, to), 0.6f, 3f, 2f);
        }

        private float Bearing =>
            Subject.PinnedStartAngle ??
            (Subject.TryBuildOrbitFromSettings(out OrbitShot orbit) ? orbit.StartAngle : 0f);
    }
}
