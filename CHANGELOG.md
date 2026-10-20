# Changelog

Everything that changes for someone using Hollowfall goes here, newest first, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) shape. Versions follow [semver](https://semver.org/); while the major version is 0, a breaking change bumps the minor. Anything that changes the world generated for a given seed counts as a breaking change.

## [Unreleased]

### Changed

- Portals have their own identity: teal and violet chamfered double-frame gates with a diagonal shimmer, replacing the blue and orange ovals. The colours, actions (`PortalTeal`, `PortalViolet`), icons and text were renamed to match; saved bindings for the old action names fall back to the defaults.
- The game is now called Hollowfall. Namespaces, projects, the solution, the Godot project and the environment variables (`HOLLOWFALL_SEED`, `HOLLOWFALL_SPAWN_FLOOR`) were renamed from `VoxelGame`.
- Dungeon entrances are only placed where the surface is at or below y=84 (`DungeonMap.MaxEntranceY`), so the entrance spiral never exceeds about 46 blocks. Dungeons are still in about a fifth of regions (17-22% across five seeds).

### Fixed

- Edited chunks (such as a locked door) are no longer unloaded and silently regenerated; `VoxelWorld.Unload` keeps them unless forced.
- Portal cameras use a better near plane at angles, and portals seen inside other portals draw as frames.

### Added

- Assets: Chakra Petch (titles) and Rajdhani (menus and HUD), both SIL OFL, with their licences; a generated vector set (`tools/Hollowfall.AssetGen`) of controller, mouse and keyboard prompts, HUD icons, style-rank emblems, the app icon and the README banner; and `assets/CREDITS.md`. A test fails if the committed files drift from the generator.
- Controller prompts for every face button in three layouts: Xbox (A, B, X, Y), PlayStation (cross, circle, square, triangle) and Nintendo (labels swapped by position), with matching shoulders, triggers and menu buttons. Prompts follow the connected controller, or can be forced in Settings.
- The HUD and menus show glyphs and keycaps for each binding, a rank emblem, a heart for health and the two portal icons on the crosshair.
- Remappable controls for keyboard, mouse and gamepad. Core has `GameAction`, `BindingMap` (one keyboard/mouse and one gamepad binding per action, swap on conflict, Escape reserved for pause, required actions stay bound), clamped `GameSettings` with a defensive, atomically saved JSON store, and the dead-zone and look maths in `InputMath`. In Godot, `InputMapper` turns bindings into InputMap actions (physical keys, so WASD works on AZERTY).
- Gamepad play: left stick moves, right stick looks (radial dead zone, response curve, adjustable speed), triggers fire, shoulders place portals.
- Menus built in code and usable with keyboard, mouse or gamepad: a title screen, a pause menu, a Controls screen with click-to-rebind, and a Settings screen (sensitivity, gamepad look speed, dead zone, field of view, render distance, inverted Y). The game pauses while a menu is open, and hints on the HUD show the buttons for the device you are using.
- HUD polish: damage and parry flashes, hit markers, rank-coloured style meter, health number and an end-of-floor result panel.
- Enemies now use portals as shortcuts to reach you.
- Portals: Q and E place a linked blue and orange pair on flat portalable surfaces. You walk or fall through with your speed conserved (a fall into a floor portal launches you out of a wall), and shots, enemy projectiles and reflected projectiles pass through too. Core has `PortalSystem`, `PortalTransform` and `PortalWalker`; `PlayerBody` gained horizontal momentum and ground friction; the Godot view draws each portal with its own camera and a code-built shader.
- Combat in the game: left and right mouse fire the revolver and shotgun, V parries, doors lock during encounters, and a HUD shows health with hard damage, the style meter, recent bonuses, the floor and the portals. R respawns after dying, and each floor is graded when you go down.
- `HOLLOWFALL_DEMO=portals` runs a scripted portal setup for screenshots.
- Playable world in Godot: chunks stream in around you, are meshed on worker threads, and are drawn with a first-person controller (WASD, jump, mouse look, free-fly) and a HUD. The game spawns at the nearest dungeon entrance; `HOLLOWFALL_SEED` and `HOLLOWFALL_SPAWN_FLOOR` choose the world and a starting dungeon floor.
- `ChunkMesher`: face-culled chunk meshes across chunk borders, with per-vertex ambient occlusion, separate opaque, cutout and water buffers, and an engine-free output format. `VoxelWorld` is a thread-safe chunk cache over the generator.
- `PlayerBody` voxel physics (gravity, jumping, wall sliding, one-block auto-step) and `Navigation` path finding. Tests walk real dungeons from the surface plaza to the boss through real collision.
- The Godot smoke test now boots the real scene and checks that the world streams in and the player lands.
- `.mcp.json` registers the third-party Coding-Solo godot-mcp server (pinned to 0.1.1) so agents can run the game, read its output and edit scenes. `AGENTS.md` records how to use it without breaking the C#-only rule.
- `--dungeon-stats` CLI option reports dungeon density and entrance depth over 256 regions.
- Godot 4.7 (.NET) project shell in `game/`, referencing `Hollowfall.Core`, with a headless smoke test (`run-smoke-test.sh`).
- C#-only language policy for the whole repository, enforced by `LanguagePolicyTests` (no GDScript or GDExtension; scenes may only attach `.cs` scripts) and a pre-commit hook.
- Dungeons: `Hollowfall.Core.Dungeon` builds seeded multi-floor dungeons. Each floor is a BSP layout of rect, L, circular and cavern rooms joined by 3-wide corridors, with doors, open loops and secret doors, and rule-based room roles (entrance, exit, boss, treasure, trap, safe). Floors are stacked 8 blocks apart and joined by spiral stairs; floor 0 is entered from a paved plaza on the surface.
- `DungeonMap` places at most one dungeon per 384-block region on dry, solid ground. `TerrainGenerator` stamps it into chunks (pass `enableDungeons: false` to turn this off), so chunks inside a dungeon footprint differ from earlier worlds.
- Dungeon blocks: stone brick, brick floor, door, secret door, pillar and stairs.
- Deterministic spawn data (monsters, boss, loot, keys, traps) scaled by depth and room role.
- `--dungeon` (and `--spawns`) CLI options print a floor plan and statistics for the dungeon nearest the origin.
- Tall grass and flowers on grassy plains and forests (`BlockInfo.IsPlant`; plants are not solid). Worlds generated before this change differ.
- Rivers: noise zero-crossings carve channels down to just below sea level, fading out toward the sea and thinning in mountains. New `River` biome with a gravel bed. Worlds generated before this change differ.
- `--biomes` option on the CLI prints a coarse ASCII biome map.
- Coal, iron and gold ore veins from per-ore 3D noise, each limited to a depth band (`OreSpec`). Worlds generated before this change differ.
- Seeded terrain generator: Perlin/fBm heightmap, six biomes, caves, trees and cacti over 16×128×16 chunks.
- `Hollowfall.Cli`, a seed inspector that prints a top-down map, block counts and biome statistics.
- Determinism tests covering seed stability, generation order and chunk borders.
