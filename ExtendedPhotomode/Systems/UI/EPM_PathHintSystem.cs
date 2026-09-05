namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using ExtendedPhotomode.Tools;

    using Game.Input;
    using Game.Tools;
    using Game.UI.Tooltip;
    using Game.UI.Widgets;

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

        // Above kToolTipPriority (0), so our contextual label wins over the action's own name.
        private const int kPriority = 1;

        private ToolSystem         m_ToolSystem;
        private EPM_PathToolSystem m_PathTool;

        // Vanilla's own tool actions, which the path tool uses for deleting and backing out. Looked
        // up the way NodeController and NetworkTools do, rather than declaring bindings of our own.
        private ProxyAction m_Secondary;
        private ProxyAction m_Cancel;

        private readonly Dictionary<ProxyAction, DisplayNameOverride> m_Overrides =
            new Dictionary<ProxyAction, DisplayNameOverride>();

        // Cached rather than rebuilt each frame, following NodeController. InputHintTooltip.Refresh
        // is written to no-op unless the name or device changed — handing it a new instance every
        // update defeats that and rebuilds the hint items on every frame the tool is running.
        private readonly Dictionary<ProxyAction, InputHintTooltip> m_Hints =
            new Dictionary<ProxyAction, InputHintTooltip>();

        protected override void OnCreate() {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PathTool   = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();

            InputManager input = InputManager.instance;

            if (input != null) {
                m_Secondary = input.FindAction(InputManager.kToolMap, "Secondary Apply");
                m_Cancel    = input.FindAction(InputManager.kToolMap, "Cancel");
            }
        }

        protected override void OnDestroy() {
            foreach (KeyValuePair<ProxyAction, DisplayNameOverride> pair in m_Overrides) {
                pair.Value.Dispose();
            }

            m_Overrides.Clear();
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool != m_PathTool ||
                InputManager.instance?.activeControlScheme != InputManager.ControlScheme.KeyboardAndMouse) {
                Deactivate();
                return;
            }

            Show(Mod.PathApplyAction, m_PathTool.DescribeApply());
            Show(Mod.PathBreakTangentAction, m_PathTool.DescribeBreakTangent());

            // Vanilla already draws a row for its own tool actions, so these are relabelled without
            // adding one — see the ownRow parameter.
            Show(m_Secondary, m_PathTool.DescribeDelete(), false);
            Show(m_Cancel, m_PathTool.DescribeCancel(), false);
            Show(Mod.PathReverseAction, m_PathTool.Path.Nodes.Count > 1 ? PathHints.Reverse : null);
            Show(Mod.PathRaiseAction, m_PathTool.SelectedPoint >= 0 ? PathHints.Raise : null);
            Show(Mod.PathLowerAction, m_PathTool.SelectedPoint >= 0 ? PathHints.Lower : null);
        }

        /// <remarks>
        /// Two halves, and only together do they read as a tooltip. The override relabels the action
        /// so its hint says what a click means <em>here</em> rather than "Apply"; the
        /// <see cref="InputHintTooltip"/> is what actually draws it at the cursor, with the glyph for
        /// whatever the player has the action bound to. Without the second call the label is correct
        /// and invisible, which is what this system did before.
        /// </remarks>
        /// <param name="ownRow">
        /// False for vanilla's own tool actions — Apply, Secondary Apply, Cancel. The game already
        /// emits a hint row for those, and <c>InputHintTooltip.Refresh</c> derives its path from
        /// <c>action.title + device</c>, so adding ours produces an identical path and
        /// <c>AddMouseTooltip</c> rejects it with "duplicate path 'Tool/CancelKeyboard, Mouse'".
        /// The override still applies, so vanilla's own row carries our label.
        /// </param>
        private void Show(ProxyAction action, string label, bool ownRow = true) {
            // isSet is false for an action the player has cleared the binding on. Without this it
            // still gets a row, showing a label with no key beside it.
            if (action == null || !action.isSet) {
                return;
            }

            if (!m_Overrides.TryGetValue(action, out DisplayNameOverride existing)) {
                if (label == null) {
                    return;
                }

                // The priority argument is not optional in practice. It defaults to
                // kDisabledPriority (-1), and DisplayNameOverride.active returns false whenever the
                // priority is -1 — so the three-argument constructor builds an override that can
                // never activate, and the hint falls back to the raw Common.ACTION[...] locale key.
                existing            = new DisplayNameOverride(kSource, action, label, kPriority);
                m_Overrides[action] = existing;
            }

            existing.active      = label != null;
            existing.displayName = label ?? existing.displayName;

            if (label == null || !ownRow) {
                return;
            }

            if (!m_Hints.TryGetValue(action, out InputHintTooltip hint)) {
                hint            = new InputHintTooltip(action);
                m_Hints[action] = hint;
            }

            // DeviceType is a flags enum, and Refresh only collects hint items for the devices asked
            // for. Passing Mouse alone leaves every keyboard-bound action — reverse, raise, lower,
            // break tangent — with a label and no glyph. Gamepad is left out because OnUpdate has
            // already established the player is on keyboard and mouse.
            hint.Refresh(InputManager.DeviceType.Keyboard | InputManager.DeviceType.Mouse);

            // An empty path means nothing was collected for this device, so the row would render as
            // a label with no key.
            if (hint.path != PathSegment.Empty) {
                AddMouseTooltip(hint);
            }
        }

        private void Deactivate() {
            foreach (KeyValuePair<ProxyAction, DisplayNameOverride> pair in m_Overrides) {
                if (pair.Value.active) {
                    pair.Value.active = false;
                }
            }
        }
    }
}
