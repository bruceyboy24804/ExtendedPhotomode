namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using UnityEngine;

    #endregion

    /// <summary>
    /// A single sampled camera pose, in the form <see cref="Game.CinematicCamera.CinematicCameraSequence"/>
    /// stores transform keys: world position plus an Euler rotation whose X is pitch and Y is yaw.
    /// </summary>
    public struct CameraSample {
        public float   Time;

        public Vector3 Position;

        public Vector3 Rotation;
    }

    /// <summary>Parameters describing an orbit shot: a camera circling a fixed world point.</summary>
    /// <remarks>
    /// This is the piece vanilla photo mode has no answer for. The cinematic camera can only record
    /// the pose you fly to by hand, so a smooth orbit means placing a ring of keyframes by eye. Here
    /// the ring is solved instead: pick a centre, a radius and an arc, and the keys fall out.
    /// </remarks>
    public struct OrbitShot {
        public Vector3 Target;

        public float Radius;

        public float EndRadius;

        public float Height;

        public float StartAngle;

        public float Sweep;

        public float Duration;

        public bool LookAtTarget;

        public float DegreesPerKey;

        public const float kDefaultDegreesPerKey = 30f;

        public const float kMinDegreesPerKey = 1f;

        public static OrbitShot Create(Vector3 target, float radius, float height) {
            return new OrbitShot {
                Target        = target,
                Radius        = radius,
                EndRadius     = radius,
                Height        = height,
                StartAngle    = 0f,
                Sweep         = 360f,
                Duration      = 30f,
                LookAtTarget  = true,
                DegreesPerKey = kDefaultDegreesPerKey,
            };
        }

        public int KeyCount {
            get {
                float step     = Mathf.Max(Mathf.Abs(DegreesPerKey), kMinDegreesPerKey);
                int   segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(Sweep) / step));
                return segments + 1;
            }
        }

        public List<CameraSample> Solve() {
            int   count   = KeyCount;
            int   last    = count - 1;
            float from    = Mathf.Max(Radius, 0.01f);
            float to      = (EndRadius > 0f) ? Mathf.Max(EndRadius, 0.01f) : from;
            var   samples = new List<CameraSample>(count);

            for (int i = 0; i < count; i++) {
                float f     = (last > 0) ? (float)i / last : 0f;
                float angle = StartAngle + Sweep * f;
                float rad   = angle * Mathf.Deg2Rad;

                float radius = Mathf.Lerp(from, to, f);

                var offset = new Vector3(Mathf.Sin(rad) * radius, Height, Mathf.Cos(rad) * radius);

                samples.Add(new CameraSample {
                    Time     = Duration * f,
                    Position = Target + offset,
                    Rotation = SolveRotation(offset, angle, radius),
                });
            }

            return samples;
        }

        private Vector3 SolveRotation(Vector3 offset, float angle, float radius) {
            if (!LookAtTarget) {
                return new Vector3(SolvePitch(radius, Height), StartAngle + 180f, 0f);
            }

            return new Vector3(SolvePitch(radius, Height), angle + 180f, 0f);
        }

        private static float SolvePitch(float radius, float height) {
            return Mathf.Atan2(height, Mathf.Max(radius, 0.01f)) * Mathf.Rad2Deg;
        }

        public static OrbitShot FromCamera(Vector3 cameraPosition, Vector3 target) {
            Vector3 offset  = cameraPosition - target;
            float   radius  = new Vector2(offset.x, offset.z).magnitude;
            var     orbit   = Create(target, radius, offset.y);

            orbit.StartAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            return orbit;
        }

        public override string ToString() {
            return string.Format(
                "OrbitShot(target={0}, r={1:0.#}->{2:0.#}m, h={3:0.#}m, start={4:0.#}°, sweep={5:0.#}°, {6:0.#}s, {7} keys, lookAt={8})",
                Target, Radius, (EndRadius > 0f) ? EndRadius : Radius, Height, StartAngle, Sweep,
                Duration, KeyCount, LookAtTarget);
        }
    }
}
