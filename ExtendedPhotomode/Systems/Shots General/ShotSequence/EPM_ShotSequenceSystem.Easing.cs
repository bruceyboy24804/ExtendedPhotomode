namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using ExtendedPhotomode.Camera;

    using Game.CinematicCamera;

    using UnityEngine;

    #endregion

    /// <summary>Per-keyframe tangents: how the camera arrives at and leaves each key.</summary>
    public partial class EPM_ShotSequenceSystem {
        private static void SmoothTransformCurves(CinematicCameraSequence sequence) {
            bool constantSpeed = Mod.Instance.Settings.ConstantSpeed;

            foreach (CinematicCameraSequence.CinematicCameraCurveModifier transform in sequence.transforms) {
                ApplyTangents(transform.curve, constantSpeed);
            }
        }

        public bool RetangentSequence() {
            CinematicCameraSequence sequence = ActiveSequence;

            if (sequence == null) {
                m_Log.Warn("No active cinematic sequence; nothing to re-tangent.");
                return false;
            }

            if (sequence.timelineLength <= 0f) {
                return false;
            }

            m_ChosenEase.Clear();

            SmoothTransformCurves(sequence);
            RefreshTransformCurveBinding();

            m_Log.Info($"Re-tangented the open sequence (constantSpeed={Mod.Instance.Settings.ConstantSpeed}).");
            return true;
        }

        public int KeyframeCount {
            get {
                CinematicCameraSequence sequence = ActiveSequence;
                AnimationCurve          curve    = sequence?.transforms[0].curve;

                return curve?.length ?? 0;
            }
        }

        public KeyframeEase GetKeyframeEase(int index) {
            CinematicCameraSequence sequence = ActiveSequence;
            AnimationCurve          curve    = sequence?.transforms[0].curve;

            if (curve == null || index < 0 || index >= curve.length) {
                return KeyframeEase.Smooth;
            }

            if (m_ChosenEase.TryGetValue(index, out KeyframeEase chosen)) {
                return chosen;
            }

            Keyframe key    = curve[index];
            bool     flatIn = Mathf.Abs(key.inTangent) < 1e-4f;
            bool     flatOut = Mathf.Abs(key.outTangent) < 1e-4f;

            if (flatIn && flatOut) {
                return KeyframeEase.InOut;
            }

            if (flatIn) {
                return KeyframeEase.In;
            }

            if (flatOut) {
                return KeyframeEase.Out;
            }

            return Mathf.Abs(key.inTangent - key.outTangent) > 1e-4f ? KeyframeEase.Linear : KeyframeEase.Smooth;
        }

        public bool SetKeyframeEase(int index, KeyframeEase ease) {
            CinematicCameraSequence sequence = ActiveSequence;
            AnimationCurve          reference = sequence?.transforms[0].curve;

            if (reference == null || index < 0 || index >= reference.length) {
                return false;
            }

            float time = reference[index].time;

            foreach (CinematicCameraSequence.CinematicCameraCurveModifier transform in sequence.transforms) {
                ApplyEaseAtTime(transform.curve, time, ease);
            }

            m_ChosenEase[index] = ease;
            RefreshTransformCurveBinding();
            m_Log.Info($"Keyframe {index} at t={time:0.##}s set to {ease}.");
            return true;
        }

        private static void ApplyEaseAtTime(AnimationCurve curve, float time, KeyframeEase ease) {
            if (curve == null || curve.length == 0) {
                return;
            }

            int index = -1;

            for (int i = 0; i < curve.length; i++) {
                if (Mathf.Abs(curve[i].time - time) < 1e-3f) {
                    index = i;
                    break;
                }
            }

            if (index < 0) {
                return;
            }

            Keyframe key   = curve[index];
            int      last  = curve.length - 1;

            float before = (index > 0) ? Slope(curve[index - 1], key) : float.NaN;
            float after  = (index < last) ? Slope(key, curve[index + 1]) : float.NaN;

            if (float.IsNaN(before)) { before = float.IsNaN(after) ? 0f : after; }
            if (float.IsNaN(after))  { after  = before; }

            float averaged = (before + after) * 0.5f;

            switch (ease) {
                case KeyframeEase.In:
                    key.inTangent  = 0f;
                    key.outTangent = after;
                    break;

                case KeyframeEase.Out:
                    key.inTangent  = before;
                    key.outTangent = 0f;
                    break;

                case KeyframeEase.InOut:
                    key.inTangent  = 0f;
                    key.outTangent = 0f;
                    break;

                case KeyframeEase.Linear:
                    key.inTangent  = before;
                    key.outTangent = after;
                    break;

                default:
                    key.inTangent  = averaged;
                    key.outTangent = averaged;
                    break;
            }

            curve.MoveKey(index, key);
        }

        private static void ApplyTangents(AnimationCurve curve, bool linear) {
            if (curve == null || curve.length < 2) {
                return;
            }

            int last = curve.length - 1;

            for (int i = 0; i <= last; i++) {
                Keyframe key = curve[i];

                float before = (i > 0) ? Slope(curve[i - 1], key) : float.NaN;
                float after  = (i < last) ? Slope(key, curve[i + 1]) : float.NaN;

                if (float.IsNaN(before)) { before = after; }
                if (float.IsNaN(after))  { after  = before; }

                if (linear) {
                    key.inTangent  = before;
                    key.outTangent = after;
                } else {
                    float averaged = (before + after) * 0.5f;
                    key.inTangent  = averaged;
                    key.outTangent = averaged;
                }

                curve.MoveKey(i, key);
            }
        }

        private static float Slope(Keyframe from, Keyframe to) {
            float dt = to.time - from.time;
            return (Mathf.Abs(dt) < 1e-6f) ? 0f : (to.value - from.value) / dt;
        }
    }
}
