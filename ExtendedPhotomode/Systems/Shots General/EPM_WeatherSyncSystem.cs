namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using Game;
    using Game.Rendering;
    using Game.Simulation;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine.Rendering;
    using UnityEngine.Rendering.HighDefinition;

    #endregion

    /// <summary>
    /// Carries the live weather state into photo mode, so mods driving the climate — Time &amp;
    /// Weather Anarchy in particular — keep showing what they set once photo mode opens.
    /// </summary>
    /// <remarks>
    /// The conflict is one of volume priority. Weather Anarchy sets
    /// <c>ClimateSystem.fog</c> / <c>cloudiness</c> / <c>precipitation</c> overrides;
    /// <see cref="ClimateRenderSystem"/> reads those through <c>SampleClimate</c> and applies the
    /// result to its own <c>ClimateControlVolume</c>, which sits at priority 50. Photo mode's
    /// <c>CinematicControlVolume</c> sits at priority <b>2000</b> and carries its own copies of the
    /// same <c>Fog</c>, <c>VolumetricClouds</c> and <c>CloudLayer</c> components. The moment a photo
    /// mode weather slider is touched, <c>PhotoModeUtils.BindProperty</c> calls
    /// <c>parameter.Override(...)</c> — which sets <c>overrideState</c> — and that value outranks the
    /// climate volume for the rest of the session. The photo mode copies start at HDRP's defaults
    /// rather than at the weather actually on screen, so the picture jumps.
    /// Vanilla already does this one layer over — <c>SyncColorProperties</c> seeds ColorAdjustments and
    /// WhiteBalance from the climate volume on entry — but never for the weather components. This
    /// copies value only, never <c>overrideState</c>, so photo mode stays transparent until opted in.
    /// </remarks>
    public partial class EPM_WeatherSyncSystem : GameSystemBase {
        private const string kVolumeField = "m_CameraControlVolume";

        private static readonly Type[] kSyncedComponents = {
            typeof(Fog),
            typeof(VolumetricClouds),
            typeof(CloudLayer),
        };

        private PhotoModeRenderSystem m_PhotoModeRenderSystem;
        private PlanetarySystem       m_PlanetarySystem;
        private ClimateSystem         m_ClimateSystem;
        private ClimateRenderSystem   m_ClimateRenderSystem;
        private PrefixedLogger        m_Log;
        private bool                  m_WasActive;

        private bool  m_Captured;
        private bool  m_OverrideTime;
        private float m_Time;
        private (bool active, float value) m_Temperature;
        private (bool active, float value) m_Precipitation;
        private (bool active, float value) m_Cloudiness;
        private (bool active, float value) m_Aurora;
        private (bool active, float value) m_Fog;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                   = new PrefixedLogger(nameof(EPM_WeatherSyncSystem));
            m_PhotoModeRenderSystem = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_ClimateRenderSystem   = World.GetOrCreateSystemManaged<ClimateRenderSystem>();
            m_PlanetarySystem       = World.GetOrCreateSystemManaged<PlanetarySystem>();
            m_ClimateSystem         = World.GetOrCreateSystemManaged<ClimateSystem>();
        }

        protected override void OnUpdate() {
            bool isActive = m_PhotoModeRenderSystem.Enabled;

            if (isActive && !m_WasActive) {
                Capture();

                if (Mod.Instance.Settings.SyncWeatherIntoPhotoMode) {
                    SyncWeather();
                }
            } else if (!isActive && m_WasActive) {
                Restore();
            }

            m_WasActive = isActive;
        }

        private void Capture() {
            if (!Mod.Instance.Settings.RestoreTimeAndWeatherOnExit) {
                return;
            }

            m_Captured        = true;
            m_OverrideTime    = m_PlanetarySystem.overrideTime;
            m_Time            = m_PlanetarySystem.time;
            m_Temperature     = Snapshot(m_ClimateSystem.temperature);
            m_Precipitation   = Snapshot(m_ClimateSystem.precipitation);
            m_Cloudiness      = Snapshot(m_ClimateSystem.cloudiness);
            m_Aurora          = Snapshot(m_ClimateSystem.aurora);
            m_Fog             = Snapshot(m_ClimateSystem.fog);
        }

        private void Restore() {
            if (!m_Captured) {
                return;
            }

            m_Captured = false;

            m_PlanetarySystem.overrideTime = m_OverrideTime;
            m_PlanetarySystem.time         = m_Time;

            Apply(m_ClimateSystem.temperature, m_Temperature);
            Apply(m_ClimateSystem.precipitation, m_Precipitation);
            Apply(m_ClimateSystem.cloudiness, m_Cloudiness);
            Apply(m_ClimateSystem.aurora, m_Aurora);
            Apply(m_ClimateSystem.fog, m_Fog);

            m_PlanetarySystem.Update();

        }

        private static (bool active, float value) Snapshot(OverridableProperty<float> property) {
            return (property.overrideState, property.overrideValue);
        }

        private static void Apply(OverridableProperty<float> property, (bool active, float value) state) {
            property.overrideValue = state.value;
            property.overrideState = state.active;
        }

        private void SyncWeather() {
            Volume climate = m_ClimateRenderSystem?.climateControlVolume;
            if (climate == null || climate.sharedProfile == null) {
                m_Log.Debug("Climate volume unavailable; nothing to sync.");
                return;
            }

            if (!(m_PhotoModeRenderSystem.GetMemberValue(kVolumeField) is Volume photoMode)
                || photoMode.sharedProfile == null) {
                m_Log.Warn($"{kVolumeField} not found on {nameof(PhotoModeRenderSystem)}; weather will not carry into photo mode.");
                return;
            }


            foreach (Type type in kSyncedComponents) {
                if (!climate.sharedProfile.TryGet(type, out VolumeComponent source)
                    || !photoMode.sharedProfile.TryGet(type, out VolumeComponent destination)) {
                    continue;
                }

                CopyOverriddenParameters(source, destination);
            }
        }

        private int CopyOverriddenParameters(VolumeComponent source, VolumeComponent destination) {
            if (source.parameters.Count != destination.parameters.Count) {
                m_Log.Warn($"{source.GetType().Name} parameter counts differ between volumes; skipping.");
                return 0;
            }

            int copied = 0;

            for (int i = 0; i < source.parameters.Count; i++) {
                VolumeParameter from = source.parameters[i];
                VolumeParameter to   = destination.parameters[i];

                if (from == null || to == null || !from.overrideState || from.GetType() != to.GetType()) {
                    continue;
                }

                to.SetValue(from);
                copied++;
            }

            return copied;
        }
    }
}
