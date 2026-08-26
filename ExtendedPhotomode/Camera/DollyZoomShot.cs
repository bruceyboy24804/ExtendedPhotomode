namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;

    #endregion

    /// <summary>
    /// A dolly zoom: the camera moves toward or away from a subject while the lens counter-zooms, so
    /// the subject holds its size in frame and the background appears to rush.
    /// </summary>
    /// <remarks>
    /// The effect depends on one relationship. The width of the frame at the subject's distance is
    /// proportional to <c>distance / focalLength</c>, so holding the subject at a constant size means
    /// holding that ratio constant — which makes focal length scale <b>linearly</b> with distance.
    /// Everything else about the shot follows from that.
    /// </remarks>
    public struct DollyZoomShot {
        public Vector3 Target;

        public float Bearing;

        public float StartDistance;

        public float EndDistance;

        public float Height;

        public float Duration;

        public int Keys;

        public const int kMinKeys = 2;

        public List<CameraSample> Solve(float startFocalLength, out List<float> focalLengths) {
            int count = Mathf.Max(kMinKeys, Keys);
            int last  = count - 1;

            var samples = new List<CameraSample>(count);
            focalLengths = new List<float>(count);

            float startDistance = Mathf.Max(StartDistance, 0.01f);
            float rad           = Bearing * Mathf.Deg2Rad;
            var   heading       = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            for (int i = 0; i < count; i++) {
                float f        = (float)i / last;
                float distance = Mathf.Max(Mathf.Lerp(startDistance, EndDistance, f), 0.01f);

                samples.Add(new CameraSample {
                    Time     = Duration * f,
                    Position = Target + heading * distance + new Vector3(0f, Height, 0f),
                    Rotation = new Vector3(Pitch(distance, Height), Bearing + 180f, 0f),
                });

                focalLengths.Add(startFocalLength * distance / startDistance);
            }

            return samples;
        }

        private static float Pitch(float distance, float height) {
            return Mathf.Atan2(height, Mathf.Max(distance, 0.01f)) * Mathf.Rad2Deg;
        }

        public override string ToString() {
            return $"DollyZoom(target={Target}, {StartDistance:0.#}m -> {EndDistance:0.#}m, h={Height:0.#}m, "
                   + $"bearing={Bearing:0.#}°, {Duration:0.#}s, {Mathf.Max(kMinKeys, Keys)} keys)";
        }
    }
}
