# Gàidhlig in Hollowfall

Hollowfall can be shown in **Gàidhlig** (Scottish Gaelic), in English, or in both. This page says what is translated, where each word came from, how sure we are, what is not translated, and how a Gaelic speaker can check and correct it.

## Please read first: this needs a native speaker

**All of the Gàidhlig here was put together by an AI model working from dictionaries, software translations and grammar pages. No native speaker has reviewed any of it.** An AI can produce Gaelic that looks right and is wrong in grammar, mutation, spelling, accents or idiom, so the rule followed was: use only what a source supports, prefer single words and short fixed phrases to sentences, and leave the English alone wherever nothing could be checked. Every string carries a status, and a string that is not backed up is not shown in Gàidhlig at all.

**The game should not ship with Gàidhlig switched on as finished until a native speaker has gone through the tables below.** See "A request to a Gaelic speaker" at the end.

### The three statuses

| Status | Meaning |
| --- | --- |
| **Verified** | A source we could read says exactly this word or phrase, with this meaning. |
| **Likely** | Put together from words and rules a source gives (for example a verb plus an adverb, or a word with a plural from a dictionary) but not quoted whole. Shown to players. |
| **NeedsReview** | A string we are not willing to show until a speaker decides. It is **held back**: `Loc` shows the English instead (a reviewer can turn them on with `Loc.ShowNeedsReview`). |
| *Untranslated* | No Gàidhlig at all (the Gàidhlig column is empty in the data): English is shown. These are the "needs a speaker" list. |

The counts are printed by the test `ReviewStatusCountsArePrintedAndAddUp` (run `dotnet test --filter ReviewStatusCounts --logger "console;verbosity=detailed"`), and each table below states its own counts.

## How the language setting works

- Settings has a **Language** row at the top, naming each language in itself: `English`, `Gàidhlig`, `Gàidhlig agus Beurla`. Changing it rebuilds every screen at once and is saved with the other settings (`GameSettings.Language`; anything unknown in a saved file is English; Reset keeps your language).
- **Gàidhlig** shows the Gàidhlig where there is some fit to show, and the English where there is not.
- **Bilingual** (Gàidhlig agus Beurla) shows the Gàidhlig with the English in brackets for short single-line text (`Cluich (Play)`), and the English on a second line for longer text. English alone is shown where there is no Gàidhlig.
- Text never depends on a count: numbers sit beside labels (`Iuchraichean 2`, not "two keys"), so no plural, numeral or mutation rules are needed.
- Scottish Gaelic uses the **grave** accent only (à è ì ò ù, À È Ì Ò Ù). The acute accent belongs to Irish (Gaeilge) and must never appear in a Gàidhlig string; a test enforces it. In English we say "Scottish Gaelic"; in Gàidhlig the language is "Gàidhlig".

## What is and is not translated

**In Gàidhlig (when something is checked):** the menu words (Play, Settings, Quit, Resume, Journal, Back, Seed, Language), the Settings and Journal headings, the Movement heading, three action labels (move forward, move back, jump), the HUD key count, three hints (open, jump, menu) and the "read" and "speak" prompts, the death and key banners, two floor-card names, a short line shown with a death, the names of the creatures in `docs/mythology.md`, a Gàidhlig name for the dungeon shown under the title, and five short greetings from Eòghann.

**Stays in English** (and why): the tagline; most settings labels and the controls help; most action labels; ranks and style bonuses; the floor-complete, boss, opening and ending text; every epigraph and every lore note (long free prose, where the risk of a plausible mistake is highest, so none was attempted); and everything Eòghann says apart from his short greetings. How much story text that is: the test output above prints the count of strings in `StoryBook`.

**Held back (NeedsReview):** `hud.floor`, "Làr {0} / {1}" for a dungeon floor, because *làr* is the floor of a room or the ground and whether it suits a level of a dungeon needs a speaker; and `pause.title`, *Air stad*, because Firefox's Pause is the command *Cuir 'na stad* and a heading that names the state is only a guess.

## Sources

Each entry in the tables cites one or more of these by id. They were read between 20 and 21 October 2026.

| Id | Source |
| --- | --- |
| WK | [Wiktionary](https://en.wiktionary.org/wiki/Category:Scottish_Gaelic_lemmas), the Scottish Gaelic sections: spelling, gender, meanings, mutated forms. Used for every noun, adjective and verb. |
| FF | [Firefox in Gàidhlig](https://github.com/mozilla-l10n/firefox-l10n/tree/main/gd) (Mozilla l10n, `gd`): `menu-settings` is *Roghainnean*, `menu-quit` is *Fàg an-seo*, resume (downloads) is *Lean air* (and pause is the command *Cuir 'na stad*), the language header is *Cànan*, *Aisig na bun-roghainnean* is Restore Defaults, and *Mòran taing* is used in a message. |
| LG | [LearnGaelic, Little by Little, unit 7](https://learngaelic.net/littlebylittle/unit07/index.jsp): prints the weather sentences, including *Tha i fuar.* (It's cold.). |
| GW | [Scottish Gaelic Grammar Wiki](https://gaelicgrammar.org/~gaelic/mediawiki/index.php/Article) (after Fisher 2004): the article (masculine *an*, *am* before b f m p, *an t-* before a vowel; feminine *a'* and lenition) and "adjectives are lenited after feminine nouns". |
| GDW | [Vicipeid na Gàidhlig](https://gd.wikipedia.org), the Gaelic Wikipedia: pages *Bean-nighe*, *Gruagach* and *Each-uisge*. |
| TAD | [Tobar an Dualchais](https://www.tobarandualchais.co.uk/track/5824): a recording titled *Each-uisge ann an Loch Fada*. |
| DRU | [Drupal Colossal Menu translation](https://ftp.drupal.org/files/translations/all/colossal_menu/colossal_menu-3.0.0.gd.po), `gd`: *Clàr-taice* for "Menu" (a software translation, not a dictionary). |

**How the sources were read.** Every page was read through a fetch tool that summarises a page for a question, not as raw text, so a summary could itself be wrong or mix entries up; a Verified row therefore means one summarised page said so, not that a person checked the dictionary. Am Faclair Beag and the LearnGaelic dictionary could not be fetched (server errors), so Dwelly was not consulted at all. **A speaker should check Am Faclair Beag first**, since that is the standard dictionary.

## The Gàidhlig dungeon name

Under the title in Gàidhlig and Bilingual modes each dungeon also has a Gàidhlig name, from `GaelicNames` (a separate flavour name chosen from the seed; the English name, the seed words like `amber-hollow-914` and every golden test are unchanged). It is made the way place names are: a **noun, then an adjective after it** (`Cnoc Dubh`, black hill), or a noun alone (`Dùn`). In Bilingual the English meaning follows in brackets (`Cnoc Dubh (Black Hill)`).

- **No article** is used (no *an* / *am* / *a'* / *an t-*), which keeps all article rules out of the generator.
- **Gender** of each noun is taken from Wiktionary: feminine uamh, beinn, clach; masculine cnoc, gleann, càrn, bothan, dùn, eas, sgùrr, allt, coire (the masculine "corrie" word). Wiktionary lists **loch** as feminine and **tobar** as masculine or feminine by dialect; the sources checked disagree with common usage or with each other, so these two are only ever used **alone**, never with an adjective.
- **Lenition**: after a feminine noun the adjective is lenited (`Beinn Mhòr`, `Clach Ghlas`, `Uamh Fhada`, `Clach Dhubh`; GW). The lenited forms (dhubh, gheal, ghlas, fhada, mhòr, bheag; *ruadh* is unchanged in writing) are stored as data from Wiktionary's mutation tables, not computed. After a masculine noun the adjective is unchanged (`Cnoc Dubh`, `Gleann Mòr`).
- **One pair is never made: Beinn + dubh.** Whether the *d* is lenited after a final *n* could not be confirmed in a source.
- **Left out: seann** (old). It goes *before* a noun and has its own mutation effects (it appears in compounds such as *seann-nòs* on Wiktionary, with nothing on how it behaves), so it was not attempted.
- Not used: any name with two adjectives, a genitive, a plural, or a personal name.

Real names that have the same shape include Beinn Fhada, Beinn Dearg, Càrn Dearg and Loch Fada (the last in the Tobar an Dualchais title above), which is the reason for trusting the pattern; a speaker should still confirm the exact forms.

## The Wulver's greetings

Eòghann (the Wulver; see `docs/story.md`) says five short Gàidhlig lines in Gàidhlig and Bilingual modes, each with its English beneath in brackets: *Halò.* and *Fàilte.* on a first meeting, *Fàilte.* on a return, *Mòran taing.* when you leave the fish, *Tha i fuar.* when you sit a while, and *Mar sin leat.* as you go. They are extra lines in his dialogue file placed before his usual ones; they are not choices. His name stays in that file only (a repository-wide test enforces it) and the guarded maker path is untouched.

## The creatures

`docs/mythology.md` lists the Gàidhlig names for the creatures the game borrows. The game does not show these on screen yet.

## How to add a string

1. Add a `LocEntry` to the right file in `src/Hollowfall.Core/Localization/` (`InterfaceStrings` for the interface, `StoryStrings` for story lines, cards, creatures and greetings). The key is `area.name`; placeholders are `{0}`, `{1}`.
2. Write the **English** exactly as it should appear. Give no Gàidhlig (use the `T(...)` helper) unless you can cite a source for it.
3. In the game scripts ask for it with `loc.Tr("area.name", args)` written with the key as a literal (a test finds every such call and checks the key exists).
4. If you add Gàidhlig: write it with grave accents only, choose a status (Verified, Likely or NeedsReview), name the source ids and say in the note how you reached it.
5. Regenerate the tables below: `HOLLOWFALL_WRITE_DOCS=1 dotnet test --filter GaelicDocsTests`, then commit the page. The test fails if the page and the data differ.

New strings that arrive from other branches (a Standing screen, a Music slider, mixed-room captions) should be added the same way: English first, no Gàidhlig until it is sourced.

## How to add a language

Add a value to `Language`, a native name in `Languages.NativeName`, a column (or a second entry type) in the tables and a rule in `Loc.Compose`. Keep each language in its own tables and its own accent rules: **Irish (Gaeilge) uses acute accents (á é í ó ú) and must never be mixed into the Gàidhlig tables**, which the accent test enforces.

## Review process

1. A speaker reads the tables below (one per area, with key, English, Gàidhlig, status, source and a note) and marks each row correct, wrong (with the fix) or unsure.
2. Corrections go into the data (one line each) with the status raised to Verified and the source noted (for example "reviewed by Name, 2026").
3. Rows marked NeedsReview stay hidden until reviewed; untranslated rows can be filled in the same way.
4. Nothing should be called finished until every Verified and Likely row has been through step 1.

## Needs a speaker

- **First:** the Likely rows that are one-word UI labels used as buttons, because the form (imperative, plural, or noun) matters: *Air ais* (Back), *Leum*, *Fosgail*, *Bruidhinn*, *Leugh*, *Gluais air adhart / air ais*, *Iuchraichean {0}*, *Clàr-taice* and *Aisig na bun-roghainnean* (for "Reset to defaults"). *Fàg an-seo* is Firefox's own wording for Quit, so it is Verified, but a speaker should say whether it suits a game's menu.
- *Làr* for a dungeon floor (held back), and whether *lobhta* or another word is better; and the Paused heading *Air stad* (held back: Firefox's Pause is the command *Cuir 'na stad*).
- Which of Controls, Seed (*Sìol*), Random, Respawn, Main menu, Quit and the settings labels (sensitivity, dead zone, field of view, volume, render distance, prompts, invert) have an established Gàidhlig term in software and games; the English is shown until they do.
- *brùnaidh* (brownie): not found in any source checked; *gruagach* is used (Likely) because the Gaelic Wikipedia describes a spirit of the cattle, but the brownie sense is reasoned, not quoted.
- Whether the greetings suit Eòghann's voice (Halò, Fàilte, Mòran taing, Tha i fuar, Mar sin leat); each is a sentence or phrase a source prints, but a source is not a speaker.
- The gender of *loch* and *tobar*, and whether Beinn + dubh lenites.
- The two floor-card names, *An Sgòrnan* and *An Doras Deireannach*, and whether *Feuch a-rithist.* suits a death line.
- Every untranslated story line (see the counts above), if translation of the story is wanted at all.

## A request to a Gaelic speaker

If you read Gàidhlig, please look over the tables below and tell us what is wrong, odd, stiff or unnatural: spelling, accents, mutation, word order, a better word, a regional preference, anything. You do not need to be a programmer: a list of the keys and what each should say is enough, in an issue or an email to the author. Corrections are welcome and will be credited if you wish. Thank you; the game takes Gaelic seriously and wants to get it right.

If you would rather correct the strings in a translation tool, run `dotnet run --project src/Hollowfall.Cli -- --po gd.po` and open `gd.po` in Poedit or any gettext editor. Each entry's comment gives its key, status and sources, and an entry marked fuzzy needs a decision before it is shown.

## Independent re-check (21 October 2026)

A second pass, made after the first build, read the primary pages directly instead of relying on summaries. It confirmed these and found nothing wrong, but it is still not a speaker's review.

| Point | Result | Source |
| --- | --- | --- |
| *Fàg an-seo* is Quit, *Roghainnean* is Settings | Confirmed in Firefox's own Scottish Gaelic file (`menu-quit` is *Fàg an-seo* on Windows and other platforms; `menu-settings` is *Roghainnean*). On macOS Firefox uses *Fàg* plus the app name. | [mozilla-l10n gd `menubar.ftl`](https://raw.githubusercontent.com/mozilla-l10n/firefox-l10n/main/gd/browser/browser/menubar.ftl) |
| *doras* (door) is masculine | Confirmed: `doras m (genitive dorais, plural dorsan)`. So *An Doras Deireannach* needs no mutation. | [Wiktionary: doras](https://en.wiktionary.org/wiki/doras) |
| *deireannach* means last, final | Confirmed (adjective; also "tardy"). | [Wiktionary: deireannach](https://en.wiktionary.org/wiki/deireannach) |
| *sgòrnan* is masculine, the throat | Confirmed: `sgòrnan m (genitive sgòrnain)`, throat, gullet, also windpipe. So *An Sgòrnan* is correct for "the Throat". | [Wiktionary: sgòrnan](https://en.wiktionary.org/wiki/sgòrnan) |
| *iuchair* (key) is feminine, plural *iuchraichean* | Confirmed (genitive *iuchrach*). The label *Iuchraichean {0}* is a correct plural. | [Wiktionary: iuchair](https://en.wiktionary.org/wiki/iuchair) |
| *cluich* is the verb "to play" | Confirmed. Imperative singular *cluich*, plural *cluichibh*. The game uses the singular. | [Wiktionary: cluich](https://en.wiktionary.org/wiki/cluich) |
| *fàg* is "leave, quit, abandon" | Confirmed, transitive only; *falbh* is the intransitive "go". *Fàg an-seo* is therefore software wording, not a rule of the dictionary. | [Wiktionary: fàg](https://en.wiktionary.org/wiki/fàg) |
| *loch* gender | **Still unsettled.** Wiktionary prints `loch f (genitive locha, plural lochan)`, yet the diminutive *lochan* is masculine there and real names such as *Loch Fada* are written without lenition, which a feminine noun would normally cause. Keep using *loch* on its own, never with an adjective. | [Wiktionary: loch](https://en.wiktionary.org/wiki/loch), [Wiktionary: lochan](https://www.wiktionary.com/wiki/lochan) |
| *tobar* gender | **Varies by dialect.** Wiktionary prints `m or f`, masculine forms in Uist and feminine in Lewis. Keep it alone. | [Wiktionary: tobar](https://en.wiktionary.org/wiki/tobar) |
| *Halò*, *Fàilte*, *Mòran taing*, *Mar sin leat* | Printed in beginner references. Note that *Mar sin leat* is the goodbye to one person you know well; to several people or a stranger it is *Mar sin leibh*, and *Mòran taing* is a "many thanks", usually a reply to thanks. | [Omniglot](https://omniglot.com/language/phrases/gaelic.php), [LearnGaelic unit 1](https://learngaelic.net/littlebylittle/unit01), [Gaelic Grammar Wiki: Idiomatic Pleasantries](https://gaelicgrammar.org/~gaelic/mediawiki/index.php/Idiomatic_Pleasantries) |
| *brùnaidh* and *gruagach* as the brownie | Both are listed as Scottish Gaelic names on Wikipedia's brownie page, which also lists *ùruisg*. No dictionary page was reached, so the status stays Likely, not Verified. | [Wikipedia: Brownie (folklore)](https://en.wikipedia.org/wiki/Brownie_(folklore)) |
| *Air ais* as "Back" on a button | **Not confirmed.** It means "back" and "ago" in general; no software source for the button was found. Keep it Likely. | (none found) |
| The name Eòghann | It is the Scottish Gaelic form of Ewan. Its meaning is disputed: "born of the yew" is the usual popular reading, while Wikipedia also gives "noble born" from Latin *Eugenius*. Say "often glossed as" rather than stating a meaning. | [Wikipedia: Ewan](https://en.wikipedia.org/wiki/Ewan) |

What this changes: nothing in the game needed fixing. *Fàg an-seo* and *Roghainnean* are now confirmed from the primary file. The two floor-card names are grammatically sound (a masculine noun and a masculine noun plus adjective, each unmutated), though the whole phrases are composed, not quoted. The cautions that remain are the ones above: *loch* and *tobar* stay out of noun-plus-adjective names, *Air ais* needs a speaker, and the greetings' register (singular and informal) is worth a speaker's ear.

## The tables

Generated from the data by `GaelicDocsTests`; do not edit by hand. "Untranslated" rows are the ones with no Gàidhlig yet.

<!-- BEGIN GENERATED TABLES -->

### Interface

Verified 9, Likely 14, NeedsReview 2, no Gàidhlig yet 146.

| Key | English | Gàidhlig | Status | Source | Note |
| --- | --- | --- | --- | --- | --- |
| `menu.play` | Play | Cluich | Verified | WK | cluich is the imperative of the verb cluich, to play |
| `menu.settings` | Settings | Roghainnean | Verified | FF WK | Firefox's own gd word for Settings; roghainn, an option or choice |
| `menu.controls` | Controls | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `menu.quit` | Quit | Fàg an-seo | Verified | FF WK | Firefox gd's Quit on Windows and Linux (macOS has 'Fàg' plus the app's name); fàg is leave or quit |
| `menu.back` | Back | Air ais | Likely | WK | air ais is the adverb 'back'; its use on a Back button is not confirmed |
| `menu.reset` | Reset | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `title.tagline` | A procedural dungeon crawler.\nMove fast. Heal by hurting things. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `title.seed` | Seed | Sìol | Verified | WK | sìol, seed (first sense) |
| `title.seedHint` | any text or number | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `title.random` | Random | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `title.daily` | Daily | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `title.board` | Verified today | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `title.boardNone` | No verified run yet today. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `title.boardRow` | Floor {0}: grade {1}, {2} points, {3} s | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pause.title` | PAUSED | AIR STAD | NeedsReview | WK FF | held back: Firefox gd's Pause is the command 'Cuir 'na stad'; 'air stad' (at a stop, from stad, a stop or pause) is a guess at a heading that names the state |
| `pause.resume` | Resume | Lean air | Verified | FF WK | Firefox gd's Resume (downloads) is 'Lean air'; Wiktionary lists lean air as continue |
| `pause.respawn` | Respawn | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pause.standing` | Standing | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pause.character` | Character | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pause.bag` | Bag | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.title` | Bag | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.slots` | Slots used: {0} of {1} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.none` | Nothing is carried yet. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.creamHint` | Eat it in a run to heal {0}. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.wear` | Wear | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.takeOff` | Take off | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.worn` | worn | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `bag.wearHint` | Worn charms take effect from your next run. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.title` | CHARACTER | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.none` | No character yet. Defeat something to begin. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.level` | Level {0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.nextLevel` | {0} of {1} experience to the next level | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.maxLevel` | Highest level reached | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.points` | Points to spend: {0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.vigour` | Vigour {0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.vigourHint` | +10 maximum health a point | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.might` | Might {0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.mightHint` | +5 percent damage from guns and parries a point | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `character.nextRun` | Points take effect from your next run. A run keeps the stats it began with. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pause.journal` | Journal | Leabhar-latha | Verified | WK | leabhar-latha, diary or journal |
| `pause.mainMenu` | Main menu | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `journal.title` | JOURNAL | LEABHAR-LATHA | Verified | WK | capitals of the entry above |
| `journal.notesRead` | {0}   -   Notes read: {1} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `journal.empty` | Nothing read yet. Notes lie against the walls of the dungeon: walk up to a scroll and use it. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.title` | CONTROLS | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.help` | Click a binding, then press the new key, button or stick direction. Esc cancels. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.colAction` | Action | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.colKeys` | Keyboard / mouse | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.colPad` | Gamepad | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.resetAll` | Reset to defaults | Aisig na bun-roghainnean | Likely | FF | Firefox gd uses this for 'Restore Defaults'; whether it suits 'Reset to defaults' needs a speaker |
| `controls.listenPad` | press a button or move a stick… | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.listenKey` | press a key or click… | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.binding` | Binding "{0}". Esc cancels. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.mouse` | mouse | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.now` | {0} is now {1}. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.swapUnbound` | {0} was used by {1}, which is now unbound. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.swapped` | {0} was used by {1}; the two swapped. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.inUse` | {0} is used by {1}, which cannot take another input. Nothing changed. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.reserved` | {0} cannot be used for {1}. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.wrongKind` | {0} cannot use {1}. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.same` | That binding is already set. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `controls.resetDone` | Bindings reset to defaults. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `group.movement` | MOVEMENT | GLUASAD | Likely | WK | gluasad is the verbal noun of gluais, move |
| `group.look` | LOOK | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `group.combat` | COMBAT | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `group.portals` | PORTALS | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `group.system` | SYSTEM | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.moveForward` | Move forward | Gluais air adhart | Likely | WK | gluais (move) + air adhart (forward) |
| `action.moveBack` | Move back | Gluais air ais | Likely | WK | gluais (move) + air ais (back) |
| `action.moveLeft` | Move left | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.moveRight` | Move right | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.lookUp` | Look up | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.lookDown` | Look down | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.lookLeft` | Look left | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.lookRight` | Look right | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.jump` | Jump | Leum | Likely | WK | leum, to jump; the bare verb is the imperative |
| `action.firePrimary` | Fire revolver | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.fireSecondary` | Fire shotgun | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.parry` | Parry | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.portalTeal` | Teal portal | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.portalViolet` | Violet portal | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.respawn` | Respawn | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.flyToggle` | Free-fly | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.flyDown` | Fly down | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.debugInfo` | Debug overlay | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.interact` | Open and use | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.closePortals` | Close portals (sever) | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.pause` | Pause menu | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.slam` | Slam | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.fireMortar` | Fire block mortar | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `action.useCream` | Use Brownie's cream | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.title` | SETTINGS | ROGHAINNEAN | Verified | FF | capitals of Firefox gd's 'Roghainnean' |
| `settings.language` | Language | Cànan | Verified | FF WK | Firefox gd's 'Language' header is Cànan; cànan, a language |
| `settings.mouseSensitivity` | Mouse sensitivity | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.padLookSpeed` | Gamepad look speed | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.deadzone` | Stick dead zone | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.fov` | Field of view | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.uiSize` | Interface size | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.autoScale` | Scale the interface with the window | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.master` | Master volume | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.effects` | Effects volume | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.ambience` | Ambience volume | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.music` | Music volume | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.viewDistance` | Render distance | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.chunks` | {0} chunks | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.prompts` | Controller prompts | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.promptsAuto` | Auto (follow controller) | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.invertMouse` | Invert mouse Y | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `settings.invertPad` | Invert gamepad Y | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hud.floor` | Floor {0} / {1} | Làr {0} / {1} | NeedsReview | WK | làr is the floor of a room or the ground; whether it fits a level of a dungeon (or lobhta, a storey) needs a speaker, so it is held back |
| `hud.keys` | Keys {0} | Iuchraichean {0} | Likely | WK | iuchraichean is the plural of iuchair, key; the number sits beside it, so no count agreement is needed |
| `hud.result.title` | FLOOR {0} COMPLETE | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hud.result.rank` | RANK  {0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hud.result.stats` | Time {0}    Kills {1}    Style {2} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hud.result.restarts` | Restarts {0}  (−{0} points) | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `toast.omen` | A washerwoman wrings out her cloth nearby. Something is coming. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `toast.boon` | The brownie warms the hearth for you (+{0} health) | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `standing.better` | {0} think better of you: {1} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `standing.worse` | {0} now regard you as {1} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `standing.title` | STANDING | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `standing.subtitle` | How the dungeon's peoples regard you. Hurt a creature in front of its kin and they will remember. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `standing.nobody` | Nobody here has an opinion of you yet. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `standing.recent` | RECENT | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `standing.nothingYet` | Nothing yet. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `gap.edge` | A gap. Fire a portal across it ({0}), then another beside you ({1}), and step through. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `gap.alcove` | A high alcove. Fire a portal up into it ({0}), then another beside you ({1}), and step through. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `gap.afterFall` | You are set back at the edge. Fire a portal across the gap instead ({0}, then {1}). | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `gap.fellBare` | FELL  -{0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `gap.spikes` | SPIKES  -{0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `gap.splash` | SPLASH  climbing out | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.revolver` | revolver | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.shotgun` | shotgun | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.parry` | parry | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.tealPortal` | teal portal | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.violetPortal` | violet portal | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.sever` | sever | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.open` | open | fosgail | Likely | WK | fosgail, open; the bare verb is the imperative |
| `hint.jump` | jump | leum | Likely | WK | leum, to jump |
| `hint.menu` | menu | clàr-taice | Likely | DRU | a software translation of 'Menu'; not in the dictionaries checked |
| `prompt.read` | {0}  read: {1} | {0}  leugh: {1} | Likely | WK | leugh, read (the imperative; Wiktionary quotes 'Leugh seo', Read this) |
| `prompt.speak` | {0}  speak | {0}  bruidhinn | Likely | WK | bruidhinn, to speak or talk; the bare verb is the imperative |
| `cat.warmth` | The fire is warm, and you feel a little better. | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `prompt.closeHint` | {0} or {1} to close | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `prompt.leaveHint` | {0} or {1} to leave | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.died` | YOU DIED | THA THU MARBH | Likely | WK | Tha thu (you are) + marbh, dead |
| `banner.respawn` | {0} to respawn | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.enemies` | Enemies {0}: kill them to open the doors | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.clear` | CLEAR | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.floorDone` | FLOOR {0} COMPLETE  {1}  {2} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.floorStats` | time {0}  kills {1}  style {2} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.firstClear` | first clear | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.newBest` | NEW BEST  (was {0}) | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.best` | best {0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.key` | KEY | IUCHAIR | Likely | WK | iuchair, key |
| `banner.chest` | CHEST | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.lockedChest` | LOCKED CHEST | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.chestHealed` | CHEST  +{0} health | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.lockedChestHealed` | LOCKED CHEST OPENED  +{0} health | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.locked` | locked: find the key on this floor | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.spikes` | SPIKES  -{0} | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.noPortal` | can't place a portal there ({0}) | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.meteor` | METEOR | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `banner.meteor2` | METEOR x2 | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.rowdy` | ROWDY | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.fierce` | FIERCE | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.brutal` | BRUTAL | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.savage` | SAVAGE | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.merciless` | MERCILESS | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.relentless` | RELENTLESS | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.apocalyptic` | APOCALYPTIC | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.legendary` | LEGENDARY | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |

### Story

Verified 0, Likely 3, NeedsReview 0, no Gàidhlig yet 11.

| Key | English | Gàidhlig | Status | Source | Note |
| --- | --- | --- | --- | --- | --- |
| `card.the-antechamber` | The Antechamber | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-sealed-gallery` | The Sealed Gallery | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-threshold-stair` | The Threshold Stair | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-drowned-archive` | The Drowned Archive | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-ossuary` | The Ossuary | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-long-fold` | The Long Fold | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-gallery-of-stitches` | The Gallery of Stitches | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-throat` | The Throat | An Sgòrnan | Likely | WK GW | sgòrnan is a masculine noun, throat (WK); a masculine noun takes 'an' before s+g (GW) |
| `card.the-unspooling` | The Unspooling | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-swallow` | The Swallow | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-last-door` | The Last Door | An Doras Deireannach | Likely | WK GW | doras is a masculine noun, door (WK); deireannach is the adjective last or final (WK); the adjective follows the noun, unlenited after a masculine noun (GW) |
| `card.the-wardens-rest` | The Warden's Rest | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `card.the-bottom-of-the-hollow` | The Bottom of the Hollow | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `story.deathHead` | Try again. | Feuch a-rithist. | Likely | WK | feuch, try (the imperative) + a-rithist, again; shown above the English death line, which is not translated |

### Creature names

Verified 4, Likely 1, NeedsReview 0, no Gàidhlig yet 1.

| Key | English | Gàidhlig | Status | Source | Note |
| --- | --- | --- | --- | --- | --- |
| `name.eachUisge` | water-horse (each-uisge) | each-uisge | Verified | GDW TAD | the Gaelic Wikipedia has a page titled Each-uisge and Tobar an Dualchais titles a recording 'Each-uisge ann an Loch Fada' |
| `name.beanNighe` | washerwoman at the ford (bean nighe) | bean-nighe | Verified | GDW WK | a feminine noun; the Gaelic Wikipedia page is titled Bean-nighe |
| `name.gruagach` | brownie, household or cattle spirit (gruagach) | gruagach | Likely | GDW WK | the Gaelic Wikipedia page Gruagach describes a spirit of the cattle that is given milk; Wiktionary's Scottish Gaelic entry glosses only 'maiden', and 'brownie' is given for the Irish word, so the brownie sense is reasoned |
| `name.ron` | seal (ròn) | ròn | Verified | WK | a masculine noun, seal (the animal); this is NOT a claim about selkies or seal-folk, for which no Gàidhlig was verified |
| `name.cailleach` | the winter hag (the Cailleach) | Cailleach | Verified | WK | cailleach, a feminine noun: old woman, hag, crone |
| `name.brownie` | brownie (a brùnaidh?) | (English shown) | Untranslated |  | the form brùnaidh was not found in any source checked; needs a speaker |

### Wulver greetings

Verified 5, Likely 0, NeedsReview 0, no Gàidhlig yet 0.

| Key | English | Gàidhlig | Status | Source | Note |
| --- | --- | --- | --- | --- | --- |
| `wulver.hello` | Hello. | Halò. | Verified | WK | an interjection, hello (borrowed from English) |
| `wulver.welcome` | Welcome. | Fàilte. | Verified | WK | fàilte, welcome |
| `wulver.farewell` | Goodbye. | Mar sin leat. | Verified | WK | an informal farewell to one person |
| `wulver.thanks` | Thank you. | Mòran taing. | Verified | WK FF | many thanks; Firefox gd also uses 'Mòran taing' |
| `wulver.weather` | It is cold. | Tha i fuar. | Verified | LG | LearnGaelic's Little by Little unit 7 prints 'Tha i fuar.' (It's cold.) |

### Place-name elements (Gaelic dungeon names)

| Gàidhlig | English | Gender (Wiktionary) | Use |
| --- | --- | --- | --- |
| Uamh | cave | Feminine | noun, adjective after it |
| Cnoc | hill | Masculine | noun, adjective after it |
| Gleann | glen | Masculine | noun, adjective after it |
| Beinn | mountain | Feminine | noun, adjective after it |
| Loch | loch | Unsure | alone only |
| Càrn | cairn | Masculine | noun, adjective after it |
| Bothan | bothy | Masculine | noun, adjective after it |
| Clach | stone | Feminine | noun, adjective after it |
| Dùn | fort | Masculine | noun, adjective after it |
| Tobar | well | Unsure | alone only |
| Eas | waterfall | Masculine | noun, adjective after it |
| Sgùrr | peak | Masculine | noun, adjective after it |
| Allt | burn | Masculine | noun, adjective after it |
| Coire | corrie | Masculine | noun, adjective after it |

| Adjective | Lenited (after a feminine noun) | English |
| --- | --- | --- |
| dubh | dhubh | black |
| geal | gheal | white |
| glas | ghlas | grey |
| ruadh | ruadh | red |
| fada | fhada | long |
| mòr | mhòr | big |
| beag | bheag | small |

Pairs never made: Beinn + dubh.

<!-- END GENERATED TABLES -->
