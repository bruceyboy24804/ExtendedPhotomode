namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Tools;

    using Game;
    using Game.Rendering;
    using Game.SceneFlow;
    using Game.Rendering.CinematicCamera;
    using Game.Reflection;
    using Game.UI.InGame;
    using Game.UI.Localization;
    using Game.UI.Menu;
    using Game.UI.Widgets;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>Registers this mod's extra <see cref="PhotoModeProperty"/> entries with vanilla photo mode.</summary>
    /// <remarks>
    /// <see cref="PhotoModeRenderSystem.AddProperty(PhotoModeProperty)"/> is public, and a property
    /// added there is used twice: <see cref="PhotoModeUISystem"/> renders it as a row, and
    /// <see cref="CinematicCameraUISystem"/> lets it be captured as a keyframable modifier on the
    /// cinematic timeline. Anything expressible as a float getter and setter becomes animatable with
    /// no timeline code.
    ///
    /// The catch is ordering: <see cref="PhotoModeUISystem"/> builds its tab list once in its own
    /// <c>OnCreate</c>. Registering later still works as a timeline modifier but never appears in the
    /// UI — hence <see cref="RebuildPhotoModeTabs"/>.
    /// </remarks>
    public partial class EPM_PhotoModePropertySystem : PhotoModePropertyBase {
        public const string kModGroup = "ExtendedPhotomode";

        private const string kTabIcon = "Media/Game/Icons/Orbit.svg";

        #region Property Ids

        public const string kGroupTitleId = "Orbit";
        public const string kSubjectTitleId = "Subject";

        public const string kRadiusGroupId = "Orbit.Radius";
        public const string kRadiusId = kRadiusGroupId + "/start";
        public const string kEndRadiusId = kRadiusGroupId + "/end";
        public const string kHeightGroupId = "Orbit.HeightSpan";
        public const string kHeightId = kHeightGroupId + "/start";
        public const string kEndHeightId = kHeightGroupId + "/end";
        public const string kSweepEaseId = "Orbit.SweepEase";
        public const string kSweepId = "Orbit.Sweep";
        public const string kDurationId = "Orbit.Duration";
        public const string kSpacingId = "Orbit.KeyframeSpacing";
        public const string kLookAtId = "Orbit.LookAtTarget";
        public const string kPreviewId = "Orbit.ShowPreview";
        public const string kEnvTitleId = "Environment";
        public const string kTimeGroupId = "Env.TimeOfDay";
        public const string kStartTimeId = kTimeGroupId + "/start";
        public const string kEndTimeId = kTimeGroupId + "/end";
        public const string kTimeKeysId = "Env.TimeOfDayPerKeyframe";
        public const string kTimeEaseId = "Env.TimeOfDayEase";
        public const string kTimeRangeId = "Env.TimeOfDayRange";
        public const string kShotSelectorTitleId = "ShotSelector";
        public const string kShotTypeId = "Shot.Type";
        public const string kPathTitleId = "PathShot";
        public const string kPathDurationId = "Path.Duration";
        public const string kPathSpacingId = "Path.KeyframeSpacing";
        public const string kPathPitchId = "Path.Pitch";
        public const string kPathLookAheadId = "Path.LookAhead";
        public const string kPathEaseId = "Path.Ease";
        public const string kPathTerrainId = "Path.Terrain";
        public const string kPathClearanceId = "Path.Clearance";
        public const string kPathObstaclesId = "Path.Obstacles";
        public const string kPathObstacleClearanceId = "Path.ObstacleClearance";
        public const string kPathLookId = "Path.LookMode";
        public const string kPinCentreId = "Orbit.PinCentre";
        public const string kFollowId = "Subject.Follow";
        public const string kFramingId = "Subject.Framing";
        public const string kFramingHoldId = "Subject.FramingHoldSize";
        public const string kFramingLensId = "Subject.FramingLens";
        public const string kFocusId = "Subject.Focus";
        public const string kFocusDepthId = "Subject.FocusDepth";
        public const string kFocusEaseId = "Subject.FocusEase";
        public const string kRigId = "Shot.Rig";
        public const string kRigStrengthId = "Shot.RigStrength";
        public const string kRigSeedId = "Shot.RigSeed";
        public const string kTransitionSecondsId = "Sequence.TransitionSeconds";
        public const string kTransitionEaseId = "Sequence.TransitionEase";
        public const string kShotTitleId = "Shot";
        public const string kShotDurationId = "Shot.Duration";
        public const string kDollyTitleId = "DollyZoom";
        public const string kDollyDistanceGroupId = "Dolly.Distance";
        public const string kDollyStartId = kDollyDistanceGroupId + "/start";
        public const string kDollyEndId = kDollyDistanceGroupId + "/end";
        public const string kDollyDurationId = "Dolly.Duration";
        public const string kDollyKeysId = "Dolly.Keys";
        public const string kSequenceTitleId = "Sequence";
        public const string kReplaceId2 = "Sequence.Chain";
        public const string kTransitionId = "Sequence.Transition";
        public const string kConstantSpeedId = "Sequence.ConstantSpeed";

        #endregion

        private EPM_ShotSequenceSystem m_ShotSequenceSystem;
        private EPM_ShotSubjectSystem  m_Subject;

        protected override string ModGroup => kModGroup;

        protected override string TabIcon => kTabIcon;

        protected override void OnCreate() {
            base.OnCreate();
            m_ShotSequenceSystem = World.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>();
            m_Subject            = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
        }

        /// <summary>
        /// What is left on the photo mode panel once the shot and the sequence have their own homes.
        /// </summary>
        /// <remarks>
        /// Everything that DEFINES a shot moved to the tool, which draws the handles that place it.
        /// Everything about how shots JOIN moved to the shot list, which is the sequence. What remains
        /// is the environment: the sun. That is not a property of a shot or of a sequence at all — it
        /// belongs to the world, it is keyframable in its own right, and photo mode is exactly where
        /// you look at it.
        /// </remarks>
        protected override void RegisterAll() {
            Section(null);
            RegisterEnvironmentProperties();
        }


        /// <summary>Only the pin. Everything else about the subject moved to the tool.</summary>
        /// <remarks>
        /// Framing, focus, the rig and follow all define the shot rather than the timeline, so they
        /// are authored where the shot is — in the tool, alongside the handles that place it. The pin
        /// stays because it is the one thing you may need without the tool running: a shot generated
        /// from this panel still has to know what it is pointed at.
        /// </remarks>


        private void SetPinned(bool pinned) {
            if (!pinned) {
                m_Subject.PinnedTarget     = null;
                m_Subject.PinnedStartAngle = null;
                m_Subject.PinnedEntity     = Entity.Null;
                return;
            }

            if (m_Subject.TryBuildOrbitFromSettings(out ExtendedPhotomode.Camera.OrbitShot orbit)) {
                m_Subject.PinnedTarget = orbit.Target;
                m_Subject.PinnedEntity = Entity.Null;
            }
        }

        private void RegisterEnvironmentProperties() {
            Setting settings = Mod.Instance.Settings;

            Add(PhotoModeUtils.GroupTitle(kModGroup, kEnvTitleId));


            AddEnum<TimeOfDayPreset>(kTimeRangeId, () => settings.TimeOfDayRange, ApplyTimeOfDayPreset,
                                     Setting.kDefaultTimeOfDayRange);

            AddDecimal(kStartTimeId, () => settings.StartTimeOfDay, v => SetTimeOfDayHour(true, v),
                       0f, 24f, Setting.kDefaultStartTimeOfDay,
                       reset: () => SetAndSave(() => {
                           settings.StartTimeOfDay = Setting.kDefaultStartTimeOfDay;
                           settings.EndTimeOfDay   = Setting.kDefaultEndTimeOfDay;
                       }),
                       isEnabled: () => settings.AnimateTimeOfDay,
                       setEnabled: v => SetAndSave(() => settings.AnimateTimeOfDay = v));

            AddDecimal(kEndTimeId, () => settings.EndTimeOfDay, v => SetTimeOfDayHour(false, v),
                       0f, 24f, Setting.kDefaultEndTimeOfDay,
                       isEnabled: () => settings.AnimateTimeOfDay,
                       setEnabled: v => SetAndSave(() => settings.AnimateTimeOfDay = v));

            AddBool(kTimeKeysId, () => settings.TimeOfDayPerKeyframe,
                    v => settings.TimeOfDayPerKeyframe = v, Setting.kDefaultTimeOfDayPerKeyframe);

            AddDecimal(kTimeEaseId, () => settings.TimeOfDayEase, v => settings.TimeOfDayEase = v,
                       0f, 1f, Setting.kDefaultTimeOfDayEase, digits: 2,
                       isEnabled: () => settings.TimeOfDayPerKeyframe);
        }

        private void ApplyTimeOfDayPreset(TimeOfDayPreset preset) {
            Setting settings = Mod.Instance.Settings;

            settings.TimeOfDayRange = preset;

            if (!TimeOfDayPresets.TryResolve(preset, m_ShotSequenceSystem.Sun, out float start, out float end)) {
                if (preset != TimeOfDayPreset.Custom && preset != TimeOfDayPreset.None) {
                    m_Log.Warn($"Could not resolve the {preset} hours; is a map loaded?");
                }

                return;
            }

            settings.StartTimeOfDay = start;
            settings.EndTimeOfDay   = end;

        }

        private void SetTimeOfDayHour(bool isStart, float value) {
            Setting settings = Mod.Instance.Settings;

            if (isStart) {
                settings.StartTimeOfDay = value;
            } else {
                settings.EndTimeOfDay = value;
            }

            settings.TimeOfDayRange = TimeOfDayPreset.Custom;
        }


        private static bool IsPathShot() { return Mod.Instance.Settings.Shot == ShotType.Path; }

        // The two shot types that circle a pinned centre, and so are the two the Subject group means
        // anything for.
        private static bool UsesOrbitCentre() {
            ShotType shot = Mod.Instance.Settings.Shot;

            return shot == ShotType.Orbit || shot == ShotType.DollyZoom;
        }





    }
}
