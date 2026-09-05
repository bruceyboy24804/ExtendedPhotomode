namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using Game;
    using Game.Rendering;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine.Rendering;
    using UnityEngine.Rendering.HighDefinition;

    #endregion

    /// <summary>Stops photo mode from silently downgrading post-process effects to HDRP's built-in defaults.</summary>
    /// <remarks>
    /// <c>VolumeHelper.GetOrCreateVolumeComponent</c> calls <c>quality.Override(3)</c> on every
    /// component it adds that derives from <c>VolumeComponentWithQuality</c>. In HDRP a quality value
    /// of 3 does not mean "highest" — <c>ScalableSettingLevelParameter</c> reads it as
    /// <c>useOverride</c>, which makes <c>UsesQualitySettings()</c> return false and the component
    /// fall back to its own inline serialized fields instead of the tier configured in the render
    /// pipeline asset. Because the override is applied with <c>overrideState = true</c> on a volume
    /// at priority 2000, it wins the blend outright the moment photo mode raises the volume's weight.
    /// The effect is photo-mode-specific only for components the photo mode volume alone carries.
    /// <c>Fog</c> is deliberately not in <see cref="kAffectedComponents"/>: the always-on
    /// <c>ClimateControlVolume</c> applies the same override to it, so fog looks identical in and out
    /// of photo mode and "fixing" it here would change normal gameplay rendering instead.
    /// Clearing <c>overrideState</c> rather than writing a level lets the value fall through the volume
    /// stack to the pipeline default, the same path the game takes outside photo mode.
    /// </remarks>
    public partial class EPM_PhotoModeQualitySystem : GameSystemBase {
        private const string kVolumeField = "m_CameraControlVolume";

        private static readonly Type[] kAffectedComponents = {
            typeof(Bloom),
            typeof(DepthOfField),
            typeof(MotionBlur),
        };

        private PhotoModeRenderSystem m_PhotoModeRenderSystem;
        private PrefixedLogger        m_Log;
        private bool                  m_Applied;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                   = new PrefixedLogger(nameof(EPM_PhotoModeQualitySystem));
            m_PhotoModeRenderSystem = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
        }

        protected override void OnUpdate() {
            if (m_Applied) {
                Enabled = false;
                return;
            }

            m_Applied = true;
            Enabled   = false;

            if (!Mod.Instance.Settings.RestorePostProcessQuality) {
                return;
            }

            ClearQualityOverrides();
        }

        private void ClearQualityOverrides() {
            if (!(m_PhotoModeRenderSystem.GetMemberValue(kVolumeField) is Volume volume)) {
                m_Log.Warn($"{kVolumeField} not found on {nameof(PhotoModeRenderSystem)}; photo mode post-process quality left as vanilla.");
                return;
            }

            VolumeProfile profile = volume.sharedProfile;
            if (profile == null) {
                m_Log.Warn("Photo mode volume has no profile; nothing to adjust.");
                return;
            }

            foreach (Type type in kAffectedComponents) {
                if (!profile.TryGet(type, out VolumeComponent component)) {
                    m_Log.Debug($"{type.Name} not present on the photo mode volume.");
                    continue;
                }

                if (!(component is VolumeComponentWithQuality withQuality)) {
                    m_Log.Warn($"{type.Name} is no longer a VolumeComponentWithQuality; skipping.");
                    continue;
                }

                if (!withQuality.quality.overrideState) {
                    continue;
                }

                withQuality.quality.overrideState = false;
            }
        }
    }
}
