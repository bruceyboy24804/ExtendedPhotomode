namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using UnityEngine;

    #endregion

    /// <summary>The orbit parameters behind a saved cinematic shot, including where it was centred.</summary>
    /// <remarks>
    /// Vanilla's <c>CinematicCameraAsset</c> stores the resulting keyframes but nothing about how
    /// they were produced, so a loaded shot cannot be re-dialled — widening an orbit by 50m means
    /// rebuilding it by eye. This is the missing half, kept in a sidecar keyed by the asset's guid.
    /// Plain fields and a parameterless constructor because it is round-tripped through JSON.
    /// </remarks>
    public class OrbitSetup {
        public float TargetX { get; set; }

        public float TargetY { get; set; }

        public float TargetZ { get; set; }

        public int Radius { get; set; }

        public int Height { get; set; }

        public float StartAngle { get; set; }

        public int Sweep { get; set; }

        public int Duration { get; set; }

        public int DegreesPerKey { get; set; }

        public bool LookAtTarget { get; set; }

        public Vector3 Target => new Vector3(TargetX, TargetY, TargetZ);

        public static OrbitSetup From(OrbitShot orbit) {
            return new OrbitSetup {
                TargetX       = orbit.Target.x,
                TargetY       = orbit.Target.y,
                TargetZ       = orbit.Target.z,
                Radius        = Mathf.RoundToInt(orbit.Radius),
                Height        = Mathf.RoundToInt(orbit.Height),
                StartAngle    = orbit.StartAngle,
                Sweep         = Mathf.RoundToInt(orbit.Sweep),
                Duration      = Mathf.RoundToInt(orbit.Duration),
                DegreesPerKey = Mathf.RoundToInt(orbit.DegreesPerKey),
                LookAtTarget  = orbit.LookAtTarget,
            };
        }

        public void ApplyTo(Setting settings) {
            settings.OrbitRadius        = Radius;
            settings.OrbitHeight        = Height;
            settings.OrbitSweep         = Sweep;
            settings.OrbitDuration      = Duration;
            settings.OrbitDegreesPerKey = DegreesPerKey;
            settings.OrbitLookAtTarget  = LookAtTarget;
        }

        public bool Matches(Setting settings, Vector3 target) {
            return Radius == settings.OrbitRadius
                && Height == settings.OrbitHeight
                && Sweep == settings.OrbitSweep
                && Duration == settings.OrbitDuration
                && DegreesPerKey == settings.OrbitDegreesPerKey
                && LookAtTarget == settings.OrbitLookAtTarget
                && (Target - target).sqrMagnitude < 0.01f;
        }
    }
}
