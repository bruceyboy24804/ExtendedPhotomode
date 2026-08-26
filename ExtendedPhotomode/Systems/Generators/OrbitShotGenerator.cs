namespace ExtendedPhotomode.Systems.Generators {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;

    #endregion

    /// <summary>A circle or spiral around a point, from the Orbit Shot settings.</summary>
    /// <remarks>
    /// The shape comes from the settings; the only thing pressing Generate contributes is
    /// <em>where</em> to orbit, which <see cref="EPM_ShotSubjectSystem"/> resolves from the live
    /// camera unless a centre is pinned.
    /// </remarks>
    public sealed class OrbitShotGenerator : GenerateShotBase {
        public override ShotType Type => ShotType.Orbit;

        public override bool TryGenerate() {
            if (!Subject.TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                Log.Warn("No active camera controller; cannot place an orbit target.");
                return false;
            }

            if (Shots.ActiveSequence == null) {
                Log.Warn("No active cinematic sequence; cannot apply orbit.");
                return false;
            }

            List<CameraSample> samples = orbit.Solve();

            if (samples.Count == 0) {
                Log.Warn($"Orbit solved to zero keys: {orbit}");
                return false;
            }

            return Shots.ApplySamples(samples, Shots.NextStartTime(Replaces), Replaces, orbit.ToString());
        }
    }
}
