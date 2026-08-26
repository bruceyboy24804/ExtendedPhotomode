namespace ExtendedPhotomode.L10n {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal;

    using ExtendedPhotomode.Systems;

    #endregion

    /// <summary>
    /// English (en-US) strings for the mod's settings UI.
    /// </summary>
    public class LocaleEN : IDictionarySource {
        private readonly Setting m_Setting;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocaleEN"/> class.
        /// </summary>
        /// <param name="setting">The settings instance whose locale ids are being filled in.</param>
        public LocaleEN(Setting setting) { m_Setting = setting; }

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
                                                                     Dictionary<string, int> indexCounts) {
            return new Dictionary<string, string> {
                { m_Setting.GetSettingsLocaleID(), "Extended Photomode" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kRenderingGroup), "Rendering" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kKeybindingGroup), "Key bindings" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestorePostProcessQuality)), "Restore post-process quality" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RestorePostProcessQuality)),
                  "Photo mode forces bloom, depth of field and motion blur onto HDRP's built-in defaults instead of the quality tier your graphics settings ask for. Leave this on to keep the quality you actually configured." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SyncWeatherIntoPhotoMode)), "Carry weather into photo mode" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SyncWeatherIntoPhotoMode)),
                  "Photo mode keeps its own copy of the fog and cloud settings at a higher priority than the game climate, so weather mods stop showing through once you touch a weather slider. Leave this on to seed photo mode with the weather you actually have." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShotSort)), "Sort saved shots" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ShotSort)),
                  "How the game lists your saved cinematic shots. Vanilla applies no order at all, so the list arrives however the asset database happens to enumerate it — which is neither alphabetical nor by date, and can differ between sessions." },

                { m_Setting.GetEnumValueLocaleID(ShotSortOrder.Default), "Game default (unordered)" },
                { m_Setting.GetEnumValueLocaleID(ShotSortOrder.NameAscending), "Name (A–Z)" },
                { m_Setting.GetEnumValueLocaleID(ShotSortOrder.NameDescending), "Name (Z–A)" },
                { m_Setting.GetEnumValueLocaleID(ShotSortOrder.Newest), "Newest first" },
                { m_Setting.GetEnumValueLocaleID(ShotSortOrder.Oldest), "Oldest first" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.HideCursorDuringPlayback)), "Hide cursor during playback" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.HideCursorDuringPlayback)),
                  "Hide the mouse pointer while a cinematic shot is playing, so it stays out of screen recordings. The pointer comes back as soon as playback stops or you leave photo mode." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SmoothCameraRotation)), "Smooth camera rotation" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SmoothCameraRotation)),
                  "The game flattens the rotation curve at every keyframe, so a cinematic camera stalls once per key as it turns. Leave this on to keep the rotation moving at a constant speed." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestoreTimeAndWeatherOnExit)), "Restore time and weather on exit" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RestoreTimeAndWeatherOnExit)),
                  "Leaving photo mode clears the game time override, which permanently disables Time & Weather Anarchy for the rest of the session. Leave this on to put your time and weather settings back." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PathToolBinding)), "Camera path tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PathToolBinding)),
                  "Open the tool that draws a camera path on the ground. Click to place points, Escape to remove the last one." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GeneratePathBinding)), "Generate path shot" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GeneratePathBinding)),
                  "Write the drawn path to the cinematic timeline as a camera move." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PathApplyBinding)), "Place path point" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PathApplyBinding)),
                  "Click that places a point while the camera path tool is open." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PathRaiseBinding)), "Raise path point" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PathRaiseBinding)),
                  "Hold to raise the path point under the cursor, or the last one placed." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PathLowerBinding)), "Lower path point" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PathLowerBinding)),
                  "Hold to lower the path point under the cursor, or the last one placed." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PathReverseBinding)), "Reverse path" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PathReverseBinding)),
                  "Flip the direction the camera flies the drawn path." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PathBreakTangentBinding)), "Break tangent" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PathBreakTangentBinding)),
                  "Toggle the hovered point between a smooth curve and a sharp corner." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ApplyOrbitBinding)), "Generate orbit" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ApplyOrbitBinding)),
                  "Generate an orbit around a point ahead of the camera and write it to the cinematic timeline." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetBindings)), "Reset key bindings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetBindings)),
                  "Reset all key bindings of the mod." },

                { m_Setting.GetBindingKeyLocaleID(Mod.kApplyOrbitActionName), "Generate orbit" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathToolActionName), "Camera path tool" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kGeneratePathActionName), "Generate path shot" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathApplyActionName), "Place path point" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathRaiseActionName), "Raise path point" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathLowerActionName), "Lower path point" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathReverseActionName), "Reverse path" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathBreakTangentActionName), "Break tangent" },
                { m_Setting.GetBindingMapLocaleID(), "Extended Photomode" },

                // Photo mode panel. PhotoModeUISystem looks every row's label up as
                // PhotoMode.PROPERTY_TITLE[<id>] and its tooltip as PhotoMode.PROPERTY_TOOLTIP[<id>],
                // falling back to the raw id when no entry exists. Supplying both is what makes our
                // rows read like the built-in ones instead of showing "Orbit.Radius" with no tooltip.
                { Title(EPM_PhotoModePropertySystem.kSubjectTitleId), "SUBJECT" },

                { Title(EPM_PhotoModePropertySystem.kGroupTitleId), "ORBIT SHOT" },

                // A grouped row: the label and tooltip hang off the shared prefix, and each component
                // gets a short label of its own under its full "prefix/component" id.
                { Title(EPM_PhotoModePropertySystem.kRadiusGroupId), "Radius" },
                { Tip(EPM_PhotoModePropertySystem.kRadiusGroupId),
                  "How far the camera sits from the point it circles, in metres. Set the two differently to spiral: a smaller end pulls inward as the shot orbits, a larger one pulls away. Equal values give a plain circle." },

                { Title(EPM_PhotoModePropertySystem.kRadiusId), "Start" },
                { Title(EPM_PhotoModePropertySystem.kEndRadiusId), "End" },

                { Title(EPM_PhotoModePropertySystem.kHeightId), "Height" },
                { Tip(EPM_PhotoModePropertySystem.kHeightId),
                  "How high above the target the camera sits, in metres. Negative values orbit from below." },

                { Title(EPM_PhotoModePropertySystem.kSweepId), "Sweep" },
                { Tip(EPM_PhotoModePropertySystem.kSweepId),
                  "How far around the target the camera travels, in degrees. 360 is one full turn; negative values orbit the other way." },

                { Title(EPM_PhotoModePropertySystem.kDurationId), "Duration" },
                { Tip(EPM_PhotoModePropertySystem.kDurationId),
                  "How long the generated move takes, in seconds." },

                { Title(EPM_PhotoModePropertySystem.kSpacingId), "Keyframe spacing" },
                { Tip(EPM_PhotoModePropertySystem.kSpacingId),
                  "Degrees of arc between generated keyframes. Smaller is smoother but puts more keys on the timeline." },

                { Title(EPM_PhotoModePropertySystem.kLookAtId), "Look at target" },
                { Tip(EPM_PhotoModePropertySystem.kLookAtId),
                  "Aim every generated keyframe at the target. Turn this off to dolly around the target while facing outward." },

                { Title(EPM_PhotoModePropertySystem.kPreviewId), "Show preview" },
                { Tip(EPM_PhotoModePropertySystem.kPreviewId),
                  "Draw the orbit in the world so you can aim it before generating. It hides automatically when you hide the UI." },

                { Title(EPM_PhotoModePropertySystem.kPinCentreId), "Pin centre" },
                { Tip(EPM_PhotoModePropertySystem.kPinCentreId),
                  "Keep the orbit centred on one spot instead of following where the camera looks. Loading a saved shot pins it automatically; turn this off to place a new centre." },

                { Title(EPM_PhotoModePropertySystem.kEnvTitleId), "ENVIRONMENT" },

                { Title(EPM_PhotoModePropertySystem.kTimeGroupId), "Hours" },
                { Tip(EPM_PhotoModePropertySystem.kTimeGroupId),
                  "The hours a generated shot runs between, from 0 to 24. The end may be earlier than the start, which runs the light backwards, but it does not wrap past midnight." },


                { Title(EPM_PhotoModePropertySystem.kTimeKeysId), "Key every frame" },
                { Tip(EPM_PhotoModePropertySystem.kTimeKeysId),
                  "Give the time of day ramp a key at every camera keyframe instead of one at each end. The light runs exactly the same either way, but each key becomes a handle in the curve editor, so you can drag them to dwell on golden hour and hurry through the night. Generating the shot again rewrites them all, so shape the ramp last." },

                { Title(EPM_PhotoModePropertySystem.kTimeEaseId), "Linger at ends" },

                { Tip(EPM_PhotoModePropertySystem.kTimeEaseId),
                  "How much the light slows down near the start and end hours you picked, from 0 (an even sweep) to 1. A ramp that is even in hours is not even in what you see — nearly all of a day's visible change happens around sunrise and sunset, so an even sweep races through those and then sits on flat daylight for the rest of the shot. Raise this to spend more of the shot on the hours you chose and less on the middle of the range. Needs a key at every camera keyframe." },
                { Title(EPM_PhotoModePropertySystem.kStartTimeId), "Start" },
                { Title(EPM_PhotoModePropertySystem.kEndTimeId), "End" },



                { Title(EPM_PhotoModePropertySystem.kKeyframeIndexId), "Keyframe" },
                { Tip(EPM_PhotoModePropertySystem.kKeyframeIndexId),
                  "Which keyframe of the shot on the timeline to edit, counting from 1." },

                { Title(EPM_PhotoModePropertySystem.kKeyframeEaseId), "Easing" },
                { "PhotoMode.SHOTTYPE[None]", "—" },
                { "PhotoMode.SHOTTYPE[Orbit]", "Orbit" },
                { "PhotoMode.SHOTTYPE[DollyZoom]", "Dolly zoom" },
                { "PhotoMode.SHOTTYPE[Path]", "Drawn path" },

                { Title(EPM_PhotoModePropertySystem.kShotSelectorTitleId), "SHOT" },

                { Title(EPM_PhotoModePropertySystem.kShotTypeId), "Shot" },
                { Tip(EPM_PhotoModePropertySystem.kShotTypeId),
                  "Which shot the Generate button produces. Each type reads its own section of settings below: Orbit and Dolly zoom use the orbit centre, Drawn path uses the path from the path tool (Ctrl+P)." },

                { Tip(EPM_PhotoModePropertySystem.kKeyframeEaseId),
                  "How the camera arrives at and leaves this keyframe, the same four choices an animation package gives you. Applies to position and rotation together. Note that regenerating the shot, or toggling Constant speed, re-tangents every key and clears this." },

                // The zero-valued members below are the slot the dropdown discards, mirroring
                // GateFitMode.None. They should never be seen; the entries exist so that if one ever
                // does surface it reads as a dash rather than a raw locale key.
                { "PhotoMode.KEYFRAMEEASE[None]", "—" },
                { "PhotoMode.TIMEOFDAYPRESET[None]", "—" },
                { "PhotoMode.TIMEOFDAYPRESET[Custom]", "Custom" },
                { "PhotoMode.TIMEOFDAYPRESET[Sunrise]", "Sunrise" },
                { "PhotoMode.TIMEOFDAYPRESET[MorningGolden]", "Golden hour (morning)" },
                { "PhotoMode.TIMEOFDAYPRESET[Daylight]", "Daylight" },
                { "PhotoMode.TIMEOFDAYPRESET[EveningGolden]", "Golden hour (evening)" },
                { "PhotoMode.TIMEOFDAYPRESET[Sunset]", "Sunset" },
                { "PhotoMode.TIMEOFDAYPRESET[BlueHour]", "Blue hour" },
                { "PhotoMode.TIMEOFDAYPRESET[FullDay]", "Full day" },

                { Title(EPM_PhotoModePropertySystem.kTimeRangeId), "Range" },
                { Tip(EPM_PhotoModePropertySystem.kTimeRangeId),
                  "Fills the start and end hours from this map's real sun times, worked out for its latitude and the current date. Editing either hour by hand switches this back to Custom. Golden hour and blue hour are the actual solved times, not round numbers." },

                { "PhotoMode.PATHLOOKMODE[None]", "—" },

                { "PhotoMode.KEYFRAMEEASE[Linear]", "Linear (constant speed)" },
                { "PhotoMode.KEYFRAMEEASE[Smooth]", "Smooth" },
                { "PhotoMode.KEYFRAMEEASE[In]", "Ease in" },
                { "PhotoMode.KEYFRAMEEASE[Out]", "Ease out" },
                { "PhotoMode.KEYFRAMEEASE[InOut]", "Ease in and out" },

                { Title(EPM_PhotoModePropertySystem.kPathTitleId), "PATH SHOT" },

                { Title(EPM_PhotoModePropertySystem.kPathDurationId), "Duration" },
                { Tip(EPM_PhotoModePropertySystem.kPathDurationId),
                  "How long the camera takes to fly the drawn path, in seconds." },

                { Title(EPM_PhotoModePropertySystem.kPathSpacingId), "Keyframe spacing" },
                { Tip(EPM_PhotoModePropertySystem.kPathSpacingId),
                  "Metres of travel between generated keyframes. Smaller is smoother but puts more keys on the timeline." },

                { Title(EPM_PhotoModePropertySystem.kPathPitchId), "Pitch" },
                { Tip(EPM_PhotoModePropertySystem.kPathPitchId),
                  "How far the camera tilts while flying the path. Positive looks down." },

                { Title(EPM_PhotoModePropertySystem.kPathHeightId), "New point height" },
                { Tip(EPM_PhotoModePropertySystem.kPathHeightId),
                  "Height above the ground at which new path points are placed, in metres. Existing points are raised and lowered in the path tool." },

                { Title(EPM_PhotoModePropertySystem.kPathLookId), "Aim" },
                { Tip(EPM_PhotoModePropertySystem.kPathLookId),
                  "How the camera is pointed as it flies the path. Look at target keeps the pinned centre framed and works out pitch for every keyframe, so the Pitch slider above has no effect in that mode." },

                // Dropdown options resolve through AutomaticSettings.GetEnumValues(type, "PhotoMode")
                // as PhotoMode.TYPENAME[Member] with the type name upper-cased — a different shape
                // from the PROPERTY_TITLE keys above, and the dropdown shows raw keys without them.
                { "PhotoMode.PATHLOOKMODE[Forward]", "Look along path" },
                { "PhotoMode.PATHLOOKMODE[Fixed]", "Hold a heading" },
                { "PhotoMode.PATHLOOKMODE[Target]", "Look at target" },

                { Title(EPM_PhotoModePropertySystem.kPathPointTitleId), "PATH POINT" },

                { Title(EPM_PhotoModePropertySystem.kPathPointId), "Point" },
                { Tip(EPM_PhotoModePropertySystem.kPathPointId),
                  "Which point on the drawn path the controls below edit." },

                { Title(EPM_PhotoModePropertySystem.kPathPointHeightId), "Point height" },
                { Tip(EPM_PhotoModePropertySystem.kPathPointHeightId),
                  "Height of the selected point above sea level, in metres." },

                { Title(EPM_PhotoModePropertySystem.kPathPointSharpId), "Sharp corner" },
                { Tip(EPM_PhotoModePropertySystem.kPathPointSharpId),
                  "Give the selected point a hard corner instead of a smooth curve through it." },

                { Title(EPM_PhotoModePropertySystem.kShotTitleId), "TIMING" },

                { Title(EPM_PhotoModePropertySystem.kShotDurationId), "Shot duration" },
                { Tip(EPM_PhotoModePropertySystem.kShotDurationId),
                  "Retime the whole cinematic sequence so it plays faster or slower. Unlike the panel is own duration slider, this rescales the keyframes instead of adding dead time at the end, and can shorten a shot below its current length." },

                { Title(EPM_PhotoModePropertySystem.kDollyTitleId), "DOLLY ZOOM" },

                { Title(EPM_PhotoModePropertySystem.kDollyDistanceGroupId), "Distance" },
                { Tip(EPM_PhotoModePropertySystem.kDollyDistanceGroupId),
                  "How far the camera sits from the subject at each end of the move, in metres. Pulling back while the lens zooms in is the classic vertigo effect; pushing in reverses it." },

                { Title(EPM_PhotoModePropertySystem.kDollyStartId), "Start" },
                { Title(EPM_PhotoModePropertySystem.kDollyEndId), "End" },

                { Title(EPM_PhotoModePropertySystem.kDollyDurationId), "Dolly duration" },
                { Tip(EPM_PhotoModePropertySystem.kDollyDurationId),
                  "How long the dolly zoom takes, in seconds." },

                { Title(EPM_PhotoModePropertySystem.kDollyKeysId), "Dolly keys" },
                { Tip(EPM_PhotoModePropertySystem.kDollyKeysId),
                  "How many keyframes the move is built from. More keys hold the subject steadier through the zoom." },

                { Title(EPM_PhotoModePropertySystem.kSequenceTitleId), "SEQUENCE" },

                { Title(EPM_PhotoModePropertySystem.kReplaceId2), "Chain shots" },
                { Tip(EPM_PhotoModePropertySystem.kReplaceId2),
                  "Add each generated shot to the end of the timeline instead of replacing it, so an orbit, a path and a dolly zoom play back as one continuous move." },

                { Title(EPM_PhotoModePropertySystem.kTransitionId), "Transition" },
                { Tip(EPM_PhotoModePropertySystem.kTransitionId),
                  "Seconds of blend between one chained shot and the next. Zero makes the camera move between them as fast as the curve allows, which is the closest this timeline gets to a cut." },

                { Title(EPM_PhotoModePropertySystem.kConstantSpeedId), "Constant speed (all keyframes)" },
                { Tip(EPM_PhotoModePropertySystem.kConstantSpeedId),
                  "Sets every keyframe at once: on holds one dead speed through the whole move with no easing anywhere, off rounds the corner at each key. This is the baseline — use the Keyframe section below to ease individual keys afterwards. Applies straight away to the shot on the timeline, including one you keyframed by hand, and to everything generated later. Only the interpolation changes; your keyframes keep their times and positions.\n\nTwo things to watch. Toggling this re-tangents every key, so it clears any per-keyframe easing you have set — do the bulk pass first. And it joins keyframes with straight lines, so an orbit at wide keyframe spacing traces a polygon rather than a circle; tighten the spacing if you can see the corners." },
            };
        }

        /// <summary>
        /// Builds the localization key photo mode reads a property's label from.
        /// </summary>
        /// <param name="propertyId">The <c>PhotoModeProperty</c> id.</param>
        /// <returns>The title localization key.</returns>
        private static string Title(string propertyId) { return $"PhotoMode.PROPERTY_TITLE[{propertyId}]"; }

        /// <summary>
        /// Builds the localization key photo mode reads a property's tooltip from.
        /// </summary>
        /// <param name="propertyId">The <c>PhotoModeProperty</c> id.</param>
        /// <returns>The tooltip localization key.</returns>
        private static string Tip(string propertyId) { return $"PhotoMode.PROPERTY_TOOLTIP[{propertyId}]"; }

        /// <inheritdoc/>
        public void Unload() { }
    }
}
