# Story

Hollowfall has a small, dark story told almost entirely in the margins: a title card on each floor, a few notes lying against dungeon walls, a line when you die and a card at the end. None of it gets in the way of the fight, and none of it is required. This page records what the story is, how it is told, and what it deliberately does not say. The words live in `Hollowfall.Core.Story` (`StoryBook` holds the text, `StoryPlan` chooses it for a seed, `NotePlacer` puts the notes in the dungeon); this page is the design.

## Premise

Under the hill there is a **Hollow**: a cavity older than any map, and it is falling in on itself. Each floor of a dungeon is one shelf of the same collapse, leaning a little further inward than the one above.

Seven **Wardens** went down to hold it shut. They sealed it with the **fold-craft**, the art of paired doors, so that every way out was folded back onto itself and a sealed thing could only wait. They paid out seven cords behind them as they went. Six of the Wardens are only names now. The seventh took the fold-craft down to the lowest door, and the stone has been coming down ever since.

You are a **Delver**. You follow the red cord down.

## Tone

- **Dark, plain and brief.** Short sentences, concrete objects (a cord, a lintel, a skull on a shelf), no adjectives that are doing the work of a plot. A note is two to four sentences; the longest is under three hundred characters.
- **People, not lore.** The notes are written by tired people who expected to come back. Nobody explains the cosmology; they write down rations, rules, grudges and apologies.
- **The game's own rules, said as in-world truth.** Blood heals because the Hollow returns what is spilled in it. Standing still is dangerous because the floor remembers weight. Portals are the fold-craft. The story never adds a rule the game does not have.
- **Nothing is explained twice,** and nothing tells you what to feel. Text is plain ASCII, so any font draws it.

## Characters

**The Delver** is you: unnamed, unvoiced, never described. The notes address "whoever follows the cord". It is not said who sent you, or why you can use a pair of gates that look like the Wardens' own.

**The Wardens** are the seven who went in. Five are voices in the notes, each with one trait:

| Warden | What the notes say of them |
|---|---|
| Tamsin | Says the walls are listening, and says it about every wall. Last seen writing in her own blood. She is the first to say it is not a collapse but an unfolding: someone took a stitch out. |
| Odile | Cut her hand and watched it close. Wrote the rule the game plays by: strike close, never wait for mercy, never rest in a room you did not clear. |
| Marrec | Held that every door folded is another stitch in cloth that cannot take one. Was right, and wanted it on record. |
| Ansel | Stepped through a gate and saw his own back. Stopped stepping through anything. |
| Brother Haldane | Counts skulls and preaches that the Hollow is a debt, not a god. |

**The last Warden** is the boss, and is named by the seed (Ysolde, Corvane, Maren, Edric, Sabine, Orsolo, Thessaly or Garrick), so two seeds do not tell quite the same story. They were the youngest of the seven and the best with the craft, and never asked why the old Wardens feared the lowest door. They unthreaded the cord from their own wrist and went on alone, saying that someone has to hold it shut from the inside. They have the fold-craft and are no longer sure which of the two of you is the more tired.

**The Hollow** is a place that behaves like a debt. It does not kill; it lets you fall until you stop getting up. It gives back what you spill in it, which is why the dead are bloodless and the living heal on what they break. Every seed names its dungeon (The Pale Vault, The Drowned Maw, The Iron Fold), the way the old maps do.

## Structure: floors, bands and threads

A dungeon has two to four floors, so the story is told by **relative depth**, not floor number. The first floor is always **Upper**, the last (the boss floor) is always **Last**, and the floors between are **Middle**, then **Lower**.

| Band | What it is about | Threads (a seed picks one per floor) |
|---|---|---|
| Upper | The expedition at its most hopeful; practical records and the first rules | `ledger` (muster roll, day two, the first healing), `warning` (scratched rules and a torn page) |
| Middle | How the sealing worked and what the place does to people | `gates` (the folded doors, Marrec, Ansel), `bones` (the ossuary, Haldane, an unsent letter) |
| Lower | The collapse, and the first doubt about one of the seven | `crack` (the survey, the tilt, Tamsin's hand), `warden` (the youngest, the empty case, the last entry) |
| Last | The verdict, left unresolved | `verdict` (the open door, the last Warden's own account, a message for you), `door` (a scratch by the frame, an epitaph) |

A **thread** is one voice told in order. A floor gets one thread, its first one to three notes, so the notes you find on a floor always belong together and always read in order; the earliest lies in the room nearest the entrance. No note is used twice in a run. With three floors you never see the Lower band, and the Warden's name still reaches you in the opening, the boss card and the last floor's notes.

### Floor by floor

1. **Floor I (Upper)**: *The Antechamber*, *The Sealed Gallery* or *The Threshold Stair*. You learn what the game already taught you, in the Wardens' voices: keep moving, strike close, heal on what you break.
2. **Middle floors**: *The Drowned Archive*, *The Ossuary*, *The Long Fold* or *The Gallery of Stitches*. The fold-craft is named, and the cost of it. The Hollow is a debt.
3. **Lower floor** (only in a four-floor dungeon): *The Throat*, *The Unspooling* or *The Swallow*. The place leans. Someone took a stitch out, and the name written under that is the last Warden's.
4. **The boss floor**: *The Last Door*, *The Warden's Rest* or *The Bottom of the Hollow*. The last Warden stands where the cord ends. The notes here are theirs.

Floor titles are numbered with Roman numerals ("III. The Throat"), no two floors in a dungeon share a title, and a title card fades in the first time you enter a floor.

## The beats

- **Title screen.** The current seed names its dungeon: "The Pale Vault".
- **Opening crawl** (the first time a seed is played, never again): where you are, what the Wardens did, who the seventh is, and what you are. Four short paragraphs, one of three openers chosen by the seed.
- **Floor card** on first entering each floor, with an epigraph.
- **Notes.** Walk up to a scroll and press Interact to read it. It goes into the **Journal** (Pause menu), which keeps what you have read on each seed.
- **Floor complete.** One line for each grade from D to P. It does not reward you; it notices you.
- **Boss intro.** When the boss room's doors lock, the last Warden speaks (one of three lines).
- **Death.** A line from a set of six, starting at a point chosen by the seed and moving on with each death, so you never see the same line twice running.
- **Ending.** When the last Warden falls the card reads *The Cord Holds*.

## Themes

- **Following** instead of conquering. The cord is the only thing that ever came back.
- **Debt.** Blood heals because the Hollow gives back what it was owed. Nothing is free, and the game rewards the player who keeps paying.
- **Holding versus finishing.** The Wardens' task was to hold the Hollow shut. The last Warden holds it by hand. You, with the same craft, may be about to finish it. The game never says which is right.
- **Stitches.** Every gate you open is a stitch taken out or put in. The player uses portals constantly; the story quietly notices.

## The ending

The last Warden falls and the Hollow stops falling. The fold-craft is warm in your hand; the last door stands open and the cord goes on. Whether you sealed the Hollow or opened it wider, the stone does not say.

## Deliberately left open

- **Who sent the Delver,** and why you can use the Wardens' craft. The gates in your hands may be a copy, a gift or a theft.
- **Whether the last Warden stole the craft or sacrificed themselves with it.** The notes support both. They took a stitch out, or they are the stitch.
- **What is behind the last door,** and whether the cord, which runs on, was ever meant to end.
- **Whether killing the last Warden is a rescue,** a murder or a mercy, and whether the Hollow stops falling because it is finished or because it is satisfied.
- **What the other six cords were attached to.**
- **Whether the surface above is still there.**

## The Wulver

A hidden figure outside the story, and a dedication to the game's solo developer. In Shetland folklore (mostly Jessie Saxby's account) the Wulver is a gentle wolf-headed man who fishes from a rock and leaves fish on the windowsills of the poor. Ours is called Eòghann (the Gaelic form of the developer's name), stands on a hill holding a branch of yew (dark, flat-needled, with red arils), and speaks in a voice nobody else in the game shares: gentle, dry, unhurried, a little Scots, fond of fishing, weather, the yew and the long way down.

`WulverPlacer` puts him on a rise you can spot: among the columns 45 to 140 blocks from the entrance you spawn beside it scores each by its prominence (its height above the mean of the land 8 to 16 blocks around it) as well as its height, so a knoll that stands out of a plain beats a plateau that is merely higher. The best few that have a level, dry, solid 3 by 3 patch with headroom and no tree or cactus within 2 blocks are then judged on whether his head can be seen from 14, 28 and 42 blocks back toward the entrance, and the best of those wins. He faces the entrance. On a very flat seed the rise is necessarily a gentle one (nothing is removed from the world to help him). When you come within about 7 blocks he turns his head to you and greets you once (generic, again only after you have gone away and come back). Press Interact within about 3 blocks and the choice panel opens; the camera eases back and turns so his whole figure stays in the clear space above the panel, and returns when it closes. When the gift is handed over there is a soft chime, a warm glow at the edge of the screen, a pulse of light round him and the fish pops in at his feet.

### The tree

Everything he says lives in one data file, `Story/Dialogue/WulverDialogue.cs`, run by a small engine in `Story/Dialogue` (`DialogueNode`, `DialogueChoice`, composable `DialogueCondition`, `DialogueContext`, `DialogueEffect`, `DialogueRunner`, and a `DialogueValidator` the tests use). There are 39 nodes and 94 choices.

- **Entries** are chosen by the player's state, first match wins: the maker's return, a key carried from the last floor, the maker's recognition, the first meeting, then hurt, often dead, carrying a key, ending reached, far below, many notes read, and a plain welcome. Each return visit remembers what you chose before, through flags saved with the story progress (`heardYew`, `askedWhoIAm`, `gaveFishBack`, `keptFish`, `claimedMaker`, `makerKey`, `makerKnown`) and a visit count, at most 32 short flags per seed.
- **First meeting:** who he is, what the place is, and the gift, which comes once per run: a full heal, hard damage cleansed and an Open guard perk, given more gently and for longer to someone badly hurt. Then the fish ritual: leave it on a windowsill or keep it.
- **The yew:** a short story, with the berries and why he carries a branch.
- **The long way down:** true hints about locked chests and keys, Sever and Meteor, and parrying. New players hear the basics; veterans (many kills, or the third floor reached) hear shorter lines.
- Every node has two to four choices, and a way to leave. There are no dead ends and no unreachable nodes (tested).

### In Gàidhlig

When the interface language is Gàidhlig or Gàidhlig agus Beurla he also says a few short Gàidhlig lines, each with its English beneath in brackets: *Halò. Fàilte.* on a first meeting and as you come up the hill, *Fàilte.* on a return, *Mòran taing.* when you leave the fish, *Tha i fuar an-diugh.* when you sit a while, and *Mar sin leat.* as you go. They are extra lines (the node and choice counts above are unchanged), written in `Localization/StoryStrings.cs` with a status and source like every Gàidhlig string, and placed before his usual lines so nothing on the guarded path below is touched. Nothing else in the story is translated: the notes, epigraphs and the rest stay English, apart from two floor-card names (*An Sgòrnan* for The Throat, *An Doras Deireannach* for The Last Door) and a short line, *Feuch a-rithist.*, shown above the English line when you die. See [gaidhlig.md](gaidhlig.md); none of it has been reviewed by a native speaker.

### The one guarded path

The developer's name is written in exactly one file, and spoken on exactly one path. The player must pick the choice that claims they made this place, AND one of these circumstances must hold: the world seed (ignoring case, accents, spaces and hyphens) is one of the developer's own names; or the ending has been reached on this seed and he has been visited at least three times; or the `makerKey` flag is set, which is earned by claiming first, then reaching the last floor carrying a key and showing it to him. The nodes on that path carry a guard, and the runner redirects to a kind reply ("A good many folk say that, and I let them") if the guard fails. When it holds he speaks to the maker by name in seven warm lines, and `makerKnown` makes later visits greet them by name with a different set. A test walks every path of the tree under every combination of those conditions and fails if the name appears anywhere else, and another scans all of Core and the game scripts for the names outside the dialogue file.

## The cat by the fire

A second hidden figure, and a gift. **Aodhán** is a Russian Blue who rests on a flat stone seat with a rug of dark cloth beside one fire pit in each world (never in the flames, never in danger: he is not an enemy and has no hit box). *Aodhán* is the Irish name usually glossed "little fire", and he is the only entity in the game with it. He speaks **Irish (Gaeilge)**, in a gentle, sleepy, dry voice, in short fixed phrases with the English beneath each line. Every Irish string, its source and its status are in [gaeilge.md](gaeilge.md), which also asks a fluent speaker to review them before release.

`CatPlacer` chooses the cat's pit from the seed: a pit in a Safe room on the first two floors if there is one, else any Safe room, else the deepest pit on floor 1, else floor 0. The dungeon is the nearest one to the origin, as for the Wulver, so there is exactly one cat per world. He sleeps curled on his rug while you are far away; within about 8 blocks he wakes and sits up, turns his head to follow you, blinks slowly at you when you look at him, and purrs when you are close. The first time he comes into view a one-line caption appears, and nothing else. Press Interact within about 2.5 blocks to talk.

### The tree

Everything he says is in one file, `Story/Dialogue/CatDialogue.cs`, run by the same dialogue engine as the Wulver (`DialogueRunner`, `DialogueCondition`, `DialogueEffect`). There are **11 nodes and 33 choices**; no node is a dead end or unreachable, and every node offers a way to say goodbye (tested).

- **Entries**, first match wins: the warm line (the third visit, once), the first meeting, and a plain return.
- **First meeting:** a slow blink and a greeting. You can blink back, answer in Irish, or leave him to sleep. Then a hub: sit by the fire, stroke him, thank him, or say goodbye. Each choice sets a short flag (`blinked`, `petted`, `sat`, `thanked`, `heardSmall`), saved with the story progress, at most 16 per seed.
- **Return visits** remember what you did: he greets you as a friend, and if you stroked or sat before he remembers.
- **The warm line** plays only on the third visit to the cat and only once. It is a short, kind line about a little fire, with its English at the end.
- **The gift:** sitting beside him gives a little warmth, once per run: your hard damage is washed away and you heal a little (20 health, never a full heal, no perk). A second sit gives nothing.
- A flag list and visit count are the only things saved, in the same `story.json` as the Wulver's, through `DataHome`.

A strict test keeps his name out of every file in Core and the game scripts except his own dialogue file, and out of every document except this one and `gaeilge.md`.

## How it is built

- **Deterministic from the seed.** The dungeon name, the last Warden, the opener and boss lines, each floor's card and note thread, and where the notes lie are all pure functions of the seed (and the floor count), built from `SeededRandom` only.
- **Placement** (`NotePlacer`): one to three scrolls on each floor, against a wall in a room reachable on foot without a secret door. Never the entrance or boss rooms, trap rooms, within three cells of stairs, within two of a door or a spawn, within four of a chest (one press of Interact must never do two things) or within eight cells of another note.
- **Saving.** What you have read is saved in `story.json` beside the records, keyed by seed. It is bounded in size, drops unknown note ids, and a damaged file loses only its bad entries. `HOLLOWFALL_STORY` overrides the path.
- **Adding text.** Add a `NoteDef`, `CardDef` or line to `StoryBook`. The tests check length, plain ASCII, no empty or repeated text, thread order and that every fragment is reachable from some seed. A thread has at most three notes; each band needs at least two threads and two cards.
