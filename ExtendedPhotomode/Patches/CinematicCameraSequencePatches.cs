namespace ExtendedPhotomode.Patches {
    #region Using Statements

    using ExtendedPhotomode.Systems;

    using Game.CinematicCamera;
    using Game.Rendering;

    using HarmonyLib;

    using UnityEngine;

    #endregion

    /// <summary>Lets a shot track a subject that moves while it plays.</summary>
    /// <remarks>
    /// <c>Refresh</c> is the single funnel every play, scrub and slider drag goes through, and it ends
    /// by writing the sampled curve values onto the controller — so a postfix here is the whole of the
    /// runtime seam, for all three, with the sequence itself left untouched. See
    /// <see cref="EPM_FollowSubjectSystem"/> for why this cannot be baked into keyframes instead.
    /// </remarks>
    [HarmonyPatch(typeof(CinematicCameraSequence), nameof(CinematicCameraSequence.Refresh))]
    public static class CinematicCameraSequenceRefreshPatch {
        [HarmonyPostfix]
        public static void Postfix(IGameCameraController controller) {
            EPM_FollowSubjectSystem.Instance?.Apply(controller);
        }
    }

    /// <summary>Snapshots the sequence before anything changes it, for undo.</summary>
    /// <remarks>
    /// One prefix over every mutating method on the sequence rather than a command log in the panel.
    /// Edits reach the sequence by several routes — our curve editor calls vanilla's own triggers,
    /// vanilla's editor is still live beside ours, and assembling a shot list rewrites every curve at
    /// once — and a log would have to model each of them, silently missing any route it did not know
    /// about. Sitting in front of the mutation itself is agnostic about what caused it.
    ///
    /// <c>AfterModifications</c> looks like a single tidier funnel and is not usable: it runs *after*
    /// the change, so the state it could capture is already the edited one.
    ///
    /// Note <c>Reset</c> is included deliberately — wiping the sequence is the edit people most want
    /// back, and it is the one vanilla offers no way to recover from.
    /// </remarks>
    [HarmonyPatch]
    public static class CinematicCameraSequenceHistoryPatch {
        [HarmonyPatch(typeof(CinematicCameraSequence), nameof(CinematicCameraSequence.MoveKeyframe))]
        [HarmonyPatch(typeof(CinematicCameraSequence), nameof(CinematicCameraSequence.AddCameraTransform))]
        [HarmonyPatch(typeof(CinematicCameraSequence), nameof(CinematicCameraSequence.RemoveCameraTransform))]
        [HarmonyPatch(typeof(CinematicCameraSequence), nameof(CinematicCameraSequence.RemoveModifierKey))]
        [HarmonyPatch(typeof(CinematicCameraSequence), nameof(CinematicCameraSequence.RemoveModifier))]
        [HarmonyPatch(typeof(CinematicCameraSequence), nameof(CinematicCameraSequence.Reset))]
        [HarmonyPrefix]
        public static void Prefix() {
            EPM_TimelineHistorySystem.Record();
        }
    }

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
