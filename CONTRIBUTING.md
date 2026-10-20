# Contributing to Hollowfall

## Project context

- Primary language: C# (.NET 10)
- Default branch: main

## Before submitting changes

- Branch from `main` as `<type>/<slug>`, and commit as `type(scope): description`.
- All game code is C#. No GDScript or GDExtension: `dotnet test` fails if a `.gd` file or a non-C# script reference appears.
- Run `./run-smoke-test.sh` when you change anything under `game/` (it needs the Godot .NET editor).
- Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes`.
- Add a line under `[Unreleased]` in `CHANGELOG.md` for anything a player or developer would notice, and update the README or `AGENTS.md` if behaviour changes.
- Merging is a rebase merge, so each commit has to build and pass on its own.
- Terrain output must stay deterministic for a given seed. If a change alters generated worlds, say so in the pull request.
- Keep commits focused and explain compatibility or operational impact.
- Open changes through a pull request with verification results.
