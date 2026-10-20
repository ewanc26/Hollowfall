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

## Glyphs, icons and logo

The controller, mouse and keyboard prompts, HUD icons, style-rank emblems, app icon and banner are original vector art made for
this project and released under the project's licence (AGPL-3.0). They are generated from code by
`tools/Hollowfall.AssetGen` (run `dotnet run --project tools/Hollowfall.AssetGen`), so they can be restyled in one place and a
test fails if the committed files drift from the generator. They contain no text elements and use no third-party artwork.

The PlayStation and Nintendo prompt sets use plain geometric symbols and letters drawn for this project, not the platform holders' artwork.

Keyboard keys are not images: the game draws a keycap and writes the key's name in the UI font.

For reference, [Kenney's Input Prompts](https://kenney.nl/assets/input-prompts) (CC0) is a ready-made alternative if a more
conventional prompt style is ever wanted; nothing from it is used here.
