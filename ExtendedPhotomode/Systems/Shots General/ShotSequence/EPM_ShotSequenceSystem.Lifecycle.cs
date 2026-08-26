namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using Game.Rendering;
    using Game.Simulation;
    using Game.UI.InGame;

    using ModsCommon.Utils;

    using Unity.Entities;

    #endregion

    /// <summary>System creation and per-frame hooks.</summary>
    public partial class EPM_ShotSequenceSystem {
        protected override void OnUpdate() { }

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                     = new PrefixedLogger(nameof(EPM_ShotSequenceSystem));
            m_CinematicCameraUISystem = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_PhotoModeRenderSystem   = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_PlanetarySystem         = World.GetOrCreateSystemManaged<PlanetarySystem>();
        }

    }
}
