namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using Game;
    using Game.Rendering;
    using Game.Rendering.CinematicCamera;
    using Game.UI.InGame;
    using Game.UI.Widgets;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Base for a system that contributes <see cref="PhotoModeProperty"/> rows to vanilla photo mode.
    /// </summary>
    /// <remarks>
    /// Everything here is the plumbing that is the same whatever the rows are: registering a property,
    /// building the right widget for its type, forcing the tab list to be rebuilt afterwards, and
    /// hiding sections that do not apply. A subclass supplies <see cref="ModGroup"/>,
    /// <see cref="TabIcon"/> and <see cref="RegisterAll"/>, and otherwise only describes its rows.
    /// <para>
    /// The ordering trap this exists to absorb: <see cref="PhotoModeUISystem"/> builds its tab list
    /// exactly once, in its own <c>OnCreate</c>. A property registered after that still works as a
    /// timeline modifier but gets no slider — hence <see cref="RebuildPhotoModeTabs"/>, which reaches
    /// the private builder by reflection. Failure there is deliberately non-fatal: the properties stay
    /// keyframable, they just get no row.
    /// </para>
    /// </remarks>
    public abstract partial class PhotoModePropertyBase : GameSystemBase {
        private const string kBuildPropertiesMethod = "BuildProperties";

        private const string kTabsProperty = "tabs";

        private const string kTabNamesBindingField = "m_TabNamesBinding";

        protected PhotoModeRenderSystem m_PhotoModeRenderSystem;

        protected PhotoModeUISystem m_PhotoModeUISystem;

        protected PrefixedLogger m_Log;

        private bool m_Registered;

        private readonly List<PhotoModeProperty> m_OwnProperties = new List<PhotoModeProperty>();

        private readonly List<Func<bool>> m_SectionHidden = new List<Func<bool>>();
        private readonly HashSet<string>  m_SeenMultiGroups = new HashSet<string>();
        private Func<bool>                m_CurrentSection;

        public IReadOnlyList<PhotoModeProperty> OwnProperties => m_OwnProperties;

        protected abstract string ModGroup { get; }

        protected abstract string TabIcon { get; }

        protected abstract void RegisterAll();

        protected virtual void OnTabsBuilt(object tabs) { }

        protected override void OnCreate() {
            base.OnCreate();
            m_Log                   = new PrefixedLogger(GetType().Name);
            m_PhotoModeRenderSystem = World.GetOrCreateSystemManaged<PhotoModeRenderSystem>();
            m_PhotoModeUISystem     = World.GetOrCreateSystemManaged<PhotoModeUISystem>();
        }

        protected override void OnUpdate() {
            if (m_Registered) {
                Enabled = false;
                return;
            }

            int before = m_OwnProperties.Count;

            RegisterAll();

            m_Registered = true;
            Enabled      = false;

            if (m_OwnProperties.Count == before) {
                m_Log.Debug("No extra photo mode properties to register yet.");
                return;
            }

            m_Log.Info($"Registered {m_OwnProperties.Count} photo mode properties.");
            RebuildPhotoModeTabs();
        }

        protected void Section(Func<bool> hidden) { m_CurrentSection = hidden; }

        protected void Add(PhotoModeProperty property) {
            if (property == null) {
                return;
            }

            property.isEnabled ??= () => false;

            if (property.group == ModGroup) {
                int slash = property.id.IndexOf("/");

                if (slash < 0 || m_SeenMultiGroups.Add(property.id.Substring(0, slash))) {
                    m_SectionHidden.Add(m_CurrentSection);
                }
            }

            m_PhotoModeRenderSystem.AddProperty(property);
            m_OwnProperties.Add(property);
        }

        protected void AddInt(string id, Func<int> get, Action<int> set, int min, int max,
                              int defaultValue, string group = null, Action reset = null) {
            Add(new PhotoModeProperty {
                id             = id,
                group          = group ?? ModGroup,
                fractionDigits = 0,
                getValue       = () => get(),
                setValue       = v => SetAndSave(() => set(Mathf.RoundToInt(v))),
                min            = () => min,
                max            = () => max,
                reset          = reset ?? (() => SetAndSave(() => set(defaultValue))),
            });
        }

        protected void AddDecimal(string id, Func<float> get, Action<float> set, float min, float max,
                                  float defaultValue, int digits = 1, string group = null,
                                  Action reset = null, Func<bool> isEnabled = null,
                                  Action<bool> setEnabled = null) {
            Add(new PhotoModeProperty {
                id             = id,
                group          = group ?? ModGroup,
                fractionDigits = digits,
                getValue       = get,
                setValue       = v => SetAndSave(() => set(v)),
                min            = () => min,
                max            = () => max,
                reset          = reset ?? (() => SetAndSave(() => set(defaultValue))),
                isEnabled      = isEnabled,
                setEnabled     = setEnabled,
            });
        }

        protected void AddEnum<TEnum>(string id, Func<TEnum> get, Action<TEnum> set, TEnum defaultValue,
                                      string group = null) where TEnum : struct, Enum {
            Add(new PhotoModeProperty {
                id       = id,
                group    = group ?? ModGroup,
                enumType = typeof(TEnum),
                getValue = () => Convert.ToInt32(get()),
                setValue = v => SetAndSave(() => set(PhotoModeUtils.FindClosestEnumValue<TEnum>(v))),
                reset    = () => SetAndSave(() => set(defaultValue)),
            });
        }

        protected void AddBool(string id, Func<bool> get, Action<bool> set, bool defaultValue,
                               string group = null) {
            Add(new PhotoModeProperty {
                id              = id,
                group           = group ?? ModGroup,
                overrideControl = PhotoModeProperty.OverrideControl.Checkbox,
                getValue        = () => PhotoModeUtils.BooleanToFloat(get()),
                setValue        = v => SetAndSave(() => set(PhotoModeUtils.FloatToBoolean(v))),
                reset           = () => SetAndSave(() => set(defaultValue)),
            });
        }

        protected void SetAndSave(Action apply) {
            apply();
            Mod.Instance.Settings.ApplyAndSave();
        }

        protected static EnumField FindEnumField(IEnumerable<IWidget> widgets, string displayName) {
            if (widgets == null) {
                return null;
            }

            foreach (IWidget widget in widgets) {
                if (widget is EnumField field &&
                    (field.displayName.id == displayName || field.displayName.value == displayName)) {
                    return field;
                }

                if (widget is Group group) {
                    EnumField found = FindEnumField(group.children, displayName);

                    if (found != null) {
                        return found;
                    }
                }
            }

            return null;
        }

        private void RebuildPhotoModeTabs() {
            if (!m_PhotoModeUISystem.TryInvokeMethod(kBuildPropertiesMethod, out object tabs) || tabs == null) {
                m_Log.Warn($"{kBuildPropertiesMethod} unavailable; added properties will be keyframable but have no photo mode slider.");
                return;
            }

            RetargetTabIcon(tabs);
            ApplySectionVisibility(tabs);
            OnTabsBuilt(tabs);

            m_PhotoModeUISystem.SetMemberValue(kTabsProperty, tabs);

            object binding = m_PhotoModeUISystem.GetMemberValue(kTabNamesBindingField);
            binding?.TryInvokeMethod("Update", out _);
        }

        private void ApplySectionVisibility(object tabs) {
            if (!(tabs is List<PhotoModeUISystem.Tab> list)) {
                return;
            }

            foreach (PhotoModeUISystem.Tab tab in list) {
                if (tab.id != ModGroup) {
                    continue;
                }

                if (tab.items.Count != m_SectionHidden.Count) {
                    m_Log.Warn($"Expected {m_SectionHidden.Count} widgets in the {ModGroup} tab but " +
                               $"found {tab.items.Count}; leaving every section visible.");
                    return;
                }

                for (int i = 0; i < tab.items.Count; i++) {
                    if (m_SectionHidden[i] != null && tab.items[i] is Widget widget) {
                        widget.hidden = m_SectionHidden[i];
                    }
                }

                return;
            }
        }

        private void RetargetTabIcon(object tabs) {
            if (!(tabs is List<PhotoModeUISystem.Tab> list)) {
                m_Log.Warn("Tab list was not the expected type; the tab will use a missing icon.");
                return;
            }

            foreach (PhotoModeUISystem.Tab tab in list) {
                if (tab.id == ModGroup) {
                    tab.icon = TabIcon;
                    return;
                }
            }
        }
    }
}
