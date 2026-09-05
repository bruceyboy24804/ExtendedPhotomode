# Changelog

All notable changes to Extended Photomode. The abbreviated version of each entry is what
appears on Paradox Mods; this file carries the detail behind it.

# 1.2.0

A fix release. Three of these were quietly broken rather than missing, including one that could
take the game down.

**Crashes while editing keyframes**

Dragging keys in the timeline could crash. The cause was addressing, not the drag.

Curves were written back by their *position* in the sequence's modifier list, and that list changes
length while you edit: deleting the last key on a curve removes the whole curve. Delete the only
Time of Day key -- double-clicking it does exactly that -- and every curve below it shifts up one.
From that point on, edits landed on the **wrong curve**, and the bottom one wrote off the end of the
list. The game range-checks neither.

Curves are now addressed by identity and resolved fresh at the moment of the write, and every edit
is bounds-checked. A stale edit is dropped instead of taking the game with it.

**Undo and redo now actually work**

They never have. The panel and the mod were listening on binding names that did not match, so both
buttons did nothing and always looked disabled -- silently, with no error on either side. The
history was recording correctly the whole time; there was simply no way to reach it.

**Box select stays in one graph**

Dragging a selection box selected the matching keys in *every* channel on the panel, so a box drawn
in Position also grabbed Rotation and Focus -- and since dragging a selection moves all of it, that
meant editing curves you could not see.

A box now selects within the graph you drew it in, on the channel that graph is editing, and it has
a real height: you can grab the top half of a curve instead of a full-height slice of time. Shift
still extends a selection.

**Removed**

- The orbit ring drawn over photo mode. It appeared for anyone who opened photo mode, whatever they
  were shooting, and the only way to turn it off was a button on a gameplay tool's toolbar you had
  to leave photo mode to reach. The ring you get while actually placing an orbit is unchanged.

# 1.1.0

A path tool release and an editing release: there was nowhere to *edit* a shot once generated. Adds a timeline and a shot list, in one panel.

**A timeline you can edit in**

**Ctrl+K** opens a curve editor for the cinematic sequence. It edits vanilla's curves directly, so anything you do here shows up in photo mode's timeline and vice versa.

- Drag keyframes and their **tangent handles**; double-click a key to delete it
- A **key inspector** nudges the selected key's time or value when dragging is not precise enough
- **Per-keyframe easing** — linear, smooth, ease in, out, both — read back from the tangents, so it works on hand-authored shots too
- **Constant speed** flattens every key at once
- Play, stop, step a frame, jump between keyframes, and **Snap** to existing keys
- A **work area** with in and out points, zoom-to and fit
- **Retime** the sequence a second at a time, rescaling every key
- **Undo and redo**
- **Ctrl+H** hides the panels and world overlays, to judge a shot on the picture alone

**Shots and the sequence are one panel**

Press **Shots** in the timeline header to slide out the shot list.

- Generated shots go to a **Generated shots** list, not straight onto the timeline, so experimenting no longer costs you the cut
- **Drag** a shot onto the timeline to add it, back to the list to remove it, within the cut to reorder, or onto the **delete area** to delete it — each target says what it will do
- **Double-click a name** to rename a shot
- The **pencil** loads a shot's settings, opens the shot panel and starts the right editor for its type
- **Shot duration** and key density now sit in the panel. Click a shot and they follow it; change one and the timeline rebuilds as you go.
- Separate totals for what you have made and what is actually in the cut
- **Assemble**, **Up**, **Down**, **Add current**, **Edit**, **Delete** and the cut dot are gone — dragging and the pencil replaced them

**Saving whole sequences**

Save, load and delete complete sequences through the game's own storage — one saved here appears in photo mode's Save/Load list and vice versa.

- Sort by name or date, sharing the order with the game's own save panel
- Each entry shows where it is kept — Steam Cloud, Paradox Mods, PDX, Xbox — or a padlock when it cannot be overwritten
- **Reset** now clears the shots as well as the curves, and **Loop** sits beside it

**Follow a moving subject**

Pin an object and the shot tracks it: **Aim at subject** turns to hold it in frame, **Ride with subject** moves the whole shot with it. Applied live rather than baked into keyframes, so it does nothing while paused and a saved shot replays without it.

**The path panel is the way in**

- **Ctrl+P** opens the panel, not the tool — press **Draw path** when you actually want to edit
- **New path** clears the current one without touching anything saved
- **Escape** stops drawing, then closes the panel; right-click deletes a point
- Every action has a cursor hint
- **Points** and **Curves** choose what a click acts on, so handles never fight the points beneath them

**Per-point properties**

A field for every point rather than one setting for the whole path: position, height, **pitch** and **sharp corner**, plus **dwell** (hold still here), **speed** (a weight on time — the path still runs its full duration), **look at** (aim at the pinned subject, blended between neighbours) and per-point **focal length** and **time of day**. Paths saved with 1.0.0 load unchanged.

**Shape and aim**

- **Terrain** — *Never below ground* lifts the shot only where it would clip; *Follow terrain* holds one altitude
- **Closed loop** joins the last point back to the first as a real curve segment, so the join is as smooth as any other
- **Snapping** — grid, angle, existing point, or road centreline
- **From timeline** rebuilds a path from the sequence's own camera keyframes
- **Look ahead** aims further down the path so bends read smoothly; **Ease** slows the move at both ends
- Orbit gains a separate **end height** for helixes, and **sweep ease**

**Fixes**

- Lens and light did nothing while scrubbing outside photo mode — the camera moved, focal length and time of day did not
- A shot dragged off the timeline onto the shot list usually stayed in the cut
- Changing a shot's duration or key density wrote the *next* shot's settings, not the one you were looking at
- Removing the last shot left the previous arrangement's curves behind
- Keyframes with no stored handle weight drew as straight lines, and nudging one could degenerate the move
- Toggling **Loop** flattened the yaw curve, stalling the camera once per keyframe
- Focal length clamped to an invented range instead of the lens's real 0.11–1466mm
- Cursor hints showed raw locale keys and collided with vanilla's tool rows

**Removed**

- The **Ctrl+L** shortcut — the **Shots** button does the same thing

# 1.0.0

First release. Early access — please report anything that misbehaves.

**Shot generators**

Pick a shot type from the Shot dropdown, then press Generate. Keys are written straight onto the vanilla cinematic timeline, so playback, scrubbing, saving and the curve editor all work as normal.

- Orbit — circle a subject, with a separate end radius for spiral moves that pull in or push out
- Dolly zoom — the camera travels while the lens counter-zooms, holding the subject the same size
- Drawn path — draw a route in the world with Ctrl+P, generate it with Ctrl+Shift+P

**Paths**

- Click to append, click a segment to insert, drag to move, PageUp/PageDown for height, Escape to delete, Ctrl+R to reverse
- Save, load, rename and delete named paths from the path library
- Aim mode: look along the path, hold a fixed heading, or track the pinned subject

**Timing**

- Per-keyframe easing — linear, smooth, ease in, ease out, ease in and out
- Constant speed to flatten every key at once
- Retime the whole sequence without regenerating it

**Environment**

- Time of day ranges filled from the map's real sunrise, sunset, golden hour and twilight times
- Optionally a time of day key at every camera keyframe, so the light can be re-paced by dragging
- Linger at ends, which spends more of the shot on sunrise and sunset instead of racing through them
- Weather carried over from the world instead of being reset on entry

**Quality of life**

- Sort saved shots by name or date
- Hide the cursor during playback
- Orbit around a selected building
- Post-process quality no longer downgrades on entering photo mode
