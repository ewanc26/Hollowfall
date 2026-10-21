# Music

Hollowfall's music is adaptive, sparse and moody: Scottish-flavoured dark fantasy, written as data and played by arithmetic.
There are no audio files. Every note is synthesised in code from the world seed, so the same world always has the same music,
and everything that decides what is heard lives in `src/Hollowfall.Core/Audio/Music/` with no engine dependency and no
`System.Random`. The Godot side (`game/scripts/MusicDirector.cs`) only renders on a background task, plays and sets volumes.

Nobody has listened to this by ear while it was written: the tests prove it is in tune, in key, seamless, bounded and
responsive, not that it is beautiful. A WAV of a scripted sequence can be written for a human to judge (see "Listening" below).

## The loop

84 BPM, 4/4, 8 bars: 32 beats of 15 750 samples at 22 050 Hz, so beats and bars land on whole samples and a loop is exactly
504 000 samples (22.86 s). Every layer of every context is rendered once to a mono `float[]` of exactly that length, so any
mix of them stays phase-aligned. Notes are added with wrap-around (`LoopBuffer`): the tail of a harp note plucked in bar 8
carries on under bar 1. Drones use frequencies with a whole number of cycles in the loop (`Instruments.SnapToLoop`) and the
low-pass filter runs round the loop several times so its state at the seam matches the start. Tests check the step across the
loop point is no bigger than the biggest step inside it, for every layer of every context.

## Contexts, keys and modes

The tonic is picked from the seed (`MusicScores.TonicPitchClass`, one of D, E, G, A, C, F, B flat or B) and shared by every
context, so moving about changes mode more than key. `MusicScores.For(context, seed)` writes the notes as plain data
(`ScoreNote`), which the tests check against the scale.

| Context | Where | Mode | Character |
| --- | --- | --- | --- |
| Surface | the open world | Dorian | folk: whistle tune, harp arpeggios |
| Plain | dungeon floor 0 | Aeolian | cold, a dull pad (650 Hz) |
| Mossy | floor 1 | Aeolian | darker, a slower and sparser harp, a duller pad (480 Hz) |
| Ashen | floor 2 and deeper | Phrygian | heaviest drum, a low pad (420 Hz) |
| WulverHill | within 30 blocks of the Wulver, on the surface | major pentatonic, a minor third above the tonic | warm whistle tune, gentle harp; uses only notes the surface Dorian already has |

The floor to context rule is `MusicContexts.Of`, which follows `DungeonThemes.ForFloor`. A hearth is not a context but a
layer on top of whichever one is playing (below).

## Layers

| Layer | What it is | Made by |
| --- | --- | --- |
| Drone | root, fifth, octave and a sub (detuned saws and sines) with slow warm saw pad chords | `Drone`, `Instruments.PadVoice` |
| Harp | plucked clarsach arpeggios | `Instruments.Pluck` (Karplus-Strong with an all-pass fractional delay) |
| Whistle | the tune, breathy with a late vibrato | `Instruments.Whistle` |
| Pulse | a soft low thump on every beat, for tension | `Instruments.Thump` |
| DrumLight | a bodhran-like groove with a roll at the end of each half | `Instruments.FrameDrum` |
| DrumHeavy | deeper, longer strokes on top, as combat intensifies | `Instruments.FrameDrum(low)` |
| Shimmer | a high glassy arpeggio with echoes | `Instruments.Glass` |
| Choir | open fifths and an octave, a low brass and voice swell | `Instruments.BrassSwell` |
| Heartbeat | a low "lub-dub" on every beat | `Instruments.Heartbeat` |
| Hearth | a low hum and one soft harp note a bar | `Hearth` |

Each layer is normalised to its own peak (`MusicTuning.Peak`), and the sum of all peaks with the drone at its largest slider
boost, times the gains the mixer asks for, is kept at or under 1 (see "Headroom").

## State to layers

`MusicState` is a plain record the host fills each frame. `MusicMixer.Targets(state, combat, heavy)` is the pure rule that turns
it into a target gain for each layer (before the master headroom of 0.8); the numbers are in `MusicTuning`.

| Layer | Calm | In combat | Boss | Other |
| --- | --- | --- | --- | --- |
| Drone | 0.4 surface, 0.6 underground | same | 0.7 | scaled by the Ambience slider (see below) |
| Harp | 0.5; mossy and ashen 0.4; Wulver 0.45 | 0.12 | 0 | 0.3 at most by a hearth |
| Whistle | surface 0.55, Wulver 0.75, plain 0.25, mossy 0.3, ashen 0 | 0 | 0 | 0.3 at most by a hearth |
| Pulse | plain 0.12, mossy 0.15, ashen 0.22, surface 0 | 0.35 + 0.35 x intensity | 0.7 | |
| DrumLight | 0 | 0.45 + 0.45 x intensity | 0.9 | |
| DrumHeavy | 0 | 0.5 + 0.4 x intensity once the heavy flag is on, else 0 | 0.9 | |
| Shimmer | 0.35 with a passive creature near, 0.22 with linked portals | 0.18 at intensity 0.75 or more | 0 | the largest applies |
| Choir | 0 | 0 | 0.85 | |
| Heartbeat | 0 above 40 percent health, rising to 0.85 at 10 percent | same | same | |
| Hearth | 0.8 when `NearHearth` | 0 (a fight drowns the fireside) | 0 | |
| everything | | | | dead: 0 for every layer |

Intensity is 0.15 + 0.4 x (enemies near, up to 6, over 6) + 0.4 x (style rank over 8) + 0.1 for a locked-door encounter,
capped at 1; the boss is always 1. Combat is on at once when an enemy is near, an encounter is active or the boss is active,
and ends only after 5 s with none of them. The heavy drum comes in at intensity 0.55 and goes below 0.35 (hysteresis), and the
boss forces it on.

## Smoothing and ducking

The mixer (`MusicMixer.Update`) moves each layer toward its target by `dt / attack` when rising and `dt / release` when falling
(seconds for the whole 0 to 1 range; `MusicTuning.AttackSeconds` and `ReleaseSeconds`). Combat layers come in fast (the light
drum in 0.6 s) and leave slowly (4.5 s); the drone takes 3 s up and 6 s down, so the bed never drops abruptly. A frame counts
as at most 0.1 s, so a hitch cannot make the music jump. Then:

* Death: every layer falls over 3 s (`DeathFadeSeconds`) and combat resets. The Death stinger plays over the fade.
* Dialogue: the mix ducks to 40 percent in 0.4 s and returns over 1.2 s.
* Pause: the mix moves to `PauseLevel` (0.6) times each layer's `PauseKeep` (drone 0.7, pulse 0.6, heartbeat 0.5, choir 0.5,
  drums 0.3, harp and whistle 0.25, shimmer 0.15), so the highs go first, as a low-pass would take them.
* Start: the whole mix rises from silence over 4 s, after the background render finishes.
* Context change: contexts crossfade over 5 s. Their weights are normalised to never add up to more than 1.

The gain of one layer of one context is `smoothed x duck x pause x start x context weight`, always 0 to 1.

## Headroom

`Targets` scales everything so that the sum of `gain x peak` over the layers, with the drone counted at the Ambience slider's
largest boost (2), is at most 1; a test sweeps over 2 300 states and checks it, and another checks the final gains of a running
mixer. The combined layers therefore cannot clip before the engine's own master and bus.

## Stingers

`MusicStingers.Render(stinger, seed)` renders a short one-shot in the world's key from the same instruments, 3.6 to 10 s, ending
in silence: **FloorComplete** (a rising pentatonic harp run with glass), **BossIntro** (a low brass fifth and a flat second with
a frame-drum roll that speeds up into one huge stroke), **Death** (falling Aeolian plucks over a sinking swell and a heartbeat),
**Ending** (a slow whistled Dorian phrase answered by the harp over a pad) and **WulverGreeting** (three whistled notes of his
tune and a soft harp fifth). `MusicEvents.For` maps a `CombatEvent` to a stinger (a death, a finished floor, the boss room's
encounter starting; the boss floor's completion gets none because the ending has its own) and `MusicEvents.For(SoundId)`
maps the Wulver's greeting sound to his motif. The ending stinger is sent by `Main` when the ending card appears.

## Settings

* **Music volume** (`GameSettings.MusicVolume`, 0 to 1, default 0.6, clamped, saved with the other settings through
  `DataHome`) scales the whole music and the stingers, on top of the master volume. At 0 the music is off and the old looping
  drone comes back.
* **Ambience volume** scales the music's drone layer by up to 2 (1 at its default of 50 percent), because once the music is
  ready its drone layer is the bed: `AudioDirector.BedReplaced` fades the old looping drone out so the two never double. The
  old drone is only the fallback while the music is rendering, if rendering fails, or when the music volume is 0.
* Master volume scales everything.

## The Godot side

`MusicDirector` renders on `Task.Run` at startup (each context on its own thread, encoded to 16-bit as soon as it is
rendered), then on the main thread builds, per context, one `AudioStreamSynchronized` of ten looping `AudioStreamWav` and one
`AudioStreamPlayer`, all started in the same frame. Each frame it updates the mixer from `MusicState` (fed by `Main` through
Core's `MusicStateBuilder`) and sets each stream's volume in decibels with `SetSyncStreamVolume`. It runs while the game is
paused. Stingers play on a player of their own. The measured numbers are logged at startup ("music ready: ...").

## Listening

`MusicOffline.Render` runs the same mixer and the same loops offline for a scripted sequence (`ListeningScript`: calm, a
fight that builds, a low-health moment, the boss, and a death), and `ToWav` writes it. Run
`HOLLOWFALL_MUSIC_WAV=/path/to/music.wav dotnet test --filter WritesTheListeningWavWhenAskedTo` to keep it.

## Constants

All tuning numbers live in `MusicTuning` (`MusicTypes.cs`): the tempo and loop length, each layer's peak, attack and release,
the pause keep, the dialogue level, the death fade, the start fade, the context crossfade, the combat hold, the heavy
thresholds, the heartbeat range, the Wulver's hearing range (30 blocks), the enemy range (24 blocks) and every target level.
Change them there.

## How to add a layer

1. Append a value to `MusicLayer` (never reorder; indices are the layer's slot in every array).
2. Add its peak, attack, release and pause keep to `MusicTuning`.
3. Write its renderer in `MusicLayers.Render`, adding notes with `LoopBuffer.Add` so tails wrap, and put any new notes on
   `MusicScore` as data so the scale test can check them.
4. Decide its target in `MusicMixer.Targets`; add its levels to `MusicTuning` and to the table above.
5. The tests that run over every layer (length, bounded, audible, seamless, headroom, size) pick it up on their own; add
   one for its behaviour in `MusicMixerTests`.

## Memory and time

Five contexts of ten layers of 504 000 samples is 50 loops of about 1.0 MB each, 49.3 MB with the stingers as 16-bit audio in the engine
(`MusicBank.Pcm16Bytes`; 100 MB as floats, which are dropped as each context is encoded). Rendering took about 0.6 to 0.9 s on
the development machine (Apple silicon, five contexts in parallel) and building the streams about 30 ms.
