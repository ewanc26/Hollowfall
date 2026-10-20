# AGENTS.md

Guidance for AI coding agents working in **VoxelGame**.

## Project overview

A voxel game in C# on .NET 10. `VoxelGame.Core` holds world data and seeded terrain generation, `VoxelGame.Cli` is a seed inspector, and `VoxelGame.Tests` is the xunit suite.

- Language: C#
- Default branch: main

## Language policy: C# only

Godot is the shell; the game is C#. This is a hard rule, not a preference.

- All game code is C#. Do not write GDScript (`.gd`), GDExtension (`.gdextension`), or attach any non-`.cs` script to a scene, resource or autoload.
- Scenes (`.tscn`) and resources (`.tres`) are data and are fine, but every script they reference must be a `.cs` file.
- Keep game logic in `src/VoxelGame.Core`, which has no Godot dependency. Scripts under `game/scripts` are thin: they connect the engine to Core and nothing more.
- Godot node classes must be `public partial class` and live in the `VoxelGame.Game` namespace.
- `LanguagePolicyTests` fails `dotnet test` if any of this is broken, and a pre-commit hook rejects `.gd` files. If a task seems to call for GDScript, write the C# equivalent instead and say so.
- Verify the Godot side with `./run-smoke-test.sh` (needs the Godot .NET editor, found via `GODOT=`, `PATH` or `/Applications/Godot_mono.app`).

## Working rules

- Inspect the README, manifests, CI workflows, and nearby code before editing.
- Preserve existing architecture, naming, formatting, and error-handling conventions.
- Use `dotnet build` and `dotnet test` for validation; never claim checks you did not run.
- Terrain generation must remain a pure function of the seed and coordinates: no `System.Random`, no shared mutable state, no dependence on generation order.
- Dungeon generation follows the same rule: `FloorLayout` and `DungeonSite.BlockAt` are pure functions of the seed and coordinates. Add new dungeon blocks at the end of `BlockType` so existing values never shift.
- Keep changes scoped and update tests or documentation when behavior changes.
- Use feature branches and pull requests.
- Treat generated files, credentials, deployment configuration, and release metadata as sensitive.
