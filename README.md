<p align="center">
  <a href="https://github.com/ewanc26/Hollowfall/actions/workflows/ci.yml"><img src="https://github.com/ewanc26/Hollowfall/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/ewanc26/Hollowfall?label=licence" alt="AGPL-3.0"></a>
  <a href="https://github.com/sponsors/ewanc26"><img src="https://img.shields.io/github/sponsors/ewanc26?logo=githubsponsors&logoColor=white&label=sponsors" alt="Sponsor"></a>
</p>

# Hollowfall

A procedural voxel dungeon crawler in C# on .NET 10 and Godot 4, built around fast first-person combat and linked portals. Blood heals, so you fight close; the dungeons are joined by gaps you cross by firing portals; and a small Scottish-folklore story runs underneath it all.

## Status

Playable, and still growing. Seeded terrain and multi-floor dungeons generate, and you can fight through them from first person:

- **Combat:** three enemy types and a boss, a revolver and a shotgun, parrying, a style meter and floor ranks. Close hits heal you, being hit locks away part of your health, and style rewards kills, headshots, parries and movement.
- **Portals:** a linked teal and violet pair that carries your speed (Meteor), cuts what stands in it (Sever), and is how you cross the gaps between rooms.
- **A living dungeon:** chests with seeded loot, keys, spike traps, fire pits, a minimap, and folk (brownies, selkies, kelpies, a Bean Nighe, hares) with their own opinion of you.
- **Story and flavour:** a short story told in notes and title cards, Scottish folklore and Gàidhlig, adaptive music and sound synthesised in code, textures drawn in code, and a few hidden things.
- **Replays:** every run is recorded, and your best run of a floor races you as a ghost.

## Layout

- `src/Hollowfall.Core`: world data, terrain and dungeon generation, chunk meshing, player physics, combat, portals, relationships, story, localisation, music and sound synthesis (no engine dependency).
- `src/Hollowfall.Cli`: prints a top-down map and block statistics for a seed.
- `game`: the Godot 4 (.NET) shell. Thin C# scripts that connect the engine to Core.
- `tests/Hollowfall.Tests`: xunit tests, including determinism, rigging and rule checks.
- `tools/Hollowfall.AssetGen`: writes the committed vector glyphs, icons and the texture-atlas preview; a test fails if they drift from it.
- `docs`: the design notes (see the list under [Documentation](#documentation)).

## Terrain

Terrain is a pure function of `(seed, x, z[, y])`, so chunks can be generated
in any order or on any thread and always agree at their borders. All
randomness comes from a SplitMix64 hash rather than `System.Random`, and noise
is seeded Perlin with fractal layering. Biomes are chosen from height,
temperature and humidity; caves come from two intersecting 3D noise fields.

## Playing

Open the Godot .NET editor on `game/`, or build and run it directly:

```bash
dotnet build game
/Applications/Godot_mono.app/Contents/MacOS/Godot --path game
```

You spawn on the plaza of the nearest dungeon, facing its stair shaft. The game starts at a title screen where you can type a seed (any text or a number) or press Random, then Play. Esc (or Start on a gamepad) opens the pause menu. Every binding below can be changed under Controls, for the keyboard and mouse and for a gamepad separately.

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
| Slam (drop straight down from the air; cracks, then breaks, brick floor) | X | D-pad up |
| Block mortar (arcing shell that digs a sphere of brick, earth and stone, in 10 cm cubes) | T | D-pad left |
| Open chests (a key for locked ones), read notes, speak to what you find | F | Left stick click |
| Respawn | R | Y |
| Free-fly (fly down) | G (Shift) | Right stick click (B) |
| Pause menu | Esc | Start |
| Debug overlay (fps, position, speed) | F3 | none |

### Settings

Language (English, Gàidhlig or Gàidhlig agus Beurla), mouse sensitivity, gamepad look speed, stick dead zone, field of view, interface size (and whether it scales with the window), master, effects, ambience and music volume, render distance, inverted Y for mouse and gamepad, and controller prompts. Button prompts follow the controller you are holding (Xbox letters, PlayStation shapes, or Nintendo letters with the bottom and right buttons swapped), or you can pick a style. The interface is laid out for 1280 by 720 and scales with the window, so it keeps its share of the screen on a small laptop, a 4K display or a retina one.

### Where things are saved

Everything the game saves lives in one folder, `~/.ewanc26/hollowfall`:

| File | What it holds |
|---|---|
| `settings.json` | Your options and bindings, and the seed you last chose |
| `records.json` | Your best grade and time on each floor of each seed |
| `story.json` | Notes you have read, and what you have said to those you have met |
| `relations.json` | Your standing with each group, per seed |
| `progression.json` | Your experience, level and stat points, across runs |
| `replays/` | The best run of each floor, for the ghost |

`HOLLOWFALL_HOME` moves the folder. A first run copies anything an earlier version saved in Godot's own user folder and leaves the old files where they were.

## How it plays

Entering a room with enemies locks its doors until they are dead. Hits at close range heal you, being hit locks away part of your health for a while, and a style meter rewards kills, headshots, parries and movement. See [docs/game-loop.md](docs/game-loop.md) for the design of the loop.

The HUD is kept clean: health, style, hints, a floor panel (floor, kills, time against par, keys held) and a minimap that fills in as you explore. It fades wherever it would cover a portal opening, and damage flashes are edge vignettes that leave the middle of the screen clear. Blocks wear 16 by 16 pixel textures drawn in code, tinted by each block's colour, and dungeon floors look different as you go deeper (plain, damp and mossy, then ashen).

**Portals.** Q and E fire the two ends of a linked teal and violet pair onto flat stone walls, floors and ceilings (a 1 x 2 opening, with room in front of it). Walk, fall or shoot into one and you come out of the other, keeping your speed, so a fall into a floor portal becomes a launch out of a wall. Your shots and enemy projectiles fly through too, including ones you parry. Standing still between two floor portals loops you like Portal's infinite fall; steer away to leave, or close the pair with C. **Sever:** replacing a portal, or closing the pair with C, cuts in half anything standing in the old opening (60 damage and 250 style; Wardens are immune).

**Meteor.** Leave a portal at 18 blocks per second or more and for 0.6 seconds you are the weapon: touching an enemy within 1.2 blocks hits it for speed x 2.5 damage (at most 120), awards the Meteor style bonus (180 points, doubled after a fall of more than 20 blocks), costs you 30% of your speed and never hurts you. Hitting a wall ends the window. The numbers live in `MeteorTuning`.

**Chests, keys and traps.** Chests hold loot picked from the seed: blood, a style burst, a cleanse of hard damage, a short perk (more blood from hits, a longer parry window) or a spare key. Treasure and boss chests are locked, spend a key and give better loot. Spike traps warn before they strike, and a jump clears them.

**Gaps.** Rooms are not all joined by corridors. Every room has at least one gap: two walled hallways with a door at the room, three steps up to a lip, and a pit you cannot cross on foot. Fire the teal portal at the far hallway, the violet one at the wall beside you, and step through. The pit is bare stone, spikes or water; a fall costs a little health (never the last point) and puts you back at the edge, so it never traps you. A few deeper links are shaft gaps: aim up into a tunnel above a sealed block. Corridors and secret doors remain, and the dungeon is always finishable (tests prove it with the real portal placement and walking physics).

**Folk and standing.** Not everything in the dungeon is hostile. Brownies, selkies, kelpies, a Bean Nighe and the odd hare share the dungeon with the Hollow's Husks, Spitters and Wardens, and each group has its own opinion of you. Hurt or kill one in front of its kin and they turn on you; treat them well and a brownie warms a hearth, a selkie fights at your side. The pause menu's **Standing** screen shows where you stand with each group (Hated to Revered), a toast says when it changes, and a small coloured pip over a creature you have noticed shows how to read it. See [docs/relations.md](docs/relations.md).

**Ghosts.** Every run is recorded as compact fixed-step inputs, and the best run of a floor is saved. On the same seed and floor it races you as a translucent ghost. Runs replay deterministically, so a stored run can be verified. See [docs/features.md](docs/features.md).

**Sound and music.** Every effect and all of the music are synthesised in code, so there are no audio files. The music is adaptive: layers fade in and out as you move between the surface and the dungeon, fight, meet the boss, run low on health or rest by a fire, and the key and mood follow the seed. Its volume defaults to 60 percent, and the Ambience slider scales the music's drone layer. See [docs/music.md](docs/music.md).

## Story

Under the hill a Hollow is falling in on itself. Seven Wardens went down to hold it shut with the fold-craft and left a red cord behind them; six are only names now, and the seventh, who took the craft, is the boss at the bottom. You are a Delver, following the cord. The story is told in the margins and never gets in the way of the fight:

- **Every seed names its dungeon** ("The Pale Vault"), shown on the title screen, and names the last Warden.
- **A first-play opening** (once per seed), and a **title card** ("I. The Antechamber") that fades in the first time you enter each floor.
- **Notes**: one to three scrolls lie against the walls of each floor, in rooms you can reach without a secret door. Walk up and press Open (F) to read one; Open or the menu button closes it. The notes on a floor are one voice and read in order, and none is used twice in a run. The **Journal** in the pause menu keeps what you have read on each seed.
- A line when a floor is finished (one per grade), when the boss wakes, when you die, and an ending card when the last Warden falls.

The flavour is Scottish: the folk are drawn from Scottish folklore, dungeon names borrow Scots and Gàidhlig words, and floors and notes nod to the Fortingall Yew, the Cailleach and the water-horse. All the words and every choice are in `Hollowfall.Core.Story`, a pure function of the seed. See [docs/story.md](docs/story.md) for the premise, characters, floor-by-floor beats and what is deliberately left open, and [docs/mythology.md](docs/mythology.md) for the folklore and its sources.

## Interface language (Gàidhlig)

Settings has a **Language** row at the top: `English`, `Gàidhlig` (Scottish Gaelic) or `Gàidhlig agus Beurla` (Gàidhlig with the English beside it). Changing it rebuilds every screen at once and is saved with your other settings. In Gàidhlig the title screen also shows a Gàidhlig name for the dungeon under its English one. Only Gàidhlig that could be checked against a source is shown; everything else stays in English, and **no native speaker has reviewed any of it yet**. [docs/gaidhlig.md](docs/gaidhlig.md) lists every string with its status and source, what is not translated, and a request for a Gaelic speaker to review it.

## Hidden things

Somewhere out on the hills, a wolf stands holding a branch of yew. He is a dedication to the game's solo developer, and he is gentle. Go and say hello, and ask him a few things: he remembers what you chose.

Fire pits burn in some of the dungeon's rooms, warm and harmless, and some of them hold a surprise.

## Dungeons

Each 384-block region may hold one dungeon: 2 to 4 floors under a 96x96 footprint, entered from a stone plaza on the surface by a spiral staircase. A floor is a BSP layout of rect, L, circular and cavern rooms joined by a mix of corridors, portal gaps and secret doors, with loops, and rooms get roles by rule (entrance, exit, boss, treasure, trap, safe). Voxelisation is a pure function of the seed and position, so any chunk can be generated alone. Spawn points for monsters, a boss, loot, keys, traps and the folk are produced as data, and the combat simulation turns them into encounters, chests, keys and spikes. Run `dotnet run --project src/Hollowfall.Cli -c Release -- <seed> 1 --dungeon --spawns` to see a floor plan.

## Language

The game is written entirely in C#. Godot is used as the engine shell only: no GDScript, no GDExtension, and every script attached to a scene must be a `.cs` file. `dotnet test` enforces this. Game logic lives in `Hollowfall.Core`, which has no Godot dependency, so it stays unit-testable.

## Environment variables

For screenshots, tests and debugging:

| Variable | What it does |
|---|---|
| `HOLLOWFALL_SEED` | Picks the world and overrides the seed saved in settings (default `voxel`) |
| `HOLLOWFALL_HOME` | Moves the whole save folder |
| `HOLLOWFALL_SETTINGS`, `HOLLOWFALL_RECORDS`, `HOLLOWFALL_STORY`, `HOLLOWFALL_RELATIONS` | Move one saved file (the smoke test points them at temporary files) |
| `HOLLOWFALL_SPAWN_FLOOR=1` | Starts you inside that dungeon floor |
| `HOLLOWFALL_SKIP_TITLE=1` | Skips the title screen |
| `HOLLOWFALL_MENU=title\|pause\|controls\|settings\|journal\|standing` | Opens that menu screen straight away |
| `HOLLOWFALL_DEMO=portals\|sever\|gap\|folk\|notes\|ending\|captions` | Runs a scripted setup for screenshots |
| `HOLLOWFALL_SHOWCASE=enemies\|poses\|weapons\|props\|effects` | Picks the page of the model showcase scene (see [docs/art-style.md](docs/art-style.md)) |
| `HOLLOWFALL_DUST=0` | Turns off the dungeon dust motes, for comparison shots |

## Documentation

- [docs/game-loop.md](docs/game-loop.md): the design of the combat loop, the style meter, ranks and the Relationships and Gaps sections.
- [docs/features.md](docs/features.md): the planned and built signature features.
- [docs/art-style.md](docs/art-style.md): the low-poly rules, rigging, textures, themes, sky and effects.
- [docs/story.md](docs/story.md), [docs/mythology.md](docs/mythology.md): the story and the folklore it borrows.
- [docs/relations.md](docs/relations.md): factions, standing, dispositions and the creatures.
- [docs/music.md](docs/music.md): the adaptive music, layer by layer.
- [docs/gaidhlig.md](docs/gaidhlig.md): the Gàidhlig strings, sources and review status.

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
