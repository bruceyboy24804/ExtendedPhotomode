namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;

    #endregion

    /// <summary>How focus distance is driven across a shot.</summary>
    /// <remarks>Sacrificial <c>None</c> at zero, like every enum bound to a photo mode dropdown.</remarks>
    public enum FocusMode {
        None = 0,

        /// <summary>Focus is left exactly as the panel has it.</summary>
        Off = 1,

        /// <summary>Focus tracks the pinned subject as the camera moves.</summary>
        Track = 2,

        /// <summary>Focus ramps from the subject to a second point across the shot.</summary>
        Rack = 3,
    }

    /// <summary>Solves depth-of-field focus distance for every keyframe of a shot.</summary>
    /// <remarks>
    /// Vanilla exposes <c>DepthOfField.focusDistance</c> as an ordinary keyframable property, so the
    /// timeline could always animate it — but nobody could author it, because the value at each key is
    /// the distance from a moving camera to a subject, which means arithmetic at every keyframe. The
    /// mod already knows both ends of that measurement, so it can simply fill it in.
    /// <para>
    /// <b>Tracking</b> keeps the subject sharp however far the camera travels: a dolly towards a tower
    /// currently goes soft, because focus is a fixed number and the distance is not.
    /// </para>
    /// <para>
    /// <b>Racking</b> ramps focus from one distance to another across the shot — the shot where the
    /// foreground falls out of focus as the background comes into it. Eased rather than linear,
    /// because a linear rack reads as mechanical: a focus puller accelerates and settles.
    /// </para>
    /// <para>
    /// Focus distance alone produces no visible blur unless the lens is open enough to have a shallow
    /// depth of field, which is why <c>aperture</c> is solved alongside it rather than left to the
    /// user to discover. A shot with a f/22 lens focused perfectly looks identical to one that is not.
    /// </para>
    /// </remarks>
    public static class FocusSolver {
        /// <summary>Vanilla's own bounds on the focus distance property, in metres.</summary>
        private const float kMinDistance = 0.1f;

        private const float kMaxDistance = 1000f;

        /// <summary>Solves focus distance per sample.</summary>
        /// <param name="samples">The shot, read for camera positions; never modified.</param>
        /// <param name="subject">What to focus on, and what a rack starts from.</param>
        /// <param name="rackTarget">Where a rack ends. Ignored unless the mode is Rack.</param>
        /// <param name="mode">How focus is driven.</param>
        /// <param name="ease">How strongly a rack eases in and out, 0 to 1.</param>
        /// <param name="distances">Filled with one focus distance per sample.</param>
        public static void Solve(IReadOnlyList<CameraSample> samples, Vector3 subject,
                                 Vector3 rackTarget, FocusMode mode, float ease,
                                 List<float> distances) {
            distances.Clear();

            if (samples == null || mode == FocusMode.Off || mode == FocusMode.None) {
                return;
            }

            for (int i = 0; i < samples.Count; i++) {
                Vector3 camera = samples[i].Position;

                if (mode == FocusMode.Track) {
                    distances.Add(Clamp(Vector3.Distance(camera, subject)));
                    continue;
                }

                // A rack is a ramp between two distances, both measured from where the camera is at
                // that moment — not a ramp between two fixed numbers. Measuring from the moving camera
                // is what keeps both ends genuinely sharp when they are reached.
                float from = Vector3.Distance(camera, subject);
                float to   = Vector3.Distance(camera, rackTarget);

                float t = (samples.Count > 1) ? (float)i / (samples.Count - 1) : 0f;

                distances.Add(Clamp(Mathf.Lerp(from, to, Easing.Blend(t, ease))));
            }
        }

        /// <summary>The widest aperture that still holds the subject sharp, as an f-number.</summary>
        /// <remarks>
        /// Shallow depth of field is the entire visible point of solving focus, and it comes from the
        /// aperture rather than from the focus distance. But a wide lens on a near subject throws so
        /// much away that nothing reads, so this opens up further the further away the subject is —
        /// which is what a real operator does, and what keeps a cityscape from dissolving.
        /// </remarks>
        public static float ApertureFor(float distance, float strength) {
            // f/1.4 at arm's length through to f/8 at a few hundred metres, scaled by how much
            // shallowness was asked for. Vanilla clamps the property to 0.7..32 either way.
            float wide   = Mathf.Lerp(1.4f, 8f, Mathf.InverseLerp(10f, 300f, distance));
            float narrow = 11f;

            return Mathf.Clamp(Mathf.Lerp(narrow, wide, Mathf.Clamp01(strength)), 0.7f, 32f);
        }

        private static float Clamp(float distance) {
            return Mathf.Clamp(distance, kMinDistance, kMaxDistance);
        }
    }
}
