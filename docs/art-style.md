# Art style: low poly

Everything in Hollowfall that is not a voxel is a **low-poly model built in code**: flat-shaded, chunky, readable at a distance. There are no model files; `src/Hollowfall.Core/Models` describes each model as boxes, prisms, wedges, gems and blobs, and `game/scripts/ModelFactory.cs` turns that into Godot meshes.

## Rules

- **Flat shading only.** Every face has one normal and one colour; no smoothing, no textures.
- **Convex primitives.** `Box`, `Prism` (cylinders, cones, pyramids by `sides` and `taper`), `Wedge`, `Gem` (octahedron) and `Blob` (icosahedron).
- **Triangle budgets** (tested): Warden 520, Husk and Spitter 460, any other model 260, the whole catalogue under 3000.
- **One shared palette** (`ModelCatalog.Palette`). Enemies are told apart by silhouette and hue: rust Husk, violet Spitter, steel and crimson Warden.
- **Glow is for meaning.** Eyes, cores, gems and flames are in the glow buffer, drawn unlit. While an enemy winds up an attack its glow flashes bright yellow, the cue to parry.
- **Hit boxes match the art.** An enemy's model is as tall and wide as its `EnemySpec`, and a head shot lands on the head.

## Rigging

A model is a set of named parts, each with a pivot at its joint. Geometry is positioned relative to the pivot, so rotating a part swings it about its joint.

| Model | Parts | Parent links |
| --- | --- | --- |
| Husk, Warden | `torso`, `head`, `arm_l`, `arm_r`, `leg_l`, `leg_r` | head and arms hang from the torso; legs from the model |
| Spitter | `body`, `mouth`, `tent_0` to `tent_3` | none |
| Revolver | `body`, `cylinder`, `hammer`, `gem` | none |
| Chest | `base`, `lid` (hinged at the back edge) | none |

Arms and legs have their pivot at the top and hang below it; feet touch y=0 at rest. A child follows its parent, so a leaning torso carries the head and arms. Poses are procedural (`EnemyView`): they move parts from the rest position the model defines, so there are no animation files and nothing to fall out of date. `ModelRigTests` checks the rules above, and the smoke test poses every enemy in the engine and checks the joints move and children follow.

## Environment textures

Blocks wear 16 by 16 pixel tiles drawn by `BlockTextures` (no image files), packed into one atlas (`docs/texture-atlas.png` is an enlarged preview; regenerate it with `dotnet run --project tools/Hollowfall.AssetGen`).

- **Patterns, not colours.** A tile is light and shade around 0.85 white. The block's palette colour and ambient occlusion tint it, so grass can change colour by biome without new art, and `Gain` brings the average back to the palette colour.
- **Chunky and low contrast.** Two scales of noise, a few specks and one clear idea per surface (mortar, strata, bark furrows, pebbles). They are meant to sit with the flat-shaded models.
- **Seamless.** Natural tiles use noise that wraps, so neighbours join; a test compares the step across the tile edge with the steps inside it.
- **Variation.** The mesher flips tiles by block position so repeats are harder to spot, and half-pixel insets stop filtering from reading a neighbouring tile.
- **Readability.** The dungeon's brick, flagstone, pillar, steps, gate and door are tested to look different from one another. A secret door shares the wall's tile exactly, so only a shot or a parry finds it.
- **Cutouts.** Only grass blades and flowers have see-through pixels, and they use an alpha-tested material.

To add a surface: add a `Tile`, paint it in `Paint`, map blocks to it in `TileFor`, give the block a palette colour, and the tests cover the rest.

## Props

Chests stand where loot spawns, keys turn in the air, spike traps fill trap rooms, and wall torches (`PropPlacer`, a pure function of the seed) light every room. Only the six torches nearest the player cast light.

Opening a chest releases a **loot pickup** that floats out of the lid, turns and shrinks away. Each kind has its own silhouette and glow so it reads before you read the banner: a red **vial** (health), a teal **drop** (cleanse), a four-pointed amber **star** (style), a red-orange **fang** (Bloodlust) and a violet **shield disc** (Open guard); a spare key is the key model. They are single-part models of at most 120 triangles (tested), well inside the 260 budget.

## Checking the art

```bash
HOLLOWFALL_SHOWCASE=enemies godot --path game res://scenes/Showcase.tscn
```

Pages: `enemies`, `poses`, `weapons`, `props`.
