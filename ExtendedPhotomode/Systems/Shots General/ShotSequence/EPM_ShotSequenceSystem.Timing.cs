namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using Game.CinematicCamera;

    using UnityEngine;

    #endregion

    /// <summary>Where a shot starts on the timeline, and how long the whole sequence runs.</summary>
    public partial class EPM_ShotSequenceSystem {
        public float NextStartTime(bool replaceExisting) {
            CinematicCameraSequence sequence = ActiveSequence;

            if (replaceExisting || sequence == null || sequence.timelineLength <= 0.01f) {
                return 0f;
            }

            return sequence.timelineLength + Mod.Instance.Settings.ShotTransition;
        }

        public float SequenceDuration => ActiveSequence?.playbackDuration ?? 0f;

        public bool RetimeSequence(float duration) {
            CinematicCameraSequence sequence = ActiveSequence;

            if (sequence == null || duration <= 0.01f) {
                return false;
            }

            float length = sequence.timelineLength;

            if (length <= 0.01f) {
                sequence.playbackDuration = duration;
                return false;
            }

            float scale = duration / length;

            if (Mathf.Approximately(scale, 1f)) {
                return false;
            }

            foreach (CinematicCameraSequence.CinematicCameraCurveModifier transform in sequence.transforms) {
                ScaleCurveTimes(transform.curve, scale);
            }

            foreach (CinematicCameraSequence.CinematicCameraCurveModifier modifier in sequence.modifiers) {
                ScaleCurveTimes(modifier.curve, scale);
            }

            sequence.playbackDuration = duration;
            RefreshTransformCurveBinding();
            return true;
        }

        private static void ScaleCurveTimes(AnimationCurve curve, float scale) {
            if (curve == null) {
                return;
            }

            for (int i = curve.length - 1; i >= 0; i--) {
                Keyframe key = curve[i];

                key.time       *= scale;
                key.inTangent  /= scale;
                key.outTangent /= scale;

                curve.MoveKey(i, key);
            }
        }
    }
}
