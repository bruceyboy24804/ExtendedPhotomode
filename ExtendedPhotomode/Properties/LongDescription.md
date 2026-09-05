# Extended Photomode

Photo mode, rebuilt for people making videos.

Vanilla's cinematic camera can only do so much: it resets your weather, it can't shoot at half speed, and building an orbit by hand means placing every keyframe yourself. Extended Photomode fills those gaps and adds shot types you'd otherwise have to fake.

Everything is written straight onto the **vanilla cinematic timeline**. Playback, scrubbing, the curve editor and saving a shot all work exactly as they already do — this extends the tool you know rather than replacing it.

### Early Access Warning
**This mod is currently in EARLY ACCESS.**
- Bug reports and feedback are very welcome on [Discord](https://discord.gg/4f7geN26S).

### Shot generators
Pick a type from the **Shot** dropdown, frame your subject, and press Generate. The panel only shows the settings that apply to the shot you picked.

* **Orbit** -- circle a subject at a set radius, height and sweep. Give it a different end radius and it spirals in or out as it goes round; a different end height and it climbs or descends into a helix. Ease the sweep to slow the swing at both ends without disturbing that travel.
* **Dolly zoom** -- the camera travels while the lens counter-zooms, holding your subject the same size while the background rushes past. The lens is written as a real curve, so it stays editable afterwards.
* **Drawn path** -- draw a route through your city and fly it.

### Drawing paths
Open the path panel with **Ctrl+P**. The panel is the hub: it shows your saved paths, how many points the current one has, and buttons to start drawing or generate the shot. Opening it does not put you into the tool -- press **Draw path** when you actually want to edit, so you can browse the library without risking a stray click.

While drawing, click empty ground to add a point, click a segment to insert one, drag to move, **PageUp/PageDown** to change a point's height, **Ctrl+R** to reverse. What a click does depends on what's under the cursor, so there are no modes to get stuck in.

**Right-click** a point to delete it. **Escape** backs out: once to stop drawing, again to close the panel. The two never overlap, so backing out can't cost you a point.

Generate the shot with **Ctrl+Shift+P**, or from the Generate button. Paths can look along their own direction, hold a fixed heading, or track your pinned subject.

Save paths by name and load them back on any later shot.

### Per-point control
Switch between **Points** and **Curves** to choose what a click acts on -- handles are only drawn and pickable in Curves mode, so they never fight the points they sit on top of.

Every point has its own settings, rather than one setting covering the whole path:

* **Dwell** -- hold still here for a few seconds. This adds to the shot instead of stealing time from the move.
* **Speed** -- a weight on time, not a change to the route. The path still runs its full duration; a stretch marked 0.5 takes twice as long to cross and the rest speeds up to pay for it.
* **Look at** -- aim at your pinned subject from this point. Blended between neighbours, so the camera swings from one subject to the next across a segment rather than snapping at the point.
* **Focal length** and **time of day** -- held at that point and interpolated to the next, so the lens and the light can be paced along the route.
* **Pitch**, **height**, position, and **sharp corner** to break the curve.

**Look ahead** decides how far down the path the camera aims. At zero it looks at the very next sample, which makes a tight bend read as jittery; aim further ahead and the turn averages out, the way a driver looks into a bend rather than at the bonnet. **Ease** slows the whole move at both ends, and composes with per-point speed instead of overriding it.

### Shaping the route
* **Terrain** -- only the points you place are set relative to the ground, so the curve between them can fly through a ridge or sail over a valley. *Never below ground* keeps your heights and lifts the shot only where it would clip; *Follow terrain* holds one altitude the whole way, which is the drone shot.
* **Closed loop** -- join the last point back to the first as a real extra curve segment, so the join is as smooth as any other corner. Pair it with the timeline's own Loop toggle for a flythrough that never stops.
* **Snapping** -- round to a **grid**, fix the heading to an **angle** step for straight runs and clean corners, land exactly on an **existing point** so a loop meets itself with no gap, or follow a **road centreline** rather than wherever the click hit the tarmac.
* **From timeline** -- rebuild a path from the cinematic sequence's own camera keyframes, one point per key. A shot you hand-authored, or generated and then dragged around in the curve editor, comes back into the tool and can be re-edited.

### Following a moving subject
Pin an object and the shot can track it while it plays -- a tram, a car, a citizen.

* **Aim at subject** -- the camera flies its keyframed course and just turns to hold the subject in frame.
* **Ride with subject** -- the whole shot travels along with it, so an orbit stays an orbit around a car that is driving away.

This is the one thing the mod cannot bake into keyframes: the timeline stores where the camera is at a moment in time, and has nowhere to put "wherever the tram is". So it is applied live during playback, which means it does nothing while the game is paused, and a saved shot replays without it in a later session.

### The timeline
Open it with **Ctrl+K**. It's a curve editor for the cinematic sequence, and it edits the game's own curves -- so anything you change here is there in photo mode's timeline too, and anything you build in photo mode opens here.

Drag keyframes, drag their **tangent handles** to shape the curve between them, and double-click a key to delete it. When dragging isn't precise enough, the **key inspector** nudges the selected key a frame or a fraction at a time.

**Play, stop, step a frame, jump to the next keyframe.** **Snap** locks the playhead and dragged keys to existing keyframes. A **work area** marks in and out points so you can zoom to the part you're working on, and **fit** brings the whole sequence back into view. **Undo and redo** cover every edit.

Save, load and delete whole sequences, with **Loop** and **Reset** in the header. These use the game's own sequence storage, so a sequence saved here shows up in photo mode's Save/Load list and vice versa. Sort the list by name or date, and each entry shows where it lives -- Steam Cloud, Paradox Mods, PDX, Xbox -- or a padlock if it can't be overwritten.

### Shots and the cut
Press **Shots** in the timeline header to slide out the shot list.

Generating a shot doesn't drop it onto the timeline -- it goes to a **Generated shots** list first, so trying an idea never costs you the cut you'd already assembled. **Drag a shot** onto the timeline to put it in the cut, drag it back out to remove it, drag within the cut to reorder, and drag it onto the delete area to throw it away. Both targets tell you what they'll do while you're dragging. The panel tracks how much you've made and how much is actually in the cut as separate totals.

**Double-click a name** to rename a shot. Press the **pencil** and that shot's settings load, the shot panel opens and the right editor for its type starts -- from the list back under your cursor in one press.

Click any shot and the **duration** and **key density** rows follow it. Change one and the timeline rebuilds as you go, so you can dial a shot's length watching its curves move rather than regenerating to find out.

### Timing
* **Per-keyframe easing** -- linear, smooth, ease in, ease out, ease in and out, set on any keyframe. Read back from the curve itself, so it works on shots you hand-authored as well as generated ones.
* **Constant speed** -- flatten every key at once for a perfectly even move.
* **Retime** -- stretch or compress a finished sequence without regenerating it.

### Light and weather
* **Time of day ranges** solved from your map's **real** sunrise, sunset, golden hour and twilight times, for its latitude and date. Pick "Golden hour" and you get the actual figures, not round numbers.
* **A key at every camera keyframe**, so you can drag the light's pacing around in the curve editor instead of placing keys by hand.
* **Linger at ends** -- a linear ramp in hours races through dawn and then sits on flat daylight, because almost all the visible change happens as the sun crosses the horizon. Turn this up and the shot spends its time where something is actually happening.
* **Weather is carried over** from your world instead of being reset when photo mode opens, so cloud and fog settings survive.

### Quality of life
* Sort your saved shots by name or by date.
* **Ctrl+H** hides the panels and the world overlays together, so you can judge a shot on the picture alone.
* Scrubbing outside photo mode shows the lens and the light, not just the camera move.
* Hide the cursor during playback.
* Orbit around a building by selecting it.
* Post-process quality no longer drops when photo mode opens -- vanilla quietly downgrades bloom, depth of field and motion blur on entry.

### Known issues
* Long shots at very high resolutions can play back unevenly; this is frame pacing rather than the camera path.
