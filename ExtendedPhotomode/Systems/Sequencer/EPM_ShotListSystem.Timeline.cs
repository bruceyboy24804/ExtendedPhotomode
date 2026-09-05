namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using UnityEngine;

    #endregion

    /// <summary>Everything the shot list panel shows about the timeline it assembles onto.</summary>
    /// <remarks>
    /// These used to be photo mode rows. They moved for the same reason the shot's own settings moved
    /// to the tool: they belong beside the thing they act on. Chaining, transitions and retiming are
    /// all about how shots join into a sequence, and the shot list IS the sequence — reading a
    /// transition length on one panel while looking at the order it applies to on another was the
    /// only reason it was ever hard to reason about.
    /// </remarks>
    public struct TimelineNumbers {
        /// <summary>Length of the whole assembled sequence, in seconds.</summary>
        public float duration;

        /// <summary>How many keyframes are on the timeline. Zero means there is nothing to retime.</summary>
        public int keyframes;

        /// <summary>Whether a generated shot appends rather than replacing what is there.</summary>
        public bool chain;

        /// <summary>Dead seconds left between one shot and the next when chaining.</summary>
        public int gap;

        public bool constantSpeed;

        public float transitionSeconds;

        public float transitionEase;
    }

    /// <summary>Timeline controls for <see cref="EPM_ShotListSystem"/>.</summary>
    public partial class EPM_ShotListSystem {
        public const string kTimelineBinding = "shotTimeline";

        public const string kSetTimelineTrigger = "setShotTimeline";

        private TimelineNumbers BuildTimeline() {
            Setting settings = Mod.Instance.Settings;

            return new TimelineNumbers {
                duration      = m_Shots.SequenceDuration,
                keyframes     = m_Shots.KeyframeCount,
                chain         = !settings.OrbitReplacesSequence,
                gap           = settings.ShotTransition,
                constantSpeed = settings.ConstantSpeed,

                transitionSeconds = settings.TransitionSeconds,
                transitionEase    = settings.TransitionEase,
            };
        }

        private void SetTimeline(string field, float value) {
            Setting settings = Mod.Instance.Settings;

            switch (field) {
                // Retiming rescales the keys that already exist rather than changing a setting, so it
                // acts on the sequence immediately and there is nothing to save.
                case "duration":
                    m_Shots.RetimeSequence(Mathf.Max(value, 1f));
                    return;

                case "chain":
                    settings.OrbitReplacesSequence = value <= 0.5f;
                    break;

                case "gap":
                    settings.ShotTransition = Mathf.Clamp(Mathf.RoundToInt(value), 0, 30);
                    break;

                // Flattening every key at once is a real edit to the curve, not just a flag, so the
                // sequence is re-tangented the moment it changes.
                case "constantSpeed":
                    settings.ConstantSpeed = value > 0.5f;
                    settings.ApplyAndSave();
                    m_Shots.RetangentSequence();
                    return;

                case "transitionSeconds":
                    settings.TransitionSeconds = Mathf.Clamp(value, 0f, 60f);
                    break;

                case "transitionEase":
                    settings.TransitionEase = Mathf.Clamp01(value);
                    break;

                // Per-keyframe easing used to live here as a picker plus a row of ease buttons. It is
                // gone: the curve editor edits the same tangents directly, with handles, and two ways
                // to set one value is two ways for them to disagree.

                default:
                    m_Log.Warn($"Unknown timeline field \"{field}\".");
                    return;
            }

            settings.ApplyAndSave();
        }
    }
}
