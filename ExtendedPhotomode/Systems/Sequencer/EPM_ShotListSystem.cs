namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Components;
    using ExtendedPhotomode.Tools;

    using Colossal.UI.Binding;

    using Game.CinematicCamera;
    using Game.Rendering;

    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>One shot in the list, as the UI sees it.</summary>
    public struct ShotListEntry {
        public int id;

        public string name;

        /// <summary>The shot type's numeric value, so the panel can pick an icon for it.</summary>
        public int type;

        public float duration;

        /// <summary>How densely the shot is keyed, in whatever unit its type counts in.</summary>
        /// <remarks>
        /// Degrees per key for an orbit, metres per key for a path, and a KEY COUNT for a dolly —
        /// three different things behind one field, because the panel shows one row for all three
        /// and labels it per type. The alternative was three fields of which two are always noise.
        /// </remarks>
        public int spacing;

        /// <summary>Where this shot begins on the assembled timeline, or -1 when it is in the bin.</summary>
        public float start;

        public int points;

        /// <summary>Whether the shot is in the cut, or waiting in the bin to be dragged in.</summary>
        public bool inSequence;
    }

    /// <summary>A named, ordered list of shots, assembled onto the cinematic timeline in one press.</summary>
    /// <remarks>
    /// <para>
    /// The mod could already generate one move at a time and chain onto the end of the timeline, but
    /// nothing remembered what those moves WERE. Change your mind about the second of five and the
    /// only route back was regenerating all five by hand, in order, from memory. This is the missing
    /// document: each shot keeps its own settings and its own drawn curves, so it can be reordered,
    /// retimed, or rebuilt on its own.
    /// </para>
    /// <para>
    /// Assembly deliberately reuses the ordinary generators rather than reimplementing them. Every
    /// shot type's solver, easing, environment and framing behaviour is therefore identical whether it
    /// was generated on its own or as part of a sequence — which is the property that makes the
    /// sequencer trustworthy, and the one a parallel implementation would quietly lose.
    /// </para>
    /// <para>
    /// The cost of that reuse is that the generators read live state: mod settings, the pinned
    /// subject, and the drawn path. So assembly writes each shot's stored state into those, generates,
    /// and puts the caller's own state back afterwards. Leaving the settings on whatever the last shot
    /// happened to use would silently rewrite the panel out from under the user.
    /// </para>
    /// </remarks>
    public partial class EPM_ShotListSystem : CommonUISystemBase {
        public const string kShotsBinding = "shotList";

        public const string kPanelOpenBinding = "shotListOpen";

        /// <summary>Binding key reporting whether the curve timeline panel is showing.</summary>
        public const string kTimelineOpenBinding = "timelinePanelOpen";

        public const string kAddTrigger = "addShot";

        public const string kRemoveTrigger = "removeShot";

        public const string kRenameTrigger = "renameShot";

        public const string kMoveTrigger = "moveShot";

        /// <summary>Drop a shot at an explicit position in the order.</summary>
        public const string kReorderTrigger = "reorderShot";

        /// <summary>Drag a shot into the sequence, or back out to the bin.</summary>
        public const string kInSequenceTrigger = "setShotInSequence";

        /// <summary>A completed drag: land a shot at a position, in or out of the cut, in one go.</summary>
        public const string kDropTrigger = "dropShot";

        /// <summary>Whether the mod's panels and the world overlays are hidden.</summary>
        public const string kHiddenBinding = "uiHidden";

        /// <summary>Where each shot actually starts, by id, measured during the last assembly.</summary>
        /// <remarks>
        /// Runtime only, never serialised: it describes the timeline currently built, not the shot.
        /// Empty until something has been assembled, which is why the list falls back to adding up
        /// durations — a rough figure is better than every row claiming to start at zero.
        /// </remarks>
        private readonly Dictionary<int, float> m_Starts = new Dictionary<int, float>();

        public const string kAssembleTrigger = "assembleShots";

        public const string kLoadTrigger = "loadShot";

        public const string kEditTrigger = "editShot";

        public const string kResetTimelineTrigger = "resetTimeline";

        public const string kSetShotNumberTrigger = "setShotNumber";

        private EPM_ShotSequenceSystem   m_Shots;
        private EPM_ShotSubjectSystem    m_Subject;
        private EPM_PathToolSystem       m_PathTool;
        private EntityQuery              m_Query;
        private ModsCommon.Utils.PrefixedLogger m_Log;
        private GetterValueBinding<ShotListEntry[]> m_ShotsBinding;

        private GetterValueBinding<bool> m_HiddenBinding;

        /// <summary>Vanilla's own overlay switch — the one photo mode's eye button drives.</summary>
        private RenderingSystem m_Rendering;

        protected override string ModId => Mod.Instance.Id;

        /// <summary>Whether the shot list panel is showing.</summary>
        public bool PanelOpen { get; private set; }

        /// <summary>Whether the curve timeline panel is showing.</summary>
        /// <remarks>
        /// Lives here rather than in its own system because it is one boolean and this system already
        /// owns the sequence-facing UI. The panel itself needs no C# at all — it reads and writes
        /// vanilla's own cinematicCamera bindings.
        /// </remarks>
        public bool TimelineOpen { get; private set; }

        /// <summary>Whether the mod's panels and the world overlays are hidden.</summary>
        /// <remarks>
        /// The point of the mod is the picture, and the panels sit over the picture. This is the same
        /// move photo mode's eye button makes and, deliberately, the same one: it drives vanilla's
        /// <c>RenderingSystem.hideOverlay</c>, so the mod's own gizmos go with it — our overlays are
        /// drawn through the same system and consult the same flag.
        /// <para>
        /// It does NOT touch <c>ui.view.enabled</c>. That is what vanilla uses to hide the HTML for a
        /// screenshot, but only for the single frame of the capture, because with the view disabled
        /// nothing is listening: no panel, no key handling, no way back. Hiding our own panels through
        /// a binding leaves the rest of the UI alive to hear the key that unhides them.
        /// </para>
        /// </remarks>
        public bool UIHidden { get; private set; }

        protected override void OnCreate() {
            base.OnCreate();
            m_Log      = new ModsCommon.Utils.PrefixedLogger(nameof(EPM_ShotListSystem));
            m_Shots    = World.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>();
            m_Subject  = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
            m_PathTool = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
            m_Rendering = World.GetOrCreateSystemManaged<RenderingSystem>();
            m_Query    = GetEntityQuery(ComponentType.ReadOnly<EPM_Shot>());

            m_ShotsBinding = CreateBinding(kShotsBinding, BuildList, false);

            CreateBinding(kPanelOpenBinding, () => PanelOpen);
            CreateTrigger<bool>(kPanelOpenBinding, open => PanelOpen = open);
            CreateBinding(kTimelineOpenBinding, () => TimelineOpen);
            m_HiddenBinding = CreateBinding(kHiddenBinding, () => UIHidden);
            CreateTrigger<bool>(kTimelineOpenBinding, open => TimelineOpen = open);

            CreateTrigger<string>(kAddTrigger, AddShot);
            CreateTrigger<int>(kRemoveTrigger, RemoveShot);
            CreateTrigger<int, string>(kRenameTrigger, RenameShot);
            CreateTrigger<int, int>(kMoveTrigger, MoveShot);
            CreateTrigger<int, int>(kReorderTrigger, ReorderShot);
            CreateTrigger<int, bool>(kInSequenceTrigger, SetInSequence);
            CreateTrigger<int, int, bool>(kDropTrigger, DropShot);
            CreateTrigger<int>(kLoadTrigger, LoadShot);
            CreateTrigger<int>(kEditTrigger, EditShot);
            CreateTrigger(kResetTimelineTrigger, ResetTimeline);
            CreateTrigger<int, string, float>(kSetShotNumberTrigger, SetShotNumber);
            CreateTrigger(kAssembleTrigger, () => Assemble());

            CreateBinding(kTimelineBinding, BuildTimeline);
            CreateTrigger<string, float>(kSetTimelineTrigger, SetTimeline);
        }

        protected override void OnUpdate() {
            base.OnUpdate();

            // The shot list has no key of its own — the Shots button in the timeline header opens it.
            if (Mod.TimelineAction != null && Mod.TimelineAction.WasPressedThisFrame()) {
                TimelineOpen = !TimelineOpen;
            }

            if (Mod.HideUIAction != null && Mod.HideUIAction.WasPressedThisFrame()) {
                SetUIHidden(!UIHidden);
            }
        }

        /// <summary>Hides or restores the panels and the world overlays together.</summary>
        /// <remarks>
        /// Both halves move as one because half a hidden UI is not what anyone wants: leaving the
        /// path gizmos drawn over a clean frame is as much of an obstruction as leaving the panel up.
        /// </remarks>
        private void SetUIHidden(bool hidden) {
            UIHidden = hidden;

            if (m_Rendering != null) {
                m_Rendering.hideOverlay = hidden;
            }

            m_HiddenBinding?.Update();
        }

        /// <summary>Captures the current settings and drawn curves as a new shot at the end.</summary>
        /// <summary>Captures the current settings and drawn curves as a new staged shot.</summary>
        /// <remarks>
        /// Public because Generate calls it too. Generating and adding are the same act now — both
        /// mean "keep what I have set up" — and the only difference used to be that one wrote
        /// straight to the timeline and the other did not.
        /// </remarks>
        public void AddShot(string name) {
            Setting settings = Mod.Instance.Settings;
            ShotType type    = settings.Shot;

            Entity entity = EntityManager.CreateEntity();

            var shot = new EPM_Shot {
                m_Id    = NextId(),
                m_Order = NextOrder(),
                m_Name  = new FixedString128Bytes(Trim(name, type)),
                m_Type  = (int)type,

                m_Target    = m_Subject.PinnedTarget ?? default,
                m_HasTarget = m_Subject.PinnedTarget.HasValue,
                m_Closed    = settings.PathClosed,

                m_OrbitRadius        = settings.OrbitRadius,
                m_OrbitEndRadius     = settings.OrbitEndRadius,
                m_OrbitHeight        = settings.OrbitHeight,
                m_OrbitEndHeight     = settings.OrbitEndHeight,
                m_OrbitSweep         = settings.OrbitSweep,
                m_OrbitSweepEase     = settings.OrbitSweepEase,
                m_OrbitDegreesPerKey = settings.OrbitDegreesPerKey,
                m_OrbitLookAtTarget  = settings.OrbitLookAtTarget,

                m_DollyStart = settings.DollyStartDistance,
                m_DollyEnd   = settings.DollyEndDistance,
                m_DollyKeys  = settings.DollyKeys,

                m_PathMetresPerKey = settings.PathMetresPerKey,
                m_PathPitch        = settings.PathPitch,
                m_PathLookAhead    = settings.PathLookAhead,
                m_PathEase         = settings.PathEase,
                m_PathLook         = (int)settings.PathLook,
                m_PathTerrain      = (int)settings.PathTerrain,
                m_PathClearance    = settings.PathClearance,

                m_Duration     = DurationFor(type, settings),
                m_TransitionIn = settings.TransitionSeconds,

                // Generated, NOT in the cut. A new shot lands in the generated list and waits to be
                // dragged onto the timeline.
                //
                // This is the whole point of having two places: generating is cheap and exploratory,
                // and a shot that appended itself to the sequence the moment it was made would
                // rewrite the timeline every time you tried an idea. Staging keeps generating free.
                m_InSequence = false,
            };

            EntityManager.AddComponentData(entity, shot);

            // Only a path shot needs curves, but the buffers are added either way so a shot can be
            // retyped later without the entity having to change shape.
            PathBuffers.Store(EntityManager, entity, m_PathTool.TravelPath, m_PathTool.RailPath);

            // No Assemble here: the new shot is not in the cut, so the sequence has not changed.
            // Rebuilding anyway would throw away any hand-edited tangents for nothing.
            m_ShotsBinding.Update();
        }

        /// <summary>Puts a shot's settings and curves back on the panel, for editing it again.</summary>
        private void LoadShot(int id) {
            Entity entity = FindById(id);

            if (entity == Entity.Null) {
                m_Log.Warn($"No shot with id {id}.");
                return;
            }

            Restore(EntityManager.GetComponentData<EPM_Shot>(entity), entity);
            Mod.Instance.Settings.ApplyAndSave();

        }

        /// <summary>Empties the timeline when the last shot leaves the cut.</summary>
        /// <remarks>
        /// Assemble writes the cut over the timeline, so an empty cut has to mean an empty timeline
        /// — otherwise dragging out the last shot leaves the previous assembly's curves behind, and
        /// the panel shows a sequence with nothing in the list that accounts for it.
        /// <para>
        /// Undoable rather than destructive: <c>Reset</c> is one of the mutating methods
        /// <see cref="Patches.CinematicCameraSequencePatches"/> hooks, so this records a snapshot on
        /// the way through and Ctrl+Z brings the whole cut back.
        /// </para>
        /// <para>
        /// Guarded on the sequence having length, because Assemble runs on every drop. Without it,
        /// dragging shots around an already-empty timeline would call Reset on each one.
        /// </para>
        /// </remarks>
        private void ClearSequence() {
            CinematicCameraSequence sequence = m_Shots.ActiveSequence;

            if (sequence == null || sequence.timelineLength <= 0f) {
                return;
            }

            sequence.Reset();

            // The curve bindings are plain ValueBindings, so the panel keeps drawing the old curves
            // until something pushes the new ones. See the history system for the full note.
            World.GetOrCreateSystemManaged<EPM_TimelineHistorySystem>().Refresh(sequence);
        }

        /// <summary>Loads a shot, opens the shot panel and starts editing it in the tool.</summary>
        /// <param name="id">The shot to edit.</param>
        /// <remarks>
        /// Load already puts every setting back, but it leaves the player looking at a list with no
        /// sign of where those settings went. Edit is the whole gesture: restore the settings, show
        /// the panel, and go live in the tool so the shot can be dragged around straight away.
        /// <para>
        /// The tool is per-type — <c>OrbitShotEditor</c>, <c>DollyShotEditor</c> and the path — and
        /// <c>Restore</c> has already set <c>Settings.Shot</c> from the saved shot by the time the
        /// tool starts, so it comes up on the right editor without being told which.
        /// </para>
        /// <para>
        /// Ctrl+P deliberately opens the panel WITHOUT starting the tool, and that is not a
        /// contradiction: a keypress landing you in a live tool is undiscoverable, whereas pressing
        /// edit on a specific shot is an unambiguous request to edit that shot.
        /// </para>
        /// <para>
        /// Photo mode blocks the tool barrier and forces the default tool, so the tool cannot take
        /// clicks while photo mode is open. Nothing here can change that; the panel and the settings
        /// still arrive, and the tool starts as soon as photo mode closes.
        /// </para>
        /// </remarks>
        private void EditShot(int id) {
            if (FindById(id) == Entity.Null) {
                m_Log.Warn($"No shot with id {id} to edit.");
                return;
            }

            LoadShot(id);

            // SetToolActive(true) opens the panel itself, so this is one call rather than two.
            World.GetOrCreateSystemManaged<EPM_PathLibrarySystem>().SetToolActive(true);
        }

        /// <summary>Writes every shot onto the timeline, in order.</summary>
        /// <returns>How many shots were written.</returns>
        public int Assemble() {
            List<Entity> ordered = InSequence();

            if (ordered.Count == 0) {
                // Not a warning any more. With shots draggable in and out of the sequence, an empty
                // cut is an ordinary state on the way to a full one, not a mistake.
                m_Log.Debug("No shots are in the sequence; nothing to assemble.");

                ClearSequence();
                m_Starts.Clear();
                m_ShotsBinding.Update();

                return 0;
            }

            // The caller's own state, put back at the end. The generators read live settings, so
            // assembling without this leaves the panel showing the last shot's numbers.
            Setting  settings = Mod.Instance.Settings;
            ShotType shot     = settings.Shot;
            bool     chain    = settings.OrbitReplacesSequence;
            Vector3? pinned   = m_Subject.PinnedTarget;

            int written = 0;

            // Where each shot actually landed, measured off the sequence rather than predicted.
            //
            // BuildList used to guess this by adding up durations and transitions, and the guess
            // drifts: the chaining gap, easing and the bridge all move the real start. Once those
            // numbers positioned blocks on the track rather than just labelling rows, the drift
            // became visible — a shot whose predicted start ran past the end of the view had its
            // block culled and simply did not appear.
            m_Starts.Clear();

            try {
                for (int i = 0; i < ordered.Count; i++) {
                    EPM_Shot data = EntityManager.GetComponentData<EPM_Shot>(ordered[i]);

                    // Read before generating: the sequence's current end IS this shot's start.
                    m_Starts[data.m_Id] = (i == 0) ? 0f : (m_Shots.ActiveSequence?.timelineLength ?? 0f);

                    Restore(data, ordered[i]);

                    // The first shot replaces whatever is on the timeline; the rest chain onto it.
                    // Doing it the other way round appends a whole new sequence to the last one.
                    settings.OrbitReplacesSequence = i == 0;

                    // Bridged before the shot is generated, so the shot lands after the bridge rather
                    // than the bridge being drawn back over the top of it. The first shot has nothing
                    // to come from, so it never gets one however it is configured.
                    if (i > 0 && data.m_TransitionIn > 0.01f) {
                        BridgeInto(data);
                    }

                    if (m_Shots.Generate((ShotType)data.m_Type)) {
                        written++;
                    } else {
                        m_Log.Warn($"Shot \"{data.m_Name}\" produced nothing and was skipped.");
                    }
                }
            } finally {
                settings.Shot                  = shot;
                settings.OrbitReplacesSequence = chain;
                m_Subject.PinnedTarget         = pinned;

                settings.ApplyAndSave();
            }


            // The starts only became known during assembly, so the list has to be pushed again with
            // them. Cheap, and it is what keeps the track's blocks sitting under their own curves.
            m_ShotsBinding.Update();

            return written;
        }

        /// <summary>Writes the move from where the timeline currently ends into where a shot begins.</summary>
        /// <remarks>
        /// The destination pose has to be known before the shot exists, so the shot is solved once here
        /// purely to read its first keyframe, and then discarded. Solving twice is the price of not
        /// having the generators return their samples — cheap next to the alternative, which is every
        /// generator growing a second entry point that produces a shot without writing it.
        /// </remarks>
        private void BridgeInto(EPM_Shot data) {
            if (!m_Shots.TryGetEndPose(out Vector3 fromPosition, out Vector3 fromRotation)) {
                return;
            }

            if (!TryGetFirstPose(data, out Vector3 toPosition, out Vector3 toRotation)) {
                return;
            }

            m_Shots.ApplyTransition(fromPosition, fromRotation, toPosition, toRotation,
                                    m_Shots.NextStartTime(false), data.m_TransitionIn,
                                    Mod.Instance.Settings.TransitionEase);
        }

        /// <summary>Solves a shot far enough to learn the pose it opens on.</summary>
        private bool TryGetFirstPose(EPM_Shot data, out Vector3 position, out Vector3 rotation) {
            position = default;
            rotation = default;

            var type = (ShotType)data.m_Type;

            if (type == ShotType.Path) {
                List<CameraSample> samples = m_PathTool.TravelPath.Solve();

                if (samples.Count == 0) {
                    return false;
                }

                position = samples[0].Position;
                rotation = samples[0].Rotation;
                return true;
            }

            if (!m_Subject.TryBuildOrbitFromSettings(out OrbitShot orbit)) {
                return false;
            }

            List<CameraSample> solved = orbit.Solve();

            if (solved.Count == 0) {
                return false;
            }

            position = solved[0].Position;
            rotation = solved[0].Rotation;
            return true;
        }

        /// <summary>Applies a stored shot to the live settings, subject and drawn curves.</summary>
        private void Restore(EPM_Shot data, Entity entity) {
            Setting settings = Mod.Instance.Settings;

            settings.Shot       = (ShotType)data.m_Type;
            settings.PathClosed = data.m_Closed;

            settings.OrbitRadius        = data.m_OrbitRadius;
            settings.OrbitEndRadius     = data.m_OrbitEndRadius;
            settings.OrbitHeight        = data.m_OrbitHeight;
            settings.OrbitEndHeight     = data.m_OrbitEndHeight;
            settings.OrbitSweep         = data.m_OrbitSweep;
            settings.OrbitSweepEase     = data.m_OrbitSweepEase;
            settings.OrbitDegreesPerKey = data.m_OrbitDegreesPerKey;
            settings.OrbitLookAtTarget  = data.m_OrbitLookAtTarget;

            settings.DollyStartDistance = data.m_DollyStart;
            settings.DollyEndDistance   = data.m_DollyEnd;
            settings.DollyKeys          = data.m_DollyKeys;

            settings.PathMetresPerKey = data.m_PathMetresPerKey;
            settings.PathPitch        = data.m_PathPitch;
            settings.PathLookAhead    = data.m_PathLookAhead;
            settings.PathEase         = data.m_PathEase;
            settings.PathLook         = (PathLookMode)data.m_PathLook;
            settings.PathTerrain      = (PathTerrainMode)data.m_PathTerrain;
            settings.PathClearance    = data.m_PathClearance;

            SetDuration((ShotType)data.m_Type, settings, data.m_Duration);

            m_Subject.PinnedTarget = data.m_HasTarget ? (Vector3)data.m_Target : (Vector3?)null;

            PathBuffers.Load(EntityManager, entity, m_PathTool.TravelPath, m_PathTool.RailPath);

            // A restored shot is a different path, so undoing across the boundary would splice one
            // shot's nodes into another's identity.
            m_PathTool.History.Clear();
            m_PathTool.ClearSelection();
        }

        private static float DurationFor(ShotType type, Setting settings) {
            switch (type) {
                case ShotType.Orbit:     return settings.OrbitDuration;
                case ShotType.DollyZoom: return settings.DollyDuration;
                default:                 return settings.PathDuration;
            }
        }

        private static void SetDuration(ShotType type, Setting settings, float duration) {
            int seconds = Mathf.Clamp(Mathf.RoundToInt(duration), 1, 600);

            switch (type) {
                case ShotType.Orbit:     settings.OrbitDuration = seconds; break;
                case ShotType.DollyZoom: settings.DollyDuration = seconds; break;
                default:                 settings.PathDuration  = seconds; break;
            }
        }

        private static string Trim(string name, ShotType type) {
            string trimmed = (name ?? string.Empty).Trim();

            return (trimmed.Length > 0) ? trimmed : type.ToString();
        }
    }
}
