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

A hidden figure outside the story, and a nod to the author. In Shetland folklore (mostly Jessie Saxby's account) the Wulver is a gentle wolf-headed man who fishes from a rock and leaves fish on the windowsills of the poor. Ours is called Eòghann, stands on a hill holding a branch of yew (dark, flat-needled, with red arils), and speaks in a voice no one else in the game shares: gentle, dry, unhurried, a little Scots, fond of fishing, weather and the long way down. His name and nine lines live in `WulverVoice`, not in `StoryBook`, and a test keeps the name out of everything else.

`WulverPlacer` puts him on the highest level, dry, open 3 by 3 patch of ground 45 to 110 blocks from the entrance you spawn beside, facing it. Pressing Interact within about 3 blocks makes him speak (his name over the line, the first line an introduction) and, the first time only, heal you to full, cleanse hard damage, grant a 40 second Open guard perk and leave a fish at his feet. Later lines follow a seeded order and do not repeat until all have been heard.

## How it is built

- **Deterministic from the seed.** The dungeon name, the last Warden, the opener and boss lines, each floor's card and note thread, and where the notes lie are all pure functions of the seed (and the floor count), built from `SeededRandom` only.
- **Placement** (`NotePlacer`): one to three scrolls on each floor, against a wall in a room reachable on foot without a secret door. Never the entrance or boss rooms, trap rooms, within three cells of stairs, within two of a door or a spawn, within four of a chest (one press of Interact must never do two things) or within eight cells of another note.
- **Saving.** What you have read is saved in `story.json` beside the records, keyed by seed. It is bounded in size, drops unknown note ids, and a damaged file loses only its bad entries. `HOLLOWFALL_STORY` overrides the path.
- **Adding text.** Add a `NoteDef`, `CardDef` or line to `StoryBook`. The tests check length, plain ASCII, no empty or repeated text, thread order and that every fragment is reachable from some seed. A thread has at most three notes; each band needs at least two threads and two cards.
