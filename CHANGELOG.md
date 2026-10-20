# Changelog

Everything that changes for someone using VoxelGame goes here, newest first, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) shape. Versions follow [semver](https://semver.org/); while the major version is 0, a breaking change bumps the minor. Anything that changes the world generated for a given seed counts as a breaking change.

## [Unreleased]

### Changed

- Dungeon entrances are only placed where the surface is at or below y=84 (`DungeonMap.MaxEntranceY`), so the entrance spiral never exceeds about 46 blocks. Dungeons are still in about a fifth of regions (17-22% across five seeds).

### Added

- `--dungeon-stats` CLI option reports dungeon density and entrance depth over 256 regions.
- Godot 4.7 (.NET) project shell in `game/`, referencing `VoxelGame.Core`, with a headless smoke test (`run-smoke-test.sh`).
- C#-only language policy for the whole repository, enforced by `LanguagePolicyTests` (no GDScript or GDExtension; scenes may only attach `.cs` scripts) and a pre-commit hook.
- Dungeons: `VoxelGame.Core.Dungeon` builds seeded multi-floor dungeons. Each floor is a BSP layout of rect, L, circular and cavern rooms joined by 3-wide corridors, with doors, open loops and secret doors, and rule-based room roles (entrance, exit, boss, treasure, trap, safe). Floors are stacked 8 blocks apart and joined by spiral stairs; floor 0 is entered from a paved plaza on the surface.
- `DungeonMap` places at most one dungeon per 384-block region on dry, solid ground. `TerrainGenerator` stamps it into chunks (pass `enableDungeons: false` to turn this off), so chunks inside a dungeon footprint differ from earlier worlds.
- Dungeon blocks: stone brick, brick floor, door, secret door, pillar and stairs.
- Deterministic spawn data (monsters, boss, loot, keys, traps) scaled by depth and room role.
- `--dungeon` (and `--spawns`) CLI options print a floor plan and statistics for the dungeon nearest the origin.
- Tall grass and flowers on grassy plains and forests (`BlockInfo.IsPlant`; plants are not solid). Worlds generated before this change differ.
- Rivers: noise zero-crossings carve channels down to just below sea level, fading out toward the sea and thinning in mountains. New `River` biome with a gravel bed. Worlds generated before this change differ.
- `--biomes` option on the CLI prints a coarse ASCII biome map.
- Coal, iron and gold ore veins from per-ore 3D noise, each limited to a depth band (`OreSpec`). Worlds generated before this change differ.
- Seeded terrain generator: Perlin/fBm heightmap, six biomes, caves, trees and cacti over 16×128×16 chunks.
- `VoxelGame.Cli`, a seed inspector that prints a top-down map, block counts and biome statistics.
- Determinism tests covering seed stability, generation order and chunk borders.
