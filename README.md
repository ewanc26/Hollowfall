# VoxelGame

A voxel-based game written in C# on .NET 10.

## Status

Early. The seeded terrain generator is done; there is no renderer, meshing or
player yet.

## Layout

- `src/VoxelGame.Core` — world data and terrain generation (no engine dependency).
- `src/VoxelGame.Cli` — prints a top-down map and block statistics for a seed.
- `tests/VoxelGame.Tests` — xunit tests, including determinism checks.

## Terrain

Terrain is a pure function of `(seed, x, z[, y])`, so chunks can be generated
in any order or on any thread and always agree at their borders. All
randomness comes from a SplitMix64 hash rather than `System.Random`, and noise
is seeded Perlin with fractal layering. Biomes are chosen from height,
temperature and humidity; caves come from two intersecting 3D noise fields.

## Building

```bash
dotnet build
dotnet test
dotnet run --project src/VoxelGame.Cli -c Release -- <seed> <chunk-radius> [--stats]
```

## Licence

AGPL-3.0. See [LICENSE](LICENSE).
