# Changelog

All notable changes to Extended Photomode. The abbreviated version of each entry is what
appears on Paradox Mods; this file carries the detail behind it.

# 1.2.0

Fixes, the path tool's first in-world controls, and a simpler way in. Three of the fixes were
quietly broken rather than missing, including one that could take the game down.

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

**Keys are spaced by distance, not by curve parameter**

`SamplePositions` stepped the node-chain parameter uniformly, which spaces samples evenly in `t` and
therefore unevenly in metres — a bezier covers far less ground per unit of `t` through a bend than
along a straight. The key count was right and the distribution was wrong, which is the hard kind of
wrong to see: the path looked sampled, and the camera changed speed for no visible reason.

It now walks the curve by arc length with `MathUtils.ClampLength`, which advances a segment until it
has covered a given distance and reports what it had left when it ran out, so the remainder carries
into the next segment.

**The path tool's in-world controls**

Values that existed only as panel rows are now grips in the world, each on a line whose length is the
value: key spacing, the bend weighting, a point's height, its lens, and an orbit's radius. Keyframes
are drawn on the path as ticks, so the spacing controls have something visible to act on.

Three different projections, chosen by what the axis means. A horizontal axis is measured on the
ground — the cursor's ground point moves left when you drag left, from any camera. A vertical one is
measured on screen, because no ground point can express a height and closest-approach against the
mouse ray is near-singular when you look level at an upright line. A grip whose direction carries no
meaning follows the cursor freely, the way the timeline's easing handles do.

Height also picks up `ElevationUp`/`ElevationDown` from `ToolBaseSystem`, so the game's own elevation
input and the tool UI's elevation buttons drive it, and it snaps to the next multiple of the step the
way `NetToolSystem` does rather than adding to whatever odd height you were on.

**The way in, reordered**

A user put it plainly: pressing Ctrl+P said "make a new orbit", and only after starting one did the
choice of path or dolly appear, at which point every label on the panel renamed itself. The shot type
is now a dropdown at the top of the panel, before anything that acts on one — the first decision is
the first control. It is the game's own `Dropdown`, not an HTML `<select>`, which takes the whole
cohtml UI down. A button in the top-left toolbar opens the panel, beside the other mods' buttons, so
Ctrl+P no longer has to be known in advance.

`Generate shot` hid a real fork. It added the shot to the generated list — off-screen unless the shot
list was open — so pressing it appeared to do nothing. It is now two named buttons, **To timeline
editor** and **To cinematic camera**, and each shows a tick when it fires. The first also opens the
timeline window and slides out the shot list — both, because the list is a pane inside the window —
so the shot arrives somewhere you are looking rather than somewhere you have to go and find.

**Simple mode**

Off by default: the tool options keep to subject and duration, and the mod decides everything it
hides — never below ground, lift over buildings, aim at the subject if there is one, curvature-weighted
keys, level pitch, a drone rig, tracked focus, centred framing. Turn on **Advanced** (in the panel or
the options menu) and every row appears and its own value is used.

Progressive disclosure rather than two modes: one panel with most of it hidden, so nothing can drift
between a simple UI and an advanced one. The decisions live in a single `Effective` table that both
the tool's preview and the generator read, because a default that lived in one and not the other
would draw a shot that does not match the one it generates. The hidden settings keep their stored
values; switching to advanced reveals them unchanged.

**Under the hood**

- Tool values are declared once, in `ToolParameters`. The forty-one hand-written setter cases each
  carried their own clamp, so a range existed in three places free to disagree — which is how a drag
  reaches a value you then cannot type. Bounds now come from one table.
- The panel's option rows are generated from `[EnumOption]` attributes on the C# enums by a small
  Roslyn tool (`Tools/ExtendedPhotomode.Codegen`). Nine hand-written tables mirroring nine enums are
  gone; add a member and the row appears, renumber one and the icons follow.
- `MathUtils` is used where it should have been: `Tangent` for exact curve direction, `Curvature` for
  the bend weighting, `Distance` against a bezier for picking, `Cut` for the drag guide,
  `SmoothDamp` for grip steadiness, `RotationAngleSignedRight` for orbit bearings. `Bezier4x3.xz`
  replaces a hand-built flattened curve.
- Key spacing can tighten through bends (`CurvatureBias`), floored at a quarter step so a hairpin
  cannot ask for unbounded keys. Off in advanced mode unless set; simple mode turns it fully on.
- The height keys record an undo step and move the whole selection, matching the flat drag.

**Fixes behind the panel**

- `PhotoModePropertyBase` recorded a section for every property it registered, but `AddProperty`
  silently drops a duplicate id. A duplicate therefore left a section predicate with no widget behind
  it, and every predicate after it applied to the wrong row — which is one way a panel ends up with
  most of its sections missing.
- Insert picking tested the drawn samples rather than the curve, so the cursor had to come within
  range of a *sample* rather than of the path. It now uses `MathUtils.Distance` against the bezier.

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
