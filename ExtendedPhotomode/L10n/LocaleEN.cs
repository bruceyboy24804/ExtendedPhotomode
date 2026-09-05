namespace ExtendedPhotomode.L10n {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal;

    using ExtendedPhotomode.Systems;
    using ExtendedPhotomode.Tools;

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

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PreviewOutsidePhotoMode)), "Preview lens and light outside photo mode" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PreviewOutsidePhotoMode)),
                  "Show focal length, focus and time of day while scrubbing the mod's timeline with photo mode closed. Scrubbing always applies these values; the reason they were invisible is that the game only renders them inside photo mode. Two things are borrowed to fix that, and only those two — the mod does not open photo mode, which would drop you out of the path tool and clear your overrides on the way back out. The cost: photo mode's control volume outranks the climate volume, so any weather override left on it applies too, the same way photo mode overrides Weather Anarchy. The first scrub also takes the camera, and hands it back when the timeline closes." },

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

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ApplyOrbitBinding)), "Generate shot" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ApplyOrbitBinding)),
                  "Generate whichever shot the Shot dropdown names and write it to the cinematic timeline. Does the same as the Generate button, and is the way to generate in the map editor, where that button does not appear." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetBindings)), "Reset key bindings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetBindings)),
                  "Reset all key bindings of the mod." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TimelineBinding)), "Curve timeline" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TimelineBinding)),
                  "Open the curve timeline — every channel of the shot on its own scale, stacked under one playhead." },

                { m_Setting.GetBindingKeyLocaleID(Mod.kTimelineActionName), "Curve timeline" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kHideUIActionName), "Hide UI" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kApplyOrbitActionName), "Generate shot" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathToolActionName), "Camera path tool" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kGeneratePathActionName), "Generate path shot" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathApplyActionName), "Place path point" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathRaiseActionName), "Raise path point" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathLowerActionName), "Lower path point" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathReverseActionName), "Reverse path" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kPathBreakTangentActionName), "Break tangent" },

                // What the path tool's cursor hints actually say. A DisplayNameOverride's displayName
                // becomes the hint's name and is resolved through Common.ACTION[<name>], so these are
                // keyed off the tokens in PathHints rather than off an action.
                { Hint(PathHints.AddPoint), "Add point" },
                { Hint(PathHints.InsertPoint), "Insert point" },
                { Hint(PathHints.MovePoint), "Move point" },
                { Hint(PathHints.ShapeCurve), "Shape curve" },
                { Hint(PathHints.PickHandle), "Drag a handle" },
                { Hint(PathHints.SharpCorner), "Sharp corner" },
                { Hint(PathHints.SmoothCorner), "Smooth corner" },
                { Hint(PathHints.Reverse), "Reverse direction" },
                { Hint(PathHints.Raise), "Raise point" },
                { Hint(PathHints.Lower), "Lower point" },
                { Hint(PathHints.DeletePoint), "Delete point" },
                { Hint(PathHints.StopDrawing), "Stop drawing" },
                { Hint(PathHints.SetLookAt), "Aim point here" },
                { Hint(PathHints.SelectForLookAt), "Pick a point to aim" },
                { Hint(PathHints.TraceRoad), "Trace this road" },
                { Hint(PathHints.PlaceSubject), "Place the subject here" },
                { Hint(PathHints.MoveOrbitCentre), "Move the subject" },
                { Hint(PathHints.DragOrbitStart), "Set where the orbit starts" },
                { Hint(PathHints.DragOrbitEnd), "Set where the orbit ends" },
                { Hint(PathHints.DragDollyStart), "Set where the dolly starts" },
                { Hint(PathHints.DragDollyEnd), "Set where the dolly ends" },

                // Fallbacks for when no override is active: the action's own name, resolved as
                // Common.ACTION[<full action id>]. A different key from the settings-menu binding
                // label above, and what shows as a raw string when an override fails.
                { Action(Mod.kApplyOrbitActionName), "Generate shot" },
                { Action(Mod.kPathToolActionName), "Camera path tool" },
                { Action(Mod.kGeneratePathActionName), "Generate path shot" },
                { Action(Mod.kPathApplyActionName), "Place path point" },
                { Action(Mod.kPathRaiseActionName), "Raise path point" },
                { Action(Mod.kPathLowerActionName), "Lower path point" },
                { Action(Mod.kPathReverseActionName), "Reverse path" },
                { Action(Mod.kPathBreakTangentActionName), "Break tangent" },
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

                { Title(EPM_PhotoModePropertySystem.kHeightGroupId), "Height" },
                { Tip(EPM_PhotoModePropertySystem.kHeightGroupId),
                  "How high above the target the camera sits, in metres, at the start and end of the shot. Negative values orbit from below. Make the two differ and the orbit climbs or descends as it goes round, giving a helix instead of a flat circle." },

                { Title(EPM_PhotoModePropertySystem.kHeightId), "Start" },
                { Title(EPM_PhotoModePropertySystem.kEndHeightId), "End" },

                { Title(EPM_PhotoModePropertySystem.kSweepEaseId), "Ease sweep" },
                { Tip(EPM_PhotoModePropertySystem.kSweepEaseId),
                  "Eases the camera into and out of its trip around the target, from 0 (an even sweep) to 1. Only the rotation is eased -- the radius and height still change evenly across the shot, so a spiral keeps pulling in at a constant rate while the swing around the subject slows at both ends." },

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

                { Title(EPM_PhotoModePropertySystem.kFollowId), "Follow subject" },
                { Tip(EPM_PhotoModePropertySystem.kFollowId),
                  "Track the pinned object while the shot plays, for a vehicle or a citizen that is still moving. Aim at subject keeps the camera on its keyframed course and just turns to hold the subject in frame; Ride with subject moves the whole shot along with it, so an orbit stays an orbit around a car that is driving away. Needs a centre pinned to an actual object — use the pin button on a selected object, not the Pin centre checkbox, which pins a bare point. Unlike everything else here it is not written into keyframes, so it does nothing while the game is paused, and a saved shot replays without it in a later session." },

                { "PhotoMode.FOLLOWMODE[None]", "—" },
                { "PhotoMode.FOLLOWMODE[Off]", "Off" },
                { "PhotoMode.FOLLOWMODE[Aim]", "Aim at subject" },
                { "PhotoMode.FOLLOWMODE[Ride]", "Ride with subject" },

                { Title(EPM_PhotoModePropertySystem.kFramingId), "Framing" },
                { Tip(EPM_PhotoModePropertySystem.kFramingId),
                  "Hold the pinned subject at a fixed place in frame for the whole shot, instead of merely pointing at it. The camera is turned off the subject by the angle that puts it on a third, so the move you drew is untouched — only the aim changes. The thirds are the standard composition: a subject on the left third looks into the space on the right." },

                { "PhotoMode.FRAMINGRULE[None]", "Off" },
                { "PhotoMode.FRAMINGRULE[Centre]", "Centred" },
                { "PhotoMode.FRAMINGRULE[LeftThird]", "Left third" },
                { "PhotoMode.FRAMINGRULE[RightThird]", "Right third" },
                { "PhotoMode.FRAMINGRULE[Headroom]", "Headroom" },

                { Title(EPM_PhotoModePropertySystem.kFocusId), "Focus" },
                { Tip(EPM_PhotoModePropertySystem.kFocusId),
                  "Drive depth-of-field focus from the shot's own geometry. Track keeps the pinned subject sharp however far the camera travels — a dolly towards a tower currently goes soft, because focus is a fixed number and the distance is not. Rack ramps focus from the subject to a second point across the shot, so the foreground falls out of focus as the background comes into it. The aperture is opened alongside it, because a perfectly focused f/22 lens looks identical to one that is not." },

                { "PhotoMode.FOCUSMODE[None]", "—" },
                { "PhotoMode.FOCUSMODE[Off]", "Off" },
                { "PhotoMode.FOCUSMODE[Track]", "Track subject" },
                { "PhotoMode.FOCUSMODE[Rack]", "Rack focus" },

                { Title(EPM_PhotoModePropertySystem.kFocusDepthId), "Depth of field" },
                { Tip(EPM_PhotoModePropertySystem.kFocusDepthId),
                  "How shallow the focus is, from 0 (everything sharp) to 1 (the widest lens that still reads). Sets the aperture once for the whole shot rather than per keyframe — an aperture that changed as the shot ran would alter the exposure, which looks like the image brightening for no reason." },

                { Title(EPM_PhotoModePropertySystem.kFocusEaseId), "Rack easing" },
                { Tip(EPM_PhotoModePropertySystem.kFocusEaseId),
                  "How much the focus pull accelerates and settles rather than sliding at a constant rate. A linear rack reads as mechanical; a focus puller does not move at one speed." },

                { Title(EPM_PhotoModePropertySystem.kFramingHoldId), "Hold subject size" },
                { Tip(EPM_PhotoModePropertySystem.kFramingHoldId),
                  "Solve the lens at every keyframe so the subject stays the size it is at the start of the shot, however far the camera travels. A camera pulling away zooms in to compensate, which keeps the subject constant while the background rushes — the same optical trick as a dolly zoom, driven by the move you drew rather than by a fixed pair of distances." },

                { Title(EPM_PhotoModePropertySystem.kFramingLensId), "Framing lens" },
                { Tip(EPM_PhotoModePropertySystem.kFramingLensId),
                  "The focal length the framing works from, in millimetres. With Hold subject size off it is simply the lens used to work out how wide the frame is, which is what the thirds are measured against." },

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



                { "PhotoMode.SHOTTYPE[None]", "—" },
                { "PhotoMode.SHOTTYPE[Orbit]", "Orbit" },
                { "PhotoMode.SHOTTYPE[DollyZoom]", "Dolly zoom" },
                { "PhotoMode.SHOTTYPE[Path]", "Drawn path" },

                { Title(EPM_PhotoModePropertySystem.kShotSelectorTitleId), "SHOT" },

                { Title(EPM_PhotoModePropertySystem.kShotTypeId), "Shot" },
                { Tip(EPM_PhotoModePropertySystem.kShotTypeId),
                  "Which shot the Generate button produces. Each type reads its own section of settings below: Orbit and Dolly zoom use the orbit centre, Drawn path uses the path from the path tool (Ctrl+P)." },

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

                { Title(EPM_PhotoModePropertySystem.kPathLookId), "Aim" },
                { Tip(EPM_PhotoModePropertySystem.kPathLookId),
                  "How the camera is pointed as it flies the path. Look at target keeps the pinned centre framed and works out pitch for every keyframe, so the Pitch slider above has no effect in that mode. Follow an aim rail uses a second drawn path as the thing being looked at — the two are matched by how far along each you are, so the start of the rail belongs to the start of the move and the end to the end. Draw the rail by switching the path tool from Camera to Rail." },

                { Title(EPM_PhotoModePropertySystem.kPathTerrainId), "Terrain" },
                { Tip(EPM_PhotoModePropertySystem.kPathTerrainId),
                  "What the path does about the ground under it. Only the points you place are set relative to the terrain; the curve between them is a straight-ish arc through the air, so a path drawn across a ridge flies through the hill and one across a valley sails level over it. Never below ground keeps your heights and only lifts the shot where it would clip. Follow terrain ignores your heights entirely and holds one altitude the whole way, which is the drone shot." },

                { "PhotoMode.PATHTERRAINMODE[None]", "—" },
                { "PhotoMode.PATHTERRAINMODE[Free]", "Off (use my heights)" },
                { "PhotoMode.PATHTERRAINMODE[Floor]", "Never below ground" },
                { "PhotoMode.PATHTERRAINMODE[Follow]", "Follow terrain" },

                { Title(EPM_PhotoModePropertySystem.kPathClearanceId), "Ground clearance" },
                { Tip(EPM_PhotoModePropertySystem.kPathClearanceId),
                  "How far above the ground the terrain modes hold the camera, in metres." },

                { Title(EPM_PhotoModePropertySystem.kPathObstaclesId), "Obstacles" },
                { Tip(EPM_PhotoModePropertySystem.kPathObstaclesId),
                  "What the path does about buildings and other objects in its way — the other half of Terrain, which only handles the ground. Warn draws the offending stretch red and changes nothing, so the fix stays yours. Lift raises the camera over what it hits, spreading the climb across the run-up either side so it reads as an aerial rising over a tower rather than as a correction." },

                { "PhotoMode.PATHCLEARANCEMODE[None]", "—" },
                { "PhotoMode.PATHCLEARANCEMODE[Off]", "Ignore objects" },
                { "PhotoMode.PATHCLEARANCEMODE[Warn]", "Warn only" },
                { "PhotoMode.PATHCLEARANCEMODE[Lift]", "Fly over them" },

                { Title(EPM_PhotoModePropertySystem.kPathObstacleClearanceId), "Object clearance" },
                { Tip(EPM_PhotoModePropertySystem.kPathObstacleClearanceId),
                  "How far above an obstruction the camera passes, in metres. Measured against the object's bounding box, so it errs a little high — which is the right way for this to be wrong." },

                { Title(EPM_PhotoModePropertySystem.kPathLookAheadId), "Look ahead" },
                { Tip(EPM_PhotoModePropertySystem.kPathLookAheadId),
                  "How far along the path the camera aims, in metres. At 0 it aims at the next sample, which makes a tight curve read as jittery — the aim swings by the whole turn between one pair of samples and the next. Aiming further ahead averages that out, the way a driver looks into a bend rather than at the bonnet. Only applies when the path faces along its own direction." },

                { Title(EPM_PhotoModePropertySystem.kPathEaseId), "Ease" },
                { Tip(EPM_PhotoModePropertySystem.kPathEaseId),
                  "How much the whole move slows at its start and end, from 0 (an even pace) to 1. Composes with per-point speed: a point marked slow stays proportionally slow, and the shot still eases at both ends." },

                // Dropdown options resolve through AutomaticSettings.GetEnumValues(type, "PhotoMode")
                // as PhotoMode.TYPENAME[Member] with the type name upper-cased — a different shape
                // from the PROPERTY_TITLE keys above, and the dropdown shows raw keys without them.
                { "PhotoMode.PATHLOOKMODE[Rail]", "Follow an aim rail" },
                { "PhotoMode.PATHLOOKMODE[Forward]", "Look along path" },
                { "PhotoMode.PATHLOOKMODE[Fixed]", "Hold a heading" },
                { "PhotoMode.PATHLOOKMODE[Target]", "Look at target" },

                { Title(EPM_PhotoModePropertySystem.kTransitionSecondsId), "Transition in" },
                { Tip(EPM_PhotoModePropertySystem.kTransitionSecondsId),
                  "How long the camera takes to travel from the previous shot into this one, when the shot list assembles them. Zero is a hard cut, which is a legitimate choice — anything above it is a move between setups, and is what makes a sequence read as one continuous piece rather than a set of clips. Captured with the shot when you press Add current, so each shot carries its own approach." },

                { Title(EPM_PhotoModePropertySystem.kTransitionEaseId), "Transition easing" },
                { Tip(EPM_PhotoModePropertySystem.kTransitionEaseId),
                  "How much the bridging move slows at each end. High values glide out of one shot and settle into the next; zero slides between them at a constant rate." },

                { Title(EPM_PhotoModePropertySystem.kRigId), "Camera rig" },
                { Tip(EPM_PhotoModePropertySystem.kRigId),
                  "Make the move obey a physical camera support instead of pure geometry. A generated move is mathematically perfect, which is exactly why it reads as computer generated — a real camera has mass, arrives at each pose slightly late, and is never perfectly still. A crane is heavy and utterly smooth; a drone holds position but drifts; handheld follows the action closely and is never still. Only where the camera is changes, never when — durations, dwells and per-point speeds all survive." },

                { "PhotoMode.CAMERARIG[None]", "—" },
                { "PhotoMode.CAMERARIG[Free]", "None (perfect)" },
                { "PhotoMode.CAMERARIG[Crane]", "Crane" },
                { "PhotoMode.CAMERARIG[Drone]", "Drone" },
                { "PhotoMode.CAMERARIG[Handheld]", "Handheld" },

                { Title(EPM_PhotoModePropertySystem.kRigStrengthId), "Rig strength" },
                { Tip(EPM_PhotoModePropertySystem.kRigStrengthId),
                  "How much of the rig's character to apply, from 0 (none) to 1. Turn it down if the lag is softening a deliberate whip-pan." },

                { Title(EPM_PhotoModePropertySystem.kRigSeedId), "Rig seed" },
                { Tip(EPM_PhotoModePropertySystem.kRigSeedId),
                  "Chooses the unsteadiness pattern. The same seed always produces the same take, so regenerating a shot does not reshuffle its handheld wobble — change this to get a different one." },

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
        /// <summary>Builds the locale key a cursor hint resolves an input action's name through.</summary>
        /// <param name="actionName">The action name as declared on <see cref="Mod"/>.</param>
        /// <remarks>
        /// The id is namespaced with the assembly and mod class, so it repeats the mod name twice —
        /// <c>ExtendedPhotomode.ExtendedPhotomode.Mod/PathApply</c>. Built here rather than written
        /// out so the two halves cannot drift apart if either is renamed.
        /// </remarks>
        private static string Action(string actionName) {
            return $"Common.ACTION[{nameof(ExtendedPhotomode)}.{typeof(Mod).FullName}/{actionName}]";
        }

        /// <summary>Builds the locale key a contextual cursor hint resolves through.</summary>
        /// <param name="hintKey">A token from <see cref="PathHints"/>.</param>
        private static string Hint(string hintKey) { return $"Common.ACTION[{hintKey}]"; }

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
