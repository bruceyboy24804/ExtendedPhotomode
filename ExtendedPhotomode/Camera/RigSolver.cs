namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;

    #endregion

    /// <summary>The physical camera support a shot pretends to have been made on.</summary>
    /// <remarks>Sacrificial <c>None</c> at zero, like every enum bound to a photo mode dropdown.</remarks>
    public enum CameraRig {
        None = 0,

        /// <summary>No constraint. The move plays exactly as solved.</summary>
        Free = 1,

        /// <summary>A heavy crane: slow to start, slow to stop, very smooth.</summary>
        Crane = 2,

        /// <summary>A drone: quick but never instant, with a little drift at the ends.</summary>
        Drone = 3,

        /// <summary>Handheld: responsive, with the small constant unsteadiness of a person.</summary>
        Handheld = 4,
    }

    /// <summary>Makes a solved move obey a physical camera support instead of pure geometry.</summary>
    /// <remarks>
    /// Generated moves are mathematically perfect, and that is exactly why they read as computer
    /// generated. A real camera has mass: it cannot change direction instantly, it overshoots slightly
    /// into a stop, and even a good operator is never perfectly still. None of that is error — it is
    /// the signal the eye uses to decide a shot was photographed rather than rendered.
    /// <para>
    /// The model is a critically damped follower rather than a filter. A smoothing filter would blur
    /// the move symmetrically, softening the start of a turn as much as the end of it; a follower with
    /// bounded acceleration LAGS, which is what mass actually does — the camera arrives at each pose a
    /// little late and settles into it. That asymmetry is the whole effect.
    /// </para>
    /// <para>
    /// Timing is never touched. The pass rewrites where the camera is at each keyframe, not when the
    /// keyframes are, so a shot's duration, its dwell holds and its per-point speeds all survive —
    /// which is what keeps this composable with everything else the mod solves.
    /// </para>
    /// </remarks>
    public static class RigSolver {
        /// <summary>Applies a rig's characteristics to a solved shot, in place.</summary>
        /// <param name="samples">The shot. Positions and rotations are rewritten; times are not.</param>
        /// <param name="rig">Which support to imitate.</param>
        /// <param name="strength">How strongly, 0 to 1.</param>
        /// <param name="seed">Chooses the unsteadiness pattern, so a shot is reproducible.</param>
        public static void Apply(List<CameraSample> samples, CameraRig rig, float strength, int seed) {
            if (samples == null || samples.Count < 3 || rig == CameraRig.Free ||
                rig == CameraRig.None || strength <= 0f) {
                return;
            }

            (float lag, float shake, float frequency) = Characteristics(rig);

            lag *= Mathf.Clamp01(strength);
            shake *= Mathf.Clamp01(strength);

            Follow(samples, lag);
            Shake(samples, shake, frequency, seed);
        }

        /// <summary>Lag, shake amplitude in metres, and shake frequency, for each rig.</summary>
        private static (float lag, float shake, float frequency) Characteristics(CameraRig rig) {
            switch (rig) {
                // A crane is heavy and mounted: a lot of lag, no unsteadiness at all.
                case CameraRig.Crane: return (0.55f, 0f, 0f);

                // A drone holds position well but drifts on the wind, slowly.
                case CameraRig.Drone: return (0.3f, 0.35f, 0.6f);

                // Handheld barely lags — a person follows the action — but is never still.
                case CameraRig.Handheld: return (0.12f, 0.55f, 3.2f);

                default: return (0f, 0f, 0f);
            }
        }

        /// <summary>Drags the camera towards each solved pose instead of placing it there.</summary>
        /// <remarks>
        /// Run forwards only, deliberately. A symmetric pass would smooth the move without lagging it,
        /// and lag is the part that reads as weight. Rotation is dragged with the same coefficient so
        /// the pan trails the move rather than leading it.
        /// </remarks>
        private static void Follow(List<CameraSample> samples, float lag) {
            if (lag <= 0f) {
                return;
            }

            float keep = Mathf.Clamp01(lag);

            Vector3 position = samples[0].Position;
            Vector3 rotation = samples[0].Rotation;

            for (int i = 1; i < samples.Count; i++) {
                CameraSample sample = samples[i];

                position = Vector3.Lerp(sample.Position, position, keep);

                // Rotation is already unwrapped by the solver, so a plain lerp cannot take the long
                // way round — no DeltaAngle needed, and using one here would undo the unwrapping.
                rotation = Vector3.Lerp(sample.Rotation, rotation, keep);

                sample.Position = position;
                sample.Rotation = rotation;
                samples[i]      = sample;
            }
        }

        /// <summary>Adds the small constant unsteadiness of a real operator.</summary>
        /// <remarks>
        /// Value noise from a seeded hash rather than <c>Random</c>: the same seed has to produce the
        /// same shot every time it is generated, or regenerating a sequence would change every take.
        /// Sampled against the keyframe TIME rather than its index, so the shake runs at a constant
        /// rate in seconds regardless of how densely the shot is keyed.
        /// </remarks>
        private static void Shake(List<CameraSample> samples, float amplitude, float frequency,
                                  int seed) {
            if (amplitude <= 0f || frequency <= 0f) {
                return;
            }

            for (int i = 0; i < samples.Count; i++) {
                CameraSample sample = samples[i];
                float        t      = sample.Time * frequency;

                sample.Position += new Vector3(Noise(t, seed), Noise(t, seed + 71),
                                               Noise(t, seed + 149)) * amplitude;

                // A quarter of the positional figure in degrees: a wobble that rotates as much as it
                // translates reads as a fault rather than as a hand.
                sample.Rotation += new Vector3(Noise(t, seed + 227), Noise(t, seed + 313), 0f)
                                   * (amplitude * 0.25f);

                samples[i] = sample;
            }
        }

        /// <summary>Smooth value noise in -1..1, deterministic for a given time and seed.</summary>
        private static float Noise(float t, int seed) {
            int   cell = Mathf.FloorToInt(t);
            float f    = t - cell;

            // Smoothstep between adjacent random values: continuous, so the camera drifts rather than
            // jumping, which white noise would do.
            float a = Hash(cell, seed);
            float b = Hash(cell + 1, seed);

            return Mathf.Lerp(a, b, f * f * (3f - 2f * f));
        }

        private static float Hash(int value, int seed) {
            unchecked {
                int h = value * 73856093 ^ seed * 19349663;

                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;

                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }
    }
}
