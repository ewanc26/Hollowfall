<p align="center">
  <a href="https://github.com/ewanc26/VoxelGame/actions/workflows/ci.yml"><img src="https://github.com/ewanc26/VoxelGame/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/ewanc26/VoxelGame?label=licence" alt="AGPL-3.0"></a>
  <a href="https://github.com/sponsors/ewanc26"><img src="https://img.shields.io/github/sponsors/ewanc26?logo=githubsponsors&logoColor=white&label=sponsors" alt="Sponsor"></a>
</p>

# VoxelGame

A voxel-based game written in C# on .NET 10.

## Status

Early. Seeded terrain and dungeon generation work; there is no renderer, meshing
or player yet. The game is a procedural voxel dungeon crawler.

## Layout

- `src/VoxelGame.Core` — world data, terrain and dungeon generation (no engine dependency).
- `src/VoxelGame.Cli` — prints a top-down map and block statistics for a seed.
- `game` — the Godot 4 (.NET) shell. Thin C# scripts that connect the engine to Core.
- `tests/VoxelGame.Tests` — xunit tests, including determinism checks.

## Terrain

Terrain is a pure function of `(seed, x, z[, y])`, so chunks can be generated
in any order or on any thread and always agree at their borders. All
randomness comes from a SplitMix64 hash rather than `System.Random`, and noise
is seeded Perlin with fractal layering. Biomes are chosen from height,
temperature and humidity; caves come from two intersecting 3D noise fields.

## Language

The game is written entirely in C#. Godot is used as the engine shell only: no
GDScript, no GDExtension, and every script attached to a scene must be a `.cs`
file. `dotnet test` enforces this. Game logic lives in `VoxelGame.Core`, which has no
Godot dependency, so it stays unit-testable.

## Dungeons

Each 384-block region may hold one dungeon: 2 to 4 floors under a 96x96 footprint,
entered from a stone plaza on the surface by a spiral staircase. A floor is a BSP
layout of rect, L, circular and cavern rooms joined by corridors, with doors,
loops and secret doors, and rooms get roles by rule (entrance, exit, boss,
treasure, trap, safe). Voxelisation is a pure function of the seed and position, so
any chunk can be generated alone. Spawn points for monsters, loot and keys are
produced as data. Run `dotnet run --project src/VoxelGame.Cli -c Release -- <seed> 1 --dungeon --spawns`
to see a floor plan.

## Building

Godot 4.7 (.NET build) is needed to run the game itself:

```bash
./run-smoke-test.sh   # builds the Godot project and runs the headless smoke test
```

The core library and its tests need only the .NET SDK:

```bash
dotnet build
dotnet test
dotnet run --project src/VoxelGame.Cli -c Release -- <seed> <chunk-radius> [--stats]
```

## Licence

AGPL-3.0. See [LICENSE](LICENSE).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow and the checks required before opening a pull request. [AGENTS.md](AGENTS.md) carries the rules for AI coding agents.

## Support

If you find this project useful, consider supporting its development:

[![Ko-fi](https://img.shields.io/badge/Ko--fi-F16061?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/ewancroft)
[![GitHub Sponsors](https://img.shields.io/badge/GitHub%20Sponsors-30363D?style=for-the-badge&logo=github&logoColor=white)](https://github.com/sponsors/ewanc26)
