namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using Colossal.UI.Binding;

    using ExtendedPhotomode.Camera;

    using Game;
    using Game.Assets;
    using Game.CinematicCamera;
    using Game.UI.InGame;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Keeps each saved cinematic shot's orbit setup alongside it, so loading a shot restores not
    /// just its keyframes but the parameters that produced them.
    /// </summary>
    /// <remarks>
    /// This watches vanilla's state rather than patching it. <see cref="CinematicCameraUISystem"/>
    /// implements save and load <b>twice</b> — once inline in the <c>save</c>/<c>load</c> trigger
    /// bindings the UI actually calls, and again in private <c>Save</c>/<c>Load</c> methods that
    /// appear unused. A Harmony patch on the private pair would very likely patch dead code, so
    /// instead this polls two observable facts and infers the event:
    /// <list type="bullet">
    /// <item><description>the guid of <c>m_LastLoaded</c>, which both paths update; and</description></item>
    /// <item><description>the identity of <c>activeSequence</c>, which only a load replaces.</description></item>
    /// </list>
    /// New guid + new sequence object is a <b>load</b>; new guid + same object is a <b>save</b>.
    /// Overwriting in place changes neither, so the setup is also rewritten when the parameters drift.
    /// </remarks>
    public partial class EPM_OrbitBookmarkSystem : GameSystemBase {
        private const string kLastLoadedField = "m_LastLoaded";

        private CinematicCameraUISystem m_CinematicCameraUISystem;
        private EPM_ShotSubjectSystem      m_Subject;
        private OrbitSetupStore         m_Store;
        private PrefixedLogger          m_Log;

        private string                  m_LastGuid;
        private CinematicCameraSequence m_LastSequence;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                     = new PrefixedLogger(nameof(EPM_OrbitBookmarkSystem));
            m_Store                   = new OrbitSetupStore(m_Log);
            m_CinematicCameraUISystem = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_Subject                 = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
        }

        protected override void OnUpdate() {
            string                  guid     = GetLastLoadedGuid();
            CinematicCameraSequence sequence = m_CinematicCameraUISystem.activeSequence;

            if (string.IsNullOrEmpty(guid)) {
                m_LastGuid     = null;
                m_LastSequence = sequence;
                return;
            }

            if (guid != m_LastGuid) {
                bool isLoad = !ReferenceEquals(sequence, m_LastSequence);

                m_LastGuid     = guid;
                m_LastSequence = sequence;

                if (isLoad) {
                    RestoreSetup(guid);
                } else {
                    CaptureSetup(guid);
                }

                return;
            }

            m_LastSequence = sequence;
            CaptureIfChanged(guid);
        }

        private void RestoreSetup(string guid) {
            if (!m_Store.TryGet(guid, out OrbitSetup setup)) {
                m_Log.Debug($"No orbit setup stored for {guid}; leaving the controls as they are.");
                return;
            }

            setup.ApplyTo(Mod.Instance.Settings);
            Mod.Instance.Settings.ApplyAndSave();

            m_Subject.PinnedTarget     = setup.Target;
            m_Subject.PinnedStartAngle = setup.StartAngle;

            m_Log.Info($"Restored orbit setup for {guid}: r={setup.Radius}m h={setup.Height}m sweep={setup.Sweep}° at {setup.Target}");
        }

        private void CaptureSetup(string guid) {
            if (!m_Subject.TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                return;
            }

            m_Store.Put(guid, OrbitSetup.From(orbit));
            m_Log.Info($"Stored orbit setup with {guid}: {orbit}");
        }

        private void CaptureIfChanged(string guid) {
            if (!m_Subject.TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                return;
            }

            if (m_Store.TryGet(guid, out OrbitSetup stored)
                && stored.Matches(Mod.Instance.Settings, orbit.Target)) {
                return;
            }

            m_Store.Put(guid, OrbitSetup.From(orbit));
        }

        private string GetLastLoadedGuid() {
            if (!(m_CinematicCameraUISystem.GetMemberValue(kLastLoadedField) is ValueBinding<CinematicCameraAsset> binding)) {
                return null;
            }

            CinematicCameraAsset asset = binding.value;
            return (asset != null) ? asset.id.guid.ToString() : null;
        }
    }
}
