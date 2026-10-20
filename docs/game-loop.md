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
| Blood heals | Hits within about 4 blocks heal, more for headshots and limbs, a lot for melee finishers. Distance matters: standing back to snipe heals nothing. |
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
3. **Clear.** Doors open, loot and the floor key become available, and the rank for the floor keeps counting.
4. **Descend.** The exit stairs on each floor need that floor's key. The last floor ends at the boss.
5. **Score.** At the end of each floor you see time, kills and style ranks and the combined letter.
6. **Repeat on a new seed**, or retry the same one for a better rank.

## Build order

1. **Rules in Core, tested first** (this branch): style meter, player vitals with blood and hard damage,
   parry, floor ranking. Engine-free, deterministic, no frame-rate dependence.
2. **Combat simulation in Core:** ray casting against voxels, weapons, enemies with simple AI, projectiles,
   encounters driven by the dungeon spawn data. All stepped with a fixed time step so a run can be replayed.
3. **Playable in Godot:** shooting and parrying input, enemy visuals, HUD (health with hard damage, style
   meter, ranks), locking doors, end-of-floor rank screen.
4. **Movement depth:** slide, dash, air control, slam, wall jump.
5. **Portals** (built): linked pairs, momentum, portal-aware shooting and rendering.
6. **Content and feel:** more enemy types, a second weapon, hit stop and sound, a boss, sharing seeds.

## Chest loot

There is no ammo and no inventory, so a chest pays off on the spot. What is inside is a pure function of the seed and the chest's
position (`LootTable.Roll`), never of the order you open things, so a seed always holds the same loot. Rarity follows the chest's tier
for its floor: ordinary chests are common, a treasure room's chest is rare and a boss's hoard epic, and rare and epic chests are the locked ones
that cost a key. Better chests roll from a table that favours perks and style, and every effect is stronger and lasts longer.

| Loot | Common | Rare | Epic |
|---|---|---|---|
| Vial of blood (health, up to the ceiling) | 30 | 60 | 90 |
| Cleansing drop (clears all hard damage) | yes | yes | yes |
| Style shard (parry-sized style points) | x2 | x4 | x8 |
| Bloodlust (blood from hits multiplied) | x1.5, 20 s | x2, 30 s | x3, 45 s |
| Open guard (longer parry window) | +100 ms, 25 s | +180 ms, 40 s | +280 ms, 60 s |
| Spare key | yes | never | never |

Weights are in `LootTable` (a locked chest never gives a key, since it cost one). Perks are timed, additive hooks: `Interactables.Perks`
feeds `PlayerVitals.HitHealScale` and the parry window, and does nothing when no perk is active. Opening a chest releases a floating pickup
model and the HUD banner names the loot.

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
  gates, stairs, pillars, leaves or snow) with the 1 x 2 opening clear in front. Locked arena doors are gates, so you
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
