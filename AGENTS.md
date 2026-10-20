# AGENTS.md

Guidance for AI coding agents working in **VoxelGame**.

## Project overview

A voxel game in C# on .NET 10. `VoxelGame.Core` holds world data and seeded terrain generation, `VoxelGame.Cli` is a seed inspector, and `VoxelGame.Tests` is the xunit suite.

- Language: C#
- Default branch: main

## Working rules

- Inspect the README, manifests, CI workflows, and nearby code before editing.
- Preserve existing architecture, naming, formatting, and error-handling conventions.
- Use `dotnet build` and `dotnet test` for validation; never claim checks you did not run.
- Terrain generation must remain a pure function of the seed and coordinates: no `System.Random`, no shared mutable state, no dependence on generation order.
- Dungeon generation follows the same rule: `FloorLayout` and `DungeonSite.BlockAt` are pure functions of the seed and coordinates. Add new dungeon blocks at the end of `BlockType` so existing values never shift.
- Keep changes scoped and update tests or documentation when behavior changes.
- Use feature branches and pull requests.
- Treat generated files, credentials, deployment configuration, and release metadata as sensitive.
