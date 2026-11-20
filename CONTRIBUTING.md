# Contributing to Hollowfall

A procedural voxel dungeon crawler in C# on .NET 10 and Godot 4, built around fast first-person combat and linked portals. Blood heals, so you fight close; the dungeons are joined by gaps you cross by firing portals; and a small Scottish-folklore story runs underneath it all. Thanks for wanting to help!

## Layout

- `src/Hollowfall.Core`: world data, terrain and dungeon generation, chunk meshing, player physics, combat, portals, relationships, story, localisation, music and sound synthesis — **no engine dependency**.
- `src/Hollowfall.Cli`: prints a top-down map and block statistics for a seed.
- `game`: the Godot 4 (.NET) shell — thin C# scripts that connect the engine to Core.
- `tests/`: the test suite.

## Notes for this project

- **Core stays engine-agnostic**: no Godot types inside `Hollowfall.Core`. If a change needs the engine, it goes in `game/`, and Core exposes what the shell needs.
- Determinism matters: seeds generate terrain, dungeons, loot, and folk opinions. A change that makes the same seed produce different dungeons is a regression, not a feature.
- The combat triangle is deliberate — close hits heal, being hit locks health away, style rewards aggression — so balance changes should be argued in an issue first, with play evidence.
- It all runs offline: "no account needed to play" is a design commitment, not an oversight.
- The CLI map tool doubles as a fast visual regression harness; run it for generation changes.

## Before opening a PR

- Build passes and tests pass; you played the affected part.
- Conventional commits; focused PRs; describe what seed you tested with and what you saw.
- AI-assisted PRs welcome; stand behind every line and run the tests yourself.

## Licence

AGPL-3.0 (see [LICENSE](LICENSE)). Contributions accepted under the same terms.