namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using ExtendedPhotomode.Camera;

    using Game.CinematicCamera;
    using Game.Rendering;
    using Game.UI.InGame;

    using ModsCommon.Extensions;
    using ModsCommon.Systems;
    using ModsCommon.Utils;

    using Unity.Entities;

    #endregion

    /// <summary>Undo and redo for the cinematic timeline.</summary>
    /// <remarks>
    /// Vanilla has none, and every edit the curve editor makes — dragging keys, deleting one, pulling
    /// a tangent, retiming, assembling a shot list over the top — is destructive. That is the single
    /// biggest thing stopping the panel from being used the way an editor is used: people do not
    /// experiment with a tool that cannot take a step back.
    ///
    /// It works by snapshot rather than by command. The edits arrive through several different
    /// routes — our panel calls vanilla's own triggers directly, vanilla's editor is still live
    /// alongside ours, and the shot list rewrites everything at once — so a command log would have to
    /// model each of them and would silently miss any route it did not know about. A snapshot taken
    /// immediately before the mutation is agnostic about what caused it.
    /// </remarks>
    public partial class EPM_TimelineHistorySystem : CommonUISystemBase {
        public const string kCanUndoBinding = "timelineCanUndo";

        public const string kCanRedoBinding = "timelineCanRedo";

        public const string kUndoTrigger = "timelineUndo";

        public const string kRedoTrigger = "timelineRedo";

        /// <summary>How many steps back the stack holds.</summary>
        /// <remarks>
        /// A sequence is a handful of curves with tens of keys each, so a step costs a few kilobytes
        /// and this is not a memory concern. The cap exists so a long session does not grow without
        /// bound, not because the snapshots are expensive.
        /// </remarks>
        private const int kDepth = 64;

        private static EPM_TimelineHistorySystem s_Instance;

        private readonly List<TimelineSnapshot> m_Undo = new List<TimelineSnapshot>();

        private readonly List<TimelineSnapshot> m_Redo = new List<TimelineSnapshot>();

        private CinematicCameraUISystem m_Cinematic;

        private PhotoModeRenderSystem m_PhotoMode;

        private CameraUpdateSystem m_Cameras;

        private PrefixedLogger m_Log;

        /// <summary>
        /// Set while this system is writing a snapshot back, so the Harmony hooks do not record the
        /// restore itself as another edit to undo.
        /// </summary>
        private bool m_Restoring;

        protected override string ModId => Mod.Instance.Id;

        public bool CanUndo => m_Undo.Count > 0;

        public bool CanRedo => m_Redo.Count > 0;

        protected override void OnCreate() {
            base.OnCreate();

            s_Instance = this;
            m_Log      = new PrefixedLogger(nameof(EPM_TimelineHistorySystem));

            m_Cinematic = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_PhotoMode = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_Cameras   = World.GetOrCreateSystemManaged<CameraUpdateSystem>();

            CreateBinding(kCanUndoBinding, () => CanUndo);
            CreateBinding(kCanRedoBinding, () => CanRedo);

            CreateTrigger(kUndoTrigger, Undo);
            CreateTrigger(kRedoTrigger, Redo);
        }

        protected override void OnDestroy() {
            s_Instance = null;

            base.OnDestroy();
        }

        /// <summary>Records the sequence as it stands, before something changes it.</summary>
        /// <remarks>
        /// Called from the Harmony hooks in <see cref="Patches.CinematicCameraSequencePatches"/>,
        /// which sit in front of every mutating method on the sequence.
        /// </remarks>
        public static void Record() {
            s_Instance?.Push();
        }

        private void Push() {
            if (m_Restoring) {
                return;
            }

            CinematicCameraSequence sequence = m_Cinematic?.activeSequence;

            if (sequence == null) {
                return;
            }

            TimelineSnapshot snapshot = TimelineSnapshot.Capture(sequence);

            // Vanilla calls MoveKeyframe on every mouse-up whether anything moved or not, so without
            // this the stack fills with steps that change nothing — and an undo that appears to do
            // nothing reads as a broken undo.
            if (m_Undo.Count > 0 && m_Undo[m_Undo.Count - 1].Matches(snapshot)) {
                return;
            }

            m_Undo.Add(snapshot);

            if (m_Undo.Count > kDepth) {
                m_Undo.RemoveAt(0);
            }

            // A new edit after undoing abandons the redo branch, as it does in every editor.
            m_Redo.Clear();
        }

        private void Undo() {
            Step(m_Undo, m_Redo);
        }

        private void Redo() {
            Step(m_Redo, m_Undo);
        }

        /// <summary>Moves one step from one stack to the other.</summary>
        /// <remarks>
        /// The current state goes onto the opposite stack before the step is applied, which is what
        /// makes undo and redo the same operation with its arguments swapped.
        /// </remarks>
        private void Step(List<TimelineSnapshot> from, List<TimelineSnapshot> to) {
            CinematicCameraSequence sequence = m_Cinematic?.activeSequence;

            if (sequence == null || from.Count == 0) {
                return;
            }

            TimelineSnapshot snapshot = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);

            to.Add(TimelineSnapshot.Capture(sequence));

            m_Restoring = true;

            try {
                snapshot.RestoreTo(sequence);
                Refresh(sequence);
            } catch (Exception error) {
                m_Log.Error($"Restoring the timeline failed: {error}");
            } finally {
                m_Restoring = false;
            }
        }

        /// <summary>Pushes the restored curves to the UI and puts the camera back on them.</summary>
        /// <remarks>
        /// The two curve bindings are plain <c>ValueBinding</c>s rather than getters, so they only
        /// reach the UI when something calls <c>Update</c> on them. Vanilla does that inside each of
        /// its own edit handlers; a restore goes around those, so without this the sequence would be
        /// correct while the panel carried on drawing the pre-undo curves.
        /// </remarks>
        /// <param name="sequence">The sequence to push. Nothing happens when it is null.</param>
        public void Refresh(CinematicCameraSequence sequence) {
            if (sequence == null) {
                return;
            }

            m_Cinematic.GetMemberValue("m_TransformAnimationCurveBinding")
                      ?.InvokeMethod("Update", new object[] { sequence.transforms });

            m_Cinematic.GetMemberValue("m_ModifierAnimationCurveBinding")
                      ?.InvokeMethod("Update", new object[] { sequence.modifiers.ToArray() });

            // Re-evaluating at the current time is what moves the live camera back onto the restored
            // curve. Without it the sequence is correct but the view stays where the undone edit left
            // it, which reads as the undo having half worked.
            //
            // `t` is private on the UI system, so it is read the same way. Falling back to zero would
            // jump the playhead, so a failed read simply skips the re-evaluation.
            object time = m_Cinematic.GetMemberValue("t");

            if (time is float at) {
                sequence.Refresh(at, m_PhotoMode.photoModeProperties, m_Cameras.activeCameraController);
            }
        }
    }
}
