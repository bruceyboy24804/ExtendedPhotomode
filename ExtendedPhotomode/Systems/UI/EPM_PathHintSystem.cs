namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Tools;

    using Game.Input;
    using Game.Tools;
    using Game.UI.Tooltip;

    using Unity.Entities;

    #endregion

    /// <summary>Shows contextual action hints at the cursor while the camera path tool is open.</summary>
    /// <remarks>
    /// The path tool has eight distinct interactions and no way to discover any of them. Vanilla
    /// solves this with <see cref="DisplayNameOverride"/>: rather than inventing a panel, an action's
    /// existing hint is relabelled for the current context, so the hint appears where the game always
    /// puts hints and in the player's own keybindings. The pattern is taken from NetworkTools'
    /// action tooltip system.
    /// The label depends on what is under the cursor, matching the tool's own rule that a click means
    /// whatever the thing beneath it implies.
    /// </remarks>
    public partial class EPM_PathHintSystem : TooltipSystemBase {
        private const string kSource = "ExtendedPhotomode.PathTool";

        private ToolSystem         m_ToolSystem;
        private EPM_PathToolSystem m_PathTool;

        private readonly Dictionary<ProxyAction, DisplayNameOverride> m_Overrides =
            new Dictionary<ProxyAction, DisplayNameOverride>();

        protected override void OnCreate() {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PathTool   = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
        }

        protected override void OnDestroy() {
            foreach (KeyValuePair<ProxyAction, DisplayNameOverride> pair in m_Overrides) {
                pair.Value.Dispose();
            }

            m_Overrides.Clear();
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool != m_PathTool) {
                SetActive(Mod.PathApplyAction, null);
                SetActive(Mod.PathBreakTangentAction, null);
                return;
            }

            SetActive(Mod.PathApplyAction, m_PathTool.DescribeApply());
            SetActive(Mod.PathBreakTangentAction, m_PathTool.DescribeBreakTangent());
        }

        private void SetActive(ProxyAction action, string label) {
            if (action == null) {
                return;
            }

            if (!m_Overrides.TryGetValue(action, out DisplayNameOverride existing)) {
                if (label == null) {
                    return;
                }

                existing            = new DisplayNameOverride(kSource, action, label);
                m_Overrides[action] = existing;
            }

            existing.active      = label != null;
            existing.displayName = label ?? existing.displayName;
        }
    }
}
