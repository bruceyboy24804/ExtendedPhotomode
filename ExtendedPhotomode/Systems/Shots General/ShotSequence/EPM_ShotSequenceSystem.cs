namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;

    using Game;
    using Game.CinematicCamera;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Rendering.CinematicCamera;
    using Game.UI.InGame;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Writes solved camera poses into the sequence the vanilla cinematic camera panel is editing,
    /// and owns everything about that sequence afterwards: timing, tangents, easing and the
    /// environment ramps.
    /// </summary>
    /// <remarks>
    /// This deliberately writes into vanilla's own <see cref="CinematicCameraSequence"/> rather than
    /// running a parallel timeline. Everything downstream — playback, scrubbing, saving to a
    /// <c>CinematicCameraAsset</c>, the curve editor in the panel — then works for free.
    /// Nothing here knows what a shot <em>is</em>. Generators solve geometry and hand it to
    /// <see cref="ApplySamples"/>; the subject they aim at lives on <see cref="EPM_ShotSubjectSystem"/>.
    /// </remarks>
    public partial class EPM_ShotSequenceSystem : GameSystemBase {
        private readonly Dictionary<int, KeyframeEase> m_ChosenEase = new Dictionary<int, KeyframeEase>();

        private CinematicCameraUISystem m_CinematicCameraUISystem;
        private PhotoModeRenderSystem   m_PhotoModeRenderSystem;
        private PlanetarySystem         m_PlanetarySystem;
        private PrefixedLogger          m_Log;

        public CinematicCameraSequence ActiveSequence => m_CinematicCameraUISystem?.activeSequence;

        public SunModel Sun => SunModel.From(m_PlanetarySystem);
        public bool ApplySamples(IReadOnlyList<CameraSample> samples, float startTime, bool replaceExisting,
                                 string description) {
            CinematicCameraSequence sequence = ActiveSequence;
            if (sequence == null) {
                m_Log.Warn("No active cinematic sequence; cannot apply shot.");
                return false;
            }

            if (samples == null || samples.Count == 0) {
                m_Log.Warn($"Nothing to apply for {description}.");
                return false;
            }

            if (replaceExisting) {
                sequence.Reset();
            }

            foreach (CameraSample sample in samples) {
                sequence.AddCameraTransform(startTime + sample.Time, sample.Position, sample.Rotation);
            }

            float end = startTime + samples[samples.Count - 1].Time;
            if (sequence.playbackDuration < end) {
                sequence.playbackDuration = end;
            }

            m_ChosenEase.Clear();

            SmoothTransformCurves(sequence);
            ApplyTimeOfDay(sequence, end);
            m_Log.Info($"Applied {samples.Count} keys from {description} at t={startTime:0.##}s");
            LogYawCurve(sequence);

            RefreshTransformCurveBinding();
            return true;
        }
    }
}
