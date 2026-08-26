namespace ExtendedPhotomode.Systems.Generators {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;

    using Game.Rendering;
    using Game.CinematicCamera;
    using Game.Rendering.CinematicCamera;

    using UnityEngine;

    #endregion

    /// <summary>A dolly zoom: the camera moves while the lens counter-zooms.</summary>
    /// <remarks>
    /// Reuses the shot subject rather than introducing a second notion of "the thing being filmed" —
    /// a dolly zoom is aimed at exactly the same point an orbit is, which is why the Subject section
    /// of the panel stays visible while this shot type is selected.
    /// </remarks>
    public sealed class DollyZoomGenerator : GenerateShotBase {
        public override ShotType Type => ShotType.DollyZoom;

        public override bool TryGenerate() {
            if (!Subject.TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                Log.Warn("No camera controller; cannot place a dolly zoom.");
                return false;
            }

            PhotoModeProperty focal = FindFocalLengthProperty();

            if (focal == null) {
                Log.Warn("No focal length property registered; cannot generate a dolly zoom.");
                return false;
            }

            var shot = new DollyZoomShot {
                Target        = orbit.Target,
                Bearing       = orbit.StartAngle,
                StartDistance = Settings.DollyStartDistance,
                EndDistance   = Settings.DollyEndDistance,
                Height        = Settings.OrbitHeight,
                Duration      = Settings.DollyDuration,
                Keys          = Settings.DollyKeys,
            };

            List<CameraSample> samples = shot.Solve(focal.getValue(), out List<float> focalLengths);
            float              start   = Shots.NextStartTime(Replaces);

            if (!Shots.ApplySamples(samples, start, Replaces, shot.ToString())) {
                return false;
            }

            CinematicCameraSequence sequence = Shots.ActiveSequence;
            float                   min      = focal.min?.Invoke() ?? 1f;
            float                   max      = focal.max?.Invoke() ?? 1000f;

            for (int i = 0; i < samples.Count; i++) {
                sequence.AddModifierKey(focal.id, start + samples[i].Time,
                                        Mathf.Clamp(focalLengths[i], min, max), min, max);
            }

            focal.setEnabled?.Invoke(true);

            Log.Info($"Applied dolly zoom: focal {focalLengths[0]:0.#}mm -> " +
                     $"{focalLengths[focalLengths.Count - 1]:0.#}mm");

            Shots.RefreshModifierCurveBinding();
            return true;
        }

        private PhotoModeProperty FindFocalLengthProperty() {
            var render = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();

            foreach (KeyValuePair<string, PhotoModeProperty> pair in render.photoModeProperties) {
                if (pair.Key.IndexOf("focalLength", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return pair.Value;
                }
            }

            return null;
        }
    }
}
