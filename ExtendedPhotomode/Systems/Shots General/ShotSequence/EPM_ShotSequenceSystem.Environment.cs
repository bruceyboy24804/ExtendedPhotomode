namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;

    using Game.CinematicCamera;
    using Game.Rendering.CinematicCamera;

    using UnityEngine;

    #endregion

    /// <summary>The time of day ramp written alongside a shot.</summary>
    public partial class EPM_ShotSequenceSystem {
        public const string kTimeOfDayPropertyId = "Time of Day";

        private void ApplyTimeOfDay(CinematicCameraSequence sequence, float endTime) {
            Setting settings = Mod.Instance.Settings;

            if (!settings.AnimateTimeOfDay) {
                return;
            }

            if (!m_PhotoModeRenderSystem.photoModeProperties.TryGetValue(kTimeOfDayPropertyId,
                                                                        out PhotoModeProperty property)) {
                m_Log.Warn($"No \"{kTimeOfDayPropertyId}\" property registered; cannot animate the light.");
                return;
            }

            float min = property.min?.Invoke() ?? 0f;
            float max = property.max?.Invoke() ?? 24f;

            sequence.modifiers.RemoveAll(m => m.id == kTimeOfDayPropertyId);

            float last = Mathf.Max(Mathf.Max(endTime, sequence.playbackDuration), 0.01f);

            float start = Mathf.Clamp(settings.StartTimeOfDay, min, max);
            float end   = Mathf.Clamp(settings.EndTimeOfDay, min, max);

            AnimationCurve todCurve = BuildTimeOfDayCurve(sequence, start, end, last);

            sequence.modifiers.Add(new CinematicCameraSequence.CinematicCameraCurveModifier {
                id    = kTimeOfDayPropertyId,
                min   = min,
                max   = max,
                curve = todCurve,
            });

            if (sequence.loop && !Mathf.Approximately(Mathf.Repeat(start, 24f), Mathf.Repeat(end, 24f))) {
                m_Log.Warn($"Loop is on, so the sequence must end where it began — the {start:0.#}h to " +
                           $"{end:0.#}h ramp will be flattened. Use a full day, or turn loop off.");
            }

            m_Log.Info($"Time of day ramps {settings.StartTimeOfDay:0.#}h -> {settings.EndTimeOfDay:0.#}h " +
                       $"over the whole sequence (0..{last:0.##}s) in {todCurve.length} keys");
            RefreshModifierCurveBinding();
        }

        private AnimationCurve BuildTimeOfDayCurve(CinematicCameraSequence sequence, float start,
                                                   float end, float last) {
            Setting settings = Mod.Instance.Settings;

            float slope = (end - start) / last;

            AnimationCurve ends = new AnimationCurve(new Keyframe(0f, start, 0f, slope),
                                                     new Keyframe(last, end, slope, 0f));

            if (!settings.TimeOfDayPerKeyframe) {
                return ends;
            }

            AnimationCurve reference =
                sequence.transforms[(int)CinematicCameraSequence.TransformCurveKey.PositionX].curve;

            var times = new List<float> { 0f, last };

            for (int i = 0; reference != null && i < reference.length; i++) {
                times.Add(Mathf.Clamp(reference[i].time, 0f, last));
            }

            times.Sort();

            float ease = Mathf.Clamp01(settings.TimeOfDayEase);

            float[] table = ease > 0f ? BuildChangeTable(Sun, start, end) : null;

            var keys = new List<Keyframe>(times.Count);

            foreach (float time in times) {
                if (keys.Count > 0 && Mathf.Approximately(keys[keys.Count - 1].time, time)) {
                    continue;
                }

                float u     = time / last;
                float shape = table == null ? u : Mathf.Lerp(u, Invert(table, u), ease);

                keys.Add(new Keyframe(time, Mathf.Lerp(start, end, shape), 0f, 0f));
            }

            if (keys.Count < 2) {
                return ends;
            }

            var curve = new AnimationCurve(keys.ToArray());

            for (int i = 0; i < curve.length; i++) {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }

        private static float[] BuildChangeTable(SunModel sun, float start, float end) {
            const int kSteps = 256;

            var   table = new float[kSteps + 1];
            float prev  = sun.Intensity(start);

            for (int i = 1; i <= kSteps; i++) {
                float now = sun.Intensity(Mathf.Lerp(start, end, (float)i / kSteps));

                table[i] = table[i - 1] + Mathf.Abs(now - prev);
                prev     = now;
            }

            return table;
        }

        private static float Invert(float[] table, float u) {
            float total = table[table.Length - 1];

            if (total <= 1e-5f) {
                return u;
            }

            float target = Mathf.Clamp01(u) * total;

            for (int i = 1; i < table.Length; i++) {
                if (table[i] < target) {
                    continue;
                }

                float span = table[i] - table[i - 1];
                float frac = span <= 1e-6f ? 0f : (target - table[i - 1]) / span;

                return (i - 1 + frac) / (table.Length - 1);
            }

            return 1f;
        }

    }
}
