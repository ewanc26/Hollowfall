# Gaeilge in Hollowfall

Hollowfall's interface can be shown in **Gaeilge** (Irish), on its own or with the English beside it, alongside the Gàidhlig described in [docs/gaidhlig.md](gaidhlig.md). A cat called **Aodhán** also lives beside one fire pit in each world and speaks Irish whatever the setting is. He is named for a person, a gift: *Aodhán* is the Irish name (a pet form of *Aodh*, "fire") usually glossed as "little fire", and it is the Irish form of the name Aidan. This page lists every Irish string the game uses, where it comes from, and how sure we are.

**The interface table (below) was assembled the same way the Gàidhlig one was: by an AI working from dictionaries, Firefox's Gaeilge and word lists, with no fluent speaker's review.** Every entry carries a status and a cited source: *Verified* means a dictionary gives exactly the word with this meaning, *Likely* means it is put together from words a dictionary gives (and is shown in the game), and *NeedsReview* means it is a coined technical term or a composed sentence that stays behind its English until a speaker checks it. **The game should not be treated as finished Gaeilge until a fluent speaker has gone through the table.**

## How the language setting works

- Settings has a **Language** row at the top, naming each language in itself: `English`, `Gàidhlig`, `Gàidhlig agus Beurla`, `Gaeilge`, `Gaeilge agus Béarla`. Changing it rebuilds every screen at once and is saved with the other settings.
- **Gaeilge** shows the Gaeilge where there is some fit to show, and the English where there is not. **Gaeilge agus Béarla** shows the Gaeilge with the English in brackets for short single-line text and beneath it for longer text.
- The interface is the only area translated; the story prose (the epigraphs, the lore notes, the boss and ending text) and the Gàidhlig dungeon names stay as they are.
- `dotnet run --project src/Hollowfall.Cli -- --po-ga gd-ga.po` writes the whole interface as a gettext catalogue for an Irish translator, with each key's status and sources in the comments and fuzzy flags on the strings that need a decision.

## Rules the game follows

- Irish text is written with the **fada** (acute accent): á é í ó ú, and capitals Á É Í Ó Ú. The game's fonts (Chakra Petch and Rajdhani) draw all ten; the smoke test checks every one with `HasChar` in all four faces the game loads, and the renders were looked at. The dialogue validator also accepts the capitals.
- **Mutations** (séimhiú, lenition, and urú, eclipsis) are only used where a dictionary shows them: *tine bheag* (feminine noun, so the adjective is lenited), *oíche mhaith*, *a chara* (vocative), *maith thú*. No urú form appears, because none could be checked in context.
- Every Irish line is followed by its **English** in brackets.
- The name is written **Aodhán** exactly (not Aodhan, not Aidan) and is spoken by nobody else. A unit test fails if it appears anywhere in code except the cat's own dialogue file, or in any doc except this one and `docs/story.md`.
- The cat is a **gift**; his words are meant to be kind, small and clear.

## Status key

| Status | Meaning |
| --- | --- |
| Verified | The exact word or phrase (or the exact pattern, noted below) is printed in a dictionary or in at least two independent learner references. |
| Likely | Built from verified pieces in an ordinary way (a verified phrase plus a verified vocative, or a verified pattern with a different pronoun) but the exact combination was not found in a source. |
| NeedsReview | Not confident. None at present. |

## Every Irish string

Used in the cat's lines unless noted. "FGB" is the *Foclóir Gaeilge–Béarla* (Ó Dónaill, 1977) as shown on teanglann.ie.

| Irish | English | Status | Source and note |
| --- | --- | --- | --- |
| Dia duit | Hello (literally "God to you") | Verified | FGB *Dia*: "(Go mbeannaí) D~ duit, daoibh! God save you!"; Wiktionary *Dia duit* ("hello"; also written *Dia dhuit*). First greeting. |
| Dia is Muire duit | God and Mary to you (the reply to *Dia duit*) | Likely | Not in the FGB *Dia* entry. Given as the standard reply by Wikibooks "Irish/Lesson One" and the Transparent Language Irish blog (also written *Dia 's Muire duit*). Appears only as a choice the player can pick. |
| Fáilte | Welcome | Verified | FGB *fáilte*, sense 2 "Welcome". |
| Fáilte, a chara | Welcome, friend | Likely | *Fáilte* and the vocative *a chara* are each verified; the pair is not quoted in a source. |
| Fáilte romhat | Welcome (you are welcome) | Verified | FGB: "Fáilte romhat, you are welcome" (under *roimh*). |
| Tá fáilte romhat | You are welcome | Verified | FGB *fáilte*: "'Tá ~ romhat!' 'You're welcome!'" |
| Tá sé fuar | It is cold | Verified | FGB *fuar*: "Tá sé ~ fliuch, it is cold and wet"; "Tá sé fuar i gceart, it is quite cold". Focal an Lae no. 327 gives "Tá sé fuar inniu". |
| Teas na tine | The warmth of the fire | Verified | FGB *teas* (masculine): "~ na gréine, na tine, the heat of the sun, of the fire". |
| Maith thú | Good for you | Verified | FGB *maith*: "~ thú, (i) good for you, (ii) thanks". |
| Suigh láimh liom | Sit beside me | Verified | FGB *suigh*: "Suigh láimh liom, sit at my hand, beside me." |
| Suigh láimh liom, a chara | Sit beside me, friend | Likely | The verified phrase plus the verified vocative *a chara*. Used in the warm line. |
| Tá sé teolaí | It is warm and cosy | Likely | FGB *te* and *teolaí*: "Tá an leaba te teolaí, the bed is warm and comfortable". Same pattern, with *sé* in place of the bed. |
| A chara | Friend (vocative) | Verified | FGB *cara*: "A chara, my friend". |
| Tine bheag | A little fire | Verified | FGB *tine* (feminine): "~ bheag, (i) little fire, (ii) small open-air fire". Also the meaning of the cat's name. |
| Slán leat | Goodbye (said to the one who leaves) | Verified | FGB *slán*: "~ agat! ~ leat! Good-bye!" The cat stays, so he says *leat*; *slán agat* is said to the one who stays. |
| Oíche mhaith agat | Good night | Verified | FGB *maith*: "Oíche mhaith agat, (I bid you) good night". |
| Codladh sámh duit | Sleep well | Verified | FGB (under *do*): "Codladh sámh duit, may you sleep well". |

**Counts:** 17 strings in the table: **13 Verified, 4 Likely, 0 NeedsReview.** The four Likely ones are *Dia is Muire duit*, *Fáilte, a chara*, *Suigh láimh liom, a chara* and *Tá sé teolaí*.

### The name

*Aodhán*: an Irish male name, a diminutive of *Aodh* (from Old Irish *Áed*, "fire"), commonly glossed "little fire"; Aidan is its anglicised form. Sources: Wikipedia "Aodhán" and "Aidan"; Behind the Name. Wiktionary gives the derivation (Old Irish *Aédán*, *Aodh* plus the suffix *-án*) but not a gloss. Status: Verified for the derivation, Likely for the exact gloss "little fire" (the sources agree, but some give "fiery one").

## Independent re-check (21 October 2026)

A second pass read the Ó Dónaill dictionary (*Foclóir Gaeilge–Béarla*) entries directly on teanglann.ie, instead of relying on summaries. It confirmed these exact lines and found nothing wrong. It is still not a fluent speaker's review.

| Irish | Result | Source |
| --- | --- | --- |
| *Oíche mhaith agat* | Confirmed: "(On taking leave, retiring) Oíche mhaith agat, (I bid you) good night." | [FGB: maith](https://www.teanglann.ie/en/fgb/maith) |
| *Maith thú* | Confirmed: "(Praise, thanks) ~ thú, (i) good for you, (ii) thanks." | [FGB: maith](https://www.teanglann.ie/en/fgb/maith) |
| *Suigh láimh liom* | Confirmed, under the entry for *lámh*: "Suigh láimh liom, sit at my hand, beside me." | [FGB: suigh](https://www.teanglann.ie/en/fgb/suigh) |
| *Tine bheag* | Confirmed: "~ bheag, (i) little fire, (ii) small open-air fire"; *tine* is feminine, which is why *bheag* is lenited. | [FGB: tine](https://www.teanglann.ie/en/fgb/tine) |
| *Slán leat* | Confirmed: "~ agat! ~ leat! Good-bye!" The dictionary does not say who each is said to; the usual rule (*slán leat* to the one who is leaving, *slán agat* to the one who stays) comes from learner usage, not from this entry. | [FGB: slán](https://www.teanglann.ie/en/fgb/slán) |
| *Codladh sámh duit* | Confirmed, under *do*: "Codladh sámh duit, may you sleep well." | [FGB: codladh](https://www.teanglann.ie/en/fgb/codladh) |
| *Dia is Muire duit* | Given as the reply to *Dia duit* by several learner references; spellings vary (*Dia 's Muire duit*, and *dhuit* in older spelling). It stays Likely, since the dictionary entry checked does not print it. | [Wikibooks: Irish, Lesson One](https://en.wikibooks.org/wiki/Irish/Lesson_One), [Transparent Language](https://blogs.transparent.com/irish/?p=4063) |
| The meaning of the name *Aodhán* | A diminutive (*-án*) of *Aodh*, "fire", often glossed "little fire". That gloss is the popular one, repeated by name sites without a dictionary; a more careful wording is "a diminutive of *Aodh*, fire". Name sites also say "little fiery one". | [Wikipedia: Aodh (given name)](https://en.wikipedia.org/wiki/Aodh_(given_name)) |

What this changes: nothing in the game needed fixing. Six of the thirteen Verified phrases were re-read in the dictionary itself and all matched. The one source-quality point is the name's meaning, which the doc above states a little more firmly than the sources do; read "little fire" as a popular gloss.

## What could not be checked

- Whether each phrase sounds natural in a particular county or dialect. The standard (*Caighdeán Oifigiúil*) spellings are used. Munster, Connacht and Ulster speakers may prefer other greetings (for example *Dia dhuit* in the older spelling, or *Cén chaoi a bhfuil tú?*).
- Whether the fragment *Tine bheag. Teas na tine. Suigh láimh liom, a chara.* reads as poetry or as a list. It is three verified pieces set side by side, without verbs between them, on purpose.
- Pronunciation and stress; the game does not speak the words aloud.
- *Dia is Muire duit* is widely taught but the dictionary entry checked did not print it.
- The phrase *Go n-éirí leat* ("good luck") was considered and left out because its mutation (urú) and exact register could not be confirmed here.

## A request for a fluent speaker

If you speak Irish, thank you for looking at this. The most useful things to check, in order:

1. The **warm line** (spoken on the third visit): *Tine bheag. Teas na tine. Suigh láimh liom, a chara.* Is it kind and natural? Would you change a word?
2. The two **greetings** (*Dia duit*, *Fáilte, a chara*) and the reply a player can choose (*Dia is Muire duit*).
3. The farewells: *Slán leat*, and *Oíche mhaith agat. Codladh sámh duit.*
4. *Tá sé fuar*, *Teas na tine*, *Tá sé teolaí*, *Maith thú*.
5. Whether a sleepy cat would say any of these differently. All of them live in one file, `src/Hollowfall.Core/Story/Dialogue/CatDialogue.cs`, so a correction is a one-line edit; please send the corrected line and I will update this table.

## The interface in Gaeilge

Generated from the data by `IrishDocsTests` (run with `HOLLOWFALL_WRITE_DOCS=1` to rewrite it); do not edit by hand. Statuses and sources follow the same rules as the cat's lines above: **FGB** is the Foclóir Gaeilge–Béarla on teanglann.ie, **WKI** Wiktionary's Irish entries, **FFI** Firefox as Gaeilge.

<!-- BEGIN GENERATED IRISH TABLES -->

| Key | English | Gaeilge | Status | Source | Note |
| --- | --- | --- | --- | --- | --- |
| `menu.play` | Play | Imir | Verified | FGB | FGB imir: imir cluiche, to play a game. The imperative used as the button word |
| `menu.settings` | Settings | Socruithe | Verified | FFI FGB | Firefox as Gaeilge's word for Settings; FGB socruithe, arrangements or settings |
| `menu.controls` | Controls | Rialú | Likely | FGB | FGB rialú, the act of controlling; whether the plural rialtáin suits a heading needs a speaker |
| `menu.quit` | Quit | Scoir | Likely | FGB | FGB scoir, to quit or leave; the imperative on a button is not confirmed |
| `menu.back` | Back | Ar ais | Likely | FGB | FGB ar ais, back or again; the use on a Back button is composition |
| `menu.reset` | Reset | Athshocraigh | NeedsReview | FGB | coined: ath- plus socraigh (FGB socruithe), re-set; a speaker should confirm the term |
| `title.tagline` | A procedural dungeon crawler.
Move fast. Heal by hurting things. | Fáil ar a dhún a ghintear le huimhir.
Bí gasta. Faigh sláinte trí ghortú. | NeedsReview | FGB WKI | composed from sourced words (dún, fortress; gin, generate; uimhir, number; gasta, quick; sláinte, health; gortú, hurting); a speaker should rewrite the line |
| `title.seed` | Seed | Síol | Verified | WKI | síol, seed (first sense) |
| `title.seedHint` | any text or number | aon téacs nó uimhir | Likely | FGB WKI | aon, any; téacs, text; uimhir, number; composed |
| `title.random` | Random | Fánach | NeedsReview | FGB | FGB fánach, wild or random; go fánach is 'at random'; whether the adjective alone suits a button needs a speaker |
| `title.daily` | Daily | Laethúil | Likely | FGB | FGB laethúil, daily; whether the bare adjective suits a button needs a speaker |
| `title.board` | Verified today | Deimhnithe inniu | Likely | FGB | deimhnithe, verified or confirmed; inniu, today; composed |
| `title.boardNone` | No verified run yet today. | Níl aon rith deimhnithe fós inniu. | Likely | FGB | rith, a run; fós, yet; composed |
| `title.boardRow` | Floor {0}: grade {1}, {2} points, {3} s | Urlár {0}: grád {1}, {2} pointí, {3} s | Likely | FGB WKI | urlár, floor or storey — the word FGB gives for a floor of a building; grád, grade; pointí, points |
| `pause.title` | PAUSED | AR SOS | Likely | FGB | FGB sos, a pause; ar sos, on pause, from learner usage rather than a quoted heading — a speaker should check ar shos |
| `pause.resume` | Resume | Lean ar aghaidh | Likely | FGB | FGB lean ar aghaidh, to continue; composed as a button word |
| `pause.respawn` | Respawn | Athbheochan | NeedsReview | FGB | FGB athbheochan, a reviving or resurrection; the gaming respawn sense is coined and needs a speaker |
| `pause.standing` | Standing | Stádas | Likely | FGB | FGB stádas, status or standing; the screen-name use is composition |
| `pause.character` | Character | Carachtar | Verified | FGB | FGB carachtar, character (a person in a book or play) |
| `pause.bag` | Bag | Mála | Verified | FGB | FGB mála, a bag or sack |
| `bag.title` | Bag | MÁLA | Verified | FGB | capitals of mála above |
| `bag.slots` | Slots used: {0} of {1} | Áiteanna in úsáid: {0} as {1} | NeedsReview | FGB | áit, a place, standing in for slot, for which Irish games have no settled word; in úsáid, in use; as, out of |
| `bag.coin` | Coin {0} | Bonn {0} | Verified | FGB | FGB bonn, a coin (one of its senses); the number sits beside it |
| `bag.none` | Nothing is carried yet. | Níl aon rud á iompar fós. | Likely | FGB | iompar, carrying; fós, yet; composed |
| `bag.wear` | Wear | Caith | Likely | FGB | FGB caith, to wear (clothes); the bare verb used as the imperative |
| `bag.takeOff` | Take off | Bain | Likely | FGB | FGB bain de, to take off (clothes); the bare bain as a button is composition |
| `bag.worn` | worn | caite | Likely | FGB | FGB caite, worn; the past participle of caith above |
| `bag.ready` | Ready | Réidh | Verified | FGB | FGB réidh, ready or prepared |
| `bag.readyMark` | ready | réidh | Verified | FGB | same word as bag.ready, lower case for the mark beside an item |
| `bag.readyHint` | The ready item is what the quick-use button spends in a run; only one comes with you. | Is é an mhír réidh an ceann a chaitheann an cnaipe úsáide tapaidh le linn rith; ní thagann ach ceann amháin leat. | NeedsReview | FGB | mír, an item; cnaipe, a button; tapa, quick; composed, a speaker should check it |
| `bag.wearHint` | Worn charms and relics take effect from your next run. | Tiocfaidh éifeacht na n-arraí chaite i bhfeidhm ó do rith eile. | NeedsReview | FGB | éifeacht, an effect; arraí, gear; caite as bag.worn; composed, a speaker should check it |
| `character.title` | CHARACTER | CARACHTAR | Verified | FGB | capitals of carachtar above |
| `character.none` | No character yet. Defeat something to begin. | Níl carachtar ann fós. Cliste rud éigin le tosú. | Likely | FGB | FGB clise, to defeat; composed sentence, a speaker should check it |
| `character.level` | Level {0} | Leibhéal {0} | Verified | FGB | FGB leibhéal, a level or degree; the number sits beside it |
| `character.nextLevel` | {0} of {1} experience to the next level | {0} as {1} taithí go dtí an chéad leibhéal eile | Likely | FGB | FGB taithí, experience; go dtí an chéad leibhéal eile, to the next level; composed |
| `character.maxLevel` | Highest level reached | An leibhéal is airde a shroich tú | Likely | FGB | FGB sroich, to reach; is airde, highest; composed |
| `character.points` | Points to spend: {0} | Pointí le caitheamh: {0} | Likely | FGB | pointí, points; caitheamh, spending (from caith as bag.wear); composed |
| `character.vigour` | Vigour {0} | Brí {0} | Verified | FGB | FGB brí, strength or vigour; the number sits beside it |
| `character.vigourHint` | +10 maximum health a point | +10 sláinte uasta do gach pointe | Likely | FGB | sláinte, health; uasta, at most; gach, each; composed |
| `character.might` | Might {0} | Neart {0} | Verified | FGB | FGB neart, might or strength; the number sits beside it |
| `character.mightHint` | +5 percent damage from guns and parries a point | +5 faoin gcéad ó ghunnaí agus cosaint do gach pointe | NeedsReview | FGB | faoin gcéad, per cent; cosaint, defence, standing in for parry; composed, a speaker should check it |
| `character.nextRun` | Points take effect from your next run. A run keeps the stats it began with. | Tiocfaidh pointí i bhfeidhm ó do rith eile. Coinníonn rith na staitisticí a raibh aige nuair a thosaigh sé. | NeedsReview | FGB | tig i bhfeidhm, takes effect; coinnigh, keeps; composed, a speaker should check it |
| `quest.title` | QUESTS | TASCANNA | Likely | FGB | FGB tasc, a task (tascanna, plural); the quest sense is from games and needs a speaker |
| `quest.none` | No quests yet. Ask among the Folk. | Níl tasc ar bith fós. Iarr ar dhaoine an dúin. | NeedsReview | FGB | tasc as quest.title; dún, fortress; composed, a speaker should check it |
| `quest.husks` | Clear the Husks | Glan na Sligíní | NeedsReview | FGB | glan, clean or clear out (the imperative); Sligín coined from FGB slige, a shell or husk, for the Husks; needs a speaker |
| `quest.spitters` | Clear the Spitters | Glan na n-Plathadóirí | NeedsReview | FGB | Plathadóir coined from FGB plath, to spurt or spit on, for the Spitters; needs a speaker |
| `quest.giver` | Given by {0} | Ó {0} | Likely | FGB | ó, from; 'given by' reads as 'from' in Gaeilge |
| `quest.progress` | {0} of {1} defeated | {0} as {1} cloíte | Likely | FGB | FGB buaigh: cloíte, defeated; FGB out of a number; composed |
| `quest.reward` | Reward: {0} experience, {1} coin and {2} | Duais: {0} taithí, {1} bonn agus {2} | Likely | FGB | FGB duais, a reward or prize; taithí as character.nextLevel; bonn as bag.coin; composed |
| `quest.take` | Take | Tóg | Likely | FGB | FGB tóg, to take; the bare imperative on a button is composition |
| `quest.taken` | Taken | Glactha | Likely | FGB | FGB glac, to take; glactha, taken |
| `quest.done` | Finished | Críochnaithe | Verified | FGB | FGB críochnaithe, finished or completed |
| `quest.refused` | Refused | Diúltaithe | Likely | FGB | FGB diúltaigh, to refuse; diúltaithe, refused |
| `quest.nextRun` | Quests count the kills you make from now on, in every run. | Áirítear na maruithe a dhéanann tú ó seo amach, i ngach rith. | NeedsReview | FGB | áirigh, to count; marú, killing; composed, a speaker should check it |
| `pause.quests` | Quests | Tascanna | Likely | FGB | plural of tasc as quest.title |
| `pause.journal` | Journal | Dialann | Verified | FGB | FGB dialann, a diary or journal |
| `pause.mainMenu` | Main menu | An príomh-roghchlár | Likely | FFI FGB | príomh, main; roghchlár as hint.menu; composed |
| `journal.title` | JOURNAL | DIALANN | Verified | FGB | capitals of dialann above |
| `journal.notesRead` | {0}   -   Notes read: {1} | {0}   -   Nótaí léite: {1} | Likely | FGB | nóta, a note; léite, read; the shape of the line is kept |
| `journal.empty` | Nothing read yet. Notes lie against the walls of the dungeon: walk up to a scroll and use it. | Níl aon rud léite fós. Tá nótaí in aghaidh na mballaí sa dún: siúil go dtí an scroll agus bain úsáid as. | NeedsReview | FGB | balla, a wall; siúil, walk; scroll, a scroll (the borrowed word); composed, a speaker should check it |
| `controls.title` | CONTROLS | RIALÚ | Likely | FGB | capitals of rialú as menu.controls; the heading use needs a speaker |
| `controls.help` | Click a binding, then press the new key, button or stick direction. Esc cancels. | Cliceáil ar cheangal, agus ansin brúigh an cnaipe, an eochair nó taobh an bhaia nua. Cuirfidh Esc ar ceal. | NeedsReview | FGB | cliceáil, click (the borrowed word); brúigh, press; composed sentence, a speaker should check it |
| `controls.colAction` | Action | Gníomh | Verified | FGB | FGB gníomh, an act or action |
| `controls.colKeys` | Keyboard / mouse | Méarchlár / luch | Likely | FFI FGB | méarchlár, keyboard, and luch, mouse (Firefox ga's words); the slash is kept from the English |
| `controls.colPad` | Gamepad | Rialtóir | NeedsReview | FGB | coined from FGB rialtaire, a controller or governor; gd has no settled word for a gamepad and neither has ga; needs a speaker |
| `controls.resetAll` | Reset to defaults | Fill na réamhshocruithe | NeedsReview | FFI FGB | réamhshocruithe, pre-settings or defaults; fill, to restore or fill back; composed after Firefox ga's wording; needs a speaker |
| `controls.listenPad` | press a button or move a stick… | brúigh cnaipe nó bog an bat… | Likely | FGB | bog, to move; bat is the borrowed word for a stick; composed |
| `controls.listenKey` | press a key or click… | brúigh eochair nó cliceáil… | Likely | FGB | brúigh, press; eochair, a key; composed |
| `controls.binding` | Binding "{0}". Esc cancels. | Ceangal "{0}". Cuirfidh Esc ar ceal. | NeedsReview | FGB | ceangal, a tie or binding; cur ar ceal, to cancel; composed |
| `controls.mouse` | mouse | luch | Verified | FGB | FGB luch, a mouse |
| `controls.now` | {0} is now {1}. | 'S é {1} atá ag {0} anois. | Likely | FGB | is é atá ag, is what belongs to; anois, now; composed |
| `controls.swapUnbound` | {0} was used by {1}, which is now unbound. | Bhí {0} in úsáid ag {1}; tá {1} dícheangailte anois. | NeedsReview | FGB | dícheangail, unbound; composed |
| `controls.swapped` | {0} was used by {1}; the two swapped. | Bhí {0} in úsáid ag {1}; malartaíodh an bheirt. | NeedsReview | FGB | malartaigh, to exchange; composed |
| `controls.inUse` | {0} is used by {1}, which cannot take another input. Nothing changed. | Tá {0} in úsáid ag {1}. Níor athraigh rud ar bith. | Likely | FGB | in úsáid, in use; athraigh, to change; composed |
| `controls.reserved` | {0} cannot be used for {1}. | Ní féidir {0} a úsáid do {1}. | Likely | FGB | is féidir, can; úsáid, use; do, for; composed |
| `controls.wrongKind` | {0} cannot use {1}. | Ní féidir le {0} {1} a úsáid. | Likely | FGB | as above with the binding as the object; composed |
| `controls.same` | That binding is already set. | Tá an ceangal seo socraithe cheana. | Likely | FGB | socraithe, set (from socruithe); cheana, already; composed |
| `controls.resetDone` | Bindings reset to defaults. | Cuireadh na ceangail ar ais chuig na réamhshocruithe. | NeedsReview | FGB | cuir ar ais, to put back; composed |
| `group.movement` | MOVEMENT | GLUASACHT | Likely | FGB | FGB gluaiseacht, movement; capitals for the heading |
| `group.look` | LOOK | AMHARC | Likely | FGB | FGB amharc, a look or glance; capitals for the heading |
| `group.combat` | COMBAT | COMHRAC | Likely | FGB | FGB comhrac, combat; capitals for the heading |
| `group.portals` | PORTALS | PORTAIL | NeedsReview | FGB | the naturalised borrowed word for portal; the loan needs a speaker's blessing |
| `group.system` | SYSTEM | CÓRAS | Verified | FGB | FGB córas, a system; capitals for the heading |
| `action.moveForward` | Move forward | Bog ar aghaidh | Likely | FGB | bog, to move (the imperative); ar aghaidh, forward; composed |
| `action.moveBack` | Move back | Bog siar | Likely | FGB | bog as above; siar, back; composed |
| `action.moveLeft` | Move left | Bog ar chlé | Likely | FGB | ar chlé, to the left; composed, a speaker should check the phrase |
| `action.moveRight` | Move right | Bog ar dheis | Likely | FGB | ar dheis, to the right; composed, a speaker should check the phrase |
| `action.lookUp` | Look up | Féach suas | Likely | FGB | féach, look (the imperative); suas, up; composed |
| `action.lookDown` | Look down | Féach síos | Likely | FGB | síos, down; composed |
| `action.lookLeft` | Look left | Féach ar chlé | Likely | FGB | féach as above; composed, a speaker should check the phrase |
| `action.lookRight` | Look right | Féach ar dheis | Likely | FGB | as above to the right |
| `action.jump` | Jump | Léim | Verified | FGB | FGB léim, to jump; the bare verb is the imperative |
| `action.firePrimary` | Fire revolver | Lámhach an gunna rothaigh | NeedsReview | FGB | lámhach, fire or shoot; gunna rothaigh is the coined revolver (hint.revolver); composed |
| `action.fireSecondary` | Fire shotgun | Lámhach an gunna scaoilte | NeedsReview | FGB | lámhach as above; gunna scaoilte is the coined shotgun (hint.shotgun); composed |
| `action.parry` | Parry | Cosaint | Likely | FGB | cosaint as hint.parry; the bare noun is the imperative |
| `action.portalTeal` | Teal portal | Portail turcaide | NeedsReview | FGB | as hint.tealPortal; composed |
| `action.portalViolet` | Violet portal | Portail corcra | Likely | FGB | as hint.violetPortal; composed |
| `action.respawn` | Respawn | Athbheochan | Likely | FGB | as pause.respawn |
| `action.flyToggle` | Free-fly | Eitilt shaor | NeedsReview | FGB WKI | eitilt, flight; saor, free; a coined compound for the debug free-fly; needs a speaker |
| `action.flyDown` | Fly down | Eitil síos | Likely | FGB | eitil, fly (the imperative); síos, down; composed |
| `action.debugInfo` | Debug overlay | Dífhabhtú | NeedsReview | FGB | the loan-translation used in ga technical glossaries for debugging; whether it suits a button needs a speaker |
| `action.interact` | Open and use | Oscail agus bain úsáid as | Likely | FGB | oscail as hint.open; bain úsáid as, make use of; composed |
| `action.closePortals` | Close portals (sever) | Dún na portail (gearr) | NeedsReview | FGB | dún, close; portail as hint.tealPortal; gearr as hint.sever; composed |
| `action.pause` | Pause menu | Roghchlár sos | Likely | FGB | roghchlár as hint.menu; sos as pause.title; composed |
| `action.slam` | Slam | Buille | Likely | FGB | FGB buille, a blow or stroke; the slamming sense needs a speaker |
| `action.fireMortar` | Fire block mortar | Lámhach an mhóirtéir | NeedsReview | FGB | móirtéar is the borrowed word for a mortar (the weapon is not in FGB); composed; needs a speaker |
| `action.useItem` | Use quick item | Úsáid an mhír thapa | Likely | FGB | úsáid as controls.reserved; mír, an item; tapa, quick; composed |
| `settings.title` | SETTINGS | SOCRUITHE | Verified | FFI FGB | capitals of Firefox as Gaeilge's Socruithe |
| `settings.language` | Language | Teanga | Verified | FFI FGB | Firefox ga's Language header is Teanga; FGB teanga, a language or tongue |
| `settings.mouseSensitivity` | Mouse sensitivity | Íogaireacht na luch | Likely | FGB | FGB íogaireacht, sensitivity; luch as controls.mouse; composed |
| `settings.padLookSpeed` | Gamepad look speed | Luas amhairc an rialtóra | NeedsReview | FGB | luas, speed; amharc, a look; rialtóir as controls.colPad; composed |
| `settings.deadzone` | Stick dead zone | Crios marbh an bhaia | NeedsReview | FGB | crios, a zone; marbh, dead; bat as controls.listenPad; a word-for-word coinage |
| `settings.fov` | Field of view | Réimse radhairc | Likely | FGB | réimse, a field; radharc, a view; composed, a speaker should check the phrase |
| `settings.uiSize` | Interface size | Méid an chomhéadain | Likely | FGB | méid, size; comhéadan, an interface (the usual ga software word); composed |
| `settings.autoScale` | Scale the interface with the window | Scálaigh an comhéadan leis an bhfuinneog | NeedsReview | FGB | scálaigh, to scale (the borrowed word); fuinneog, a window; composed |
| `settings.master` | Master volume | Príomhfhuaim | Likely | FGB | príomh, main; fuaim, a sound; composed |
| `settings.effects` | Effects volume | Fuaim na n-éifeachtaí | Likely | FGB | éifeacht, an effect (éifeachtaí, plural); composed |
| `settings.ambience` | Ambience volume | Fuaim an chúlra | Likely | FGB | cúlra, a background or backdrop; composed |
| `settings.music` | Music volume | Fuaim na ceoil | Likely | FGB | ceol, music (ceoil in the genitive); composed |
| `settings.viewDistance` | Render distance | Fad taispeána | Likely | FGB | fad, a distance; taispeáin, to show; a compound for render distance |
| `settings.chunks` | {0} chunks | {0} bloic | Likely | FGB WKI | bloc, a block (bloic, plural) standing in for chunk, for which ga has no word |
| `settings.ghost` | Show the ghost of your best run | Taispeáin taibhse do rith is fearr | Likely | FGB | taispeáin, show (the imperative); taibhse, a ghost or spectre; rith, a run; composed |
| `settings.prompts` | Controller prompts | Leideanna cnaipe an rialtóra | NeedsReview | FGB | leid, a hint; cnaipe, a button; composed, a speaker should check it |
| `settings.promptsAuto` | Auto (follow controller) | Uath (lean an rialtóir) | Likely | FGB | uath-, the usual ga prefix for automatic (uathoibríoch); lean, to follow; composed |
| `settings.invertMouse` | Invert mouse Y | Inbhéartaigh Y na luch | Likely | FGB | inbhéartaigh, to invert (the technical ga word); composed |
| `settings.invertPad` | Invert gamepad Y | Inbhéartaigh Y an rialtóra | NeedsReview | FGB | as above with the coined rialtóir |
| `hud.floor` | Floor {0} / {1} | Urlár {0} / {1} | Likely | FGB | FGB urlár, a floor or storey of a building, which suits a dungeon floor better than the gd làr held back for the same reason |
| `hud.keys` | Keys {0} | Eochracha {0} | Likely | FGB | FGB eochair, a key (eochracha, plural); the number sits beside it, so no count agreement is needed |
| `hud.result.title` | FLOOR {0} COMPLETE | URLÁR {0} CRÍOCHNAITHE | Likely | FGB | urlár as hud.floor; críochnaithe as quest.done; capitals for the banner |
| `hud.result.rank` | RANK  {0} | CÉIM  {0} | Likely | FGB | FGB céim, a rank or grade; two spaces kept before the number |
| `hud.result.stats` | Time {0}    Kills {1}    Style {2} | Am {0}  MARUITHE {1}  STÍL {2} | Likely | FGB | am, time; maraithe, killed (marú, killing); stíl, style; the spacing is kept |
| `hud.result.restarts` | Restarts {0}  (−{0} points) | Atosuithe {0}  (-{0} pointí) | NeedsReview | FGB | atosú, a restarting (FGB tos); pointí, points; coined for the line |
| `toast.omen` | A washerwoman wrings out her cloth nearby. Something is coming. | Tá bean ní in aice láimhe. Tá rud éigin ag teacht. | NeedsReview | FGB | bean ní, the washerwoman of folklore; in aice láimhe, nearby; composed, a speaker should check it |
| `toast.boon` | The brownie warms the hearth for you (+{0} health) | Déanann an duine beag an tine te duit (+{0} sláinte) | NeedsReview | FGB | the brownie rendered as duine beag, the little person of folklore; te, warm; sláinte, health; composed |
| `standing.better` | {0} think better of you: {1} | Measann {0} níos mó ort: {1} | Likely | FGB | meas, to think or regard; níos mó ort, more of you; composed, a speaker should check it |
| `standing.worse` | {0} now regard you as {1} | Breathnaíonn {0} ort mar {1} anois | Likely | FGB | breathnaigh, to look at or regard; composed, a speaker should check it |
| `standing.title` | STANDING | STÁDAS | Likely | FGB | capitals of stádas as pause.standing |
| `standing.subtitle` | How the dungeon's peoples regard you. Hurt a creature in front of its kin and they will remember. | Conas a fheiceann muintir an dúin thú. Má ghortaíonn tú creatúir chun toigh a chine, cuimhneoidh siad. | NeedsReview | FGB | feic, to see; gortaigh, to hurt; cine, kin; cuimhnigh, to remember; composed, a speaker should check it |
| `standing.nobody` | Nobody here has an opinion of you yet. | Níl tuairim ag duine ar bith ort fós. | Likely | FGB | tuairim, an opinion; duine ar bith, anyone; fós, yet; composed |
| `standing.recent` | RECENT | LE DEIREANNAÍ | NeedsReview | FGB | FGB le deireanaí, recently; whether the short form suits a column heading needs a speaker |
| `standing.nothingYet` | Nothing yet. | Níl aon rud go fóill. | Likely | FGB | go fóill, yet; composed |
| `gap.edge` | A gap. Fire a portal across it ({0}), then another beside you ({1}), and step through. | Bearna. Lámhach portail trasna ({0}), ceann eile in aice leat ({1}), agus siúil tríd. | NeedsReview | FGB | bearna, a gap; lámhach, fire or shoot; trasna, across; in aice leat, beside you; composed, a speaker should check it |
| `gap.alcove` | A high alcove. Fire a portal up into it ({0}), then another beside you ({1}), and step through. | Áit ard. Lámhach portail suas ann ({0}), ceann eile in aice leat ({1}), agus siúil tríd. | NeedsReview | FGB | the alcove described as a high place, not named; composed |
| `gap.afterFall` | You are set back at the edge. Fire a portal across the gap instead ({0}, then {1}). | Cuireadh tú ar ais go dtí an imeall. Lámhach portail trasna na bearna in ionad ({0}, ansin {1}). | NeedsReview | FGB | imeall, an edge; in ionad, instead of; composed |
| `gap.fellBare` | FELL  -{0} | THIT  -{0} | Likely | FGB | FGB thit, fell (from tit, to fall); two spaces kept before the number |
| `gap.spikes` | SPIKES  -{0} | BIORANNA  -{0} | Likely | FGB | bior, a spike or prong (bioranna, plural) |
| `gap.splash` | SPLASH  climbing out | PLEISG  ag dreapadóireacht amach | NeedsReview | FGB | pleisc is coined for splash (FGB has it for a shell and a burst); dreapadóireacht, climbing; needs a speaker |
| `hint.revolver` | revolver | gunna rothach | NeedsReview | FGB | a coined compound, gunna, gun, plus rothach, wheeled, for the revolver; needs a speaker |
| `hint.shotgun` | shotgun | gunna scaoilte | NeedsReview | FGB | a coined compound, gunna plus scaoilte, scattering, for the shotgun; needs a speaker |
| `hint.parry` | parry | cosaint | Likely | FGB | FGB cosaint, a defence; the fencing sense of parry needs a speaker |
| `hint.tealPortal` | teal portal | portail turcaide | NeedsReview | FGB | portail is the naturalised borrowed word for portal; turcaide coined from turcaid, turquoise; composed |
| `hint.violetPortal` | violet portal | portail corcra | Likely | FGB WKI | corcra, purple or violet; composed with portail |
| `portal.mark.chevron` | chevron |  | Untranslated |  |  |
| `portal.mark.diamond` | diamond |  | Untranslated |  |  |
| `hint.sever` | sever | gearr | Likely | FGB | FGB gearr, to cut (the imperative); sever is read as cutting the link |
| `hint.open` | open | oscail | Likely | FGB | FGB oscail, to open; the bare verb is the imperative |
| `hint.jump` | jump | léim | Verified | FGB | FGB léim, to jump |
| `hint.menu` | menu | roghchlár | Verified | FFI FGB | Firefox ga's word for a menu; FGB roghchlár, a menu or bill of fare |
| `prompt.read` | {0}  read: {1} | {0}  léigh: {1} | Likely | FGB | FGB léigh, to read; the bare verb is the imperative |
| `prompt.speak` | {0}  speak | {0}  labhair | Likely | FGB | FGB labhair, to speak; the bare verb is the imperative |
| `cat.warmth` | The fire is warm, and you feel a little better. | Tá an tine teolaí, agus tá tú beagáinín níos fearr. | NeedsReview | FGB | teolaí, warm and cosy (checked for the cat's lines); beagáinín, a little; composed |
| `prompt.closeHint` | {0} or {1} to close | {0} nó {1} lena dhúnadh | Likely | FGB | nó, or; lena dhúnadh, to close it; composed |
| `prompt.leaveHint` | {0} or {1} to leave | {0} nó {1} lena fhágáil | Likely | FGB | fágáil, leaving; composed |
| `banner.died` | YOU DIED | TÁ TÚ MARBH | Likely | FGB | FGB marbh, dead; composed as the banner |
| `banner.respawn` | {0} to respawn | {0} chun athbheochain | Likely | FGB | chun, for the purpose of; athbheochan as pause.respawn |
| `banner.enemies` | Enemies {0}: kill them to open the doors | Naimhde {0}: maraigh iad leis na doirse a oscailt | Likely | FGB | naimhde, enemies; maraigh, kill (the imperative); composed |
| `banner.clear` | CLEAR | GLANTA | Likely | FGB | FGB glan, to clean or clear; glanta, cleared; capitals for the banner |
| `banner.floorDone` | FLOOR {0} COMPLETE  {1}  {2} | URLÁR {0} CRÍOCHNAITHE  {1}  {2} | Likely | FGB | as hud.result.title; the spacing is kept |
| `banner.floorStats` | time {0}  kills {1}  style {2} | am {0}  maraithe {1}  stíl {2} | Likely | FGB | as hud.result.stats, lower case |
| `banner.firstClear` | first clear | an chéad ghlanadh | NeedsReview | FGB | an chéad, the first; glanadh, the clearing of (from glan as banner.clear); a coinage for the banner |
| `banner.newBest` | NEW BEST  (was {0}) | IS FEARR NUA  (bhí {0}) | Likely | FGB | is fearr, best; nua, new; bhí, it was; composed, a speaker should check it |
| `banner.best` | best {0} | is fearr {0} | Likely | FGB | is fearr, best; the number sits beside it |
| `banner.key` | KEY | EOCHAIR | Verified | FGB | FGB eochair, a key; capitals for the banner |
| `banner.chest` | CHEST | CISTE | Verified | FGB | FGB ciste, a chest or box; capitals for the banner |
| `banner.lockedChest` | LOCKED CHEST | CISTE GHLASAITHE | Likely | FGB | glasaithe, locked (from glasáil, to lock); lenition after the feminine ciste |
| `banner.chestHealed` | CHEST  +{0} health | CISTE  +{0} sláinte | Likely | FGB | as banner.chest plus sláinte, health; composed for the banner |
| `banner.lockedChestHealed` | LOCKED CHEST OPENED  +{0} health | CISTE GHLASAITHE OSCAILTE  +{0} sláinte | Likely | FGB | as banner.lockedChest plus oscailte, opened |
| `banner.item` | {0}  +{1} to your bag | {0}  +{1} i do mhála | Likely | FGB | i do mhála, into your bag (mála as pause.bag); composed |
| `banner.bagFull` | {0}  your bag is full | {0}  tá do mhála lán | Likely | FGB | lán, full; composed |
| `banner.locked` | locked: find the key on this floor | glasaithe: faigh an eochair ar an urlár seo | Likely | FGB | glasaithe as banner.lockedChest; faigh, get or find (the imperative); urlár as hud.floor; composed |
| `banner.spikes` | SPIKES  -{0} | BIORANNA  -{0} | Likely | FGB | as gap.spikes |
| `banner.noPortal` | can't place a portal there ({0}) | Ní féidir portail a chur ansin ({0}) | Likely | FGB | ní féidir, cannot; ansin, there; composed |
| `banner.meteor` | METEOR | MEATAN | Likely | FGB | meatán, meteor; capitals for the banner |
| `banner.meteor2` | METEOR x2 | MEATAN x2 | Likely | FGB | as banner.meteor; the x2 is kept |
| `pact.title` | THE DUNGEON OFFERS A PACT |  | Untranslated |  |  |
| `pact.strength` | its strength follows your style rank |  | Untranslated |  |  |
| `pact.boon` | Boon |  | Untranslated |  |  |
| `pact.burden` | Burden |  | Untranslated |  |  |
| `pact.decline` | Take none |  | Untranslated |  |  |
| `pact.hint` | {0} to take one, {1} to leave them |  | Untranslated |  |  |
| `pact.bloodsworn.name` | Bloodsworn |  | Untranslated |  |  |
| `pact.bloodsworn.boon` | Parries heal 20 more and the spent parry window's cooldown is refunded |  | Untranslated |  |  |
| `pact.bloodsworn.burden` | Hard damage takes half of damage taken, up from 35% |  | Untranslated |  |  |
| `pact.hollowHeart.name` | Hollow Heart |  | Untranslated |  |  |
| `pact.hollowHeart.boon` | Blood heals 1.5x |  | Untranslated |  |  |
| `pact.hollowHeart.burden` | Maximum health 30 lower |  | Untranslated |  |  |
| `pact.gravedigger.name` | Gravedigger |  | Untranslated |  |  |
| `pact.gravedigger.boon` | Slam shockwaves reach 1.5 blocks further and break floors in one hit |  | Untranslated |  |  |
| `pact.gravedigger.burden` | Style decays 20% faster |  | Untranslated |  |  |
| `pact.showman.name` | Showman |  | Untranslated |  |  |
| `pact.showman.boon` | All style points 25% higher |  | Untranslated |  |  |
| `pact.showman.burden` | Weapons go stale 40% faster |  | Untranslated |  |  |
| `pact.twinGate.name` | Twin Gate |  | Untranslated |  |  |
| `pact.twinGate.boon` | A third portal slot: the newest rotates in, the oldest rotates out |  | Untranslated |  |  |
| `pact.twinGate.burden` | Placing a portal costs 5 health while below Brutal |  | Untranslated |  |  |
| `rank.rowdy` | ROWDY | BORRACH | NeedsReview | FGB | FGB borrach, insolent or elated; the rowdy sense needs a speaker |
| `rank.fierce` | FIERCE | FIÁIN | NeedsReview | FGB | FGB fiáin, wild; whether it carries the fierce rank sense needs a speaker |
| `rank.brutal` | BRUTAL | BRÚIDIÚIL | Likely | FGB | FGB brúidiúil, brutal or cruel |
| `rank.savage` | SAVAGE | FÍOCHMHAR | Likely | FGB | FGB fíochmhar, fierce or savage |
| `rank.merciless` | MERCILESS | GAN TRÓCAIRE | NeedsReview | FGB | trócaire, mercy; the phrase 'without mercy' as a rank name needs a speaker |
| `rank.relentless` | RELENTLESS | GAN STAONADH | NeedsReview | FGB | staonadh, relenting; the phrase 'without relenting' as a rank name needs a speaker |
| `rank.apocalyptic` | APOCALYPTIC | APACAILIPT | NeedsReview | WKI | coined from the WKI Irish for an apocalypse; the adjective's form needs a speaker |
| `rank.legendary` | LEGENDARY | MIOTASACH | NeedsReview | FGB | coined from miotas, a myth, for legendary; needs a speaker |

Counts: Verified 26, Likely 110, NeedsReview 54, no Gaeilge yet 23.

<!-- END GENERATED IRISH TABLES -->

## Sources

- Foclóir Gaeilge–Béarla (Ó Dónaill) on teanglann.ie: [tine](https://www.teanglann.ie/en/fgb/tine), [fáilte](https://www.teanglann.ie/en/fgb/fáilte), [suigh](https://www.teanglann.ie/en/fgb/suigh), [cat](https://www.teanglann.ie/en/fgb/cat), [Dia](https://www.teanglann.ie/en/fgb/dia), [maith](https://www.teanglann.ie/en/fgb/maith), [slán](https://www.teanglann.ie/en/fgb/slán), [fuar](https://www.teanglann.ie/en/fgb/fuar), [te](https://www.teanglann.ie/en/fgb/te), [teas](https://www.teanglann.ie/en/fgb/teas), [teolaí](https://www.teanglann.ie/en/fgb/teolaí), [cara](https://www.teanglann.ie/en/fgb/cara), [codladh](https://www.teanglann.ie/en/fgb/codladh), [cluthar](https://www.teanglann.ie/en/fgb/cluthar).
- Wiktionary: [Dia duit](https://en.wiktionary.org/wiki/Dia_duit), [Aodhán](https://en.wiktionary.org/wiki/Aodhán).
- Wikipedia: [Aodhán](https://en.wikipedia.org/wiki/Aodhán), [Aidan](https://en.wikipedia.org/wiki/Aidan), [Aodh (given name)](https://en.wikipedia.org/wiki/Aodh_(given_name)).
- Wikibooks: [Irish, Lesson One](https://en.wikibooks.org/wiki/Irish/Lesson_One) (*Dia is Muire duit*).
- Transparent Language Irish blog (greetings and *tá mé fuar*), and Focal an Lae no. 327 ([Sabhal Mòr Ostaig](https://www3.smo.uhi.ac.uk/gaeilge/donncha/focal/focal327.html)) for *Tá sé fuar inniu*.

Searches of focloir.ie, gaeilge.ie and Tuairisc.ie did not return pages for these exact phrases in the session; the Ó Dónaill dictionary on teanglann.ie is the main authority used.
