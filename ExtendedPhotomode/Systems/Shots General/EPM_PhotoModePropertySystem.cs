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

        private const float kMinNodeHeight = 0f;

        private const float kMaxNodeHeight = 2000f;

        private enum KeyframeSlot {
            None = 0,

            First = 1,
        }

        #region Property Ids

        public const string kGroupTitleId = "Orbit";
        public const string kSubjectTitleId = "Subject";

        public const string kRadiusGroupId = "Orbit.Radius";
        public const string kRadiusId = kRadiusGroupId + "/start";
        public const string kEndRadiusId = kRadiusGroupId + "/end";
        public const string kHeightId = "Orbit.Height";
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
        public const string kPathHeightId = "Path.PointHeight";
        public const string kPathLookId = "Path.LookMode";
        public const string kPathPointTitleId = "PathPoint";
        public const string kPathPointId = "Path.Point";
        public const string kPathPointHeightId = "Path.PointY";
        public const string kPathPointSharpId = "Path.PointSharp";
        public const string kPinCentreId = "Orbit.PinCentre";
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
        public const string kKeyframeIndexId = "Keyframe.Index";
        public const string kKeyframeEaseId = "Keyframe.Ease";

        #endregion

        private EPM_ShotSequenceSystem m_ShotSequenceSystem;
        private EPM_ShotSubjectSystem  m_Subject;
        private EPM_PathToolSystem  m_PathTool;
        private int                 m_SelectedNode;
        private int                 m_SelectedKeyframe;

        protected override string ModGroup => kModGroup;

        protected override string TabIcon => kTabIcon;

        protected override void OnCreate() {
            base.OnCreate();
            m_ShotSequenceSystem = World.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>();
            m_Subject            = World.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
            m_PathTool        = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
        }

        protected override void RegisterAll() {
            Section(null);
            RegisterShotSelector();

            Section(() => !UsesOrbitCentre());
            RegisterSubjectProperties();

            Section(() => Mod.Instance.Settings.Shot != ShotType.Orbit);
            RegisterOrbitProperties();

            Section(() => Mod.Instance.Settings.Shot != ShotType.Path);
            RegisterPathProperties();

            Section(() => Mod.Instance.Settings.Shot != ShotType.DollyZoom);
            RegisterDollyProperties();

            Section(null);
            RegisterTimingProperties();
            RegisterSequencerProperties();
            RegisterEnvironmentProperties();
        }

        private void RegisterShotSelector() {
            Add(PhotoModeUtils.GroupTitle(kModGroup, kShotSelectorTitleId));

            AddEnum<ShotType>(kShotTypeId, () => Mod.Instance.Settings.Shot,
                              v => Mod.Instance.Settings.Shot = v, Setting.kDefaultShot);
        }

        private void RegisterSubjectProperties() {
            Setting settings = Mod.Instance.Settings;

            Add(PhotoModeUtils.GroupTitle(kModGroup, kSubjectTitleId));

            AddInt(kRadiusId, () => settings.OrbitRadius, v => settings.OrbitRadius = v,
                   10, 1000, Setting.kDefaultOrbitRadius, kModGroup,
                   reset: () => SetAndSave(() => {
                       settings.OrbitRadius    = Setting.kDefaultOrbitRadius;
                       settings.OrbitEndRadius = Setting.kDefaultOrbitEndRadius;
                   }));

            AddInt(kEndRadiusId, () => settings.OrbitEndRadius, v => settings.OrbitEndRadius = v,
                   10, 1000, Setting.kDefaultOrbitEndRadius);
            AddInt(kHeightId, () => settings.OrbitHeight, v => settings.OrbitHeight = v,
                   -100, 500, Setting.kDefaultOrbitHeight);

            Add(new PhotoModeProperty {
                id              = kPinCentreId,
                group           = kModGroup,
                overrideControl = PhotoModeProperty.OverrideControl.Checkbox,
                getValue        = () => PhotoModeUtils.BooleanToFloat(m_Subject.PinnedTarget.HasValue),
                setValue        = v => SetPinned(PhotoModeUtils.FloatToBoolean(v)),
            });
        }

        private void RegisterOrbitProperties() {
            Setting settings = Mod.Instance.Settings;

            Add(PhotoModeUtils.GroupTitle(kModGroup, kGroupTitleId));

            AddInt(kSweepId, () => settings.OrbitSweep, v => settings.OrbitSweep = v,
                   -720, 720, Setting.kDefaultOrbitSweep);
            AddInt(kDurationId, () => settings.OrbitDuration, v => settings.OrbitDuration = v,
                   5, 300, Setting.kDefaultOrbitDuration);
            AddInt(kSpacingId, () => settings.OrbitDegreesPerKey, v => settings.OrbitDegreesPerKey = v,
                   5, 90, Setting.kDefaultOrbitDegreesPerKey);

            AddBool(kLookAtId, () => settings.OrbitLookAtTarget, v => settings.OrbitLookAtTarget = v,
                    Setting.kDefaultOrbitLookAtTarget);
            AddBool(kPreviewId, () => settings.ShowOrbitPreview, v => settings.ShowOrbitPreview = v,
                    Setting.kDefaultShowOrbitPreview);
        }

        private void SetPinned(bool pinned) {
            if (!pinned) {
                m_Subject.PinnedTarget     = null;
                m_Subject.PinnedStartAngle = null;
                return;
            }

            if (m_Subject.TryBuildOrbitFromSettings(out ExtendedPhotomode.Camera.OrbitShot orbit)) {
                m_Subject.PinnedTarget = orbit.Target;
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

            m_Log.Info($"{preset} on this map runs {start:0.##}h to {end:0.##}h.");
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

        private void RegisterPathProperties() {
            Setting settings = Mod.Instance.Settings;

            Add(PhotoModeUtils.GroupTitle(kModGroup, kPathTitleId));

            AddInt(kPathDurationId, () => settings.PathDuration, v => settings.PathDuration = v,
                   5, 300, Setting.kDefaultPathDuration, kModGroup);
            AddInt(kPathSpacingId, () => settings.PathMetresPerKey, v => settings.PathMetresPerKey = v,
                   5, 200, Setting.kDefaultPathMetresPerKey, kModGroup);
            AddInt(kPathPitchId, () => settings.PathPitch, v => settings.PathPitch = v,
                   -80, 80, Setting.kDefaultPathPitch, kModGroup);
            AddInt(kPathHeightId, () => settings.PathPointHeight, v => settings.PathPointHeight = v,
                   0, 500, Setting.kDefaultPathPointHeight, kModGroup);

            AddEnum<PathLookMode>(kPathLookId, () => settings.PathLook, v => settings.PathLook = v,
                                  Setting.kDefaultPathLook, kModGroup);

            RegisterSelectedNodeProperties();
        }

        private void RegisterDollyProperties() {
            Setting settings = Mod.Instance.Settings;

            Add(PhotoModeUtils.GroupTitle(kModGroup, kDollyTitleId));

            AddInt(kDollyStartId, () => settings.DollyStartDistance, v => settings.DollyStartDistance = v,
                   5, 1000, Setting.kDefaultDollyStartDistance, kModGroup,
                   reset: () => SetAndSave(() => {
                       settings.DollyStartDistance = Setting.kDefaultDollyStartDistance;
                       settings.DollyEndDistance   = Setting.kDefaultDollyEndDistance;
                   }));

            AddInt(kDollyEndId, () => settings.DollyEndDistance, v => settings.DollyEndDistance = v,
                   5, 1000, Setting.kDefaultDollyEndDistance, kModGroup);
            AddInt(kDollyDurationId, () => settings.DollyDuration, v => settings.DollyDuration = v,
                   1, 120, Setting.kDefaultDollyDuration, kModGroup);
            AddInt(kDollyKeysId, () => settings.DollyKeys, v => settings.DollyKeys = v,
                   2, 120, Setting.kDefaultDollyKeys, kModGroup);
        }

        private void RegisterKeyframeProperties() {
            Add(new PhotoModeProperty {
                id       = kKeyframeIndexId,
                group    = kModGroup,

                enumType = typeof(KeyframeSlot),
                getValue = () => ClampedKeyframe(m_SelectedKeyframe) + 1,
                setValue = v => m_SelectedKeyframe = ClampedKeyframe(Mathf.RoundToInt(v) - 1),
            });

            Add(new PhotoModeProperty {
                id       = kKeyframeEaseId,
                group    = kModGroup,
                enumType = typeof(KeyframeEase),
                getValue = () => Convert.ToInt32(
                    m_ShotSequenceSystem.GetKeyframeEase(ClampedKeyframe(m_SelectedKeyframe))),
                setValue = v => m_ShotSequenceSystem.SetKeyframeEase(
                    ClampedKeyframe(m_SelectedKeyframe), PhotoModeUtils.FindClosestEnumValue<KeyframeEase>(v)),
            });
        }

        private int ClampedKeyframe(int index) {
            return Mathf.Clamp(index, 0, Mathf.Max(0, m_ShotSequenceSystem.KeyframeCount - 1));
        }

        private int ClampedNode(int index) {
            return Mathf.Clamp(index, 0, Mathf.Max(0, m_PathTool.Path.Nodes.Count - 1));
        }

        private void RegisterSequencerProperties() {
            Setting settings = Mod.Instance.Settings;

            Add(PhotoModeUtils.GroupTitle(kModGroup, kSequenceTitleId));

            AddBool(kReplaceId2, () => !settings.OrbitReplacesSequence,
                    v => settings.OrbitReplacesSequence = !v,
                    !Setting.kDefaultOrbitReplacesSequence, kModGroup);

            AddInt(kTransitionId, () => settings.ShotTransition, v => settings.ShotTransition = v,
                   0, 30, Setting.kDefaultShotTransition, kModGroup);
        }

        private void RegisterTimingProperties() {
            Setting settings = Mod.Instance.Settings;

            Add(PhotoModeUtils.GroupTitle(kModGroup, kShotTitleId));

            Add(new PhotoModeProperty {
                id             = kShotDurationId,
                group          = kModGroup,
                fractionDigits = 1,
                getValue       = () => m_ShotSequenceSystem.SequenceDuration,
                setValue       = v => m_ShotSequenceSystem.RetimeSequence(v),
                min            = () => 1f,

                max            = null,
            });

            AddBool(kConstantSpeedId, () => settings.ConstantSpeed,
                    v => {
                        settings.ConstantSpeed = v;
                        m_ShotSequenceSystem.RetangentSequence();
                    },
                    Setting.kDefaultConstantSpeed, kModGroup);

            RegisterKeyframeProperties();
        }

        private void RegisterSelectedNodeProperties() {
            Add(PhotoModeUtils.GroupTitle(kModGroup, kPathPointTitleId));

            Add(new PhotoModeProperty {
                id             = kPathPointId,
                group          = kModGroup,
                fractionDigits = 0,
                getValue       = () => ClampedNode(m_SelectedNode) + 1,
                setValue       = v => m_SelectedNode = ClampedNode(Mathf.RoundToInt(v) - 1),
                min            = () => 1f,
                max            = () => Mathf.Max(1, m_PathTool.Path.Nodes.Count),
            });

            Add(new PhotoModeProperty {
                id             = kPathPointHeightId,
                group          = kModGroup,
                fractionDigits = 0,
                getValue       = () => SelectedNode()?.Position.y ?? 0f,
                setValue       = SetSelectedNodeHeight,
                min            = () => kMinNodeHeight,
                max            = () => kMaxNodeHeight,
            });

            Add(new PhotoModeProperty {
                id              = kPathPointSharpId,
                group           = kModGroup,
                overrideControl = PhotoModeProperty.OverrideControl.Checkbox,
                getValue        = () => PhotoModeUtils.BooleanToFloat(SelectedNode()?.Broken ?? false),
                setValue        = v => SetSelectedNodeBroken(PhotoModeUtils.FloatToBoolean(v)),
            });
        }

        private PathNode SelectedNode() {
            List<PathNode> nodes = m_PathTool.Path.Nodes;
            return m_SelectedNode < nodes.Count ? nodes[m_SelectedNode] : null;
        }

        private void SetSelectedNodeHeight(float height) {
            PathNode node = SelectedNode();

            if (node == null) {
                return;
            }

            Vector3 position = node.Position;
            position.y       = height;
            node.Position    = position;

            m_PathTool.Path.RefreshAutoTangents();
        }

        private void SetSelectedNodeBroken(bool broken) {
            PathNode node = SelectedNode();

            if (node == null) {
                return;
            }

            node.Broken = broken;

            if (!broken) {
                node.SetHandleOut(node.HandleOut);
            }
        }

        private static bool UsesOrbitCentre() {
            ShotType shot = Mod.Instance.Settings.Shot;

            return shot == ShotType.Orbit || shot == ShotType.DollyZoom;
        }

        protected override void OnTabsBuilt(object tabs) {
            if (!(tabs is List<PhotoModeUISystem.Tab> list)) {
                return;
            }

            EnumField field = null;

            foreach (PhotoModeUISystem.Tab tab in list) {
                if (tab.id != kModGroup) {
                    continue;
                }

                field = FindEnumField(tab.items, kKeyframeIndexId + "Dropdown");
                break;
            }

            if (field == null) {
                m_Log.Warn("Keyframe dropdown not found; it will list a single placeholder option.");
                return;
            }

            field.itemsVersion  = () => m_ShotSequenceSystem.KeyframeCount;
            field.itemsAccessor = new DelegateAccessor<EnumMember[]>(BuildKeyframeOptions);
        }

        private EnumMember[] BuildKeyframeOptions() {
            int count   = Mathf.Max(1, m_ShotSequenceSystem.KeyframeCount);
            var options = new EnumMember[count + 1];

            options[0] = new EnumMember(0UL, LocalizedString.Value(string.Empty));

            for (int i = 0; i < count; i++) {
                options[i + 1] = new EnumMember((ulong)(i + 1), LocalizedString.Value($"Keyframe #{i + 1}"));
            }

            return options;
        }

    }
}
