# Extended Photomode

A Cities: Skylines II mod that extends the built-in photo mode and cinematic camera for people making
videos.

Vanilla's cinematic camera resets your weather when photo mode opens, can't shoot at fractional
simulation speed, and building an orbit means placing every keyframe by hand. This fills those gaps
and adds shot types you would otherwise have to fake.

Everything is written straight onto the **vanilla cinematic timeline**, so playback, scrubbing, the
game's own curve editor and saving a shot all keep working — this extends the tool you already know
rather than replacing it.

> **Early access.** Expect rough edges, and please report them.

## What it does

- **Shot generators** — orbits (including spirals and helixes), dolly zooms, and drawn camera paths
- **A curve editor** (`Ctrl+K`) over the cinematic sequence, with tangent handles, per-key easing,
  snapping, a work area, retiming, and undo/redo
- **A shot list** — generate shots into a staging list, then drag them into the cut
- **Path drawing** (`Ctrl+P`) with per-point dwell, speed, look-at, focal length and time of day,
  terrain modes, closed loops, snapping, and rebuild-from-timeline
- **Subject following** — aim at, or ride along with, a moving tram, car or citizen
- **Time of day ramps** solved from your map's real sunrise and sunset for its latitude and date
- **Weather carried over** into photo mode instead of being reset

The full feature list is on the [Paradox Mods page](https://mods.paradoxplaza.com/mods/156815/Windows).

## Installing

Subscribe on **[Paradox Mods](https://mods.paradoxplaza.com/mods/156815/Windows)**. It requires
[Unified Icon Library](https://mods.paradoxplaza.com/mods/74417/Windows), and targets game version
`1.6.*`.

## Building from source

You need the **Cities: Skylines II modding toolchain** installed, which sets the `CSII_TOOLPATH` user
environment variable. The build imports `Mod.props` and `Mod.targets` from there, so it will not
configure without it.

```bash
git clone --recurse-submodules https://github.com/bruceyboy24804/ExtendedPhotomode.git
cd ExtendedPhotomode
dotnet build
```

If you already cloned without `--recurse-submodules`:

```bash
git submodule update --init
```

`dotnet build` does everything — it compiles the mod, deploys it to your local mods folder, and runs
the UI's `npm run build` as part of the deploy.

**Close the game first.** The deploy wipes the mod folder before copying, and it cannot do that while
the game holds the mod DLL open. A build that fails there leaves the mod folder with no UI at all,
and the symptom is confusing: the game silently runs an old bundle, or none. For the same reason,
running `npm run build` on its own is pointless if a C# build follows — the deploy recreates the
folder and the build step writes the bundle back into it.

### Build configurations

| Configuration | What it's for |
|---|---|
| `Debug` | Development. Defines `IS_DEBUG` and `ENABLE_PROFILER`. |
| `Release` | Publishing. Defines `USE_BURST`. |
| `I18N` | Regenerates `L10n/lang/en-US.json` from the source strings. |

### Working on the UI

The frontend is TypeScript and React under `ExtendedPhotomode/UI/`. `npm run dev` watches and
rebuilds, which is useful once the mod folder already exists from a full build.

## Layout

```
ExtendedPhotomode/
├── Camera/        # Path, orbit and dolly solvers — the geometry, no ECS
├── Components/    # ECS components
├── Systems/       # ECS systems: shot generation, the sequence, UI bindings
├── Tools/         # The in-world path and shot editors
├── Patches/       # Harmony patches against vanilla photo mode
├── UI/            # TypeScript/React frontend
├── L10n/          # Localisation
└── Common/        # ModsCommon submodule — shared mod infrastructure
```

[`Common/`](https://github.com/bruceyboy24804/ModsCommon) is compiled *into* the mod assembly by
source inclusion rather than referenced as a separate DLL. Note that pushing there does not update
this repo — the pointer has to be moved and committed here as well.

## Changelog

See [CHANGELOG.md](CHANGELOG.md). The changelog shown on Paradox Mods is a shorter, player-facing
summary of the same releases.

## Feedback

Bug reports and feature requests are very welcome on
**[Discord](https://discord.gg/4f7geN26S)**, or as
[issues](https://github.com/bruceyboy24804/ExtendedPhotomode/issues) here.

A crash or a misbehaving panel is much easier to track down with your `Player.log`, which lives at:

```
%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II\Player.log
```
