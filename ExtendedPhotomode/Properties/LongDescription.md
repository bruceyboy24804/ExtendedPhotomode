# Extended Photomode

Photo mode, rebuilt for people making videos.

Vanilla's cinematic camera can only do so much: it resets your weather, it can't shoot at half speed, and building an orbit by hand means placing every keyframe yourself. Extended Photomode fills those gaps and adds shot types you'd otherwise have to fake.

Everything is written straight onto the **vanilla cinematic timeline**. Playback, scrubbing, the curve editor and saving a shot all work exactly as they already do — this extends the tool you know rather than replacing it.

### Early Access Warning
**This mod is currently in EARLY ACCESS.**
- Not all features are complete, and updates may be frequent.
- Please **back up your save** before using it.
- Bug reports and feedback are very welcome on [Discord](https://discord.gg/4f7geN26S).

### Shot generators
Pick a type from the **Shot** dropdown, frame your subject, and press Generate. The panel only shows the settings that apply to the shot you picked.

* **Orbit** -- circle a subject at a set radius, height and sweep. Give it a different end radius and it spirals in or out as it goes round.
* **Dolly zoom** -- the camera travels while the lens counter-zooms, holding your subject the same size while the background rushes past. The lens is written as a real curve, so it stays editable afterwards.
* **Drawn path** -- draw a route through your city and fly it.

### Drawing paths
Open the path tool with **Ctrl+P** and draw in the world. Click empty ground to add a point, click a segment to insert one, drag to move, **PageUp/PageDown** to change a point's height, **Escape** to delete, **Ctrl+R** to reverse. What a click does depends on what's under the cursor, so there are no modes to get stuck in.

Generate the shot with **Ctrl+Shift+P**, or from the Generate button. Paths can look along their own direction, hold a fixed heading, or track your pinned subject.

Save paths by name and load them back on any later shot.

### Timing
* **Per-keyframe easing** -- linear, smooth, ease in, ease out, ease in and out, set on any keyframe.
* **Constant speed** -- flatten every key at once for a perfectly even move.
* **Retime** -- stretch or compress a finished sequence without regenerating it.

### Light and weather
* **Time of day ranges** solved from your map's **real** sunrise, sunset, golden hour and twilight times, for its latitude and date. Pick "Golden hour" and you get the actual figures, not round numbers.
* **A key at every camera keyframe**, so you can drag the light's pacing around in the curve editor instead of placing keys by hand.
* **Linger at ends** -- a linear ramp in hours races through dawn and then sits on flat daylight, because almost all the visible change happens as the sun crosses the horizon. Turn this up and the shot spends its time where something is actually happening.
* **Weather is carried over** from your world instead of being reset when photo mode opens, so cloud and fog settings survive.

### Quality of life
* Sort your saved shots by name or by date.
* Hide the cursor during playback.
* Orbit around a building by selecting it.
* Post-process quality no longer drops when photo mode opens -- vanilla quietly downgrades bloom, depth of field and motion blur on entry.

### Known issues
* The orbit preview draws in normal gameplay but not inside photo mode, which suppresses overlays.
* Long shots at very high resolutions can play back unevenly; this is frame pacing rather than the camera path.
