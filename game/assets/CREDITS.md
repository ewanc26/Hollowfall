# Asset credits

## Fonts

Both families are licensed under the [SIL Open Font License 1.1](https://openfontlicense.org/) (see the `OFL.txt` next to
each font). They may be used, embedded and redistributed with the game. Neither may be sold on its own, and any modified
version must keep the licence. They were downloaded unmodified from the
[Google Fonts repository](https://github.com/google/fonts).

| Family | Used for | Files | Copyright |
|---|---|---|---|
| Chakra Petch | Titles, headings, rank and number emphasis | `fonts/ChakraPetch/ChakraPetch-Bold.ttf`, `-SemiBold.ttf` | Copyright 2018 The Chakra Petch Project Authors |
| Rajdhani | Menus, HUD text, hints | `fonts/Rajdhani/Rajdhani-Medium.ttf`, `-SemiBold.ttf`, `-Bold.ttf` | Copyright (c) 2014 Indian Type Foundry |

Both families were checked in the real game scene to draw every Scottish Gaelic grave-accented vowel (à è ì ò ù and the capitals), which the smoke test (`every UI font draws the grave-accented vowels`) keeps true.

## Gàidhlig text

The Gàidhlig (Scottish Gaelic) words in the interface were checked against, and in places taken from, these sources (full detail and the status of every string in [docs/gaidhlig.md](../../docs/gaidhlig.md)):

- [Wiktionary](https://en.wiktionary.org/wiki/Category:Scottish_Gaelic_lemmas), the Scottish Gaelic entries (CC BY-SA 4.0): spellings, genders, meanings and mutated forms.
- [Firefox in Gàidhlig](https://github.com/mozilla-l10n/firefox-l10n/tree/main/gd) (Mozilla Public Licence 2.0): the software words *Roghainnean*, *Fàg an-seo*, *Lean air*, *Cànan* and *Aisig na bun-roghainnean*.
- [LearnGaelic, Little by Little](https://learngaelic.net/littlebylittle/unit07/index.jsp) (Bòrd na Gàidhlig): the weather sentence *Tha i fuar.*
- [Scottish Gaelic Grammar Wiki](https://gaelicgrammar.org/~gaelic/mediawiki/index.php/Article): the article and lenition rules.
- [Vicipeid na Gàidhlig](https://gd.wikipedia.org) and [Tobar an Dualchais](https://www.tobarandualchais.co.uk/track/5824): the creature names.
- A Gàidhlig translation of the Drupal Colossal Menu module: *Clàr-taice*.

Only short words and phrases are used, as facts about the language; no text was copied from any of them. The wording is the project's own and has not been reviewed by a native speaker.

## Glyphs, icons and logo

The controller, mouse and keyboard prompts, HUD icons, style-rank emblems, app icon and banner are original vector art made for
this project and released under the project's licence (AGPL-3.0). They are generated from code by
`tools/Hollowfall.AssetGen` (run `dotnet run --project tools/Hollowfall.AssetGen`), so they can be restyled in one place and a
test fails if the committed files drift from the generator. They contain no text elements and use no third-party artwork.

The PlayStation and Nintendo prompt sets use plain geometric symbols and letters drawn for this project, not the platform holders' artwork.

Keyboard keys are not images: the game draws a keycap and writes the key's name in the UI font.

For reference, [Kenney's Input Prompts](https://kenney.nl/assets/input-prompts) (CC0) is a ready-made alternative if a more
conventional prompt style is ever wanted; nothing from it is used here.

## Models

All low-poly models are described in code for this project (`src/Hollowfall.Core/Models`) and use no third-party assets.
