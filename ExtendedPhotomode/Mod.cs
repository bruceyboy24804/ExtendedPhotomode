namespace ExtendedPhotomode {
    #region Using Statements

    using Colossal;

    using ExtendedPhotomode.L10n;
    using ExtendedPhotomode.Systems;
    using ExtendedPhotomode.Tools;

    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.Serialization;
    using Game.Settings;

    using ModsCommon.Mod;

    #endregion

    /// <summary>
    /// Entry point for ExtendedPhotomode.
    /// </summary>
    /// <remarks>
    /// See the note at the top of <see cref="ModsCommonBase{TSelf}"/> for why the base class does not
    /// implement <see cref="IMod"/> itself — the concrete mod has to declare it.
    /// </remarks>
    public class Mod : ModsCommonBase<Mod>, IMod {
        /// <summary>Name of the input action that generates an orbit shot.</summary>
        public const string kApplyOrbitActionName = "ApplyOrbit";

        /// <summary>Name of the input action that opens the camera path tool.</summary>
        public const string kPathToolActionName = "PathTool";

        /// <summary>Name of the input action that writes a drawn path to the timeline.</summary>
        public const string kGeneratePathActionName = "GeneratePath";

        /// <summary>Name of the click action that places a path point.</summary>
        public const string kPathApplyActionName = "PathApply";

        /// <summary>Name of the action that raises a path point.</summary>
        public const string kPathRaiseActionName = "PathRaise";

        /// <summary>Name of the action that lowers a path point.</summary>
        public const string kPathLowerActionName = "PathLower";

        /// <summary>Name of the action that reverses a drawn path.</summary>
        public const string kPathReverseActionName = "PathReverse";

        /// <summary>Name of the action that breaks or smooths a tangent.</summary>
        public const string kPathBreakTangentActionName = "PathBreakTangent";

        /// <summary>Name of the action that opens the curve timeline.</summary>
        public const string kTimelineActionName = "Timeline";

        /// <summary>Name of the action that hides the mod's panels and the world overlays.</summary>
        public const string kHideUIActionName = "HideUI";

        /// <summary>
        /// Gets the resolved input action that generates an orbit shot.
        /// </summary>
        public static ProxyAction ApplyOrbitAction { get; private set; }

        /// <summary>Gets the action that opens the camera path tool.</summary>
        public static ProxyAction PathToolAction { get; private set; }

        /// <summary>Gets the action that writes a drawn path to the timeline.</summary>
        public static ProxyAction GeneratePathAction { get; private set; }

        /// <summary>Gets the click action that places a path point.</summary>
        public static ProxyAction PathApplyAction { get; private set; }

        /// <summary>Gets the action that raises a path point.</summary>
        public static ProxyAction PathRaiseAction { get; private set; }

        /// <summary>Gets the action that lowers a path point.</summary>
        public static ProxyAction PathLowerAction { get; private set; }

        /// <summary>Gets the action that reverses a drawn path.</summary>
        public static ProxyAction PathReverseAction { get; private set; }

        /// <summary>Gets the action that breaks or smooths a tangent.</summary>
        public static ProxyAction PathBreakTangentAction { get; private set; }

        /// <summary>Gets the action that opens the curve timeline.</summary>
        public static ProxyAction TimelineAction { get; private set; }

        /// <summary>Hides the panels so a shot can be judged on the picture alone.</summary>
        public static ProxyAction HideUIAction { get; private set; }

        /// <inheritdoc/>
        public override string ModName => nameof(ExtendedPhotomode);

        /// <inheritdoc/>
        // Must match "id" in UI/mod.json — it is the binding group both sides key off.
        public override string Id => "ExtendedPhotomode";

        /// <inheritdoc/>
        protected override string UiHostPrefix => "extendedphotomode";

        /// <summary>
        /// Gets the mod's typed settings.
        /// </summary>
        public new Setting Settings => (Setting)base.Settings;

        /// <inheritdoc/>
        protected override ModSetting CreateSettings(IMod mod) { return new Setting(mod); }

        /// <inheritdoc/>
        protected override IDictionarySource CreateEnUsLocalization(ModSetting settings) {
            return new LocaleEN((Setting)settings);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Input actions can only be resolved after <c>RegisterKeyBindings</c> has run, which the
        /// base class does immediately before calling this hook.
        /// </remarks>
        protected override void OnAfterLoad(UpdateSystem updateSystem) {
            ApplyOrbitAction = Settings.GetAction(kApplyOrbitActionName);
            ApplyOrbitAction.shouldBeEnabled = true;

            PathToolAction     = Settings.GetAction(kPathToolActionName);
            GeneratePathAction = Settings.GetAction(kGeneratePathActionName);
            PathApplyAction    = Settings.GetAction(kPathApplyActionName);
            PathRaiseAction    = Settings.GetAction(kPathRaiseActionName);
            PathLowerAction    = Settings.GetAction(kPathLowerActionName);
            PathReverseAction  = Settings.GetAction(kPathReverseActionName);
            PathBreakTangentAction = Settings.GetAction(kPathBreakTangentActionName);

            PathRaiseAction.shouldBeEnabled = true;
            PathLowerAction.shouldBeEnabled = true;
            PathReverseAction.shouldBeEnabled = true;
            PathBreakTangentAction.shouldBeEnabled = true;

            TimelineAction = Settings.GetAction(kTimelineActionName);
            TimelineAction.shouldBeEnabled = true;

            HideUIAction = Settings.GetAction(kHideUIActionName);
            HideUIAction.shouldBeEnabled = true;

            PathToolAction.shouldBeEnabled     = true;
            GeneratePathAction.shouldBeEnabled = true;
        }

        /// <inheritdoc/>
        protected override void RegisterSystems(UpdateSystem updateSystem) {
            // EPM_PhotoModePropertySystem must reach PhotoModeRenderSystem before PhotoModeUISystem
            // builds its tab list, which it does once in its own OnCreate. Registering at
            // PostSimulation is not early enough on its own — the system self-registers its
            // properties on first update and asks the UI to rebuild. See the system for details.
            updateSystem.UpdateAt<EPM_PhotoModeQualitySystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_WeatherSyncSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_PhotoModePropertySystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_ShotSequenceSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_ShotSubjectSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_FollowSubjectSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_OrbitUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_OrbitBookmarkSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_PathLibrarySystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_PathPreviewSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_ShotListSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_ScrubPreviewSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_TimelineHistorySystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_TimelineEditSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_CursorHideSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<EPM_ShotSortSystem>(SystemUpdatePhase.UIUpdate);
            // Nothing is registered at Rendering any more. EPM_OrbitPreviewSystem drew the orbit ring
            // in photo mode and was removed: it appeared for anyone who opened photo mode, whatever
            // they were shooting, and its only off switch was a toggle in a GAMEPLAY tool's toolbar.
            // The ring that matters is the one OrbitShotEditor draws while the shot tool is open,
            // which is authoring feedback rather than an overlay laid over someone's photograph.
            // CustomOverlayRenderSystem went with it — the preview was its only writer, and an
            // overlay renderer with nothing to draw is a buffer and an update per frame for nothing.
            // Re-register both together if anything ever needs to draw at Rendering again.
            updateSystem.UpdateAt<EPM_PathToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<EPM_PathToolToggleSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<EPM_PathHintSystem>(SystemUpdatePhase.UITooltip);
            // PreSerialize&lt;T&gt; is vanilla is wrapper that calls IPreSerialize just before the world
            // is written; registering the store system directly would never fire that hook.
            updateSystem.UpdateBefore<PreSerialize<EPM_PathStoreSystem>>(SystemUpdatePhase.Serialize);
        }
    }
}
