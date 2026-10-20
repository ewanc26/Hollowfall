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

## Props

Chests stand where loot spawns, keys turn in the air, spike traps fill trap rooms, and wall torches (`PropPlacer`, a pure function of the seed) light every room. Only the six torches nearest the player cast light.

## Checking the art

```bash
HOLLOWFALL_SHOWCASE=enemies godot --path game res://scenes/Showcase.tscn
```

Pages: `enemies`, `poses`, `weapons`, `props`.
