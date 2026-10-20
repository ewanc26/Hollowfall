# Changelog

Everything that changes for someone using VoxelGame goes here, newest first, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) shape. Versions follow [semver](https://semver.org/); while the major version is 0, a breaking change bumps the minor. Anything that changes the world generated for a given seed counts as a breaking change.

## [Unreleased]

### Added

- Seeded terrain generator: Perlin/fBm heightmap, six biomes, caves, trees and cacti over 16×128×16 chunks.
- `VoxelGame.Cli`, a seed inspector that prints a top-down map, block counts and biome statistics.
- Determinism tests covering seed stability, generation order and chunk borders.
