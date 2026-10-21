# Unique features

This page designs the features that would make Hollowfall feel like its own game instead of a mix of its parents.
Ultrakill gave us the combat loop (blood heals, style decays, parry), Portal gave us linked openings, and roguelikes gave us
procedural floors. Each of those is well worn on its own. What nobody has is **all three on a voxel world that can be cut
open**, so every feature below is picked because it needs at least two of our systems at once.

> **Fold space. Dig down. Chain it.**

Three pillars, a few features each. Section 5 says what to build first, and section 6 lists the ideas I rejected.

## 1. Why these and not others

A feature earns a place if it passes three tests:

1. **Only here.** It needs portals *and* voxels *and* the style loop (or two of them). A feature that works in any shooter is
   content, not identity.
2. **Rules, not scripts.** It is a small rule the simulation already understands (a block change, a portal crossing, a style
   award), so it composes with everything else and can be unit tested in `Hollowfall.Core`.
3. **Deterministic.** The simulation is fixed-step and seed-driven, so a feature must not break replays (see feature 9).

What we already have to build on: `PortalSystem` (placement, `TryCross`, `TraceRay`, enemies using portals as shortcuts),
`CombatSim` (events, encounters that lock doors, parry, projectiles), `StyleMeter` (decaying rank, bonuses),
`VoxelWorld` (edits that persist, `WorldView.MarkDirty` re-meshing), `FloorLayout` and `DungeonSite` (pure, seeded), and
`BindingMap` (so any new verb is rebindable with a gamepad default).

## 2. Pillar one: Fold

Portals stop being a puzzle tool and become a combat and level-design language.

### 2.1 Sever

**Pitch.** Closing a portal on someone cuts them in half.

**Rules.** When a portal is replaced or removed (you place a new one, or a pact or boss removes it), any enemy whose body
overlaps the old opening (the 1 x 2 face, plus 0.3 blocks in front) takes 60 damage and is *severed*; severed enemies drop no
blood but award the **Sever** style bonus (250 points). Heavier enemies are staggered instead of killed. Placing a portal
never hurts the player. Wardens (the boss) are immune.

**Why it is ours.** It turns the act of re-placing a portal, which in other games is a free reset, into a decision with a
payoff, and it rewards luring enemies through your own gate (they already walk through to chase you).

**Build on.** `PortalSystem.Place` already replaces a portal; add a `PortalClosed` event and an overlap test against
`Enemy.Bounds` in `CombatSim`. **Status: built** (`Combat/Sever.cs`; `CombatSim.ClosePortal`/`ClosePortals`, the Close portals action, the `PortalClosed` and `EnemySevered` events). Heavy means more health than the cut does; those are staggered and left on at least 1 health. **Tests:** enemy inside opening takes damage, outside does not, bosses are immune, only on
replacement, style awarded once per enemy. **Effort:** small.

### 2.2 Meteor (momentum kills)

**Pitch.** Fall through a floor portal, leave a wall portal at 25 blocks per second, and you are the weapon.

**Rules.** For 0.6 seconds after leaving a portal, if your speed is at least 18 blocks per second, touching an enemy within
1.2 blocks hits it for `speed x 2.5` damage (capped at 120) and awards **Meteor** (180 points, doubled if you fell more than
20 blocks). You lose 30% of your speed on impact and take no damage. Hitting a wall ends the window.

**Why it is ours.** Momentum is already conserved through portals (`PortalWalker`), but nothing rewards it. This makes the
fling the best attack in the game and gives vertical rooms a reason to exist.

**Build on.** `PlayerBody` velocity, `Crossing` from `PortalWalker.Step`, a short timer in `CombatSim`. **Tests:** speed
thresholds, the window expiring, the damage formula, no self-damage, speed loss. **Effort:** small.

### 2.3 Rift plates (portals are earned)

**Pitch.** In deeper floors most stone is dead stone. Portals only stick to marked plates, so the generator is a puzzle setter.

**Rules.** From floor 2, walls become `Obsidian` (not portalable). The layout places 2 to 5 `RiftPlate` blocks per arena, always
at least one facing each other across the room and one on a high wall or ceiling. `BlockInfo.Portalable` becomes depth-aware:
floor 0 and 1 allow everything (so the mechanic is learned freely), later floors allow only plates. Plates survive carving
(feature 3.3).

**Why it is ours.** A procedural dungeon normally cannot guarantee a portal puzzle is solvable; here the generator places the
plates and a test proves the arena can be crossed using only them. It also makes portal placement a read-the-room skill.

**Build on.** `BlockInfo.Portalable`, `PortalSystem.Place`, `FloorLayout` pillars placement as a model for plate placement,
a `Navigation` check that treats a plate pair as an edge. **Tests:** every arena has a connected plate pair, plates are never
in a doorway, floors 0 and 1 are unrestricted. **Effort:** medium.

### 2.4 Rift doors (impossible geography)

**Pitch.** Some doors lead to a room that is nowhere near.

**Rules.** The generator may add **static portals**: a fixed pair, drawn in grey-gold, that links two rooms that are not
adjacent in the layout graph (a different corner of the floor, or the floor below). They cannot be removed, but a Sever
(2.1) works on them. Static portals count as links in the room graph, so reachability tests still hold, and the boss route
never requires one.

**Why it is ours.** Procedural dungeons are trees with loops. With rift doors the floor is a folded graph: the exit can be
twenty steps away on foot and two through a door, and enemy shortcuts (already implemented) mean fights spill through them.

**Build on.** `FloorLayout.Links` (add a `Rift` flag), `PortalSystem` (static pairs that `Clear()` does not remove).
**Tests:** layouts with rift links are still fully connected without them, links join distant rooms, replays stay
deterministic. **Effort:** medium.

### 2.4a Gaps (built)

The generated half of 2.4: rooms are joined by corridors, portal gaps (hallways across a pit) and rare shaft gaps, and every room is guaranteed a gap. See `docs/game-loop.md` (Room links and gaps), `LinkMix`, `GapCrossing` and `DungeonReach`.

### 2.5 The Warden steals your portals

**Pitch.** A boss that fights with the same tool you do.

**Rules.** In phase two the Warden places its own pair on the arena walls, and for a few seconds your portals are *disabled*
while his are live: his projectile volleys go through his pair and arrive from behind you. A parried projectile that goes
through one of his portals comes back out of the other, so the way to damage him is to reflect shots through his own gate.
Phase three: he swaps your two portals' positions on a timer.

**Why it is ours.** Boss fights usually add enemies or health. This one inverts the player's core verb and reuses the
reflect-and-redirect rules that already exist.

**Build on.** `PortalSystem` supports several pairs only if generalised to a list (small refactor), `CombatSim` projectile
crossing. **Tests:** projectile from the boss's gate reaches the player, a reflected shot through the boss's gate damages him,
your portals are disabled in the window and restored after. **Effort:** large (needs the boss to exist first).

### 2.6 Portal-aware sound and a readable pair

Small, and unusual enough to matter. Sound sources are heard from the closer of the direct path and the path through a portal,
using the same reflection test the enemy shortcut logic uses, so you can hear a room through a gate before you look. The two
portals also get a non-colour cue (teal frames get chevron notches, violet frames get diamond notches) so the pair is
readable without telling teal from violet.

## 3. Pillar two: Dig

The world is voxels, so the world should be breakable, in a few controlled ways.

### 3.1 Slam and Dive

**Pitch.** Break the floor and fall into the next arena.

**Rules.** A new action, *Slam* (default C on the keyboard and L3, the left stick click, on a gamepad), pressed while airborne, drives
you straight down at 30 blocks per second. On landing it shockwaves a 3.5 block radius for 30 damage and awards **Slam** (60)
or **Air Slam** (160 from over 8 blocks). Floors on arena rooms are made of `BrickFloor`; the first slam cracks the tile
(`CrackedFloor`), the second within 3 seconds breaks a round hole about three blocks across, cut in 10 cm cubes. Slam only breaks floor where there is a floor below it,
never over a stair shaft, and never on plates. Dropping through a hole skips the rest of the room above and awards **Hollowfall**
(150 points) if you land in another arena.

**Why it is ours.** It makes the dungeon's vertical stacking, which already exists (eight blocks per floor), a movement option
instead of a staircase, and it makes "push forward" literal.

**Build on.** `VoxelWorld.SetBlock`, `WorldView.MarkDirty`, `Navigation` for the landing check; `PlayerBody` needs a slam state.
Needs a new `GameAction`. **Tests:** crack then break timing, no break over shafts, hole size, enemies in the radius damaged,
falling through counts as a floor change. **Effort:** medium.

### 3.2 Block Mortar (a weapon that digs) (built)

**Pitch.** A third weapon that trades damage for terrain.

**Rules.** Slow arcing shell (1.4 second cooldown). On impact it digs a sphere of radius 2.5 out of the world in 10 cm cubes. Each 1 m block is a 10 x 10 x 10 grid of cubes, so craters have
partly dug walls; a block is only removed once every cube in it is gone. Only `Breakable` blocks are dug (brick, dirt, sand,
stone; never bedrock, plates, gates or door frames). It deals 40 at the centre falling to 10 at the edge. Dug holes are drawn
per cube, but collision still treats a block as solid until it is fully dug, so the player stands on a crater's rim.
Debris voxels are not built yet. It can open a wall to a secret,
drop a ceiling slab onto a group, or carve a line of sight for a portal.

**Why it is ours.** It makes the portal gun and the world talk: carve a wall, place a portal on the fresh surface (Rift plates
still apply, so on deep floors it also costs you the plate).

**Build on.** `RayCast` and `CombatSim` projectiles; a `Breakable` flag in `BlockInfo`. **Tests:** radius, protected blocks,
damage falloff, performance of re-meshing neighbouring chunks (cap blocks per shot at 90). **Effort:** medium.

### 3.3 Collapse (the signature set piece)

**Pitch.** Kill the floor boss and the floor falls out from under you.

**Rules.** When a floor's boss or last arena is cleared, the stair floor starts to give way in rings: every 0.4 seconds the next
ring of floor blocks out from the room centre disappears (6 rings, about 2.5 seconds). The player falls with the debris to the
next floor and lands in an arena that has *just woken up*: the first encounter starts at 0.5 seconds with no door-locking
telegraph. Floor ranking pauses the timer during the fall. A **Freefall** bonus (120) pays for every kill made while falling.

**Why it is ours.** It is the dungeon's namesake ("hollow", "fall") as a spectacle, uses voxel removal in a way no
mesh-based shooter can, and gives each floor a distinct ending.

**Build on.** `VoxelWorld` batch edits and `WorldView.MarkDirty` (needs batching, see risks), `CombatSim` floor transitions.
**Tests:** rings remove the expected blocks in order, the player ends on the next floor, no softlocks if the player stands on
the stairs, floor timer pauses. **Effort:** large.

## 4. Pillar three: Chain

Make style the currency of a run.

### 4.1 Pacts (conditional boons)

**Pitch.** Boons that only work while you are playing well.

**Rules.** After each floor you are offered three pacts, drawn from the seed and the floor index. Each is a **boon** and a
**burden**, and the boon's strength depends on your current style rank: at Rowdy it is half strength, at Savage and above it is
full, at Legendary it doubles. Examples:

| Pact | Boon | Burden |
|---|---|---|
| Bloodsworn | Parry heals 20 more and refunds the last portal cooldown | Hard damage share 35% to 50% |
| Hollow Heart | Blood heals 1.5x | Maximum health 100 to 70 |
| Open Hand | Portal exits give a 0.8 second speed boost | Revolver damage -25% |
| Gravedigger | Slam radius +1.5 and breaks floors in one hit | Style decays 20% faster |
| Twin Gate | A third portal slot (rotates in) | Portals cost 5 health to place below Brutal |

**Why it is ours.** Roguelike boons are usually flat. Tying them to the decaying style meter means the best build is the one you
can keep alive, and a pact you cannot sustain is a trap, which is an interesting thing for the player to read.

**Build on.** `StyleMeter.Rank`, `PlayerVitals` constants made into parameters, a `RunState` (pacts, floor). **Tests:** strength
scaling by rank, pact offers deterministic for a seed, burdens always apply, stacking limits. **Effort:** medium to large.

### 4.2 Room mutators

**Pitch.** Every arena has a rule.

**Rules.** A seeded affix is chosen per arena (none on the first floor): **Low Gravity** (gravity halved, jump doubled), **Dark**
(ambient light zero, your torch radius 8, enemies glow faintly), **Conveyor** (floor pushes at 3 blocks per second in one
direction), **Brittle** (floor tiles pre-cracked, so a single slam opens it), **Ricochet** (walls bounce projectiles, yours
included), **Gale** (a steady crosswind that bends projectiles). Style gets a 1.25 multiplier in mutated rooms.

**Why it is ours.** They multiply with portals (a conveyor into a portal is a launcher; low gravity makes meteors enormous) and
with digging (brittle plus slam is a free elevator), which is what keeps a small list from feeling small.

**Build on.** `RoomRole` assignment in `FloorLayout`, `PlayerBody` gravity as a parameter, `CombatSim` projectile update. **Tests:**
deterministic per seed, none on floor 0, every affix leaves the room crossable. **Effort:** medium.

### 4.3 Replays, ghosts and verified runs

**Pitch.** Every run is a short string, and any machine can re-play it.

**Rules.** The simulation is already fixed-step and seeded, and movement and combat do not read the clock or random state
except through the seed. A replay is the seed plus one compact record of input per tick (move vector, jump, look angles
quantised to 16 bits, and the action bits). Playing it back reproduces the run exactly, which gives:

- **Ghosts**: race your best floor as a translucent runner (and see where your portals were).
- **Daily descent**: seed from the date; the leaderboard is just the best rank.
- **Verified scores**: a submitted run is re-simulated headlessly with the same `CombatSim`, so ranks cannot be faked, and
  `Hollowfall.Core` already runs without an engine.
- **Sharing**: a seed and a replay string fit in a message.

**Why it is ours.** Determinism is the hidden asset of this codebase; almost no action game can offer verified replays.

**Status: recording, replay and ghosts are built** (`Hollowfall.Core.Replay`); verified scores, dailies and sharing are not.

- **What is recorded.** The simulation reads only a `PlayerView` and `PlayerActions` per 1/60 s tick, plus the host's calls to
  `Respawn()`, so that is what a run is: `ReplayRecorder.Capture` quantises each tick (feet and eye offset in 1/1024 block,
  look direction as three 16-bit components, the action and movement flags) and *returns the quantised values*, which the game
  feeds to `CombatSim`. A live run and its replay therefore see identical input. The decoded view uses only division and a
  square root, so it is exact on every machine.
- **File format** (`ReplayCodec`, `.hfr`): magic, format and sim version, seed text, dungeon region, floor, end tick, the
  result (grade, points, seconds), then runs of identical ticks stored as zig-zag varint deltas of only the fields that changed,
  and an FNV-1a checksum. Standing still costs nothing; sprinting and turning costs about 1 KB a second. Runs are capped at
  20 minutes (`RunReplay.MaxTicks`) and files at 2 MiB; a longer run stops recording and is not saved. The stored run is the
  prefix from tick 0 to the tick the floor was completed, because simulation state depends on all that came before.
- **Replaying and verifying.** `ReplayRunner.Play` feeds a replay to a fresh `CombatSim` and returns `StateHash` (vitals, style,
  floor progress, keys, portals, enemies and projectiles, rounded to 1e-4 so it is portable). `ReplayRunner.Verify` re-simulates
  a replay and checks it completes its floor on its last tick with the grade, points and time it claims, and refuses a replay
  recorded under a different `RunReplay.SimVersion`. **Bump `SimVersion` whenever a change to `CombatSim`, `StyleMeter` or
  `Interactables` alters a run's outcome**: older files still load (a ghost only needs the positions) but are no longer verifiable.
- **Ghosts.** `ReplayStore` keeps the best run per seed and floor (`replays/` next to `records.json`, at most 64 files, atomic
  writes, loaded defensively and bounded). The game shows it as a translucent Husk (`ReplayDirector`), started and paced by
  the time you have spent on that floor, hidden when it finishes or you complete the floor.
- **Tests.** Golden bytes for the format, the same seed and inputs giving the same final state twice and across parallel threads,
  live play equal to the replay of its decoded file, respawns replayed, fake grades rejected, fuzzed and truncated files never throwing.

**Not done yet.** Portal markers in the ghost, a replay string for sharing, a settings toggle for the ghost, and the daily
seed and leaderboard. Keep worker-thread meshing and chunk loading out of anything the sim reads (it already is).
**Effort for what is left:** small, and it is the best value on this page.

## 5. Recommended build order

The first three give the clearest taste of the identity for the least work, and each leaves the game better even if the rest
never happens.

| # | Feature | Pillar | Effort | Needs | Why this order |
|---|---|---|---|---|---|
| 1 | Sever (2.1) | Fold | Small | portals, enemies | Gives portal re-placement meaning in an afternoon. |
| 2 | Meteor (2.2) | Fold | Small | portals, physics | Makes the fling the best move. |
| 3 | Replays and ghosts (4.3) | Chain | Medium | determinism (done) | Unlocks verified scores, dailies and every later balance test. |
| 4 | Slam and Dive (3.1) | Dig | Medium | voxel edits, new action | Adds the vertical verb and the first real use of block edits in combat. |
| 5 | Room mutators (4.2) | Chain | Medium | physics parameters | Cheap variety that multiplies features 1 to 4. |
| 6 | Rift plates and rift doors (2.3, 2.4) | Fold | Medium | layout generator | Turns portals into generated puzzles. |
| 7 | Block Mortar (3.2) | Dig | Medium | projectiles, block flags | The weapon that links Fold and Dig. |
| 8 | Pacts (4.1) | Chain | Medium to large | run state | The meta layer, once there is enough to choose between. |
| 9 | Collapse (3.3) and the Warden's portals (2.5) | Dig, Fold | Large | boss, batch block edits | The two set pieces; build them last, on a stable base. |

Portal-aware sound and the non-colour cue (2.6) go in when audio and accessibility work start; they are small and independent.

## 6. Ideas I left out

- **Plain destructible everything** (Teardown style). It would cost chunk re-meshing, lighting and netcode-shaped complexity for a
  feature every voxel game has. Controlled digging (3.1 to 3.3) keeps the fun and the performance.
- **A grappling hook.** Great, but not ours; the portal fling covers the same space with our own twist.
- **Co-op.** The deterministic core would make it possible, but it multiplies everything else; revisit after verified replays.
- **Inventory, crafting and gear.** They dilute the push-forward loop, so chest loot (see the game loop) is used on the spot: a heal, a style burst or a short perk. Pacts are the only meta layer.
- **Voice or story.** A seeded name and a few lines per boss are enough; the game's identity is its rules.

## 7. Risks and open questions

- **Re-meshing cost.** Collapse and the mortar change many blocks at once. `WorldView.MarkDirty` handles single chunks; batching
  edits per frame and capping blocks per event (the mortar's 90) is needed before either ships.
- **Determinism and edits.** Replays (4.3) need block edits to be part of the simulated state, not only the cached chunks.
  Today edits live in `VoxelWorld` chunks; the replay player must apply the same edits in the same order, which it will if it
  drives the same `CombatSim`.
- **Single pair of portals.** Pacts (Twin Gate) and the Warden need more than one pair; `PortalSystem` should become a list of
  pairs before either is built.
- **Balance numbers.** Everything above is a first guess and lives in constants so playtesting changes numbers, not logic.
- **Bindings.** Slam needs a key (suggested: C on keyboard, left stick click on gamepad) and an entry in `GameAction`,
  `BindingMap.Defaults` and `ActionInfo`, which the input tests will insist on.
