namespace ExtendedPhotomode.Patches {
    #region Using Statements

    using Game.CinematicCamera;

    using HarmonyLib;

    using UnityEngine;

    #endregion

    /// <summary>Repairs the yaw curve's tangents after vanilla flattens them.</summary>
    /// <remarks>
    /// <c>CinematicCameraSequence.PatchRotations</c> keeps yaw continuous by rewriting each key as
    /// <c>MoveKey(i, new Keyframe(time, value))</c> — and <c>Keyframe(float, float)</c> zeroes both
    /// tangents, so the curve comes out flat at every key and the camera's rotation stalls once per
    /// keyframe. It re-runs from <c>AfterModifications</c> on any sequence change, so re-smoothing
    /// after writing a shot is not enough: toggling loop or dragging a key reinstates it.
    /// </remarks>
    [HarmonyPatch(typeof(CinematicCameraSequence), "PatchRotations")]
    public static class CinematicCameraSequencePatchRotationsPatch {
        [HarmonyPostfix]
        public static void Postfix(CinematicCameraSequence __instance) {
            if (!Mod.Instance.Settings.SmoothCameraRotation) {
                return;
            }

            CinematicCameraSequence.CinematicCameraCurveModifier[] transforms = __instance?.transforms;

            int index = (int)CinematicCameraSequence.TransformCurveKey.RotationY;
            if (transforms == null || index >= transforms.Length) {
                return;
            }

            AnimationCurve yaw = transforms[index].curve;
            if (yaw == null) {
                return;
            }

            for (int i = 0; i < yaw.length; i++) {
                yaw.SmoothTangents(i, 0f);
            }
        }
    }
}
