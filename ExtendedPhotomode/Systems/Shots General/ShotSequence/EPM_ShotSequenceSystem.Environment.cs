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

        /// <summary>Substring of vanilla's depth-of-field focus distance property id.</summary>
        /// <remarks>
        /// Matched by substring for the same reason the lens is: vanilla derives these ids from an
        /// expression tree over a captured field, so the exact string depends on compiler-generated
        /// naming and is not safe to hardcode.
        /// </remarks>
        public const string kFocusDistancePropertyId = "focusDistance";

        public const string kAperturePropertyId = "aperture";

        /// <summary>Where a rack focus ends, set from the world by the path tool.</summary>
        public Vector3? m_RackTarget;

        /// <summary>How densely a transition bridge is keyed, per second.</summary>
        /// <remarks>
        /// Enough that the curve follows the intended interpolation rather than inventing its own
        /// route between two distant keys, and few enough that the bridge stays editable by hand.
        /// </remarks>
        private const float kTransitionKeysPerSecond = 2f;

        /// <summary>Writes a modifier curve from a per-sample value, skipping the samples that have none.</summary>
        /// <param name="propertyId">The photo mode property to drive. Matched by substring.</param>
        /// <param name="samples">The samples just written, for their times.</param>
        /// <param name="values">One value per sample; NaN where that sample asks for nothing.</param>
        /// <param name="startTime">Timeline time the samples were written at.</param>
        /// <remarks>
        /// This is how a per-point lens or hour reaches the timeline: not as a camera transform key,
        /// which only carries position and rotation, but as a modifier on vanilla's own property —
        /// the same mechanism the dolly zoom uses for its lens, so the result stays editable in the
        /// curve editor afterwards.
        /// </remarks>
        public void ApplyPointCurve(string propertyId, IReadOnlyList<CameraSample> samples,
                                    IReadOnlyList<float> values, float startTime) {
            CinematicCameraSequence sequence = ActiveSequence;

            if (sequence == null || samples == null || values == null) {
                return;
            }

            PhotoModeProperty property = FindProperty(propertyId);

            if (property == null) {
                return;
            }

            // Not every vanilla property declares bounds — focusDistance is registered with an enable
            // predicate and nothing else — and the curve editor scales a graph's Y axis to the
            // MODIFIER's declared range. Falling through to the float extremes therefore draws an axis
            // running to 3e+38 with the real values flattened onto zero. Where the property has no
            // opinion, the range is taken from the data instead.
            bool  bounded = property.min != null && property.max != null;
            float min     = bounded ? property.min.Invoke() : float.MinValue;
            float max     = bounded ? property.max.Invoke() : float.MaxValue;

            var keys = new List<Keyframe>();

            for (int i = 0; i < samples.Count && i < values.Count; i++) {
                if (float.IsNaN(values[i])) {
                    continue;
                }

                float time = startTime + samples[i].Time;

                // A dwell writes the same time twice, and AnimationCurve refuses a duplicate.
                if (keys.Count > 0 && Mathf.Approximately(keys[keys.Count - 1].time, time)) {
                    continue;
                }

                keys.Add(new Keyframe(time, Mathf.Clamp(values[i], min, max)));
            }

            if (keys.Count == 0) {
                return;
            }

            // Headroom rather than the exact data range: CinematicCameraSequence.MoveKeyframe clamps a
            // dragged key to the modifier's min and max, so a range fitted tightly to the generated
            // values would let you see the curve but not pull any key beyond it.
            if (!bounded) {
                float low  = keys[0].value;
                float high = keys[0].value;

                foreach (Keyframe key in keys) {
                    low  = Mathf.Min(low, key.value);
                    high = Mathf.Max(high, key.value);
                }

                float pad = Mathf.Max((high - low) * 0.5f, Mathf.Abs(high) * 0.5f, 1f);

                min = low - pad;
                max = high + pad;
            }

            sequence.modifiers.RemoveAll(m => m.id == property.id);
            sequence.modifiers.Add(new CinematicCameraSequence.CinematicCameraCurveModifier {
                id    = property.id,
                min   = min,
                max   = max,
                curve = new AnimationCurve(keys.ToArray()),
            });

            for (int i = 0; i < sequence.modifiers.Count; i++) {
                if (sequence.modifiers[i].id == property.id) {
                    AnimationCurve curve = sequence.modifiers[i].curve;

                    for (int k = 0; k < curve.length; k++) {
                        curve.SmoothTangents(k, 0f);
                    }
                }
            }

            RefreshModifierCurveBinding();
        }

        /// <summary>Finds a registered photo mode property whose id contains <paramref name="match"/>.</summary>
        /// <remarks>
        /// Matched by substring because vanilla derives the lens property's id from an expression tree
        /// over a captured field, so it depends on compiler-generated naming not worth relying on.
        /// </remarks>
        public PhotoModeProperty FindProperty(string match) {
            if (m_PhotoModeRenderSystem.photoModeProperties.TryGetValue(match, out PhotoModeProperty exact)) {
                return exact;
            }

            foreach (KeyValuePair<string, PhotoModeProperty> pair in m_PhotoModeRenderSystem.photoModeProperties) {
                if (pair.Key.IndexOf(match, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                    return pair.Value;
                }
            }

            return null;
        }

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


        /// <summary>Writes the focus curve for a solved shot, and opens the lens enough to see it.</summary>
        /// <remarks>
        /// Both curves are written as ordinary timeline modifiers over vanilla's own properties, so
        /// they stay editable in the curve editor afterwards like anything else the mod generates.
        /// <para>
        /// The aperture is deliberately a single value across the shot rather than a curve. It is set
        /// from the subject distance at the START of the move: an aperture that changed per keyframe
        /// would alter the exposure as the shot ran, which reads as the image brightening and dimming
        /// for no reason.
        /// </para>
        /// </remarks>
        public void ApplyFocus(IReadOnlyList<CameraSample> samples, float startTime) {
            Setting settings = Mod.Instance.Settings;

            if (settings.Focus == FocusMode.Off || settings.Focus == FocusMode.None ||
                samples == null || samples.Count == 0) {
                return;
            }

            Vector3? subject = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>().PinnedTarget;

            if (!subject.HasValue) {
                m_Log.Warn("Focus is set but no subject is pinned; leaving the lens alone.");
                return;
            }

            // A rack with no second target is a track, not an error: the ramp would run from the
            // subject to the subject, which is exactly what tracking already does.
            Vector3 rackTo = m_RackTarget ?? subject.Value;

            var distances = new List<float>(samples.Count);

            FocusSolver.Solve(samples, subject.Value, rackTo, settings.Focus, settings.FocusEase,
                              distances);

            ApplyPointCurve(kFocusDistancePropertyId, samples, distances, startTime);

            float opening = FocusSolver.ApertureFor(distances[0], settings.FocusDepth);

            SetProperty(kAperturePropertyId, opening);

        }

        /// <summary>Sets a vanilla property to a fixed value, without keyframing it.</summary>
        private void SetProperty(string match, float value) {
            PhotoModeProperty property = FindProperty(match);

            if (property?.setValue == null) {
                m_Log.Warn($"No \"{match}\" property registered; cannot set it.");
                return;
            }

            property.setValue(value);
        }

        /// <summary>Bridges the gap between the last shot's end pose and the next shot's first.</summary>
        /// <param name="seconds">How long the move takes. Zero leaves a hard cut.</param>
        /// <param name="ease">How strongly the bridge eases in and out, 0 to 1.</param>
        /// <returns>How much time the bridge consumed, to offset the shot that follows it.</returns>
        /// <remarks>
        /// Without this the sequencer produces hard cuts: shot B simply begins wherever it begins and
        /// the camera teleports. A cut is a legitimate choice, so zero is honoured — but it is not the
        /// only one, and a move between setups is what makes a sequence read as one continuous piece.
        /// <para>
        /// The bridge is written as ordinary transform keys, which is what lets it be dragged in the
        /// curve editor afterwards like anything else. It samples its own interpolation rather than
        /// leaving two distant keys for the curve to join, because a two-key bridge takes whatever
        /// route the tangents invent — usually a wide arc through somewhere neither shot goes.
        /// </para>
        /// <para>
        /// Yaw is unwrapped against the pose it leaves, so a bridge that crosses north turns the short
        /// way instead of spinning most of a circle to get there.
        /// </para>
        /// </remarks>
        public float ApplyTransition(Vector3 fromPosition, Vector3 fromRotation, Vector3 toPosition,
                                     Vector3 toRotation, float startTime, float seconds, float ease) {
            if (seconds <= 0.01f) {
                return 0f;
            }

            CinematicCameraSequence sequence = ActiveSequence;

            if (sequence == null) {
                return 0f;
            }

            // Shortest-way yaw, measured from where the camera actually is rather than from whatever
            // absolute number the next shot's first key happens to carry.
            toRotation.y = fromRotation.y + Mathf.DeltaAngle(fromRotation.y, toRotation.y);

            int steps = Mathf.Clamp(Mathf.CeilToInt(seconds * kTransitionKeysPerSecond), 2, 120);

            for (int i = 0; i <= steps; i++) {
                float t = (float)i / steps;
                float f = Easing.Blend(t, ease);

                sequence.AddCameraTransform(startTime + seconds * t,
                                            Vector3.Lerp(fromPosition, toPosition, f),
                                            Vector3.Lerp(fromRotation, toRotation, f));
            }

            m_Log.Debug($"Bridged {seconds:0.#}s between shots with {steps + 1} keys.");
            return seconds;
        }

        /// <summary>Reads the pose the sequence currently ends on, for a transition to start from.</summary>
        /// <returns>False when there is nothing on the timeline yet.</returns>
        public bool TryGetEndPose(out Vector3 position, out Vector3 rotation) {
            position = default;
            rotation = default;

            CinematicCameraSequence sequence = ActiveSequence;

            if (sequence == null || sequence.transformCount == 0) {
                return false;
            }

            float end = NextStartTime(false);

            var transforms = sequence.transforms;

            position = new Vector3(transforms[0].curve.Evaluate(end),
                                   transforms[1].curve.Evaluate(end),
                                   transforms[2].curve.Evaluate(end));

            rotation = new Vector3(transforms[3].curve.Evaluate(end),
                                   transforms[4].curve.Evaluate(end), 0f);

            return true;
        }

        /// <summary>Runs a solved shot through its camera rig, whatever generator produced it.</summary>
        /// <remarks>
        /// Applied AFTER framing and BEFORE the keys are written. Framing decides where the camera
        /// should be looking; the rig decides how faithfully a physical support could have got there.
        /// Doing it the other way round would have the framing solver correct away the very lag the
        /// rig exists to introduce.
        /// </remarks>
        public void ApplyRig(List<CameraSample> samples) {
            Setting settings = Mod.Instance.Settings;

            RigSolver.Apply(samples, settings.Rig, settings.RigStrength, settings.RigSeed);
        }

        /// <summary>Applies the framing rule to a solved shot, whatever generator produced it.</summary>
        /// <remarks>
        /// One choke point rather than a call in each generator: framing is a property of the shot, not
        /// of how its path was arrived at, so an orbit, a drawn path and a dolly should all obey it
        /// identically. Silently does nothing when no subject is pinned — there is nothing to frame,
        /// and a shot that quietly aimed at the origin instead would be worse than one that ignored the
        /// setting.
        /// </remarks>
        public void ApplyFraming(List<CameraSample> samples, List<float> focalLengths) {
            Setting settings = Mod.Instance.Settings;

            if (settings.Framing == FramingRule.None) {
                return;
            }

            Vector3? subject = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>().PinnedTarget;

            if (!subject.HasValue) {
                m_Log.Warn("Framing is set but no subject is pinned; leaving the shot's own aim alone.");
                return;
            }

            FramingSolver.Apply(samples, subject.Value, settings.Framing, focalLengths,
                                settings.FramingHoldSize, settings.FramingFocalLength);
        }
    }
}
