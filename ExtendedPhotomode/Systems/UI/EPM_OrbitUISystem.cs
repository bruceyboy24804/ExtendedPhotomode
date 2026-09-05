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

        /// <remarks>
        /// The hotkey is what makes generating work in the map editor. Our Generate button is a React
        /// portal that finds the in-game photo mode panel's Take Photo button by its icon style and
        /// inserts a sibling; the editor's panel has no such button, so the portal never mounts there.
        /// A keybinding needs no DOM at all and works in both.
        /// </remarks>
        protected override void OnUpdate() {
            base.OnUpdate();

            var generate = Mod.ApplyOrbitAction;

            if (generate != null && generate.WasPressedThisFrame()) {
                GenerateShot();
            }
        }

        // Delegates to the sequence system's dispatch, which the sequencer's assembly pass also uses.
        // Two lookups meant two places for a new shot type to be missing from.
        /// <summary>Stages the current setup as a shot, rather than writing it to the timeline.</summary>
        /// <remarks>
        /// This used to call <c>Generate</c> and put the move straight onto the timeline. It does not
        /// any more: generating is exploratory, and a shot that appended itself to the sequence the
        /// moment it was made rewrote the timeline every time an idea was tried.
        /// <para>
        /// The shot lands in the generated list and waits to be dragged onto the track, which is what
        /// puts it in the cut and assembles it. Nothing is solved here — <c>Assemble</c> runs the
        /// ordinary generators when the shot enters the sequence, so a staged shot and a generated
        /// one produce identical curves.
        /// </para>
        /// </remarks>
        private void GenerateShot() {
            World.GetOrCreateSystemManaged<EPM_ShotListSystem>().AddShot(string.Empty);
        }

        private void OrbitSelection() { m_Subject.TryPinToSelection(); }
    }
}
