namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;

    #endregion

    /// <summary>How the camera is aimed as it travels a path.</summary>
    /// <remarks>
    /// Shaped like <c>UnityEngine.Camera.GateFitMode</c> — see <see cref="KeyframeEase"/> for why the
    /// zero-valued <see cref="None"/> is there and must stay first.
    /// </remarks>
    public enum PathLookMode {
        None = 0,

        Forward = 1,

        Fixed = 2,

        Target = 3,
    }

    /// <summary>A camera move defined by control points the user places in the world, flown as a smooth curve.</summary>
    /// <remarks>
    /// A cubic Bezier chain through the control points, with per-node tangent handles; left on auto
    /// they reproduce a uniform Catmull-Rom spline exactly.
    ///
    /// Sampling is by ARC LENGTH, not by curve parameter. Stepping the parameter uniformly bunches
    /// samples where the curve bends, which makes the camera change speed for no visible reason.
    /// </remarks>
    public class CameraPath {
        private const int kLengthSamplesPerSegment = 16;

        public const float kMinMetresPerKey = 1f;

        public const float kDefaultMetresPerKey = 25f;

        public List<PathNode> Nodes { get; } = new List<PathNode>();

        public float Duration { get; set; } = 30f;

        public float MetresPerKey { get; set; } = kDefaultMetresPerKey;

        public PathLookMode LookMode { get; set; } = PathLookMode.Forward;

        public float Pitch { get; set; }

        public float FixedYaw { get; set; }

        public Vector3 Target { get; set; }

        public bool IsValid => Nodes.Count >= 2;

        public void Clear() { Nodes.Clear(); }

        public (Vector3 a, Vector3 b, Vector3 c, Vector3 d) GetSegment(int segment) {
            PathNode from = Nodes[segment];
            PathNode to   = Nodes[segment + 1];

            return (from.Position, from.HandleOut, to.HandleIn, to.Position);
        }

        public Vector3 Evaluate(int segment, float t) {
            (Vector3 a, Vector3 b, Vector3 c, Vector3 d) = GetSegment(segment);
            return Bezier(a, b, c, d, t);
        }

        public void RefreshAutoTangents() {
            int last = Nodes.Count - 1;

            for (int i = 0; i <= last; i++) {
                PathNode node = Nodes[i];

                if (!node.Auto) {
                    continue;
                }

                Vector3 before = Nodes[Mathf.Max(i - 1, 0)].Position;
                Vector3 after  = Nodes[Mathf.Min(i + 1, last)].Position;
                Vector3 out_   = (after - before) / 6f;

                node.TangentOut = out_;
                node.TangentIn  = -out_;
            }
        }

        public float MeasureLength() {
            if (!IsValid) {
                return 0f;
            }

            float   length   = 0f;
            Vector3 previous = Nodes[0].Position;

            for (int segment = 0; segment < Nodes.Count - 1; segment++) {
                for (int step = 1; step <= kLengthSamplesPerSegment; step++) {
                    Vector3 current = Evaluate(segment, (float)step / kLengthSamplesPerSegment);
                    length  += Vector3.Distance(previous, current);
                    previous = current;
                }
            }

            return length;
        }

        public List<CameraSample> Solve() {
            var samples = new List<CameraSample>();

            if (!IsValid) {
                return samples;
            }

            List<Vector3> positions = SamplePositions(MetresPerKey);
            int           last      = positions.Count - 1;

            for (int i = 0; i < positions.Count; i++) {
                float f = (last > 0) ? (float)i / last : 0f;

                samples.Add(new CameraSample {
                    Time     = Duration * f,
                    Position = positions[i],
                    Rotation = SolveRotation(positions, i),
                });
            }

            Unwrap(samples);
            return samples;
        }

        public List<Vector3> SamplePositions(float metresPerSample) {
            float spacing = Mathf.Max(metresPerSample, kMinMetresPerKey);
            float length  = MeasureLength();
            int   count   = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);

            var positions = new List<Vector3>(count);
            int segments  = Nodes.Count - 1;

            for (int i = 0; i < count; i++) {
                float global  = (float)i / (count - 1) * segments;
                int   segment = Mathf.Min((int)global, segments - 1);

                positions.Add(Evaluate(segment, global - segment));
            }

            return positions;
        }

        private Vector3 SolveRotation(List<Vector3> positions, int index) {
            if (LookMode == PathLookMode.Fixed) {
                return new Vector3(Pitch, FixedYaw, 0f);
            }

            if (LookMode == PathLookMode.Target) {
                return AimAt(positions[index]);
            }

            int     before = Mathf.Max(index - 1, 0);
            int     after  = Mathf.Min(index + 1, positions.Count - 1);
            Vector3 travel = positions[after] - positions[before];

            if (travel.sqrMagnitude < 0.0001f) {
                return new Vector3(Pitch, FixedYaw, 0f);
            }

            return new Vector3(Pitch, Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg, 0f);
        }

        private Vector3 AimAt(Vector3 from) {
            Vector3 toTarget = Target - from;
            float   flat     = new Vector2(toTarget.x, toTarget.z).magnitude;

            if (flat < 0.0001f && Mathf.Abs(toTarget.y) < 0.0001f) {
                return new Vector3(Pitch, FixedYaw, 0f);
            }

            float yaw   = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(toTarget.y, Mathf.Max(flat, 0.0001f)) * Mathf.Rad2Deg;

            return new Vector3(pitch, yaw, 0f);
        }

        private static void Unwrap(List<CameraSample> samples) {
            for (int i = 1; i < samples.Count; i++) {
                float previous = samples[i - 1].Rotation.y;
                float delta    = Mathf.DeltaAngle(previous, samples[i].Rotation.y);

                CameraSample sample = samples[i];
                sample.Rotation.y = previous + delta;
                samples[i]        = sample;
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) {
            float u  = 1f - t;
            float u2 = u * u;
            float t2 = t * t;

            return (u2 * u) * a + (3f * u2 * t) * b + (3f * u * t2) * c + (t2 * t) * d;
        }
    }
}
