namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Linq;
    using System.Reflection;

    using Game;
    using Game.Assets;
    using Game.UI.InGame;

    using ModsCommon.Extensions;
    using ModsCommon.Systems;
    using ModsCommon.Utils;

    #endregion

    /// <summary>Sorts the saved cinematic shots the game lists, which vanilla leaves in database order.</summary>
    /// <remarks>
    /// <c>CinematicCameraUISystem</c> builds its list as
    /// <c>AssetDatabase.global.GetAssets(...).Where(...).ToArray()</c> and nothing else — no
    /// <c>OrderBy</c>, no <c>Sort</c>, on save or anywhere else. The order shown is therefore whatever
    /// the asset database happens to enumerate, which is neither alphabetical nor chronological and is
    /// not guaranteed stable between sessions.
    /// Rather than rebuild the list, this wraps the getter the game's own binding already uses, so the
    /// filter vanilla applies — it hides one hardcoded built-in shot — keeps working untouched and
    /// only the order changes.
    /// </remarks>
    public partial class EPM_ShotSortSystem : CommonUISystemBase {
        public const string kOrderBinding = "shotSortOrder";

        public const string kSetOrderTrigger = "setShotSortOrder";

        private const string kAssetsBindingField = "m_Assets";

        private const string kGetterField = "m_Getter";

        private CinematicCameraUISystem      m_CinematicCameraUISystem;
        private object                       m_Binding;
        private FieldInfo                    m_GetterField;
        private Func<CinematicCameraAsset[]> m_Original;
        private bool                         m_Wrapped;
        private ShotSortOrder                m_LastOrder;

        protected override string ModId => Mod.Instance.Id;

        protected override void OnCreate() {
            base.OnCreate();
            m_CinematicCameraUISystem = World.GetOrCreateSystemManaged<CinematicCameraUISystem>();
            m_LastOrder               = Mod.Instance.Settings.ShotSort;

            CreateBinding(kOrderBinding, () => (int)Mod.Instance.Settings.ShotSort);
            CreateTrigger<int>(kSetOrderTrigger, SetOrder);
        }

        private void SetOrder(int order) {
            Setting settings = Mod.Instance.Settings;

            if (!Enum.IsDefined(typeof(ShotSortOrder), order)) {
                m_Log.Warn($"Ignoring unknown sort order {order}.");
                return;
            }

            settings.ShotSort = (ShotSortOrder)order;
            settings.ApplyAndSave();
        }

        protected override void OnUpdate() {
            base.OnUpdate();

            if (!m_Wrapped) {
                TryWrap();
                return;
            }

            ShotSortOrder order = Mod.Instance.Settings.ShotSort;

            if (order != m_LastOrder) {
                m_LastOrder = order;
                m_Binding.TryInvokeMethod("Update", out _);
            }
        }

        protected override void OnDestroy() {
            Unwrap();
            base.OnDestroy();
        }

        private void Unwrap() {
            if (!m_Wrapped || m_GetterField == null || m_Original == null || m_Binding == null) {
                return;
            }

            m_GetterField.SetValue(m_Binding, m_Original);
            m_Binding.TryInvokeMethod("Update", out _);

            m_Wrapped  = false;
            m_Original = null;

            m_Log.Debug("Restored the game's own shot ordering.");
        }

        private void TryWrap() {
            m_Binding = m_CinematicCameraUISystem.GetMemberValue(kAssetsBindingField);

            if (m_Binding == null) {
                return;
            }

            FieldInfo getter = m_Binding.GetType().GetField(kGetterField,
                                                            BindingFlags.NonPublic | BindingFlags.Instance);

            if (!(getter?.GetValue(m_Binding) is Func<CinematicCameraAsset[]> original)) {
                m_Log.Warn($"Could not reach {kGetterField}; saved shots will stay in database order.");
                m_Wrapped = true;
                return;
            }

            m_GetterField = getter;
            m_Original    = original;

            getter.SetValue(m_Binding, new Func<CinematicCameraAsset[]>(() => Sort(original())));

            m_Wrapped = true;
            m_Binding.TryInvokeMethod("Update", out _);
            m_Log.Info("Saved cinematic shots will now be sorted.");
        }

        private CinematicCameraAsset[] Sort(CinematicCameraAsset[] assets) {
            if (assets == null || assets.Length < 2) {
                return assets;
            }

            Setting settings = Mod.Instance?.Settings;

            if (settings == null) {
                return assets;
            }

            switch (settings.ShotSort) {
                case ShotSortOrder.Newest:
                    return assets.OrderByDescending(WriteTime).ToArray();

                case ShotSortOrder.Oldest:
                    return assets.OrderBy(WriteTime).ToArray();

                case ShotSortOrder.NameAscending:
                    return assets.OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase).ToArray();

                case ShotSortOrder.NameDescending:
                    return assets.OrderByDescending(a => a.name, StringComparer.OrdinalIgnoreCase).ToArray();

                default:
                    return assets;
            }
        }

        private static DateTime WriteTime(CinematicCameraAsset asset) {
            try {
                return asset.GetMeta().lastWriteTime;
            } catch (Exception) {
                return DateTime.MinValue;
            }
        }
    }
}
