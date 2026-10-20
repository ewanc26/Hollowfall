# Changelog

Everything that changes for someone using Hollowfall goes here, newest first, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) shape. Versions follow [semver](https://semver.org/); while the major version is 0, a breaking change bumps the minor. Anything that changes the world generated for a given seed counts as a breaking change.

## [Unreleased]

### Added

- Chests hold real loot, rolled from the seed (`LootTable`): the same dungeon always holds the same loot. Ordinary chests are common; locked chests (treasure and boss rooms) roll a better table with stronger, longer effects. Outcomes are a vial of blood (health), a cleansing drop (clears hard damage), a style shard (a burst of style), Bloodlust (hits heal more for a while), Open guard (a longer parry window for a while) and, from ordinary chests only, a spare key.
- Opening a chest releases a floating pickup (five new low-poly models: vial, drop, star, fang and shield) and the banner names the loot. `ChestOpened` now carries the `Loot` and what it did.
- `PlayerVitals.Cleanse` and `HitHealScale`, `Interactables.Perks`, and `CombatSim.ParryWindowLeft`: small hooks the perks use. The props showcase page shows the new pickups.

### Fixed

- Chest lids now swing open in the game (the props view never registered its chests, so the lid animation never ran).

### Changed

- Chests no longer always heal: the flat 30 (60 locked) health is now one possible outcome, at the same amounts for common and rare chests.

- Free-fly moved to G so that F can open chests. Saved bindings keep working.
- Portals have their own identity: teal and violet chamfered double-frame gates with a diagonal shimmer, replacing the blue and orange ovals. The colours, actions (`PortalTeal`, `PortalViolet`), icons and text were renamed to match; saved bindings for the old action names fall back to the defaults.
- The game is now called Hollowfall. Namespaces, projects, the solution, the Godot project and the environment variables (`HOLLOWFALL_SEED`, `HOLLOWFALL_SPAWN_FLOOR`) were renamed from `VoxelGame`.
- Dungeon entrances are only placed where the surface is at or below y=84 (`DungeonMap.MaxEntranceY`), so the entrance spiral never exceeds about 46 blocks. Dungeons are still in about a fifth of regions (17-22% across five seeds).

### Fixed

- Edited chunks (such as a locked door) are no longer unloaded and silently regenerated; `VoxelWorld.Unload` keeps them unless forced.
- Portal cameras use a better near plane at angles, and portals seen inside other portals draw as frames.

### Added

- Meteor (momentum kills, `MeteorTuning`, `MeteorTracker`): for 0.6 seconds after leaving a portal at 18 blocks per second or more, touching an enemy within 1.2 blocks hits it for speed x 2.5 damage (capped at 120) and awards the Meteor style bonus (180 points, doubled after a fall of more than 20 blocks). The impact costs 30% of your speed (the host applies `MeteorImpact.SpeedKept`), never damages you, and a wall ends the window. A 0.4 second cooldown and a 2 second per-enemy lockout stop a looping portal pair from farming one enemy. It has its own sound (`SoundId.Meteor`) and a METEOR banner. `PlayerView` gained optional `Velocity` and `Crossed` fields for it.
- Sever (docs/features.md 2.1): closing a portal on an enemy cuts it in half. Replacing a portal (placing a new one of the same colour) or removing it (the new Close portals action, `C` / Back, or `CombatSim.ClosePortal` for pacts and bosses) hits every enemy overlapping the old 1 x 2 opening plus 0.3 blocks in front for 60 damage. Kills drop no blood and earn the Sever style bonus (250 points, once per enemy); heavy enemies are staggered instead of killed, Wardens are immune and the player is never hurt. New `PortalClosed` and `EnemySevered` events, a cutting-arc and snap-shut effect, shards and a synthesised sound.
- Environment textures, drawn by code (`BlockTextures`): a 16 by 16 pixel tile for every surface (stone, dirt, grass top and side, sand, sandstone, snow, water, gravel pebbles, bark and log rings, leaves, cactus ribs, ore flecks, grass blades, flowers, dungeon bricks, flagstones, fluted pillars, steps, iron gates and doors), packed into one atlas. Tiles are light-and-shade patterns tinted by each block's palette colour and ambient occlusion, wrap at their edges, and are flipped by block position to hide repeats. A secret door uses exactly the wall's tile. `docs/texture-atlas.png` is a preview, written by `tools/Hollowfall.AssetGen`.
- A minimap under the floor panel (`FloorMap`): it fills in by line of sight as you explore, secret doors look like wall until found, and the exit stairs show once seen.
- Choose a world: the title screen has a seed box (any text or a number) and a Random button that makes names like `amber-hollow-914`; the seed is saved in settings, and the pause menu has a Main menu button.
- Personal bests (`RunRecords`): each floor's best grade, points and time per seed is saved to `records.json`, and the floor-complete banner says whether it is a new best.
- Chests, keys and spike traps now work (`Interactables`). Walk over a key to take it; press Open (F, or left stick click) beside a chest to open it for health; chests in treasure and boss rooms are locked and spend a key (one per floor). Spike traps warn, then strike on their own beat for 22 damage, and a jump clears them. The floor panel shows keys held, and chests and spikes animate.
- Sound, with no audio files: every effect and the dungeon drone is synthesised in Core (`Synth`), so it is identical everywhere and unit tested (bounded, click-free, deterministic, a seamless loop). Combat events map to sounds in one tested place (`SoundMap`), world sounds play from where they happen, and menus tick and select. Settings gained master, effects and ambience volume.
- Low-poly art for everything that is not a voxel: a code-built model system (`Hollowfall.Core.Models`) with flat-shaded primitives, named pivoted parts with parent links, glow buffers and anchors; models for the three enemies, the enemy orb, the revolver and shotgun, chests, keys, spike traps and torches. See `docs/art-style.md`.
- Enemies are procedurally animated from their state (limb swing, windup poses that flash yellow, stagger), first-person weapon models with kick, muzzle flash and portal-colour gems, and dungeon props including deterministic wall torches (`PropPlacer`) with flickering lights near the player.
- A model showcase scene (`HOLLOWFALL_SHOWCASE=enemies|poses|weapons|props`) and tests for triangle budgets, hit boxes, mirroring and rigging.
- Portal polish: the inside of a portal is now exactly the other camera's image with nothing laid over it (no tint, shimmer or inner lines), the glowing frame sits outside the opening, new portals open with a short grow animation, and the portal camera matches the main view's lens, size and anti-aliasing. The HUD fades out wherever it would cover a portal opening, and damage and parry flashes are edge vignettes that keep the centre clear.
- HUD refinements: a floor panel (floor, kills, time against par), a debug overlay that is off until F3 is pressed, display-font banners, and the wordmark logo on the title screen.
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
