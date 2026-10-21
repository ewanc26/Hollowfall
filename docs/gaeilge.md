# Gaeilge in Hollowfall

A cat called **Aodhán** lives beside one fire pit in each world. He is named for a person, a gift: *Aodhán* is the Irish name (a pet form of *Aodh*, "fire") usually glossed as "little fire", and it is the Irish form of the name Aidan. He speaks **Irish (Gaeilge)**, not Scottish Gaelic, in a gentle, sleepy, dry voice. This page lists every Irish string the game uses, where it comes from, and how sure we are.

**This text needs a fluent speaker's review before release.** It was assembled by looking each word and phrase up in dictionaries and learner references, and by keeping to single words and short fixed phrases. Nothing here is a long sentence that was invented. A fluent Irish speaker may well find a phrase stiff, regional, or not what a cat would say.

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

## Sources

- Foclóir Gaeilge–Béarla (Ó Dónaill) on teanglann.ie: [tine](https://www.teanglann.ie/en/fgb/tine), [fáilte](https://www.teanglann.ie/en/fgb/fáilte), [suigh](https://www.teanglann.ie/en/fgb/suigh), [cat](https://www.teanglann.ie/en/fgb/cat), [Dia](https://www.teanglann.ie/en/fgb/dia), [maith](https://www.teanglann.ie/en/fgb/maith), [slán](https://www.teanglann.ie/en/fgb/slán), [fuar](https://www.teanglann.ie/en/fgb/fuar), [te](https://www.teanglann.ie/en/fgb/te), [teas](https://www.teanglann.ie/en/fgb/teas), [teolaí](https://www.teanglann.ie/en/fgb/teolaí), [cara](https://www.teanglann.ie/en/fgb/cara), [codladh](https://www.teanglann.ie/en/fgb/codladh), [cluthar](https://www.teanglann.ie/en/fgb/cluthar).
- Wiktionary: [Dia duit](https://en.wiktionary.org/wiki/Dia_duit), [Aodhán](https://en.wiktionary.org/wiki/Aodhán).
- Wikipedia: [Aodhán](https://en.wikipedia.org/wiki/Aodhán), [Aidan](https://en.wikipedia.org/wiki/Aidan), [Aodh (given name)](https://en.wikipedia.org/wiki/Aodh_(given_name)).
- Wikibooks: [Irish, Lesson One](https://en.wikibooks.org/wiki/Irish/Lesson_One) (*Dia is Muire duit*).
- Transparent Language Irish blog (greetings and *tá mé fuar*), and Focal an Lae no. 327 ([Sabhal Mòr Ostaig](https://www3.smo.uhi.ac.uk/gaeilge/donncha/focal/focal327.html)) for *Tá sé fuar inniu*.

Searches of focloir.ie, gaeilge.ie and Tuairisc.ie did not return pages for these exact phrases in the session; the Ó Dónaill dictionary on teanglann.ie is the main authority used.
