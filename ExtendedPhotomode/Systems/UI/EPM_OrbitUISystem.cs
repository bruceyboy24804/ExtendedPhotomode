namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;

    using Game.Rendering;

    using ModsCommon.Systems;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>Bridges the shot generators to the photo mode UI.</summary>
    /// <remarks>
    /// One Generate button and a Shot dropdown, rather than a button per generator: the photo mode
    /// button row overflows as soon as more than a couple are added, and each one added shrinks all of
    /// them. A shot.s shape comes from mod settings; the only thing pressing Generate contributes is
    /// where to put it, read from the live camera at that moment.
    /// </remarks>
    public partial class EPM_OrbitUISystem : CommonUISystemBase {
        public const string kCanGenerateBinding = "canGenerateOrbit";

        public const string kGenerateShotTrigger = "generateShot";

        public const string kShotTypeBinding = "shotType";

        public const string kOrbitSelectionTrigger = "orbitSelection";

        private readonly Dictionary<ShotType, GenerateShotBase> m_Generators =
            new Dictionary<ShotType, GenerateShotBase>();

        private EPM_ShotSequenceSystem m_ShotSequenceSystem;
        private EPM_ShotSubjectSystem  m_Subject;
        private CameraUpdateSystem  m_CameraUpdateSystem;

        protected override string ModId => Mod.Instance.Id;

        protected override void OnCreate() {
            base.OnCreate();
            m_ShotSequenceSystem = World.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>();
            m_Subject            = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();

            foreach (var pair in GenerateShotBase.Discover(World)) {
                m_Generators.Add(pair.Key, pair.Value);
            }

            CreateTrigger(kGenerateShotTrigger, GenerateShot);
            CreateTrigger(kOrbitSelectionTrigger, OrbitSelection);
            CreateBinding(kCanGenerateBinding, () => m_ShotSequenceSystem.ActiveSequence != null);
            CreateBinding(kShotTypeBinding, () => (int)Mod.Instance.Settings.Shot);
        }

        private void GenerateShot() {
            ShotType shot = Mod.Instance.Settings.Shot;

            if (!m_Generators.TryGetValue(shot, out GenerateShotBase generator)) {
                m_Log.Warn($"No generator registered for shot type {shot}.");
                return;
            }

            generator.TryGenerate();
        }

        private void OrbitSelection() { m_Subject.TryPinToSelection(); }
    }
}
