# Game loop

Hollowfall is a procedural voxel dungeon crawler built around a fast, aggressive combat loop in the
tradition of ULTRAKILL and DOOM (2016): you are never safe standing still, your health comes from hurting
things, and a style meter rewards you for doing it with flair. This page records what we learned from those
games, what we are taking from them, and how it maps onto this codebase. The numbers in the tables are
starting values to be playtested, and live in code (`Hollowfall.Core.Combat`) so tests can pin them.

## What the reference games do

Facts below come from the ULTRAKILL wiki and design write-ups listed under [Sources](#sources).

**Health comes from aggression, not from hiding.** ULTRAKILL has no health packs or regeneration. Hits at
close range heal you (a punch 10 to 30 depending on where it lands, a head hit with a hitscan gun 30, a
point-blank shotgun pellet 3), a melee gib heals 120, and a successful parry refills health completely. Taking
damage also creates "hard damage" (35% of the hit), a temporary ceiling on health that drains away after a
short delay, so being hit is costly but recoverable by playing well. DOOM (2016) arrives at the same place
by another route: enemies are walking health and ammo packs, staggered enemies can be finished with a glory
kill that showers health, and id cut anything that made the player retreat from the fight. They call it
push-forward combat.

**Style is a second, visible score that also feeds survival.** ULTRAKILL's meter has eight named ranks from
Destructive up to ULTRAKILL, each needing more points (200 for the first, 1500 for the last) and each draining
faster when idle (15 per second up to 120). Kills, double and triple kills, headshots, air shots, ground and air
slams, parries and trick shots all add points. Multipliers reward movement (up to 3x while sliding or in the
air) and weapon variety, since a weapon you keep using goes "stale" and earns less, down to nothing. Taking
damage drops you one rank. Higher style ranks also speed up hard damage recovery, which is why style and
health reinforce each other.

**Parry is the risk and reward move.** Attacks flash yellow when they can be parried and blue when they cannot.
A good parry refills health, restores stamina, redirects projectiles with more speed and damage, and earns
a large style bonus (100 points, 400 for sending an enemy's own attack back).

**Levels are arenas, scored three ways.** Each level gets a letter (D to S) for time, for kills and for style,
worth 1 to 5 points each. The final rank is the sum minus one point per checkpoint restart: 14 is S, and a P
rank needs S in all three with no restarts. Thresholds are set per level.

**Movement is the skill ceiling.** Sliding, dashing, wall-jumping and slamming keep you moving, and standing
still is dangerous. The game was designed around fast pace, constant action and player expression.

## What we take, and what we change

| Idea | In Hollowfall |
|---|---|
| Push forward | No regeneration, no pickups to retreat for. Health comes from blood. |
| Blood heals | Hits within about 4 blocks heal, more for headshots and limbs. Distance matters: standing back to snipe heals nothing. Melee finishers that heal a lot (`PlayerVitals.HealFromGib`) are in the rules but there is no melee attack yet. |
| Hard damage | 35% of each hit becomes a temporary cap on health that drains after a delay. |
| Style meter | Eight ranks with passive decay, bonuses, movement and freshness multipliers, damage knocks you down a rank. |
| Parry | Time-limited window, telegraphed attacks, full heal, style, projectile reflection. |
| Meteor | Leave a portal at 18+ blocks per second and, for 0.6 s, touching an enemy hits it for speed x 2.5 (max 120) and awards style (180, doubled after a 20+ block fall), at a 30% speed cost and no self-damage. Tuned in `MeteorTuning`. |
| Arenas | Dungeon rooms are arenas. Entering one with enemies locks its doors until it is cleared. |
| Ranking | Each dungeon floor is scored on time, kills and style, D to S plus a P rank, with a restart penalty. |
| Procedural | A seed gives the same dungeon, enemies and loot, so ranks are comparable and seeds can be shared. |

What is new here is that the arenas are generated: the room shape (rect, L, circle, cavern), size, pillars
and doors come from the seed, and room roles decide the encounter (standard fights, trap rooms with hazards,
treasure rooms with a guard, the boss room last). Because layouts differ, thresholds for time and kills are
computed from the floor itself (enemy count and path length) instead of being hand-tuned per level.

## The loop, step by step

1. **Enter a room.** If it holds enemies, the doors lock and the encounter starts.
2. **Fight.** Move constantly, hit things up close to heal, chain kills and tricks to climb the style rank,
   parry telegraphed attacks to recover.
3. **Clear.** Doors open, and the rank for the floor keeps counting. Chests heal you; locked ones (treasure and boss rooms)
   spend a key, and every floor hides one key per locked chest on it, in a standard or trap room away from the entrance.
4. **Descend.** The exit stairs are always open: you can skip a floor's fights, at the cost of its kills and style grades.
   The last floor ends when the boss dies.
5. **Score.** At the end of each floor you see time, kills and style ranks and the combined letter.
6. **Repeat on a new seed**, or retry the same one for a better rank. Your best run of each floor is recorded and replayed as a translucent ghost to race (see `docs/features.md` 4.3).

## Build order

1. **Rules in Core, tested first** (built): style meter, player vitals with blood and hard damage,
   parry, floor ranking. Engine-free, deterministic, no frame-rate dependence.
2. **Combat simulation in Core** (built): ray casting against voxels, weapons, enemies with simple AI, projectiles,
   encounters driven by the dungeon spawn data. All stepped with a fixed time step so a run can be replayed.
3. **Playable in Godot** (built): shooting and parrying input, enemy visuals, HUD (health with hard damage, style
   meter, ranks), locking doors, end-of-floor rank screen.
4. **Movement depth** (not built): slide, dash, air control, slam, wall jump.
5. **Portals** (built): linked pairs, momentum, portal-aware shooting and rendering.
6. **Content and feel** (partly built): three enemy types, a second weapon (the shotgun), synthesised sound, a boss (the Warden),
   chests, keys and traps, typed seeds you can share and personal bests per seed are in. Still to do: more enemy types, hit stop.

## Chest loot

There is no ammo, but there is now a bag. What is inside a chest is a pure function of the seed and the chest's position
(`LootTable.Roll`), never of the order you open things, so a seed always holds the same loot. Rarity follows the chest's tier for its
floor: ordinary chests are common, a treasure room's chest is rare and a boss's hoard epic, and rare and epic chests are the locked
ones that cost a key. Every chest but a key chest puts its loot in the bag; the run's ready item is the one consumable you chose to
bring, and new loot is used from the next run. A full bag leaves the loot behind.

| Loot | Becomes | Common | Rare | Epic |
|---|---|---|---|---|
| Health | Vial of blood, heals 30 | 1 | 2 | 3 |
| Cleanse | Cleansing drop, clears all hard damage | 1 | 1 | 1 |
| Style | Style shard, x2 parry points | 1 | 2 | 3 |
| Bloodlust | Bloodlust draught, x1.5 for 20 s | 1 | 2 | 3 |
| Open guard | Open guard tonic, +100 ms for 25 s | 1 | 2 | 3 |
| Spare key | stays a key, used on the spot | yes | never | never |

Weights are in `LootTable` (a locked chest never gives a key, since it cost one). Perks are timed, additive hooks: `Interactables.Perks`
feeds `PlayerVitals.HitHealScale` and the parry window, and does nothing when no perk is active. Opening a chest releases a floating pickup
model and the HUD banner names what went into the bag.

## Portals

Linked portals are an established puzzle-game idea (Valve's Portal series is the best known), and they change how the combat loop and the
dungeons play. Ours have their own look so they read as Hollowfall's: a chamfered, double-framed gate in teal and violet, not a
glowing blue and orange oval.

- **Traversal puzzles and shortcuts.** A dungeon floor is a graph of rooms, but a portal pair lets you ignore the graph:
  skip a locked arena's long way round, drop through a ceiling into the room below, or bring the exit to you.
- **Momentum.** Speed is conserved through a portal. Falling into a floor portal and leaving through a wall portal
  turns height into a horizontal launch, which is the movement skill expression we want alongside sliding and dashing.
- **Shooting through.** Hitscan shots, enemy projectiles and parried projectiles all continue out of the linked portal,
  so you can shoot an enemy that is behind you, or send a parried shot into a room you cannot see.
- **Limits.** Portals need a flat, solid, portalable surface (stone brick, stone, dirt, sand and similar: not bedrock,
  gates, secret doors, stairs, pillars, leaves, cactus, logs or snow) with the 1 x 2 opening clear in front. Locked arena doors are gates, so you
  cannot portal past them; a respawn removes your portals.

How it works: the frame of each portal is an axis-aligned right-handed basis (`Right x Up = Normal`). A point in front of
one maps to the mirror point behind the other and is turned half way round about the portal's up axis, so walking in
means walking out. The two blocks behind a portal are treated as open for the player, so the body can sink in until
its centre crosses the face, and then it is moved to the other portal with its velocity and facing turned to match
(at least a minimum exit speed, so you never stall inside). Rendering uses a second camera per portal placed where
the viewer would be if the pair were one doorway; portals are not drawn inside portal views, so there is one level of
see-through, not infinite recursion; a portal seen inside another portal's view appears as a glowing frame. The picture inside an opening
is the other camera's image, unaltered (same lens, size and anti-aliasing as the main view); the glow frame is drawn entirely outside the
opening, and the HUD fades out wherever it would overlap an opening. Enemies treat a
portal as a shortcut: if the player's reflection behind a portal's face is closer than the player, they walk into it and
come out beside you.

## Room links and gaps

Rooms are joined by a mix of link types (`LinkKind`, chosen per link from the seed, the floor and the rooms' places in the graph; the numbers are in `LinkMix`).

| Link | What it is |
|---|---|
| Corridor | The walkable 3-wide corridor with doors. Encounter doors lock and open as before. |
| PortalGap | Two entrance hallways facing across a pit that cannot be crossed on foot. Each is 3 wide, with a door cell at the room (so an encounter can seal it), a flat landing, three steps up and 0 to 2 flat cells to the lip, which stands 3 blocks above the pit floor. The pit is 6 to 16 cells long (6 to 8 for the first gap out of the entrance room) and floored with bare stone (a fall costs 8 health), spikes (20) or water (0, but a slow climb out). Fire a portal at the far hallway and another beside you, and step through. |
| ShaftGap | Rare, from floor 1: a hallway ending in a sealed 3-high block with a tunnel through its top, out of jump and step reach. Fire one portal up into the tunnel and one on the wall beside you. |
| SecretDoor | As before: an extra loop behind a secret door. |

**Guarantees.** Every room has at least one PortalGap, so an exit gap and an entrance hallway. A room with one way out leaves through a gap, and so do the entrance, the boss and the exit rooms. The first link out of the entrance room is a short bare-stone gap so the player learns the trick on an easy one. Gaps are cut into a straight stretch of the link's own corridor (into the facing edge of the rooms where the street between them is too short), so the room graph, depths and roles never change. If the rooms cannot all be given a gap, the floor is dug again with the same rooms (and only after 20 tries re-split).

**Mix.** Every room needs a gap, so even floor 0 is about half gaps (the fewest the rule allows); each deeper floor turns more of the remaining links into gaps (40 percent of them on floor 1, 60 percent from floor 2), and the pit hazards get commoner (bare stone is the rule on floor 0, spikes and water below).

**Proof.** `GapCrossing.Plan` finds a way over each gap with the real `PortalSystem.Place` and the real `PortalWalker` and body, and `DungeonReach` proves every room, the exit stairs and the boss are reached by walking plus those hops. The tests run both over seeds 1 to 30 and several regions.

**Falling in.** The sim never lets a pit trap you: it takes a little health (never the last point), then after a moment puts you back at the last safe grounded cell on your own side (`PlayerFell`, `PlayerRecovered`). Enemies stop at the lip. The first time you stand at a lip the game says how to cross, with your real portal bindings.
## Relationships

Phase 1 of the move toward an RPG. Not everything in the dungeon wants you dead. Full design, tables and constants: [relations.md](relations.md).

- **Factions.** The Hollow (Husks, Spitters, Wardens, Cù-sìth) is always an enemy. The Folk (Brownies, Selkies, Kelpies, the villagers of the surface villages), Wanderers (the Bean Nighe) and Vermin (hares) each have a standing with you, -100 to 100, in seven tiers (Hated, Hostile, Wary, Neutral, Friendly, Trusted, Revered) with hysteresis so the tier does not flicker, and a slow drift back to neutral.
- **Acts ripple.** Killing a Husk warms the Folk a little (they fear the Hollow); killing a brownie in front of Folk turns them against you and leaves the Hollow unmoved. Merely hurting someone never earns anyone's approval.
- **Dispositions.** Every creature is Passive, Skittish, Neutral, Territorial, Curious, Aggressive or Allied, worked out from its faction's regard for you, its nature, a seeded personality and what it remembers. Witnesses within 14 blocks with a clear view take a kill to heart, and those who panic tell the ones beside them. Provoked creatures warn before they strike (and attacks can be parried like any other); anger fades.
- **What it does to the loop.** The Hollow behaves exactly as before. Creatures are never part of an encounter, so a room holding only creatures does not lock and clearing counts only the monsters. Hurting a pacifist earns no style. A Bean Nighe near you is an omen: the next encounter room holds an extra wave. A Friendly brownie leaves a warm hearth once; a Selkie you have won over fights at your side.
- **Seen on screen.** The pause menu's **Standing** screen lists each faction with its tier and a bar and the latest changes; a toast announces a change of tier; a faint pip over a creature you have noticed says how to read it (green friendly, yellow wary, red hostile, blue allied). Standing is saved per seed in `relations.json`.

## Principles

- **Rules live in Core** as pure, fixed-step code. Godot scripts only translate input and draw.
- **Everything is testable without an engine.** If a rule cannot be unit tested, it is in the wrong place.
- **Numbers are data.** Tuning values sit in one place per system so playtesting changes constants, not logic.
- **No retreat tax.** If a mechanic makes stopping or hiding the best play, fix the mechanic.

## Sources

- [ULTRAKILL wiki: Ranking](https://ultrakill.wiki.gg/wiki/Ranking), [Health](https://ultrakill.wiki.gg/wiki/Health),
  [Parry](https://ultrakill.wiki.gg/wiki/Parry) and [Style](https://ultrakill.wiki.gg/wiki/Style)
- [ULTRAKILL on Wikipedia](https://en.wikipedia.org/wiki/Ultrakill) and the
  [Film Stories developer interview](https://filmstories.co.uk/gaming/ultrakill-improvisational-and-impulsive/)
- [How Doom's Push Forward Design Cured my Hoarder Syndrome](https://www.gamedeveloper.com/design/how-doom-s-push-forward-design-cured-my-hoarder-syndrome),
  [Cyber Demons: The AI of Doom 2016](https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-) and
  [the id Software GDC Q&A](https://gdconf.com/news/speaker-qa-kurt-loudy-jake-campbell-break-combat-philosophy-doom)
