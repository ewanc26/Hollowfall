<p align="center">
  <a href="https://github.com/ewanc26/VoxelGame/actions/workflows/ci.yml"><img src="https://github.com/ewanc26/VoxelGame/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/ewanc26/VoxelGame?label=licence" alt="AGPL-3.0"></a>
  <a href="https://github.com/sponsors/ewanc26"><img src="https://img.shields.io/github/sponsors/ewanc26?logo=githubsponsors&logoColor=white&label=sponsors" alt="Sponsor"></a>
</p>

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

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow and the checks required before opening a pull request. [AGENTS.md](AGENTS.md) carries the rules for AI coding agents.

## Support

If you find this project useful, consider supporting its development:

[![Ko-fi](https://img.shields.io/badge/Ko--fi-F16061?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/ewancroft)
[![GitHub Sponsors](https://img.shields.io/badge/GitHub%20Sponsors-30363D?style=for-the-badge&logo=github&logoColor=white)](https://github.com/sponsors/ewanc26)
