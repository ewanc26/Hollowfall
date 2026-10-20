<p align="center">
  <a href="https://github.com/ewanc26/Hollowfall/actions/workflows/ci.yml"><img src="https://github.com/ewanc26/Hollowfall/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/ewanc26/Hollowfall?label=licence" alt="AGPL-3.0"></a>
  <a href="https://github.com/sponsors/ewanc26"><img src="https://img.shields.io/github/sponsors/ewanc26?logo=githubsponsors&logoColor=white&label=sponsors" alt="Sponsor"></a>
</p>

# Hollowfall

A voxel-based game written in C# on .NET 10.

## Status

Early, but playable. Seeded terrain and multi-floor dungeons generate, and you can fight through them in Godot with a
first-person controller: three enemy types and a boss, a revolver and a shotgun, parrying, a style meter, portals, chests,
keys and spike traps, synthesised sound, a minimap and personal bests per seed. The game is a procedural voxel dungeon crawler.

## Layout

- `src/Hollowfall.Core` — world data, terrain and dungeon generation, chunk meshing and player physics (no engine dependency).
- `src/Hollowfall.Cli` — prints a top-down map and block statistics for a seed.
- `game` — the Godot 4 (.NET) shell. Thin C# scripts that connect the engine to Core.
- `tests/Hollowfall.Tests` — xunit tests, including determinism checks.
- `tools/Hollowfall.AssetGen` — writes the committed vector glyphs, icons and the texture-atlas preview; a test fails if they drift from it.
- `docs` — the game loop, art style and planned features.

## Terrain

Terrain is a pure function of `(seed, x, z[, y])`, so chunks can be generated
in any order or on any thread and always agree at their borders. All
randomness comes from a SplitMix64 hash rather than `System.Random`, and noise
is seeded Perlin with fractal layering. Biomes are chosen from height,
temperature and humidity; caves come from two intersecting 3D noise fields.

## Playing

Open the Godot .NET editor on `game/`, or run it directly:

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot --path game
```

You spawn on the plaza of the nearest dungeon, facing its stair shaft.

The game starts at a title screen, and Esc (or Start on a gamepad) opens the pause menu. Every binding below can be
changed under Controls, for the keyboard and mouse and for a gamepad separately, and the options under Settings are saved
to `~/.ewanc26/hollowfall/settings.json`. Everything the game saves lives in that one folder: `settings.json`, `records.json`, `story.json`, and the best replays in `replays/`. `HOLLOWFALL_HOME` moves the folder, and a first run copies anything an earlier version saved in Godot's own user folder (the old files are left where they were).

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Look | Mouse | Right stick |
| Jump | Space | A |
| Revolver | Left click | RT |
| Shotgun | Right click | LT |
| Parry (hit it while an enemy flashes yellow) | V | X |
| Teal / violet portal | Q / E | LB / RB |
| Close portals (sever anything standing in them) | C | D-pad down |
| Respawn | R | Y |
| Open chests (a key for locked ones; each holds seeded loot) | F | Left stick click |
| Open chests (a key for locked ones), read notes | F | Left stick click |
| Free-fly (fly down) | G (Shift) | Right stick click (B) |
| Pause menu | Esc | Start |
| Debug overlay (fps, position, speed) | F3 | none |

Settings: mouse sensitivity, gamepad look speed, stick dead zone, field of view, interface size (and whether it scales with the window), master, effects and ambience volume, render distance, inverted Y for mouse and
gamepad, and controller prompts. Button prompts follow the controller you are holding (Xbox letters, PlayStation shapes, or
Nintendo letters, with the bottom and right buttons swapped), or you can pick a style.

Blocks wear 16 by 16 pixel textures drawn in code, tinted by each block's colour with baked ambient occlusion, and you carry a torch for the dungeons.
The HUD is kept clean: health, style, hints, a floor panel (floor, kills, time against par and keys held) and a minimap that fills in as you explore. The HUD fades out wherever it would cover a portal opening, and damage flashes are edge vignettes that leave the middle of the screen clear.
Entering a room with enemies locks its doors until they are dead. Hits at close range heal you, being hit
locks away part of your health for a while, and a style meter rewards kills, headshots, parries and movement.
Chests hold loot picked from the seed: blood, a style burst, a cleanse of hard damage, a short perk (more blood from hits, a longer parry window) or a spare key; treasure and boss chests are locked, spend a key and give better loot. Spike traps warn before they strike.
Pick a world by typing a seed (any text or number) on the title screen, or press Random; your best grade and time on each
floor of each seed is saved to `records.json` beside `settings.json`.
See [docs/game-loop.md](docs/game-loop.md) for the design of the loop and [docs/features.md](docs/features.md) for planned features.

**Portals.** Q and E fire the two ends of a linked teal and violet pair onto flat stone walls, floors and ceilings (a 1 x 2
opening, with room in front of it). Walk, fall or shoot into one and you come out of the other, keeping your
speed, so a fall into a floor portal becomes a launch out of a wall. Your shots and enemy projectiles fly through
too, including ones you parry. **Sever:** replacing a portal, or closing the pair with C, cuts in half anything standing
in the old opening (60 damage and 250 style; Wardens are immune).

**Meteor.** Leave a portal at 18 blocks per second or more and for 0.6 seconds you are the weapon: touching an enemy within
1.2 blocks hits it for speed x 2.5 damage (at most 120), awards the Meteor style bonus (180 points, doubled after a fall of
more than 20 blocks), costs you 30% of your speed and never hurts you. Hitting a wall ends the window. The numbers live in
`MeteorTuning`.

Environment variables: `HOLLOWFALL_SEED` picks the world and overrides the seed saved in settings (default `voxel`),
`HOLLOWFALL_SPAWN_FLOOR=1` starts inside that dungeon floor, `HOLLOWFALL_DEMO=portals` runs a scripted
portal setup for screenshots, `HOLLOWFALL_DEMO=sever` closes the pair on a husk, and `HOLLOWFALL_DEMO=notes|ending|captions` stage the story screens.
`HOLLOWFALL_SETTINGS`, `HOLLOWFALL_RECORDS` and `HOLLOWFALL_STORY` set where settings, personal bests (with the `replays/` folder) and read notes are
saved (the smoke test points them at temporary files), `HOLLOWFALL_SKIP_TITLE=1` skips the title screen,
`HOLLOWFALL_MENU=title|pause|controls|settings|journal` opens that menu screen straight away, and `HOLLOWFALL_SHOWCASE=enemies|poses|weapons|props|effects`
picks the page of the model showcase scene (see [docs/art-style.md](docs/art-style.md)).

## Language

The game is written entirely in C#. Godot is used as the engine shell only: no
GDScript, no GDExtension, and every script attached to a scene must be a `.cs`
file. `dotnet test` enforces this. Game logic lives in `Hollowfall.Core`, which has no
Godot dependency, so it stays unit-testable.

## Dungeons

Each 384-block region may hold one dungeon: 2 to 4 floors under a 96x96 footprint,
entered from a stone plaza on the surface by a spiral staircase. A floor is a BSP
layout of rect, L, circular and cavern rooms joined by corridors, with doors,
loops and secret doors, and rooms get roles by rule (entrance, exit, boss,
treasure, trap, safe). Voxelisation is a pure function of the seed and position, so
any chunk can be generated alone. Spawn points for monsters, a boss, loot, keys and traps are
produced as data, and the combat simulation turns them into encounters, chests, keys and spikes. Run `dotnet run --project src/Hollowfall.Cli -c Release -- <seed> 1 --dungeon --spawns`
to see a floor plan.

## Story

Under the hill a Hollow is falling in on itself. Seven Wardens went down to hold it shut with the fold-craft and left a red cord behind them; six are only names now, and the seventh, who took the craft, is the boss at the bottom. You are a Delver, following the cord. The story is told in the margins and never gets in the way of the fight:

- **Every seed names its dungeon** ("The Pale Vault"), shown on the title screen, and names the last Warden.
- **A first-play opening** (once per seed), and a **title card** ("I. The Antechamber") that fades in the first time you enter each floor.
- **Notes**: one to three scrolls lie against the walls of each floor, in rooms you can reach without a secret door. Walk up and press Open (F) to read one; Open or the menu button closes it. The notes on a floor are one voice and read in order, and none is used twice in a run. The **Journal** in the pause menu keeps what you have read on each seed, saved in `story.json`.
- A line when a floor is finished (one per grade), when the boss wakes, when you die, and an ending card when the last Warden falls.

All the words and every choice are in `Hollowfall.Core.Story`, a pure function of the seed. See [docs/story.md](docs/story.md) for the premise, characters, floor-by-floor beats and what is deliberately left open. `HOLLOWFALL_STORY` overrides where `story.json` is saved.

## Agent control (MCP)

Agents drive Godot through the off-the-shelf [Coding-Solo godot-mcp](https://github.com/Coding-Solo/godot-mcp)
server (MIT, Node 18+), registered in `.mcp.json` as `godot` and pinned to `@coding-solo/godot-mcp@0.1.1`.
It needs the Godot .NET editor; `GODOT_PATH` defaults to `/Applications/Godot_mono.app/Contents/MacOS/Godot`
and can be overridden from your environment. Claude Code asks you to approve project servers, and needs a
reload the first time. To add it by hand: `claude mcp add godot -e GODOT_PATH=/path/to/godot -- npx -y @coding-solo/godot-mcp@0.1.1`.

It can open the editor, run and stop the game, return its console output and errors, create and save
scenes, add nodes and read project info. Use it with `project path = game/`.

It does not know about this game's world. To inspect generated terrain and dungeons, use the CLI
(`dotnet run --project src/Hollowfall.Cli -c Release -- <seed> 1 --dungeon --spawns`, `--biomes`, `--stats`,
`--dungeon-stats`) and the unit tests. Running a project executes its code, so only point the server at
projects you trust, and approve its tools individually rather than auto-approving them.

## Building

Godot 4.7 (.NET build) is needed to run the game itself:

```bash
./run-smoke-test.sh   # builds the Godot project and runs the headless smoke test
```

The core library and its tests need only the .NET SDK:

```bash
dotnet build
dotnet test
dotnet run --project src/Hollowfall.Cli -c Release -- <seed> <chunk-radius> [--stats]
```

## Licence

AGPL-3.0. See [LICENSE](LICENSE).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow and the checks required before opening a pull request. [AGENTS.md](AGENTS.md) carries the rules for AI coding agents.

## Support

If you find this project useful, consider supporting its development:

[![Ko-fi](https://img.shields.io/badge/Ko--fi-F16061?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/ewancroft)
[![GitHub Sponsors](https://img.shields.io/badge/GitHub%20Sponsors-30363D?style=for-the-badge&logo=github&logoColor=white)](https://github.com/sponsors/ewanc26)
