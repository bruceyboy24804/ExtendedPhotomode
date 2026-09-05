namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;

    #endregion

    /// <summary>Where in frame a subject should sit.</summary>
    /// <remarks>
    /// Shaped with a sacrificial <c>None</c> at zero, like every enum bound to a photo mode dropdown.
    /// </remarks>
    public enum FramingRule {
        None = 0,

        /// <summary>Dead centre.</summary>
        Centre = 1,

        /// <summary>On the left third, looking into the right of frame.</summary>
        LeftThird = 2,

        /// <summary>On the right third, looking into the left of frame.</summary>
        RightThird = 3,

        /// <summary>Centred horizontally, sitting low with headroom above.</summary>
        Headroom = 4,
    }

    /// <summary>Holds a subject at a chosen place in frame, and a chosen size, for a whole move.</summary>
    /// <remarks>
    /// The difference between "the camera points at the stadium" and "the stadium sits on the right
    /// third and stays the same size while the camera arcs around it". Nobody hand-authors this: it is
    /// projection arithmetic at every keyframe, against a camera position that is itself moving.
    /// <para>
    /// Two independent corrections, and they are worth keeping separate. Composition is an ANGULAR
    /// offset applied to the aim — nudging the camera off the subject by the angle that puts it on a
    /// third rather than in the middle. Size is a FOCAL LENGTH solved from the distance, so a subject
    /// that the camera pulls away from is kept the same size on screen by zooming in. Solving both by
    /// moving the camera instead would fight the path the user drew, which is the one thing an
    /// auto-framer must not do.
    /// </para>
    /// </remarks>
    public static class FramingSolver {
        /// <summary>Vanilla's default sensor height in millimetres, which its own conversions assume.</summary>
        public const float kSensorHeight = 24f;

        /// <summary>The horizontal thirds line, as a fraction of half the frame width.</summary>
        /// <remarks>
        /// A third of the way in from the edge is two thirds of the way out from centre, so the offset
        /// from centre is 1/3 of the half-width — not 1/3 of the full width, which is the easy mistake
        /// and puts the subject much too close to the edge.
        /// </remarks>
        private const float kThird = 1f / 3f;

        /// <summary>How far below centre the headroom rule sits the subject.</summary>
        private const float kHeadroom = 0.25f;

        /// <summary>Applies a framing rule to a solved run of samples.</summary>
        /// <param name="samples">The shot, whose rotations are adjusted in place.</param>
        /// <param name="subject">What to keep framed.</param>
        /// <param name="rule">Where in frame it should sit.</param>
        /// <param name="focalLengths">
        /// Per-sample focal length, written when <paramref name="holdSize"/> is set. NaN entries mean
        /// "no opinion", which is what the timeline writer treats as leaving the lens alone.
        /// </param>
        /// <param name="holdSize">
        /// Whether to solve focal length so the subject keeps the size it has at the first sample.
        /// </param>
        /// <param name="baseFocalLength">The lens the shot starts on, in millimetres.</param>
        public static void Apply(List<CameraSample> samples, Vector3 subject, FramingRule rule,
                                 List<float> focalLengths, bool holdSize, float baseFocalLength) {
            if (samples == null || samples.Count == 0 || rule == FramingRule.None) {
                return;
            }

            float reference = Vector3.Distance(samples[0].Position, subject);

            for (int i = 0; i < samples.Count; i++) {
                CameraSample sample = samples[i];

                float distance = Mathf.Max(Vector3.Distance(sample.Position, subject), 0.01f);

                // The lens is solved first, because the composition offset is an angle measured in
                // fractions of the frame — and how wide the frame is depends on the lens.
                float focal = holdSize ? Mathf.Clamp(baseFocalLength * distance / reference, 0.11f, 1466f)
                                       : baseFocalLength;

                if (holdSize && focalLengths != null && i < focalLengths.Count) {
                    focalLengths[i] = focal;
                }

                Vector3 aim = CameraAim.Euler(sample.Position, subject, sample.Rotation);

                (float across, float down) = OffsetFor(rule);

                if (across != 0f || down != 0f) {
                    float half = Mathf.Clamp(UnityEngine.Camera.FocalLengthToFieldOfView(
                                                 Mathf.Max(focal, 0.0001f), kSensorHeight),
                                             1f, 179f) * 0.5f;

                    // Turning the camera by this angle moves the subject the other way in frame, which
                    // is why the offsets are negated: to put the subject on the LEFT third the camera
                    // has to look to the RIGHT of it.
                    aim.y -= Mathf.Atan(across * Mathf.Tan(half * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                    aim.x -= Mathf.Atan(down * Mathf.Tan(half * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                }

                sample.Rotation = aim;
                samples[i]      = sample;
            }

            Unwrap(samples);
        }

        /// <summary>Re-continues yaw after the rotations have been rewritten.</summary>
        /// <remarks>
        /// The solver already unwrapped its own yaw so the curve never jumps 359 to 1; every rotation
        /// written above came back from a fresh aim in the range -180 to 180, which undoes that. Not
        /// re-running it leaves the camera spinning the long way round once per crossing.
        /// </remarks>
        private static void Unwrap(List<CameraSample> samples) {
            for (int i = 1; i < samples.Count; i++) {
                float previous = samples[i - 1].Rotation.y;
                float delta    = Mathf.DeltaAngle(previous, samples[i].Rotation.y);

                CameraSample sample = samples[i];

                sample.Rotation.y = previous + delta;
                samples[i]        = sample;
            }
        }

        /// <summary>The subject's offset from frame centre, as a fraction of the half-frame.</summary>
        private static (float across, float down) OffsetFor(FramingRule rule) {
            switch (rule) {
                case FramingRule.LeftThird:  return (-kThird, 0f);
                case FramingRule.RightThird: return (kThird, 0f);
                case FramingRule.Headroom:   return (0f, kHeadroom);
                default:                     return (0f, 0f);
            }
        }
    }
}
