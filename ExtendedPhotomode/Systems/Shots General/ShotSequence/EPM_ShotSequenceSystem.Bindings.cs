namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using Game.CinematicCamera;

    using ModsCommon.Extensions;

    using UnityEngine;

    #endregion

    /// <summary>Pushing changed curves back to the vanilla cinematic panel.</summary>
    public partial class EPM_ShotSequenceSystem {
        private const string kTransformCurveBindingField = "m_TransformAnimationCurveBinding";

        private const string kGetTransformCurvesMethod = "GetTransformCurves";

        private const string kModifierCurveBindingField = "m_ModifierAnimationCurveBinding";

        private void LogYawCurve(CinematicCameraSequence sequence) {
            AnimationCurve yaw = sequence.transforms[(int)CinematicCameraSequence.TransformCurveKey.RotationY].curve;
            if (yaw == null || yaw.length == 0) {
                return;
            }

            var keys = new System.Text.StringBuilder();
            for (int i = 0; i < yaw.length; i++) {
                Keyframe key = yaw[i];
                keys.Append($"[t={key.time:0.##} v={key.value:0.#} in={key.inTangent:0.##} out={key.outTangent:0.##}] ");
            }

            m_Log.Debug($"Yaw curve: {keys}");
        }

        public void RefreshModifierCurveBinding() {
            CinematicCameraSequence sequence = ActiveSequence;

            if (sequence == null) {
                return;
            }

            object binding = m_CinematicCameraUISystem.GetMemberValue(kModifierCurveBindingField);

            if (binding == null) {
                m_Log.Warn($"{kModifierCurveBindingField} not found; the curve editor may show stale modifiers.");
                return;
            }

            binding.TryInvokeMethod("Update", out _, sequence.modifiers.ToArray());
        }

        internal void RefreshTransformCurveBinding() {
            object binding = m_CinematicCameraUISystem.GetMemberValue(kTransformCurveBindingField);
            if (binding == null) {
                m_Log.Warn($"{kTransformCurveBindingField} not found; cinematic panel may show stale curves.");
                return;
            }

            if (!m_CinematicCameraUISystem.TryInvokeMethod(kGetTransformCurvesMethod, out object curves)) {
                m_Log.Warn($"{kGetTransformCurvesMethod} not found; cinematic panel may show stale curves.");
                return;
            }

            binding.TryInvokeMethod("Update", out _, curves);
        }
    }
}
