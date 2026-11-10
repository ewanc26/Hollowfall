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

**In Gàidhlig:** every interface string now has a Gàidhlig rendering — the whole title, pause, settings, controls, journal, standing, character, quests and bag screens, every action label and group heading, every hint, prompt, toast, banner and style rank. Each one carries the status it earned: single words and short phrases put together from a source are **Likely** and are shown; longer sentences and coined technical terms (the tagline, the controls help sentences, the gap captions, the gun names, *dì-bhugachadh* for the debug overlay) are **NeedsReview** and held back for a speaker, so the player sees the English for them until then.

**Stays in English** (and why): every epigraph and every lore note, the floor-complete, boss, opening and ending prose, and everything Eòghann says apart from his short greetings — long free prose, where the risk of a plausible mistake is highest, decided on principle in the tables below. How much story text that is: the test output above prints the count of strings in `StoryBook`. This is the last block of interface English, not a technical one; a speaker who checks the story lines would finish the job.

**Held back (NeedsReview):** the composed sentences named above, plus `hud.floor`, "Làr {0} / {1}" for a dungeon floor, because *làr* is the floor of a room or the ground and whether it suits a level of a dungeon needs a speaker; and `pause.title`, *Air stad*, because Firefox's Pause is the command *Cuir 'na stad* and a heading that names the state is only a guess.

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

- **First:** the Likely rows that are one-word UI labels used as buttons, because the form (imperative, plural, or noun) matters: *Air ais* (Back), *Leum*, *Fosgail*, *Bruidhinn*, *Leugh*, *Caith*, *Gluais air adhart / air ais*, *Iuchraichean {0}*, *Clàr-taice* and *Aisig na bun-roghainnean* (for "Reset to defaults"). *Fàg an-seo* is Firefox's own wording for Quit, so it is Verified, but a speaker should say whether it suits a game's menu.
- *Smachd* as the heading for Controls, and whether the plural *smachdan* is better; *Seasamh* as the name of the Standing screen; and whether *Caith* and *caithte* are the right words for wearing and worn charms and relics.
- *Làr* for a dungeon floor (held back), and whether *lobhta* or another word is better; and the Paused heading *Air stad* (held back: Firefox's Pause is the command *Cuir 'na stad*).
- The composed direction and look phrases: *Gluais air chlì / air dheis*, *Seall suas / sìos*, *Seall air chlì / air dheis*, and *Fosgail agus cleachd*. Each word is sourced, but the phrases themselves are put together by rule, not quoted.
- The other Likely phrases: *Meur-chlàr / luch* for "Keyboard / mouse", *Bioran* for "Spikes", *Ciste Ghlaiste* for "Locked chest", and *CISTE +{0} slàinte* for "Chest +{0} health".
- Which of Seed (*Sìol*), Random, Respawn, Main menu, Quit and the settings labels (sensitivity, dead zone, field of view, volume, render distance, prompts, invert) have an established Gàidhlig term in software and games; the English is shown until they do.
- *brùnaidh* (brownie): not found in any source checked; *gruagach* is used (Likely) because the Gaelic Wikipedia describes a spirit of the cattle, but the brownie sense is reasoned, not quoted.
- Whether the greetings suit Eòghann's voice (Halò, Fàilte, Mòran taing, Tha i fuar, Mar sin leat); each is a sentence or phrase a source prints, but a source is not a speaker.
- The gender of *loch* and *tobar*, and whether Beinn + dubh lenites.
- The two floor-card names, *An Sgòrnan* and *An Doras Deireannach*, and whether *Feuch a-rithist.* suits a death line.
- Every untranslated story line (see the counts above), if translation of the story is wanted at all.

## A request to a Gaelic speaker

If you read Gàidhlig, please look over the tables below and tell us what is wrong, odd, stiff or unnatural: spelling, accents, mutation, word order, a better word, a regional preference, anything. You do not need to be a programmer: a list of the keys and what each should say is enough, in an issue or an email to the author. Corrections are welcome and will be credited if you wish. Thank you; the game takes Gaelic seriously and wants to get it right.

If you would rather correct the strings in a translation tool, run `dotnet run --project src/Hollowfall.Cli -- --po-gd gd.po` and open `gd.po` in Poedit or any gettext editor. Each entry's comment gives its key, status and sources, and an entry marked fuzzy needs a decision before it is shown.

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

Verified 28, Likely 118, NeedsReview 44, no Gàidhlig yet 27.

| Key | English | Gàidhlig | Status | Source | Note |
| --- | --- | --- | --- | --- | --- |
| `menu.play` | Play | Cluich | Verified | WK | cluich is the imperative of the verb cluich, to play |
| `menu.settings` | Settings | Roghainnean | Verified | FF WK | Firefox's own gd word for Settings; roghainn, an option or choice |
| `menu.controls` | Controls | Smachd | Likely | WK | smachd is the noun control or command; used as a heading here, a speaker should say whether the plural smachdan suits better |
| `menu.quit` | Quit | Fàg an-seo | Verified | FF WK | Firefox gd's Quit on Windows and Linux (macOS has 'Fàg' plus the app's name); fàg is leave or quit |
| `menu.back` | Back | Air ais | Likely | WK | air ais is the adverb 'back'; its use on a Back button is not confirmed |
| `menu.reset` | Reset | Aisig | Likely | FF | from Firefox gd's restoration wording (Aisig na bun-roghainnean); the bare noun for a Reset button is not quoted |
| `title.tagline` | A procedural dungeon crawler.\nMove fast. Heal by hurting things. | Sealgair-dhùn air a chruthachadh le àireamh.\nGluais luath. Faigh slàint le leòn. | NeedsReview | WK | composed from sourced words (sealgair, hunter; dùn, fortress; àireamh, number; luath, fast; slàint, health; leòn, wounding); a speaker should rewrite the line |
| `title.seed` | Seed | Sìol | Verified | WK | sìol, seed (first sense) |
| `title.seedHint` | any text or number | teacsa no àireamh sam bith | Likely | WK | teacsa, text; àireamh, number; sam bith, any |
| `title.random` | Random | Air thuaiream | Likely | WK | air thuaiream, at random (idiom); whether it suits a Random button needs a speaker |
| `title.daily` | Daily | Làitheil | Verified | WK | làitheil, daily or everyday (adjective) |
| `title.board` | Verified today | Air a dhearbhadh an-diugh | Likely | WK | dearbh, verify (air a dhearbhadh, verified); an-diugh, today; composed |
| `title.boardNone` | No verified run yet today. | Chan eil ruith dhearbhte ann fhathast an-diugh. | Likely | WK | ruith, a run or race; chan eil … fhathast, there is not … yet; composed |
| `title.boardRow` | Floor {0}: grade {1}, {2} points, {3} s | Làr {0}: inbhe {1}, {2} puing, {3} s | NeedsReview | WK | làr as held back for hud.floor; inbhe as hud.result.rank; puing, point; composed |
| `pause.title` | PAUSED | AIR STAD | NeedsReview | WK FF | held back: Firefox gd's Pause is the command 'Cuir 'na stad'; 'air stad' (at a stop, from stad, a stop or pause) is a guess at a heading that names the state |
| `pause.resume` | Resume | Lean air | Verified | FF WK | Firefox gd's Resume (downloads) is 'Lean air'; Wiktionary lists lean air as continue |
| `pause.respawn` | Respawn | Tilleadh | Likely | WK | tilleadh, the act of returning (till, return); the gaming sense of respawn is coined and needs a speaker |
| `pause.standing` | Standing | Seasamh | Likely | WK | seasamh is the verbal noun of seas, to stand, and is listed as position or attitude; its use as this screen's name is not quoted |
| `pause.character` | Character | Caractar | Likely | WK | caractar, character (the borrowed word); the sense of a game persona needs a speaker |
| `pause.bag` | Bag | Poca | Verified | WK | poca, bag or sack |
| `bag.title` | Bag | POCA | Verified | WK | capitals of poca, bag |
| `bag.slots` | Slots used: {0} of {1} | Àitichean air a chleachdadh: {0} à {1} | NeedsReview | WK | àite, place (àitichean, places) standing in for slot; air a chleachdadh, in use; à, out of; composed |
| `bag.coin` | Coin {0} | Bonn {0} | Verified | WK | bonn, coin (one of its senses); the number sits beside it |
| `bag.none` | Nothing is carried yet. | Chan eil dad air a ghiùlan fhathast. | Likely | WK | dad, anything; giùlan, carrying; composed |
| `bag.wear` | Wear | Caith | Likely | WK | caith, to wear (clothes); the bare verb is the imperative |
| `bag.takeOff` | Take off | Thoir air falbh | Likely | WK | thoir, take (imperative); air falbh, away or off; the phrase for taking clothes off needs a speaker |
| `bag.worn` | worn | caithte | Likely | WK | caithte, worn out or used up, the past participle of caith |
| `bag.ready` | Ready | Deiseil | Verified | WK | deiseil, ready or prepared |
| `bag.readyMark` | ready | deiseil | Verified | WK | same word as bag.ready, lower case for the mark beside an item |
| `bag.readyHint` | The ready item is what the quick-use button spends in a run; only one comes with you. | 'S e an nì deiseil an tè a bhios putan grad-chleachdaidh a' chosgadh ann an ruith; cha tig ach fear leat | NeedsReview | WK | nì, item or thing; deiseil as bag.ready; grad, quick; cosg, spend; ruith, run; composed, a speaker should check it |
| `bag.wearHint` | Worn charms and relics take effect from your next run. | Bidh buaidh aig na seunan agus na taisgeulan caithte bhon ath-ruith. | NeedsReview | WK | buaidh, effect; seun, charm; taisgeul, a relic or treasure (coined here); caithte as bag.worn; composed, a speaker should check it |
| `character.title` | CHARACTER | CARACTAR | Likely | WK | capitals of caractar above |
| `character.none` | No character yet. Defeat something to begin. | Chan eil caractar ann fhathast. Buail rudeigin airson tòiseachadh. | Likely | WK | buail, strike; rudeigin, something; tòiseachadh, beginning; composed |
| `character.level` | Level {0} | Ìre {0} | Verified | WK | ìre, degree or level; the number sits beside it |
| `character.nextLevel` | {0} of {1} experience to the next level | {0} à {1} eòlas chun na h-ath ìre | Likely | WK | eòlas, experience (also knowledge); chun na h-ath ìre, towards the next level; composed |
| `character.maxLevel` | Highest level reached | An ìre as àirde a ràinig thu | Likely | WK | as àirde, highest; ràinig, reached; composed |
| `character.points` | Points to spend: {0} | Puinn ri chaitheamh: {0} | Likely | WK | puing, point (genitive plural puinn); caitheamh, spending (from caith, the verb also used for Wear); composed |
| `character.vigour` | Vigour {0} | Neart {0} | Verified | WK | neart, strength, power or vigour; the number sits beside it |
| `character.vigourHint` | +10 maximum health a point | +10 slàinte as motha airson gach puing | Likely | WK | slàinte, health; as motha, most or maximum; gach, each; composed |
| `character.might` | Might {0} | Cumhachd {0} | Verified | WK | cumhachd, power or might; the number sits beside it |
| `character.mightHint` | +5 percent damage from guns and parries a point | +5 ceudad bho ghunnaichean agus dìon gach puing | NeedsReview | WK | ceudad, per cent; gunna, gun; dìon, defence used for parry; composed, a speaker should check it |
| `character.nextRun` | Points take effect from your next run. A run keeps the stats it began with. | Bidh puingean an gnìomh bhon ath-ruith. Cumaidh an ruith na staitist leis an tòisich e | NeedsReview | WK | gnìomh, action or effect; staitist is the borrowed plural of statistic; tòisich, begin; composed, a speaker should check it |
| `quest.title` | QUESTS | IARRTASAN | Likely | WK | iarrtas, a request or errand (iarrtasan, plural); the quest sense is from games and needs a speaker |
| `quest.none` | No quests yet. Ask among the Folk. | Chan eil iarrtas ann fhathast. Faighnich am measg muinntir an dùin. | NeedsReview | WK | faighnich, ask; am measg, among; muinntir, people; dùin, of the fortress; composed |
| `quest.husks` | Clear the Husks | Falamhaich na Sligich | Likely | WK | falamhaich, empty or clear out (imperative); Slige is the Gàidhlig name of the Husks in docs/mythology.md |
| `quest.spitters` | Clear the Spitters | Falamhaich na Plathairean | Likely | WK | as above; Plathair is the Gàidhlig name of the Spitters in docs/mythology.md |
| `quest.giver` | Given by {0} | Bho {0} | Likely | WK | bho, from; 'given by' reads as 'from' in Gàidhlig |
| `quest.progress` | {0} of {1} defeated | {0} à {1} air an ceannsachadh | Likely | WK | ceannsaich, conquer or subdue; air an ceannsachadh, defeated; composed |
| `quest.reward` | Reward: {0} experience, {1} coin and {2} | Duais: {0} eòlas, {1} bonn agus {2} | Likely | WK | duais, reward or prize; bonn, coin as in bag.coin; composed |
| `quest.take` | Take | Gabh | Likely | WK | gabh, take (imperative); its use on a Take button is not confirmed |
| `quest.taken` | Taken | Air a ghabhail | Likely | WK | gabh, take; air a ghabhail, taken |
| `quest.done` | Finished | Deiseil | Verified | WK | deiseil, ready, prepared or finished |
| `quest.refused` | Refused | Air a dhiùltadh | Likely | WK | diùlt, refuse; air a dhiùltadh, refused |
| `quest.nextRun` | Quests count the kills you make from now on, in every run. | Cunntaidh na h-iarrtanas na marbhadh a nì thu bho seo a-mach, anns gach ruith | NeedsReview | WK | cunnt, count; iarrtas, errand as quest.title; marbhadh, killing; bho seo a-mach, from now on; composed, a speaker should check it |
| `pause.quests` | Quests | Iarrtasan | Likely | WK | plural of iarrtas, an errand, as quest.title |
| `pause.journal` | Journal | Leabhar-latha | Verified | WK | leabhar-latha, diary or journal |
| `pause.mainMenu` | Main menu | A' phrìomh chlàr-taice | Likely | WK | prìomh, main or chief; clàr-taice, menu (as hint.menu); lenited and slenderised after the feminine article; composed |
| `journal.title` | JOURNAL | LEABHAR-LATHA | Verified | WK | capitals of the entry above |
| `journal.notesRead` | {0}   -   Notes read: {1} | {0}   -   Nòtaichean air an leughadh: {1} | Likely | WK | nòta, note; air an leughadh, read (leugh as prompt.read); the shape of the line is kept |
| `journal.empty` | Nothing read yet. Notes lie against the walls of the dungeon: walk up to a scroll and use it. | Chan eil dad air a leughadh fhathast. Tha nòtaichean ri na ballachan anns an dùn: coisich gu sgrìoll agus cleachd e. | NeedsReview | WK | balla, wall; coisich, walk (imperative); sgrìoll, scroll (the borrowed word); cleachd, use (as Interact); composed, a speaker should check it |
| `controls.title` | CONTROLS | SMACHD | Likely | WK | capitals of smachd, control or command; the heading use needs a speaker |
| `controls.help` | Click a binding, then press the new key, button or stick direction. Esc cancels. | Briog air ceangal, agus an uair sin brùth air a' phutan, an iuchair ùr no taobh a' bhata. Sguiridh Esc dheth | NeedsReview | WK | briog, click; ceangal, binding; brùth, press; putan, button; iuchair, key; ùr, new; bata, stick; composed sentence, a speaker should check it |
| `controls.colAction` | Action | Gnìomh | Verified | WK | gnìomh, act or action |
| `controls.colKeys` | Keyboard / mouse | Meur-chlàr / luch | Likely | WK | meur-chlàr, keyboard, and luch, mouse; the slash is kept from the English |
| `controls.colPad` | Gamepad | Gèama-phàd | NeedsReview | WK | a coined compound of gèama, game, and the borrowed pàd, pad; gd has no settled word for gamepad |
| `controls.resetAll` | Reset to defaults | Aisig na bun-roghainnean | Likely | FF | Firefox gd uses this for 'Restore Defaults'; whether it suits 'Reset to defaults' needs a speaker |
| `controls.listenPad` | press a button or move a stick… | brùth putan no gluais bata… | Likely | WK | brùth, press; putan, button; gluais, move (as the movement actions); bata, stick; composed |
| `controls.listenKey` | press a key or click… | brùth iuchair no briog… | Likely | WK | brùth, press; iuchair, key; briog, click; composed |
| `controls.binding` | Binding "{0}". Esc cancels. | Ceangal "{0}". Sguiridh Esc dheth. | NeedsReview | WK | ceangal, a binding or tie; sgir dheth, stop or cancel; the sentence is composed |
| `controls.mouse` | mouse | luch | Verified | WK | luch, mouse, including the computing sense |
| `controls.now` | {0} is now {1}. | 'S e {1} a th' ann an {0} a-nis. | Likely | WK | 's e … a th' ann, it is; a-nis, now; composed |
| `controls.swapUnbound` | {0} was used by {1}, which is now unbound. | Bha {0} aig {1}; tha {1} gun cheangal a-nis. | Likely | WK | gun cheangal, without a binding; composed |
| `controls.swapped` | {0} was used by {1}; the two swapped. | Bha {0} aig {1}; chaidh an dà chur an malairt. | Likely | WK | cuir an malairt, exchange or swap; chaidh … , was …; composed |
| `controls.inUse` | {0} is used by {1}, which cannot take another input. Nothing changed. | Tha {0} aig {1}. Cha do dh'atharraich dad. | Likely | WK | atharraich, change; cha do dh'atharraich dad, nothing changed |
| `controls.reserved` | {0} cannot be used for {1}. | Cha ghabh {0} a chleachdadh airson {1}. | Likely | WK | cha ghabh … a chleachdadh, cannot be used; airson, for; composed |
| `controls.wrongKind` | {0} cannot use {1}. | Cha ghabh {0} {1} a chleachdadh. | Likely | WK | as above, with the binding as the object; composed |
| `controls.same` | That binding is already set. | Tha an ceangal seo ann mu thràth. | Likely | WK | mu thràth, already; composed |
| `controls.resetDone` | Bindings reset to defaults. | Chaidh na ceanglaichean air ais dhan bun-roghainnean | NeedsReview | WK | chaidh … air ais, was put back; bun-roghainnean as controls.resetAll; composed, a speaker should check it |
| `group.movement` | MOVEMENT | GLUASAD | Likely | WK | gluasad is the verbal noun of gluais, move |
| `group.look` | LOOK | SEALLADH | Verified | WK | sealladh, a look or glance (noun) |
| `group.combat` | COMBAT | COGADH | Likely | WK | cogadh, war or battle; the heading sense needs a speaker |
| `group.portals` | PORTALS | PORTAILEAN | NeedsReview | WK | portail is the naturalised borrowed word for portal (portailean, plural); the loan needs a speaker's blessing |
| `group.system` | SYSTEM | SIOSTAM | Verified | WK | siostam, system |
| `action.moveForward` | Move forward | Gluais air adhart | Likely | WK | gluais (move) + air adhart (forward) |
| `action.moveBack` | Move back | Gluais air ais | Likely | WK | gluais (move) + air ais (back) |
| `action.moveLeft` | Move left | Gluais air chlì | Likely | WK | gluais (move) + air chlì (on the left); clì is left. The phrase is composed, so a speaker should check it |
| `action.moveRight` | Move right | Gluais air dheis | Likely | WK | gluais (move) + air dheis (on the right); deas is right, lenited dheis. The phrase is composed |
| `action.lookUp` | Look up | Seall suas | Likely | WK | seall (look, the imperative) + suas (up) |
| `action.lookDown` | Look down | Seall sìos | Likely | WK | seall (look, the imperative) + sìos (down) |
| `action.lookLeft` | Look left | Seall air chlì | Likely | WK | seall (look) + air chlì (on the left); composed, so a speaker should check it |
| `action.lookRight` | Look right | Seall air dheis | Likely | WK | seall (look) + air dheis (on the right); composed, so a speaker should check it |
| `action.jump` | Jump | Leum | Likely | WK | leum, to jump; the bare verb is the imperative |
| `action.firePrimary` | Fire revolver | Losg a' ghunna-thionndaidh | NeedsReview | WK | losg, fire or shoot; gunna-thionndaidh is a coined compound, gun plus tionndaidh, turning, for the revolver; needs a speaker |
| `action.fireSecondary` | Fire shotgun | Losg a' ghunna-sgaoilidh | NeedsReview | WK | losg, fire or shoot; the coined gunna-sgaoilidh as hint.shotgun; composed |
| `action.parry` | Parry | Dìon | Likely | WK | dìon as hint.parry; the bare noun is the imperative |
| `action.portalTeal` | Teal portal | Portail uaine-gorm | NeedsReview | WK | portail as group.portals; uaine-gorm, turquoise; composed |
| `action.portalViolet` | Violet portal | Portail purpaidh | Likely | WK | as hint.violetPortal; composed |
| `action.respawn` | Respawn | Tilleadh | Likely | WK | as pause.respawn; composed |
| `action.flyToggle` | Free-fly | Eitealachd shaor | NeedsReview | WK | itealachd, flying (itealaich, to fly); saor, free; a coined compound for the debug free-fly; needs a speaker |
| `action.flyDown` | Fly down | Itealaich sìos | Likely | WK | itealaich, fly (the imperative); sìos, down (as Seall sìos); composed |
| `action.debugInfo` | Debug overlay | Dì-bhugachadh | NeedsReview | WK | a borrowed-word coinage modelled on English debugging; whether gd software uses it needs checking |
| `action.interact` | Open and use | Fosgail agus cleachd | Likely | WK | fosgail (open) + agus (and) + cleachd (use), both bare verbs used as imperatives |
| `action.closePortals` | Close portals (sever) | Dùin na portailichean (gèarr) | NeedsReview | WK | dùin, close; portailichean, the loan's plural; gèarr as hint.sever; composed |
| `action.pause` | Pause menu | Clàr-taice stad | Likely | WK | clàr-taice as hint.menu; stad as pause.title; composed |
| `action.slam` | Slam | Bualadh | Likely | WK | bualadh, a striking (buail, to strike); the slamming sense needs a speaker |
| `action.fireMortar` | Fire block mortar | Losg am moirtiar-bloca | NeedsReview | WK | moirtiar is the borrowed word for a mortar (the weapon is not in the dictionaries); bloca, block; coined; needs a speaker |
| `action.useItem` | Use quick item | Cleachd an nì grad | Likely | WK | cleachd, use (as Interact); nì, item or thing; grad, quick; composed |
| `settings.title` | SETTINGS | ROGHAINNEAN | Verified | FF | capitals of Firefox gd's 'Roghainnean' |
| `settings.language` | Language | Cànan | Verified | FF WK | Firefox gd's 'Language' header is Cànan; cànan, a language |
| `settings.mouseSensitivity` | Mouse sensitivity | Mothachalachd na luch | Likely | WK | mothachalachd, sensitivity (technical gd); luch, mouse as controls.mouse; composed |
| `settings.padLookSpeed` | Gamepad look speed | Luaths seallaidh a' gheama-phàid | NeedsReview | WK | luaths, speed; sealladh, look (as the look actions); the coined gèama-phàd; composed |
| `settings.deadzone` | Stick dead zone | Raon marbh a' bhata | NeedsReview | WK | raon, zone or field; marbh, dead; bata, stick; a word-for-word coinage |
| `settings.fov` | Field of view | Raon an t-seallaidh | Likely | WK | raon, field; sealladh, view; composed |
| `settings.uiSize` | Interface size | Meud an eadar-aghaidh | Likely | WK | meud, size; eadar-aghaidh, interface (the usual gd software word); composed |
| `settings.autoScale` | Scale the interface with the window | Sgèil an eadar-aghaidh leis an uinneag | NeedsReview | WK | sgèil, scale; uinneag, window; the sentence is composed |
| `settings.master` | Master volume | Prìomh-fhuaim | Likely | WK | prìomh, main or master (as the main menu); fuaim, sound; composed |
| `settings.effects` | Effects volume | Fuaim nan èifeachd | Likely | WK | èifeachd, effect (èifeachd, plural); composed |
| `settings.ambience` | Ambience volume | Fuaim na h-àrainne | Likely | WK | àrainn, surroundings or environment (àrainne in the genitive); composed |
| `settings.music` | Music volume | Fuaim na ciùil | Likely | WK | ceòl, music (ciùil in the genitive); composed |
| `settings.viewDistance` | Render distance | Astar-taisbeanaidh | Likely | WK | astar, distance; taisbean, show or display; a compound for render distance |
| `settings.chunks` | {0} chunks | {0} blocaichean | Likely | WK | bloca, block (blocaichean, plural) standing in for chunk, for which gd has no word |
| `settings.ghost` | Show the ghost of your best run | Seall taibhse an ruith as fheàrr agad | Likely | WK | taibhse, ghost or spectre; ruith, run; as fheàrr, best; composed |
| `settings.prompts` | Controller prompts | Comharran an t-smachdair | NeedsReview | WK | comharra, sign or mark (the button labels); smachdar, a coined controller after smachd; needs a speaker |
| `settings.promptsAuto` | Auto (follow controller) | Fèin (a-rèir an t-smachdair) | Likely | WK | fèin, self, the usual gd prefix for automatic; a-rèir, according to; needs a speaker |
| `settings.invertMouse` | Invert mouse Y | Tionndaidh Y na luch | Likely | WK | tionndaidh, turn about (imperative); the technical gd for Invert; composed |
| `settings.invertPad` | Invert gamepad Y | Tionndaidh Y a' gheama-phàid | NeedsReview | WK | as above with the coined gèama-phàd |
| `hud.floor` | Floor {0} / {1} | Làr {0} / {1} | NeedsReview | WK | làr is the floor of a room or the ground; whether it fits a level of a dungeon (or lobhta, a storey) needs a speaker, so it is held back |
| `hud.keys` | Keys {0} | Iuchraichean {0} | Likely | WK | iuchraichean is the plural of iuchair, key; the number sits beside it, so no count agreement is needed |
| `hud.result.title` | FLOOR {0} COMPLETE | LÀR {0} DEISEIL | Likely | WK | làr as held back for hud.floor; deiseil as quest.done, finished |
| `hud.result.rank` | RANK  {0} | INBHE  {0} | Verified | WK | inbhe, rank or status; capitals for the banner and two spaces kept before the number |
| `hud.result.stats` | Time {0}    Kills {1}    Style {2} | Ùine {0}  Marbhadh {1}  Stoidhle {2} | Likely | WK | ùine, time; marbhadh, killing; stoidhle, style; the spacing is kept |
| `hud.result.restarts` | Restarts {0}  (−{0} points) | Ath-thòiseachaidh {0}  (-{0} puing) | Likely | WK | ath-thòiseachadh, starting again; puing, point |
| `toast.omen` | A washerwoman wrings out her cloth nearby. Something is coming. | Tha bean-nighe faisg air làimh. Tha rudeigin a' tighinn | NeedsReview | WK | bean-nighe, the washerwoman of folklore; faisg air làimh, nearby; Tha rudeigin a' tighinn, something is coming; the wringing is dropped, a speaker should check the line |
| `toast.boon` | The brownie warms the hearth for you (+{0} health) | Tha am brùnaidh a' blàthachadh an làr-teine dhut (+{0} slàinte) | NeedsReview | WK | brùnaidh, brownie (as docs/mythology.md); blàth, warm (a' blàthachadh, warming); làr-teine, hearth; composed, a speaker should check the idiom |
| `standing.better` | {0} think better of you: {1} | Tha {0} measail ort tuilleadh: {1} | Likely | WK | measail ort, fond of you; tuilleadh, more; composed |
| `standing.worse` | {0} now regard you as {1} | Tha {0} a-nis 'gad mheas mar {1} | Likely | WK | meas, regard (agad mheas, regarding you); mar, as; composed |
| `standing.title` | STANDING | SEASAMH | Likely | WK | capitals of seasamh, position or standing; the screen-name sense needs a speaker |
| `standing.subtitle` | How the dungeon's peoples regard you. Hurt a creature in front of its kin and they will remember. | Mar a tha muinntir an dùin 'gad fhaicinn. Ma leòn thu creutair air beulaibh a chinneadh, chuimhnich iad | NeedsReview | WK | muinntir an dùin, the fortress's people; 'gad fhaicinn, seeing you; leòn, wound or hurt; creutair, creature; beulaibh, in front of; cinneadh, kin; chuimhnich, will remember; composed |
| `standing.nobody` | Nobody here has an opinion of you yet. | Chan eil beachd aig neach sam bith ort fhathast. | Likely | WK | beachd, opinion; neach sam bith, anyone; composed |
| `standing.recent` | RECENT | ÙR | NeedsReview | WK | ùr, new; a short heading for the recent changes, and whether a speaker would use it here is untested |
| `standing.nothingYet` | Nothing yet. | Chan eil dad. | Likely | WK | chan eil dad, there is nothing |
| `gap.edge` | A gap. Fire a portal across it ({0}), then another beside you ({1}), and step through. | Beàrn. Losg portail tarsainn air ({0}), agus fear eile ri do thaobh ({1}), agus coisich troimhe. | NeedsReview | WK | beàrn, gap; losg, fire or shoot (imperative); tarsainn air, across; fear eile, another one; ri do thaobh, beside you; coisich, walk; composed, a speaker should check it |
| `gap.alcove` | A high alcove. Fire a portal up into it ({0}), then another beside you ({1}), and step through. | Àite àrd. Losg portail suas ann ({0}), agus fear eile ri do thaobh ({1}), agus coisich troimhe. | NeedsReview | WK | suas ann, up into it; the alcove is described as a high place, not named; composed |
| `gap.afterFall` | You are set back at the edge. Fire a portal across the gap instead ({0}, then {1}). | Chaidh do chur air ais dhan oir. Losg portail tarsainn air a' bheàrn ({0}, an uairsin {1}). | NeedsReview | WK | chaidh do chur air ais, you were put back; oir, edge; an uairsin, then; composed |
| `gap.fellBare` | FELL  -{0} | THUIT  -{0} | Verified | WK | thuit is the past tense of tuit, to fall; two spaces kept before the number |
| `gap.spikes` | SPIKES  -{0} | BIORAN  -{0} | Likely | WK | bioran is the plural of bior, spike or prong |
| `gap.splash` | SPLASH  climbing out | PLESG  a' dìreadh a-mach | NeedsReview | WK | plesg is coined for splash (nothing in the dictionaries); a' dìreadh a-mach, climbing out |
| `hint.revolver` | revolver | gunna-chuartach | NeedsReview | WK | a coined compound, gunna, gun, plus cuairt, a turn, for the revolving gun; needs a speaker |
| `hint.shotgun` | shotgun | gunna-sgaoilidh | NeedsReview | WK | a coined compound, gunna plus sgaoil, scatter, for the shotgun; needs a speaker |
| `hint.parry` | parry | dìon | Likely | WK | dìon, defence; the fencing sense of parry needs a speaker |
| `hint.tealPortal` | teal portal | portail uaine-gorm | NeedsReview | WK | portail as group.portals; uaine-gorm, turquoise (uaine, green, + gorm, blue); composed |
| `hint.violetPortal` | violet portal | portail purpaidh | Likely | WK | purpaidh, violet or purple; composed with portail |
| `portal.mark.chevron` | chevron | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `portal.mark.diamond` | diamond | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `portal.mark.bar` | bar | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `hint.sever` | sever | gèarr | Likely | WK | gèarr, cut (imperative); sever is read as cutting the link |
| `hint.open` | open | fosgail | Likely | WK | fosgail, open; the bare verb is the imperative |
| `hint.jump` | jump | leum | Likely | WK | leum, to jump |
| `hint.menu` | menu | clàr-taice | Likely | DRU | a software translation of 'Menu'; not in the dictionaries checked |
| `prompt.read` | {0}  read: {1} | {0}  leugh: {1} | Likely | WK | leugh, read (the imperative; Wiktionary quotes 'Leugh seo', Read this) |
| `prompt.speak` | {0}  speak | {0}  bruidhinn | Likely | WK | bruidhinn, to speak or talk; the bare verb is the imperative |
| `cat.warmth` | The fire is warm, and you feel a little better. | Tha an teine blàth, agus tha thu nas fheàrr beagan | NeedsReview | WK | blàth, warm; nas fheàrr beagan, a little better; composed |
| `prompt.closeHint` | {0} or {1} to close | {0} no {1} airson dùineadh | Likely | WK | airson, for the purpose of; dùineadh, closing (dùin, close); composed |
| `prompt.leaveHint` | {0} or {1} to leave | {0} no {1} airson fàgail | Likely | WK | fàgail, leaving (fàg as Quit); composed |
| `banner.died` | YOU DIED | THA THU MARBH | Likely | WK | Tha thu (you are) + marbh, dead |
| `banner.respawn` | {0} to respawn | {0} airson tilleadh | Likely | WK | tilleadh as pause.respawn; airson, for |
| `banner.enemies` | Enemies {0}: kill them to open the doors | Nàimhdean {0}: marbh iad gus na dorsan fhosgladh | Likely | WK | nàimhdean, enemies; marbh, kill (imperative); gus … fhosgladh, to open; dorsan, doors (as An Doras Deireannach); composed |
| `banner.clear` | CLEAR | FALAMH | Likely | WK | falamh, empty; capitals for the banner, the cleared sense needs a speaker |
| `banner.floorDone` | FLOOR {0} COMPLETE  {1}  {2} | LÀR {0} DEISEIL  {1}  {2} | Likely | WK | as hud.result.title; the spacing is kept |
| `banner.floorStats` | time {0}  kills {1}  style {2} | ùine {0}  marbhadh {1}  stoidhle {2} | Likely | WK | as hud.result.stats, lower case |
| `banner.firstClear` | first clear | a' chiad fhalamhadh | NeedsReview | WK | a' chiad, the first; fhalamhadh, the clearing of (from falamh); a coinage for the banner |
| `banner.newBest` | NEW BEST  (was {0}) | AS FHEÀRR ÙR  (bha {0}) | Likely | WK | as fheàrr, best; ùr, new; bha, it was; composed |
| `banner.best` | best {0} | as fheàrr {0} | Likely | WK | as fheàrr, best; the number sits beside it |
| `banner.key` | KEY | IUCHAIR | Likely | WK | iuchair, key |
| `banner.chest` | CHEST | CISTE | Verified | WK | ciste, chest or box; capitals for the banner |
| `banner.lockedChest` | LOCKED CHEST | CISTE GHLAISTE | Likely | WK GW | ciste (feminine) + glaiste, locked; the adjective is lenited after a feminine noun |
| `banner.chestHealed` | CHEST  +{0} health | CISTE  +{0} slàinte | Likely | WK | ciste, chest, and slàinte, health; composed for the banner |
| `banner.lockedChestHealed` | LOCKED CHEST OPENED  +{0} health | CISTE GHLAISTE AIR FHOSGLADH  +{0} slàinte | Likely | WK | as banner.lockedChest plus air fhosgladh, opened; slàinte as banner.chestHealed |
| `banner.item` | {0}  +{1} to your bag | {0}  +{1} dhan phoca agad | Likely | WK | dhan phoca agad, into your bag (poca as pause.bag); composed |
| `banner.bagFull` | {0}  your bag is full | {0}  tha do phoca làn | Likely | WK | tha … làn, is full; composed |
| `banner.locked` | locked: find the key on this floor | glaiste: faigh an iuchair air an làr seo | Likely | WK | glaiste as banner.lockedChest; faigh, get or find (imperative); air an làr seo, on this floor; composed |
| `banner.spikes` | SPIKES  -{0} | BIORAN  -{0} | Likely | WK | bioran is the plural of bior, spike or prong |
| `banner.noPortal` | can't place a portal there ({0}) | chan urrainn dhut portail a chur an sin ({0}) | Likely | WK | cha(n) urrainn dhut, you cannot; a chur an sin, to put it there; composed |
| `banner.meteor` | METEOR | MEATAN | Likely | WK | meatan, meteor; capitals for the banner |
| `banner.meteor2` | METEOR x2 | MEATAN x2 | Likely | WK | as banner.meteor; the x2 is kept |
| `pact.title` | THE DUNGEON OFFERS A PACT | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.strength` | its strength follows your style rank | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.boon` | Boon | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.burden` | Burden | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.decline` | Take none | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.hint` | {0} to take one, {1} to leave them | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.bloodsworn.name` | Bloodsworn | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.bloodsworn.boon` | Parries heal 20 more and the spent parry window's cooldown is refunded | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.bloodsworn.burden` | Hard damage takes half of damage taken, up from 35% | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.hollowHeart.name` | Hollow Heart | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.hollowHeart.boon` | Blood heals 1.5x | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.hollowHeart.burden` | Maximum health 30 lower | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.gravedigger.name` | Gravedigger | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.gravedigger.boon` | Slam shockwaves reach 1.5 blocks further and break floors in one hit | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.gravedigger.burden` | Style decays 20% faster | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.showman.name` | Showman | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.showman.boon` | All style points 25% higher | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.showman.burden` | Weapons go stale 40% faster | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.twinGate.name` | Twin Gate | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.twinGate.boon` | A third portal slot: the newest rotates in, the oldest rotates out | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.twinGate.burden` | Placing a portal costs 5 health while below Brutal | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.openHand.name` | Open Hand | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.openHand.boon` | Portal exits lend extra speed for a moment | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `pact.openHand.burden` | Revolver damage 25% lower | (English shown) | Untranslated |  | no verified Gàidhlig; English is shown |
| `rank.rowdy` | ROWDY | RUGDA | Likely | WK | rugda, rough or rude; the rowdy sense needs a speaker |
| `rank.fierce` | FIERCE | FIADHAICH | Verified | WK | fiadhaich, fierce or wild |
| `rank.brutal` | BRUTAL | BRÙIDEIL | Verified | WK | brùideil, brutal or cruel |
| `rank.savage` | SAVAGE | FIADHACH | Likely | WK | fiadhach, savage; kept apart from FIADHAICH above, which a speaker should confirm |
| `rank.merciless` | MERCILESS | MI-THROCAIREACH | Likely | WK | mi-throcaireach, merciless (trocair, mercy) |
| `rank.relentless` | RELENTLESS | DIAN | Likely | WK | dian, intense, firm or relentless |
| `rank.apocalyptic` | APOCALYPTIC | APACAILIPT | NeedsReview | WK | coined from apacailiops, an apocalypse; the form and the adjective's earlier h need a speaker |
| `rank.legendary` | LEGENDARY | SGEULACH | NeedsReview | WK | sgeulach, of the tales (sgeul, a tale); whether it carries the English sense needs a speaker |

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
