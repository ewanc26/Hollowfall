# Relationships

Hollowfall is growing from a combat game into an RPG. Phase 1 is a relationship system: who the things in the dungeon are, what they
think of the player, and how that changes what they do. Everything here is in `Hollowfall.Core.Relations`: pure, engine-free, driven by
the seed and tested. `CombatSim` has an optional `Relations` object; without one the dungeon behaves exactly as it did before.

The Scottish folklore behind the Folk (brownies, selkies, kelpies, the Bean Nighe) and the wanderers is summarised under
[The Folk](#the-folk) below; `docs/story.md` has the story it sits beside.

## The pieces

| Piece | File | Job |
|---|---|---|
| Factions and the graph | `Factions.cs` | Who is who, how each faction regards each other, and the ripple table |
| Reputation | `Reputation.cs` | The player's standing with each faction, tiers, hysteresis, decay |
| Saving | `RelationsStore.cs` | `relations.json` in the data folder (`DataHome.RelationsPath`, env `HOLLOWFALL_RELATIONS`) |
| Character | `Progression/Progression.cs`, `ProgressionStore.cs` | `progression.json` in the data folder (`DataHome.ProgressionPath`, env `HOLLOWFALL_PROGRESSION`). Phase 2 (see below) |
| Species and personality | `Species.cs` | What each kind of creature is by nature; bravery, grudge and curiosity drawn from the seed |
| Memory | `Memory.cs` | Anger, fear and gratitude of one individual, and how they fade |
| Disposition and brain | `Behaviour.cs` | Standing + temperament + personality + memory become a disposition, then an intent; `Mind` bundles one creature |
| The run's rules | `RelationSystem.cs` | Turns hurting, killing and giving into standing, memory and spreading news |

## Factions

The Delver is the player. Every other faction has a standing with the player. The Hollow's three factions are **fixed**: their
stance never changes whatever the standing says, so the existing monsters stay exactly as aggressive as they were.

| Faction | Group | Stance toward the player | Fixed | Starts at |
|---|---|---|---|---|
| Husks, Spitters, Wardens | Hollow | Enemy | yes | -90 (Hated) |
| Brownies | Folk | Neutral | no | 0 |
| Selkies | Folk | Neutral | no | 0 |
| Kelpies | Folk | Hostile | no | -50 (Hostile) |
| Wanderers (the Wulver, the Bean Nighe) | Wanderers | Neutral | no | 0 |
| Vermin (hares) | Vermin | Neutral | no | 0 |

### How the factions regard each other

Row looks at column. A = Ally, F = Friendly, N = Neutral, W = Wary, H = Hostile, E = Enemy. Every faction is its own Ally, Ally is
always mutual, and no pair disagrees by more than three steps (tests check all of it).

| | Delver | Husks | Spitters | Wardens | Brownies | Selkies | Kelpies | Wanderers | Vermin |
|---|---|---|---|---|---|---|---|---|---|
| Delver | A | E | E | E | N | N | H | N | N |
| Husks, Spitters, Wardens | E | A | A | A | H | H | N | W | N |
| Brownies | N | E | E | E | A | F | W | N | F |
| Selkies | N | E | E | E | F | A | W | F | N |
| Kelpies | H | N | N | N | W | W | A | N | W |
| Wanderers | N | H | H | H | N | N | W | A | N |
| Vermin | N | W | W | W | N | N | W | N | A |

The matrix decides how a witness feels about a victim (see below) and who an allied creature will help against. The Folk fear the Hollow
and the Hollow hunts them.

### Ripples

Acting on a faction moves several standings at once: the size of the act times a weight. A negative weight on a negative act is a gain.

| You kill or hurt a... | Moves (weight x act) |
|---|---|
| Husk | Husks 1, Spitters 0.4, Wardens 0.4, **Brownies -0.25, Selkies -0.25** (the Folk are glad), Wanderers -0.1 |
| Spitter | Spitters 1, Husks 0.4, Wardens 0.4, Brownies -0.25, Selkies -0.25, Wanderers -0.1 |
| Warden | Wardens 1, Husks 0.5, Spitters 0.5, Brownies -0.4, Selkies -0.4, Wanderers -0.2 |
| Brownie | Brownies 1, **Selkies 0.8**, Kelpies 0.2, Wanderers 0.15 (the Hollow: nothing) |
| Selkie | Selkies 1, Brownies 0.8, Kelpies 0.2, Wanderers 0.15 |
| Kelpie | Kelpies 1, Brownies -0.3, Selkies -0.3, Wanderers -0.1 |
| Wanderer (Bean Nighe) | Wanderers 1, **Brownies 0.2, Selkies 0.2**, Vermin 0.1 |
| Vermin | Vermin 1, Wanderers 0.1, Brownies 0.05 |

| You help (heal, give) a... | Moves |
|---|---|
| Brownie | Brownies 1, Selkies 0.3, Wanderers 0.1 |
| Selkie | Selkies 1, Brownies 0.3, Wanderers 0.1 |
| Kelpie | Kelpies 1 |
| Wanderer | Wanderers 1, Brownies 0.1, Selkies 0.1 |
| Vermin | Vermin 1, Wanderers 0.1 |

Rules: the act's own faction always moves the right way; merely hurting someone never earns anyone's approval; gains that arrive
second-hand (not by helping that faction directly) stop at the ripple ceiling of 40, so nobody becomes your devoted friend by
you slaughtering Husks; everything is clamped to -100..100.

## Reputation

Standing runs -100 to 100 in seven tiers. A standing must clear a boundary by 4 points to enter a tier and fall 4 below it to leave
one (hysteresis), so a number hovering at the edge does not flicker. Standings drift toward 0 at 0.02 points per second, except
the Hated tier (grudges last) and the fixed Hollow. A run starts from each faction's starting standing; `Reputation.Reset` restores
that. `relations.json` records where each seed's run ended (at most 50 seeds); carrying that into a new run is not switched on yet.

| Tier | From | Meaning to a creature |
|---|---|---|
| Hated | -100 | Enemy: attacks on sight if it can |
| Hostile | -75 | Hostile: guards ground, brave ones attack |
| Wary | -40 | Wary: skittish things flee sooner, guards guard |
| Neutral | -10 | Its own nature |
| Friendly | 15 | Friendly: gentle, curious ones follow |
| Trusted | 50 | Friendly, and boons are earned |
| Revered | 80 | Ally: those that can fight at your side do |

## Dispositions

Each individual has a **disposition** computed from its faction's stance (the player's tier, or the Hollow's fixed Enemy), its species'
natural temperament, its personality and its memory:

| Disposition | Behaviour |
|---|---|
| Passive | Flees, ignores, never attacks |
| Skittish | Flees when hurt, frightened, or approached fast |
| Neutral | Ignores the player until provoked, then fights until it forgets |
| Territorial | Attacks inside its own radius only, after a warning |
| Curious | Follows at a distance |
| Aggressive | Attacks on sight |
| Allied | Fights enemies of the player; never attacks the player |

Precedence: Hollow (always Aggressive) first; then memory (provoked: pacifists flee, others fight unless cowardly; frightened: flee);
then the stance table (a Revered player makes a Selkie Allied; a Hated one makes a Neutral thing Aggressive; Wary and Hostile make gentle
things Skittish and guards Territorial).

### Personality

Bravery, grudge and curiosity, 0 to 1, drawn from the seed and the creature's spawn position within ranges its species sets (a Hare is
never brave, a Kelpie is never timid), so two Selkies differ and a seed always makes the same ones. Bravery shortens warnings and
flee radii and decides whether an angry creature fights or runs; grudge sets how long anger lasts (15 to 240 seconds); curiosity
decides who follows.

### Memory and news

Each creature remembers anger, fear and gratitude (0 to 1), which fade at rates set by its personality. Being hurt raises anger and
fear. When the player hurts or kills something, every creature within 14 blocks with a line of sight to the victim witnesses it: kin
(the same faction or an Ally of it) grow angry, anyone who is not the victim's enemy grows afraid (less so if brave). Those who took real alarm
call out, and creatures within 8 blocks of them hear a weaker echo with no line of sight needed (one hop). Gifts raise gratitude, which
warms a stance by a step.

### Escalation

Anger over 0.3 provokes a creature (`EntityProvoked`): it **warns** (it stops and squares up for 0.5 to 1.4 seconds, shorter if brave)
and then **attacks**; anger over 0.7 skips the warning. When anger fades below 0.1 it calms (`EntityCalmed`) and goes back to its own
nature. Attacks reuse the combat rules: a windup that can be parried.

## The Folk

Notes on the folklore the creatures lean on; the games takes the flavour, not the scholarship.

| Creature | Role | Behaviour in Hollowfall |
|---|---|---|
| Brownie | Passive household helper. In Scottish tradition he does the work of the house at night and is repaid with a bowl of cream, never with clothes. | Shy and pacifist. When the Brownies are Friendly or better, pressing Interact beside one gives a boon (a healing and a clean slate of hard damage), once per brownie. |
| Selkie | Neutral, skittish seal-woman of Orkney and Shetland tales. | Lives by cistern-like round rooms. Flees if you rush it; at Trusted she follows and at Revered she fights at your side. |
| Kelpie | Water-horse that lures travellers to the water. | Round "cistern" rooms. Stands calm at its pool until you come within its radius, then warns and lunges: territorial. Dangerous, and parryable. |
| Bean Nighe | The washerwoman, an omen of death, seen washing the clothes of those about to die. | Passive wanderer. While you are within 12 blocks of her, the next encounter room you enter spawns an extra wave: she is warning you. Kill her and the Folk's standing plunges (she is a Wanderer; Brownies and Selkies -4 each). |
| Hare | Vermin, wildlife. | Skittish and harmless. Makes rooms feel lived in. |

## Spawning

`SpawnKind.Creature` is appended to the end of the spawn plan, after every existing spawn, using its own random stream, so the existing
monsters, loot, keys and traps are exactly where they were. `Spawn.Variant` holds the `EnemyKind`. Placement by room:

- **Safe and Entrance rooms**: a Brownie (60%), a Hare (35%).
- **Standard rooms**: a Hare (25%); a Brownie (8%).
- **Round rooms ("cisterns"), Standard or Safe**: a Kelpie (45%) or a Selkie (35%). The view lays a shallow pool on the floor.
- **One Bean Nighe per floor** (50%, from floor 1 down) in a Standard room away from the entrance.

Creatures are spawned when the player first reaches their floor and only when the relationship system is on. They are never part of an
encounter: a room holding only passive creatures does not lock, and clearing an encounter counts only the aggressive monsters.

## Tuning constants

| Constant | Value | Where |
|---|---|---|
| Kill / harm / help size | -20 / -3 / +12 | `FactionGraph` |
| Ripple ceiling (second-hand gain) | 40 | `FactionGraph.RippleCeiling` |
| Tier boundaries | -75, -40, -10, 15, 50, 80 | `ReputationTiers` |
| Hysteresis | 4 | `ReputationTiers.Hysteresis` |
| Decay | 0.02 per second | `Reputation.DecayPerSecond` |
| Anger / fear per hurt | 0.4 + 0.5 x health fraction / 0.3 + 0.4 x fraction | `Memory` |
| Witness anger / fear | 0.55 / 0.5, x relation x severity (kill 1, hurt 0.4) | `Memory` |
| Witness / alarm radius | 14 / 8 blocks | `RelationSystem` |
| Hurt debounce | 0.2 s | `RelationSystem.HurtDebounce` |
| Provoked / calm / fury | anger 0.3 / 0.1 / 0.7 | `Mind`, `Brain` |
| Warn time | 1.4 s (timid) to 0.5 s (brave) | `Brain` |
| Skittish / passive flee radius | 7 / 3.5 blocks, scaled by bravery | `Brain` |
| Omen radius | 12 blocks | `RelationSystem.OmenRadius` |

## Seeing it

The pause menu has a **Standing** screen: each faction with its tier and a bar, and the most recent changes. A toast appears when a tier
changes ("The Folk now regard you as Wary" - here, "The Brownies now regard you as Wary"). A small pip above a creature in view, once
you have noticed it, shows how to read it: green friendly, yellow wary, red hostile, blue allied. The Hollow's own show no pip.
`StandingBoard` (Core) decides the rows, the toast text, the recent-change lines and the pip colours; the game only draws them.
Several toasts that arrive together (a kill moves several factions) stack instead of replacing each other. The toast also announces
an omen and a brownie's boon. For screenshots, `HOLLOWFALL_MENU=standing` opens the screen and `HOLLOWFALL_DEMO=folk` lines the
creatures up in the first room.

### Models, animation and sound

| Creature | Model (triangles under a 260 budget) | Animation | Voice (on being provoked) |
|---|---|---|---|
| Brownie | Hooded, big-eared, apron | Scurrying walk, arms up when frightened | `BrownieHiss` |
| Selkie | Sealskin cloak, flippers, long hair, sea-teal eyes | Walk; arms rise before a blow | `SelkieCry` |
| Kelpie | Dark water-horse with a weed mane and tail | Gallop in diagonal pairs; rears (body and forelegs) before it lunges | `KelpieWhinny` |
| Bean Nighe | Stooped, green gown, hood, pale cloth | Wrings the cloth with both arms, sways | `NigheWail` (also `OmenToll` for the omen) |
| Hare | Fur, long ears | Hops; ears back when it runs | `HareScream` |

Also: `StandingUp`/`StandingDown` on a tier change, `BoonChime` for a boon or an offering, `AllyJoin` when a creature becomes an ally.
The cistern pool is a model too (`cistern_pool`), laid once per pool at a water-folk creature's home.

## Later phases

Phase 2 (in progress, issue #3): character progression and stats. Built so far: experience from the Hollow (10 for a Husk, 12 for a Spitter, 60 for a Warden; creatures give none), levels to 20 on a curve of 30 times (level minus one) squared, one stat point per level after the first, and Vigour (+10 maximum health per point) and Might (+5 percent damage per point) to spend them on. Done: the run reads Vigour (maximum health) and Might (guns and parries) once at its start, and the replay records them (format 3). Still to do: a screen to spend points, and Might on melee, meteor and the Warden's explosions. Phase 3 (in progress, issue #4): inventory and equipment. Built: the bag, the worn charm, and the cream a friendly Brownie leaves (`Progression/Inventory.cs`). Not yet: using the cream, the charm's effect, and a screen for the bag. Phase 4: quests and
merchants, using standing for prices and access. The relationship system is built so each of those reads `Reputation` and `Mind` rather
than adding new state.
