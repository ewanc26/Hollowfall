# AGENTS.md

Guidance for AI coding agents working in **Hollowfall**.

## Project overview

A voxel dungeon crawler in C# on .NET 10. `Hollowfall.Core` holds world data, seeded terrain and dungeon generation, the combat and portal simulation, input rules and sound synthesis (no engine dependency), `game/` is the Godot shell, `Hollowfall.Cli` is a seed inspector, `tools/Hollowfall.AssetGen` writes the committed static assets (SVG glyphs, the block atlas PNG and the sound effect WAVs; `Hollowfall.Core.Assets.StaticAssets` lists them, and the game falls back to generating them only when a file is missing), and `Hollowfall.Tests` is the xunit suite.

- Language: C#
- Default branch: main

## Language policy: C# only

Godot is the shell; the game is C#. This is a hard rule, not a preference.

- All game code is C#. Do not write GDScript (`.gd`), GDExtension (`.gdextension`), or attach any non-`.cs` script to a scene, resource or autoload.
- Scenes (`.tscn`) and resources (`.tres`) are data and are fine, but every script they reference must be a `.cs` file.
- Keep game logic in `src/Hollowfall.Core`, which has no Godot dependency. Scripts under `game/scripts` are thin: they connect the engine to Core and nothing more.
- Godot node classes must be `public partial class` and live in the `Hollowfall.Game` namespace.
- `LanguagePolicyTests` fails `dotnet test` if any of this is broken, and a pre-commit hook rejects `.gd` files. If a task seems to call for GDScript, write the C# equivalent instead and say so.
- Verify the Godot side with `./run-smoke-test.sh` (needs the Godot .NET editor, found via `GODOT=`, `PATH`, `/Applications/Godot_mono.app` or the Flatpak). It boots the real scene headless, and points `HOLLOWFALL_SETTINGS` and `HOLLOWFALL_RECORDS` at temporary files so it never touches your saves.

## Input: actions, never keys

All player input goes through `GameAction` and `BindingMap` (`Hollowfall.Core.Input`) so every control can be remapped
and works on keyboard, mouse and gamepad.

- A new player ability gets a `GameAction`, a label in `ActionInfo`, and default bindings for both slots in `BindingMap.Defaults`. Game scripts ask `InputMapper.Pressed(GameAction.X)`; they never name a key, button or axis. `NoHardCodedInputTests` fails if they do, and if an action is never read.
- Escape is reserved for the pause menu. Required actions cannot be left unbound, and rebinding onto a used input swaps the two actions. These rules live in Core and are unit tested; keep new rules there too.
- Settings are clamped and loaded defensively (`GameSettings.FromJson` never throws), and saved atomically. Add new options to `GameSettings` with a range and a default, and to the Settings screen.
- Menus are code-built Godot controls that work with the keyboard, mouse and gamepad (focus, not hover). The game tree is paused while one is open.

## Agent control: use the Godot MCP

Agents manipulate the engine through the `godot` MCP server in `.mcp.json` (Coding-Solo godot-mcp, pinned).

- Point it at `game/`. Use it to run and stop the game, read its debug output, and edit scenes and nodes.
- It must never be used to introduce GDScript. Attach only `.cs` scripts; the language policy above still applies and `LanguagePolicyTests` will catch a violation.
- It cannot see this game's world. Check terrain and dungeons with the CLI (`--dungeon`, `--biomes`, `--stats`) and with unit tests in `tests/`, not by guessing from screenshots.
- Do not auto-approve its tools; running a project executes its code.
- Bump the pinned version in `.mcp.json` deliberately, after checking the server still starts and lists its tools.

## Saved data

All saved data lives in one folder, `~/.ewanc26/hollowfall` (`Hollowfall.Core.Storage.DataHome`; `HOLLOWFALL_HOME` moves it).

- A new saved file gets its path from `DataHome` (add a name and a path property there, and an optional per-file environment variable for tests). Never write to Godot's `user://` folder or any other place.
- Save atomically, load defensively (a damaged or hand-edited file must never stop the game), and keep sizes bounded.
- Tests and the smoke test point every file at a temporary path with the per-file variables, so they never touch the real folder.

## Working rules

- Inspect the README, manifests, CI workflows, and nearby code before editing.
- Preserve existing architecture, naming, formatting, and error-handling conventions.
- Use `dotnet build` and `dotnet test` for validation; never claim checks you did not run.
- Terrain generation must remain a pure function of the seed and coordinates: no `System.Random`, no shared mutable state, no dependence on generation order.
- Dungeon generation follows the same rule: `FloorLayout` and `DungeonSite.BlockAt` are pure functions of the seed and coordinates. Add new dungeon blocks at the end of `BlockType` so existing values never shift.
- Keep changes scoped and update tests or documentation when behavior changes.
- Use feature branches and pull requests.
- Treat generated files, credentials, deployment configuration, and release metadata as sensitive.
