using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Input;
using Hollowfall.Core.Ui;
using Hollowfall.Core.Meshing;
using Hollowfall.Core.Models;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Relations;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;
using Hollowfall.Core.Replay;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// Headless check that the Godot shell can load Hollowfall.Core and run it. Prints a
/// "==== N passed, M failed ====" summary and exits non-zero on failure
/// (run-smoke-test.sh depends on both).
/// </summary>
public partial class SmokeTest : Node
{
    private int _passed;
    private int _failed;

    public override async void _Ready()
    {
        // Never touch the real settings or records files from a test run, and never let the caller's seed override the settings file.
        string settingsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hollowfall-smoke-{System.Environment.ProcessId}.json");
        string recordsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hollowfall-smoke-records-{System.Environment.ProcessId}.json");
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_HOME", System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hollowfall-smoke-home-{System.Environment.ProcessId}"));
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_SETTINGS", settingsPath);
        string storyPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hollowfall-smoke-story-{System.Environment.ProcessId}.json");
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_STORY", storyPath);
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_RECORDS", recordsPath);
        string relationsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hollowfall-smoke-relations-{System.Environment.ProcessId}.json");
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_RELATIONS", relationsPath);
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_SEED", null);

        Check("both fonts load as real fonts, not the fallback", () =>
            UiFonts.Display != ThemeDB.FallbackFont && UiFonts.Body != ThemeDB.FallbackFont && UiFonts.Display.HasChar('H') && UiFonts.Body.HasChar('9'));

        Check("both fonts draw every Irish letter with an acute accent (the cat speaks Gaeilge)", () =>
        {
            const string irish = "\u00e1\u00e9\u00ed\u00f3\u00fa\u00c1\u00c9\u00cd\u00d3\u00da";
            bool ok = true;
            foreach (var font in new[] { UiFonts.Display, UiFonts.DisplayMedium, UiFonts.Body, UiFonts.BodyBold })
                foreach (char c in irish)
                    if (!font.HasChar(c)) { GD.PrintErr($"    a font is missing U+{(int)c:X4}"); ok = false; }
            ok &= UiFonts.FaceWithGlyphs(CatDialogue.Name, UiFonts.Display).HasChar('\u00e1');
            return ok;
        });

        Check("every glyph and icon imports as a texture", () =>
        {
            var ids = BindingGlyphs.AllIds.Concat(GameIcons.All).Concat(GameIcons.RankIcons).Append("mouse").ToList();
            var missing = ids.Where(id => Glyphs.Get(id) == null).ToList();
            if (missing.Count > 0) GD.PrintErr($"    missing glyphs: {string.Join(", ", missing)}");
            return missing.Count == 0 && ids.All(id => Glyphs.Get(id)!.GetWidth() > 0);
        });

        Check("hint bars draw a glyph or keycap for each style", () =>
        {
            var bar = new HintBar();
            AddChild(bar);
            var map = BindingMap.CreateDefault();
            bool ok = true;
            foreach (var style in Enum.GetValues<ControllerStyle>())
            {
                bar.Set([(map.Get(GameAction.Jump, Slot.Gamepad), "jump"), (map.Get(GameAction.Jump, Slot.KeyboardMouse), "jump")], style);
                ok &= bar.GetChildCount() >= 2;
            }
            bar.QueueFree();
            return ok;
        });

        Check("terrain is deterministic", () =>
            new TerrainGenerator(7).GenerateChunk(2, 3).RawBlocks.SequenceEqual(new TerrainGenerator(7).GenerateChunk(2, 3).RawBlocks));

        Check("terrain has solid ground", () =>
        {
            var chunk = new TerrainGenerator(7).GenerateChunk(0, 0);
            return chunk.TopY(8, 8) > 0 && chunk[8, 0, 8] == BlockType.Bedrock;
        });

        Check("a dungeon is found and has floors", () =>
        {
            var generator = new TerrainGenerator(1);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (generator.Dungeons!.GetSite(rx, rz) is { } site)
                        return site.FloorCount >= 2 && site.Floors.All(f => f.Rooms.Count >= 4);
            return false;
        });

        Check("dungeon chunks carry dungeon blocks", () =>
        {
            var generator = new TerrainGenerator(1);
            var site = generator.Dungeons!.GetSite(0, 1)!;
            var chunk = generator.GenerateChunk(site.EntranceX >> 4, site.EntranceZ >> 4);
            return chunk.RawBlocks.ToArray().Contains(BlockType.Stairs);
        });

        Check("a land chunk converts to a Godot mesh with surfaces", () =>
        {
            var world = new VoxelWorld(7);
            var mesh = ChunkMeshBuilder.ToArrayMesh(ChunkMesher.Build(0, 0, world.GetBlock));
            return mesh != null && mesh.GetSurfaceCount() >= 1 && mesh.GetAabb().Size.Y > 1;
        });

        Check("the block atlas uploads and chunk surfaces carry texture coordinates", () =>
        {
            var atlas = ChunkMeshBuilder.Atlas;
            var mesh = ChunkMeshBuilder.ToArrayMesh(ChunkMesher.Build(0, 0, (_, y, _) => y < 5 ? BlockType.Stone : BlockType.Air));
            if (mesh == null || atlas.GetWidth() != Hollowfall.Core.Textures.BlockTextures.Width) return false;
            var uvs = mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            return uvs.Length > 0 && uvs.Any(u => u != Vector2.Zero);
        });

        Check("an all-air chunk converts to no mesh", () =>
            ChunkMeshBuilder.ToArrayMesh(ChunkMesher.Build(0, 0, (_, _, _) => BlockType.Air)) == null);

        Check("every enemy model is rigged: poses move the joints and children follow their parents", CheckEnemyRigs);

        Check("the Folk, the Bean Nighe and the hare are rigged and animated", CheckCreatureRigs);

        Check("every model loads with its parts and its baked clips", () =>
        {
            foreach (var model in ModelCatalog.All)
            {
                var rig = ModelFactory.Build(model);
                bool parts = model.Parts.All(p => rig.Part(p.Name) != null) && rig.Parts.Count == model.Parts.Count;
                int clips = rig.Animator?.GetAnimationList().Length ?? 0;
                if (!parts || clips != EnemyMotions.For(model).Count)
                {
                    GD.PrintErr($"    {model.Id}: parts={parts} clips={clips}");
                    rig.Root.Free();
                    return false;
                }
                rig.Root.Free();
            }
            return true;
        });

        Check("baked clips put every joint where Core's angles say", CheckClipComposition);

        Check("every sound becomes an engine stream and plays", () =>
        {
            var audio = new AudioDirector();
            AddChild(audio);
            bool ok = true;
            foreach (var id in Enum.GetValues<Hollowfall.Core.Audio.SoundId>())
            {
                if (audio.Length(id) < 0.04) ok = false;
                audio.Play(id);
                audio.Play(id, new Vector3(1, 2, 3));
            }
            audio.QueueFree();
            return ok;
        });

        await CheckMainSceneAsync();

        foreach (var file in new[] { settingsPath, settingsPath + ".tmp", recordsPath, recordsPath + ".tmp", relationsPath, relationsPath + ".tmp" })
            try { System.IO.File.Delete(file); } catch (System.IO.IOException) { }

        GD.Print($"==== {_passed} passed, {_failed} failed ====");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    /// <summary>Boots the real game scene and waits for the world to stream in and the player to land.</summary>
    private async Task CheckMainSceneAsync()
    {
        const string name = "the main scene streams in a world and the player lands";
        try
        {
            var main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
            AddChild(main);

            var view = main.GetNode<WorldView>("World");
            var player = main.GetNode<PlayerController>("Player");
            var deadline = Time.GetTicksMsec() + 20_000;
            while (Time.GetTicksMsec() < deadline && !(view.ShownChunks >= 40 && player.Body.OnGround))
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            bool ok = view.ShownChunks >= 40 && player.Body.OnGround;
            if (ok) { _passed++; GD.Print($"  ok   {name} ({view.ShownChunks} chunks, feet at y={player.Body.Y:F1})"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: chunks={view.ShownChunks} onGround={player.Body.OnGround}"); }

            await CheckCombatAsync(main);
            await CheckMusicAsync(main);
            await CheckInputAndMenusAsync(main);
            await CheckLanguageAsync(main);
            await CheckStoryAsync(main);
            CheckWulver(main);
            CheckWulverKeyRoute(main);
            await CheckWulverFramingAsync(main);
            await CheckWulverOwnNameSeedAsync(main.Settings);
            CheckFirePitsAndCat(main);
            CheckVolumeSlidersPersist(main);
            await CheckMiniMapAsync(main);
            CheckChestOpens(main);
            CheckPedlar(main);
            CheckTrapStrikes(main);
            CheckRunRecordsSaveAndReload(main);
            await CheckReplaySavedByARealRunAsync(main);
            CheckUiScaling(main);
            await CheckAGapIsCrossedAsync(main);
            await CheckABotCanPlayTheWholeDungeonAsync(main);
            await CheckCreaturesAsync(main);
            await CheckSeedChangesTheWorldAsync(main);
            main.QueueFree();
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>The adaptive music in the real scene: it renders in the background, builds its streams, follows the state and obeys the music volume.</summary>
    private async Task CheckMusicAsync(Main main)
    {
        const string name = "the adaptive music renders in the background, follows the game state and obeys the music volume";
        try
        {
            var music = main.Music;
            var deadline = Time.GetTicksMsec() + 60_000;
            while (Time.GetTicksMsec() < deadline && !music.IsReady && music.Failure == null) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            bool built = music.IsReady && music.Failure == null
                && Enum.GetValues<Hollowfall.Core.Audio.Music.MusicContext>().All(c => music.StreamCount(c) == Enum.GetValues<Hollowfall.Core.Audio.Music.MusicLayer>().Length);
            if (!built) { _failed++; GD.PrintErr($"  FAIL {name}: ready={music.IsReady} failure={music.Failure}"); return; }
            GD.Print($"    music: render {music.RenderMilliseconds:F0} ms, build {music.BuildMilliseconds:F0} ms, {music.BufferBytes / 1048576.0:F1} MB of audio, engine static memory {OS.GetStaticMemoryUsage() / 1048576.0:F0} MB");

            var layer = Hollowfall.Core.Audio.Music.MusicLayer.DrumLight;
            var context = Hollowfall.Core.Audio.Music.MusicContext.Plain;
            music.FollowGame = false;
            var calm = new Hollowfall.Core.Audio.Music.MusicState { Underground = true, Floor = 0 };
            music.State = calm;
            music.ResetMix();
            for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            double before = music.Mixer.Smoothed(layer);

            music.State = calm with { EncounterActive = true, EnemiesNear = 4, StyleRank = 4 };
            var until = Time.GetTicksMsec() + 8_000;
            while (Time.GetTicksMsec() < until && music.Mixer.Smoothed(layer) < 0.1) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            double after = music.Mixer.Smoothed(layer);
            bool follows = before < 0.01 && after >= 0.1 && music.Mixer.Combat;

            // The music volume turns the whole thing down and back up.
            var settings = main.Settings;
            double saved = settings.MusicVolume;
            settings.MusicVolume = 0;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            bool silent = Enum.GetValues<Hollowfall.Core.Audio.Music.MusicLayer>().All(l => music.DbOf(context, l) <= -79.9f);
            settings.MusicVolume = 1;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            float loud = music.DbOf(context, layer);
            double loudGain = music.GainOf(context, layer);
            settings.MusicVolume = 0.5;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            float half = music.DbOf(context, layer);
            double halfGain = music.GainOf(context, layer);
            settings.MusicVolume = saved;
            music.FollowGame = true;
            bool volume = silent && loud > -79 && Math.Abs(loud - Mathf.LinearToDb((float)(loudGain * 1.0 * settings.MasterVolume))) < 0.2 && Math.Abs(half - Mathf.LinearToDb((float)(halfGain * 0.5 * settings.MasterVolume))) < 0.2;

            if (follows && volume) { _passed++; GD.Print($"  ok   {name} (drum gain {before:F2} to {after:F2})"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: follows={follows} (drum {before:F3} to {after:F3}) volume={volume} (silent={silent}, {loud:F1} dB, half {half:F1} dB)"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>Combat and portals running inside the real scene: enemies get bodies, portals get nodes and cameras.</summary>
    private async Task CheckCombatAsync(Main main)
    {
        const string name = "combat and portals run in the scene";
        try
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            main.MovePlayerToFloor(0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var view = main.Player.View;
            var feet = new Vec3(view.Feet.X + 3, view.Feet.Y, view.Feet.Z);
            var enemy = combat.Sim.SpawnEnemy(EnemyKind.Husk, feet);

            // Fire both portals at the walls either side of the room.
            var east = view with { Forward = new Vec3(1, 0, 0) };
            var west = view with { Forward = new Vec3(-1, 0, 0) };
            combat.Step(1.0 / 60, east, new PlayerActions(false, false, false, PortalTeal: true));
            combat.Step(1.0 / 60, west, new PlayerActions(false, false, false, PortalViolet: true));
            for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            combat.UpdatePortalViews(main.Player.Camera);

            bool enemyDrawn = combat.GetNodeOrNull<EnemyView>($"Enemy_{enemy.Id}") != null;
            bool portalsDrawn = combat.GetNodeOrNull<PortalView>("Portal_Teal") != null && combat.GetNodeOrNull<PortalView>("Portal_Violet") != null;
            bool linked = combat.Sim.Portals.Linked;

            bool ok = enemyDrawn && portalsDrawn && linked;
            if (ok) { _passed++; GD.Print($"  ok   {name}"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: enemyDrawn={enemyDrawn} portalsDrawn={portalsDrawn} linked={linked}"); }

            await CheckHudStaysOffPortalsAsync(main, combat);
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>
    /// The HUD must never cover the view through a portal: with a portal dead ahead, the crosshair fades out, and it comes
    /// back when you look away.
    /// </summary>
    private async Task CheckHudStaysOffPortalsAsync(Main main, CombatView combat)
    {
        const string name = "the HUD fades off a portal opening and returns when you look away";
        try
        {
            var portal = combat.Sim.Portals.Teal ?? throw new InvalidOperationException("no teal portal");
            var n = portal.Frame.Normal;
            var at = portal.Frame.Center + n * 3;
            var hud = main.GetNode<Hud>("Hud");

            // Stand in front of the portal looking straight at it.
            main.Player.PlaceAt(new Vector3((float)at.X, (float)(portal.Frame.Center.Y - 2.62), (float)at.Z), Math.Atan2(n.X, n.Z));
            for (int i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            float onPortal = hud.CrosshairAlpha;

            // Face along the wall instead: both portals leave the screen (the other one is straight behind) and the crosshair returns.
            double dx = -n.Z, dz = n.X;
            main.Player.PlaceAt(new Vector3((float)at.X, (float)(portal.Frame.Center.Y - 2.62), (float)at.Z), Math.Atan2(-dx, -dz));
            for (int i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            float away = hud.CrosshairAlpha;

            bool ok = onPortal < 0.1f && away > 0.9f;
            if (ok) { _passed++; GD.Print($"  ok   {name}"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: alpha on portal {onPortal:F2}, looking away {away:F2}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>Every action is mapped, bindings survive the trip through Godot events, and the menus open, pause and rebind.</summary>
    private async Task CheckInputAndMenusAsync(Main main)
    {
        Check("every action is registered with its default inputs", () =>
        {
            foreach (var a in Enum.GetValues<GameAction>())
            {
                string name = InputMapper.Name(a);
                if (!InputMap.HasAction(name)) return false;
                if (InputMap.ActionGetEvents(name).Count != main.Settings.Bindings.All(a).Count()) return false;
            }
            return true;
        });

        Check("default bindings round-trip through Godot input events", () =>
        {
            foreach (var a in Enum.GetValues<GameAction>())
                foreach (var b in main.Settings.Bindings.All(a))
                {
                    var ev = InputMapper.ToEvent(b);
                    switch (ev)
                    {
                        case InputEventKey k: k.Pressed = true; break;
                        case InputEventMouseButton m: m.Pressed = true; break;
                        case InputEventJoypadButton j: j.Pressed = true; break;
                    }
                    if (ev == null || InputMapper.FromEvent(ev, includeGamepad: true, includeKeyboardMouse: true) != b) return false;
                }
            return true;
        });

        Check("a rebind changes the live input map and survives saving", () =>
        {
            var settings = main.Settings;
            if (settings.Bindings.Rebind(GameAction.Parry, Binding.Key("K")) != RebindOutcome.Ok) return false;
            InputMapper.Apply(settings.Bindings, settings.Deadzone);
            bool mapped = InputMap.ActionGetEvents(InputMapper.Name(GameAction.Parry)).OfType<InputEventKey>().Any(k => k.PhysicalKeycode == Key.K);
            bool saved = settings.TrySave(Main.SettingsPath) && GameSettings.Load(Main.SettingsPath).Bindings.Get(GameAction.Parry, Slot.KeyboardMouse) == Binding.Key("K");
            settings.Bindings.ResetToDefaults();
            InputMapper.Apply(settings.Bindings, settings.Deadzone);
            return mapped && saved;
        });

        const string menuName = "menus pause the game, open every screen and resume";
        try
        {
            var menus = main.Menus;
            menus.Open(MenuScreen.Pause);
            bool paused = GetTree().Paused && menus.Current == MenuScreen.Pause;
            menus.OpenFrom(MenuScreen.Pause, MenuScreen.Controls);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            bool controls = menus.Current == MenuScreen.Controls;
            menus.OpenFrom(MenuScreen.Pause, MenuScreen.Settings);
            bool settings = menus.Current == MenuScreen.Settings;
            menus.Close();
            bool resumed = !GetTree().Paused && !menus.IsOpen;

            bool ok = paused && controls && settings && resumed;
            if (ok) { _passed++; GD.Print($"  ok   {menuName}"); }
            else { _failed++; GD.PrintErr($"  FAIL {menuName}: paused={paused} controls={controls} settings={settings} resumed={resumed}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {menuName}: {e.Message}");
        }
    }

    /// <summary>The story inside the real scene: a note can be walked up to and read, is remembered and saved, and a floor card fades in.</summary>
    private async Task CheckStoryAsync(Main main)
    {
        const string name = "a note can be read: the panel opens and pauses, the journal remembers it, and it is saved";
        try
        {
            var story = main.Story;
            var ui = main.StoryUi;
            if (story.Spots.Count == 0) throw new InvalidOperationException("the dungeon holds no notes");
            var spot = story.Spots[0];
            var feet = new Hollowfall.Core.Combat.Vec3(spot.X + spot.FaceX, spot.Y, spot.Z + spot.FaceZ);

            var prompted = story.Filter(new Hollowfall.Core.Combat.PlayerActions(false, false, false), feet, alive: true);
            bool promptOnly = !ui.Modal && !prompted.Interact;

            var used = story.Filter(new Hollowfall.Core.Combat.PlayerActions(false, false, false, Interact: true), feet, alive: true);
            bool opened = ui.Modal && ui.CurrentNoteId == spot.Note.Id && ui.ReaderText == spot.Note.Text && GetTree().Paused && !used.Interact;
            bool remembered = story.IsRead(spot.Note.Id) && story.Journal().Any(j => j.Note.Id == spot.Note.Id);
            bool saved = StoryProgress.Load(Main.StoryPath).IsRead(main.SeedName, spot.Note.Id);

            ui.Close();
            bool closed = !ui.Modal && !GetTree().Paused;

            var menus = main.Menus;
            menus.OpenFrom(MenuScreen.Pause, MenuScreen.Journal);
            bool journal = menus.Current == MenuScreen.Journal;
            menus.Close();

            story.OnFloor(0);
            var deadline = Time.GetTicksMsec() + 5_000;
            while (Time.GetTicksMsec() < deadline && !ui.CardVisible) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            bool card = ui.CardVisible && ui.CardTitle == story.Plan.Cards[0].Title;
            ui.ClearCaption();

            bool ok = promptOnly && opened && remembered && saved && closed && journal && card;
            if (ok) { _passed++; GD.Print($"  ok   {name}"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: promptOnly={promptOnly} opened={opened} remembered={remembered} saved={saved} closed={closed} journal={journal} card={card}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>The Wulver in the real scene: standing beside him, Interact opens his conversation; a path through it gives the gift once and is saved.</summary>
    private void CheckWulver(Main main)
    {
        Check("the Wulver greets, opens his choice panel, gives the gift once, remembers what was chosen and never speaks the maker's name on an ordinary seed", () =>
        {
            var wulver = main.Wulver ?? throw new InvalidOperationException("no Wulver in the world");
            var view = main.WulverView!;
            var ui = main.StoryUi;
            var vitals = main.Combat!.Sim.Vitals;
            var spot = wulver.Encounter.Spot;
            var near = new Hollowfall.Core.Combat.Vec3(spot.X + 1.5, spot.Y, spot.Z);
            var press = new Hollowfall.Core.Combat.PlayerActions(false, false, false, Interact: true);
            string seen = "";

            vitals.Damage(50);
            bool hurt = vitals.Health < 100 && vitals.HardDamage > 0;
            var far = wulver.Filter(press, new Hollowfall.Core.Combat.Vec3(spot.X + 30, spot.Y, spot.Z), alive: true);
            bool ignoredFar = far.Interact && !ui.DialogueOpen && wulver.Greeted == 0;

            // Walking up: he greets once, with no key press, and nothing more until you have gone and come back.
            wulver.Filter(new Hollowfall.Core.Combat.PlayerActions(false, false, false), new Hollowfall.Core.Combat.Vec3(spot.X + 6, spot.Y, spot.Z), alive: true);
            bool greeted = wulver.Greeted == 1 && ui.SpeakerText == WulverDialogue.Name && ui.CaptionText == WulverDialogue.GreetingFirst;
            wulver.Filter(new Hollowfall.Core.Combat.PlayerActions(false, false, false), new Hollowfall.Core.Combat.Vec3(spot.X + 5, spot.Y, spot.Z), alive: true);
            greeted &= wulver.Greeted == 1;

            var used = wulver.Filter(press, near, alive: true);
            bool opened = ui.DialogueOpen && !used.Interact && ui.DialogueText.Contains(WulverDialogue.Name) && ui.DialogueChoices.Count >= 2 && GetTree().Paused;
            seen += ui.DialogueText;
            ui.PressDialogueChoice(1); // what is this place
            seen += ui.DialogueText;
            bool offered = ui.DialogueChoices.Any(c => c.StartsWith("You mentioned a gift"));
            ui.PressDialogueChoice(1); // the gift
            seen += ui.DialogueText;
            ui.PressDialogueChoice(0); // take it
            seen += ui.DialogueText;
            bool gift = vitals.Health == 100 && vitals.HardDamage == 0 && main.Combat.Sim.Items.Perks.ParryWindowBonus > 0 && view.FishVisible;
            ui.PressDialogueChoice(0); // thank you -> the fish
            ui.PressDialogueChoice(0); // leave it on a windowsill
            seen += ui.DialogueText;
            ui.Close();
            bool closed = !ui.DialogueOpen && !GetTree().Paused;

            var saved = Hollowfall.Core.Story.StoryProgress.Load(Main.StoryPath);
            bool remembered = saved.WulverVisits(main.SeedName) == 1 && saved.WulverFlags(main.SeedName).Contains(WulverDialogue.GaveFishBack);

            // A return visit: the gift is not offered again, and claiming to be the maker on an ordinary seed earns only the kind reply.
            vitals.Damage(30);
            main.Combat.Sim.Items.Perks.Clear();
            wulver.Filter(new Hollowfall.Core.Combat.PlayerActions(false, false, false), new Hollowfall.Core.Combat.Vec3(spot.X + 30, spot.Y, spot.Z), alive: true);
            var again = wulver.Filter(press, near, alive: true);
            bool back = ui.DialogueOpen && !again.Interact && !ui.DialogueChoices.Any(c => c.StartsWith("About that gift")) && vitals.Health < 100;
            seen += ui.DialogueText;
            ui.PressDialogueChoice(0); // sit a while
            ui.PressDialogueChoice(2); // I made this place
            seen += ui.DialogueText;
            bool kind = ui.DialogueText.Contains("A good many folk say that");
            ui.Close();
            bool quiet = !seen.Contains(WulverDialogue.MakerSeeds[0], StringComparison.OrdinalIgnoreCase);

            ui.ClearCaption();
            vitals.Reset();
            main.Combat.Sim.Items.Perks.Clear();

            bool font = UiFonts.FaceWithGlyphs(WulverDialogue.Name, UiFonts.Display).HasChar('\u00f2');
            GD.Print($"    wulver: hurt={hurt} ignoredFar={ignoredFar} greeted={greeted} opened={opened} offered={offered} gift={gift} closed={closed} remembered={remembered} back={back} kind={kind} quiet={quiet} font={font}");
            return hurt && ignoredFar && greeted && opened && offered && gift && closed && remembered && back && kind && quiet && font;
        });
    }

    /// <summary>The maker's name as the dialogue file spells it (the seeds list holds it lower case); built here so no authored string names him.</summary>
    private static string MakerWord => char.ToUpperInvariant(WulverDialogue.MakerSeeds[0][0]) + WulverDialogue.MakerSeeds[0][1..];

    /// <summary>Presses the first choice whose text starts with <paramref name="prefix"/>; false if there is none.</summary>
    private static bool PressChoice(StoryUi ui, string prefix)
    {
        var choices = ui.DialogueChoices;
        for (int i = 0; i < choices.Count; i++)
            if (choices[i].StartsWith(prefix, StringComparison.Ordinal)) { ui.PressDialogueChoice(i); return true; }
        return false;
    }

    /// <summary>
    /// The key route in the real scene, on an ordinary seed: having claimed the hill earlier (the check above did, on a return visit), the player
    /// stands on the last floor holding a key, speaks to him, shows it, and he now knows the maker. It is saved.
    /// </summary>
    private void CheckWulverKeyRoute(Main main)
    {
        Check("the Wulver, shown a key from the last floor by someone who claimed the hill, recognises the maker and saves it", () =>
        {
            var wulver = main.Wulver ?? throw new InvalidOperationException("no Wulver in the world");
            var ui = main.StoryUi;
            var spot = wulver.Encounter.Spot;
            var near = new Hollowfall.Core.Combat.Vec3(spot.X + 1.5, spot.Y, spot.Z);
            var press = new Hollowfall.Core.Combat.PlayerActions(false, false, false, Interact: true);
            var realFacts = wulver.Facts;
            // On the last floor, with a key in the pack.
            wulver.Facts = () => realFacts() with { DeepestFloor = realFacts().FloorCount - 1, HasKey = true };
            bool before = !Hollowfall.Core.Story.StoryProgress.Load(Main.StoryPath).WulverFlags(main.SeedName).Contains(WulverDialogue.MakerKnown);
            wulver.Filter(new Hollowfall.Core.Combat.PlayerActions(false, false, false), new Hollowfall.Core.Combat.Vec3(spot.X + 30, spot.Y, spot.Z), alive: true);
            wulver.Filter(press, near, alive: true);
            bool offered = ui.DialogueOpen && ui.DialogueChoices.Any(c => c.StartsWith("Show him the key"));
            string asked = ui.DialogueText;
            bool shown = PressChoice(ui, "Show him the key");
            bool maker = ui.DialogueText.Contains(MakerWord, StringComparison.Ordinal);
            ui.Close();
            wulver.Facts = realFacts;
            var flags = Hollowfall.Core.Story.StoryProgress.Load(Main.StoryPath).WulverFlags(main.SeedName);
            bool known = flags.Contains(WulverDialogue.MakerKnown) && flags.Contains(WulverDialogue.MakerKey);
            bool askedQuiet = !asked.Contains(MakerWord, StringComparison.Ordinal);
            GD.Print($"    wulver key route: before={before} offered={offered} shown={shown} maker={maker} known={known} askedQuiet={askedQuiet}");
            return before && offered && shown && maker && known && askedQuiet && !ui.DialogueOpen && !GetTree().Paused;
        });
    }

    /// <summary>While his panel is open the camera frames his whole figure in the screen above the panel, and the player's own view returns on close.</summary>
    private async Task CheckWulverFramingAsync(Main main)
    {
        const string name = "while the Wulver talks the camera keeps his whole figure above the panel, and the player's view returns on close";
        try
        {
            var wulver = main.Wulver ?? throw new InvalidOperationException("no Wulver in the world");
            var framing = wulver.Framing ?? throw new InvalidOperationException("no conversation camera");
            var ui = main.StoryUi;
            var player = main.GetNode<PlayerController>("Player");
            var spot = wulver.Encounter.Spot;
            double fx = -Math.Sin(spot.Yaw), fz = -Math.Cos(spot.Yaw);
            double px = spot.X + fx * 2.4, pz = spot.Z + fz * 2.4;
            int ground = main.World.Generator.SampleColumn((int)Math.Floor(px), (int)Math.Floor(pz)).Height;
            player.PlaceAt(new Vector3((float)px, ground + 1.2f, (float)pz), Math.Atan2(px - spot.X, pz - spot.Z));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            // The headless window is tiny; give it a real size and a plain scale so the panel and the projection mean something.
            var window = main.GetWindow();
            var oldSize = window.Size;
            bool oldAuto = main.Settings.AutoUiScale;
            double oldScale = main.Settings.UiScale;
            window.Size = new Vector2I(1280, 720);
            main.Settings.AutoUiScale = false;
            main.Settings.UiScale = 1.0;
            main.ApplyUiScale();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            wulver.Talk();
            var settle = Time.GetTicksMsec() + 4000;
            while ((framing.Amount < 0.999 || Time.GetTicksMsec() < settle - 3500) && Time.GetTicksMsec() < settle) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var camera = player.Camera;
            double viewH = ui.GetViewport().GetVisibleRect().Size.Y, viewW = ui.GetViewport().GetVisibleRect().Size.X;
            double panelTop = ui.DialoguePanelTop * viewH;
            var feet = new Vector3((float)spot.X, (float)spot.Y - 0.35f, (float)spot.Z);
            var head = new Vector3((float)spot.X, (float)(spot.Y + 2.75), (float)spot.Z);
            var f = camera.UnprojectPosition(feet);
            var h = camera.UnprojectPosition(head);
            bool inFront = !camera.IsPositionBehind(feet) && !camera.IsPositionBehind(head);
            bool visible = inFront && h.Y >= 0 && h.X >= 0 && h.X <= viewW && f.X >= 0 && f.X <= viewW && f.Y <= panelTop;
            bool framed = framing.Amount > 0.99 && framing.Active;
            GD.Print($"    wulver framing: head=({h.X:F0},{h.Y:F0}) feet=({f.X:F0},{f.Y:F0}) panelTop={panelTop:F0} of {viewW:F0}x{viewH:F0} dolly={framing.Dolly:F1}");

            ui.Close();
            var until = Time.GetTicksMsec() + 4000;
            while (framing.Active && Time.GetTicksMsec() < until) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            bool home = !framing.Active && camera.Position.IsEqualApprox(Vector3.Zero) && Math.Abs(camera.Rotation.Y) < 0.001f;
            window.Size = oldSize;
            main.Settings.AutoUiScale = oldAuto;
            main.Settings.UiScale = oldScale;
            main.ApplyUiScale();
            Report(name, visible && framed && home, $"visible={visible} framed={framed} home={home}");
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>The maker's own seed in a freshly built scene: claiming to have made the hill earns the maker's own lines, and what he knows is saved.</summary>
    private async Task CheckWulverOwnNameSeedAsync(GameSettings original)
    {
        const string name = "on the maker's own seed the Wulver answers the claim to have made the hill with the maker's lines and saves that he knows";
        try
        {
            if (!new GameSettings { Seed = WulverDialogue.MakerSeeds[0] }.TrySave(Main.SettingsPath)) throw new InvalidOperationException("could not save the settings file");
            var scene = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
            AddChild(scene);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var wulver = scene.Wulver ?? throw new InvalidOperationException("no Wulver on the maker's seed");
            var ui = scene.StoryUi;
            var spot = wulver.Encounter.Spot;
            var near = new Hollowfall.Core.Combat.Vec3(spot.X + 1.5, spot.Y, spot.Z);
            var press = new Hollowfall.Core.Combat.PlayerActions(false, false, false, Interact: true);
            bool seed = scene.SeedName == WulverDialogue.MakerSeeds[0];
            wulver.Filter(press, near, alive: true);
            bool opened = ui.DialogueOpen;
            string seen = ui.DialogueText;
            PressChoice(ui, "Who are you");
            seen += ui.DialogueText;
            bool claimed = PressChoice(ui, "I built this hill");
            bool maker = ui.DialogueText.Contains(MakerWord, StringComparison.Ordinal);
            for (int i = 0; i < 6 && ui.DialogueOpen; i++) PressChoice(ui, "Go on");
            ui.Close();
            bool quietBefore = !seen.Contains(MakerWord, StringComparison.Ordinal);
            bool known = Hollowfall.Core.Story.StoryProgress.Load(Main.StoryPath).WulverFlags(scene.SeedName).Contains(WulverDialogue.MakerKnown);

            // Back again later, he greets the maker by name.
            wulver.Filter(new Hollowfall.Core.Combat.PlayerActions(false, false, false), new Hollowfall.Core.Combat.Vec3(spot.X + 30, spot.Y, spot.Z), alive: true);
            wulver.Filter(press, near, alive: true);
            bool returns = ui.DialogueOpen && ui.DialogueText.Contains(MakerWord, StringComparison.Ordinal);
            ui.Close();

            scene.QueueFree();
            Report(name, seed && opened && claimed && maker && quietBefore && known && returns, $"seed={seed} opened={opened} claimed={claimed} maker={maker} quietBefore={quietBefore} known={known} returns={returns}");
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
        finally
        {
            original.TrySave(Main.SettingsPath);
        }
    }

    /// <summary>
    /// Fire pits and the cat in the real scene: pits are drawn and lit, the cat sleeps beside one and wakes as you come near, turns his head, the
    /// conversation opens, sitting gives a little warmth once, what he remembers is saved to the temporary story file, and the third visit says the warm line.
    /// </summary>
    private void CheckFirePitsAndCat(Main main)
    {
        Check("fire pits are drawn with real light, and the cat wakes, talks, warms you once and remembers, saving only to the temporary story file", () =>
        {
            var cat = main.Cat ?? throw new InvalidOperationException("no cat in the world");
            var view = main.CatView!;
            var props = main.Props ?? throw new InvalidOperationException("no props");
            var ui = main.StoryUi;
            var vitals = main.Combat!.Sim.Vitals;
            var spot = cat.Encounter.Spot;
            var press = new PlayerActions(false, false, false, Interact: true);
            var idle = new PlayerActions(false, false, false);
            var near = new Vec3(spot.X + 1.5, spot.Y, spot.Z);
            var gaze = new Vector3(0, 0, -1);

            bool pits = props.PitCount > 0;
            var pitPos = props.PitPositions.Any(p => Math.Abs(p.X - (float)spot.Pit.X) < 0.01f && Math.Abs(p.Z - (float)spot.Pit.Z) < 0.01f);
            props.Update(0.1, new Vector3((float)spot.X + 3, (float)spot.Y, (float)spot.Z));
            bool lit = props.LightsOn > 0;
            double apart = Math.Sqrt(Math.Pow(spot.X - spot.Pit.X, 2) + Math.Pow(spot.Z - spot.Pit.Z, 2));
            bool beside = apart > 0.95 && apart < 1.6;

            // Far away he sleeps; near him he wakes and his head follows the player.
            var far = new Vector3((float)spot.X + 30, (float)spot.Y, (float)spot.Z);
            for (int i = 0; i < 30; i++) view.Update(0.05, far, gaze);
            bool asleep = view.Awake < 0.05f && view.EyesOpen < 0.05f;
            var side = new Vector3((float)spot.X + 4f, (float)spot.Y, (float)spot.Z + 4f);
            int woke = 0;
            view.Woke += () => woke++;
            for (int i = 0; i < 80; i++) view.Update(0.05, side, (new Vector3((float)spot.X, (float)spot.Y, (float)spot.Z) - side).Normalized());
            bool awake = view.Awake > 0.95f && view.EyesOpen > 0.05f && woke == 1;
            float yawA = view.HeadYaw;
            var other = new Vector3((float)spot.X - 4f, (float)spot.Y, (float)spot.Z - 4f);
            for (int i = 0; i < 80; i++) view.Update(0.05, other, gaze);
            bool follows = Math.Abs(view.HeadYaw - yawA) > 0.3f;
            bool purrs = view.PurrLevel > 0;
            for (int i = 0; i < 400; i++) view.Update(0.05, far, gaze);
            bool sleepsAgain = view.Awake < 0.05f;

            // The first sight is a single caption; a press far away does nothing.
            vitals.Damage(50);
            double hurt = vitals.Health;
            bool hardDamage = vitals.HardDamage > 0;
            var ignored = cat.Filter(press, new Vec3(spot.X + 30, spot.Y, spot.Z), alive: true);
            bool ignoredFar = ignored.Interact && !ui.DialogueOpen;
            cat.Filter(idle, new Vec3(spot.X + 6, spot.Y, spot.Z), alive: true);
            bool caption = cat.Sighted == 1 && ui.CaptionText == CatDialogue.FirstSight;
            cat.Filter(idle, new Vec3(spot.X + 5, spot.Y, spot.Z), alive: true);
            caption &= cat.Sighted == 1;

            // Walk up and speak: blink back, then sit by the fire.
            var used = cat.Filter(press, near, alive: true);
            bool opened = ui.DialogueOpen && !used.Interact && ui.DialogueText.Contains("Dia duit") && ui.DialogueChoices.Count >= 2 && GetTree().Paused;
            ui.PressDialogueChoice(0); // blink back
            bool offered = ui.DialogueChoices.Any(c => c.StartsWith("Sit down"));
            ui.PressDialogueChoice(0); // sit down
            bool warmed = vitals.HardDamage == 0 && vitals.Health > hurt && vitals.Health <= hurt + CatEncounter.WarmthHeal + 0.01 && vitals.Health < 100;
            double afterSit = vitals.Health;
            ui.PressDialogueChoice(2); // goodbye, which ends on a good night
            bool nightLine = ui.DialogueText.Contains("Oíche mhaith agat");
            ui.PressDialogueChoice(0); // go
            bool closed = nightLine && !ui.DialogueOpen && !GetTree().Paused;
            var saved = StoryProgress.Load(Main.StoryPath);
            bool remembered = saved.CatVisits(main.SeedName) == 1 && saved.CatFlags(main.SeedName).Contains(CatDialogue.Sat) && saved.CatFlags(main.SeedName).Contains(CatDialogue.Blinked);

            // A second visit: the warmth is not given again.
            vitals.Damage(30);
            double hurtAgain = vitals.Health;
            cat.Filter(idle, new Vec3(spot.X + 30, spot.Y, spot.Z), alive: true);
            cat.Filter(press, near, alive: true);
            bool back = ui.DialogueOpen && ui.DialogueText.Contains("Friend") && !ui.DialogueChoices.Any(c => c.StartsWith("Sit down"));
            ui.PressDialogueChoice(1); // stroke him
            ui.Close();
            bool once = Math.Abs(vitals.Health - hurtAgain) < 0.001 && afterSit > hurt;

            // The third visit says the warm line, once.
            cat.Filter(press, near, alive: true);
            bool warm = ui.DialogueOpen && ui.DialogueText.Contains("Tine bheag") && ui.DialogueText.Contains("A little fire");
            ui.Close();
            cat.Filter(press, near, alive: true);
            bool notAgain = ui.DialogueOpen && !ui.DialogueText.Contains("Tine bheag");
            ui.Close();
            var savedAgain = StoryProgress.Load(Main.StoryPath);
            bool visits = savedAgain.CatVisits(main.SeedName) == 4 && savedAgain.CatFlags(main.SeedName).Contains(CatDialogue.HeardSmall);

            ui.ClearCaption();
            vitals.Reset();
            bool font = UiFonts.FaceWithGlyphs(CatDialogue.Name, UiFonts.Display).HasChar('\u00e1');
            GD.Print($"    cat: pits={pits} pitPos={pitPos} lit={lit} beside={beside} asleep={asleep} awake={awake} follows={follows} purrs={purrs} sleepsAgain={sleepsAgain} hurt={hardDamage} ignoredFar={ignoredFar} caption={caption} opened={opened} offered={offered} warmed={warmed} closed={closed} remembered={remembered} back={back} once={once} warm={warm} notAgain={notAgain} visits={visits} font={font}");
            return pits && pitPos && lit && beside && asleep && awake && follows && purrs && sleepsAgain && hardDamage && ignoredFar && caption && opened && offered && warmed && closed && remembered && back && once && warm && notAgain && visits && font;
        });

        Check("the crackle and purr loops play at a level set by distance and stop at silence", () =>
        {
            var audio = new AudioDirector();
            AddChild(audio);
            audio.SetLoopLevel(Hollowfall.Core.Audio.SoundId.FireCrackle, 0.8);
            audio.SetLoopLevel(Hollowfall.Core.Audio.SoundId.CatPurr, 0.5);
            audio._Process(0.1);
            bool on = audio.LoopPlaying(Hollowfall.Core.Audio.SoundId.FireCrackle) && audio.LoopPlaying(Hollowfall.Core.Audio.SoundId.CatPurr);
            audio.SetLoopLevel(Hollowfall.Core.Audio.SoundId.FireCrackle, 0);
            audio._Process(0.1);
            bool off = !audio.LoopPlaying(Hollowfall.Core.Audio.SoundId.FireCrackle) && audio.LoopLevel(Hollowfall.Core.Audio.SoundId.CatPurr) == 0.5;
            audio.QueueFree();
            return on && off;
        });
    }

    // ---- gameplay checks driven through the real scene -------------------------------------------

    private static PlayerView Stand(Vec3 feet) => new(feet, feet + new Vec3(0, 1.62, 0), new Vec3(0, 0, -1), false, false);

    private static IEnumerable<T> Descendants<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    /// <summary>Moving the real sliders on the Settings screen changes the settings, and leaving the screen saves them to disk.</summary>
    private void CheckVolumeSlidersPersist(Main main)
    {
        const string name = "the volume sliders change the settings and persist across a reload";
        try
        {
            var menus = main.Menus;
            HSlider SliderFor(string label) =>
                Descendants<HBoxContainer>(menus).First(row => row.GetChildCount() > 1 && row.GetChild(0) is Label l && l.Text == label && row.GetChild(1) is HSlider).GetChild<HSlider>(1);

            menus.OpenFrom(MenuScreen.Pause, MenuScreen.Settings);
            var master = SliderFor("Master volume");
            var effects = SliderFor("Effects volume");
            var ambience = SliderFor("Ambience volume");
            (double m, double e, double a) original = (master.Value, effects.Value, ambience.Value);

            master.Value = 0.35;
            effects.Value = 0.6;
            ambience.Value = 0.15;
            bool live = Near(main.Settings.MasterVolume, 0.35) && Near(main.Settings.EffectsVolume, 0.6) && Near(main.Settings.AmbienceVolume, 0.15);

            menus.Open(MenuScreen.Pause); // leaving the Settings screen saves
            var loaded = GameSettings.Load(Main.SettingsPath);
            bool saved = Near(loaded.MasterVolume, 0.35) && Near(loaded.EffectsVolume, 0.6) && Near(loaded.AmbienceVolume, 0.15);

            menus.OpenFrom(MenuScreen.Pause, MenuScreen.Settings);
            master.Value = original.m;
            effects.Value = original.e;
            ambience.Value = original.a;
            menus.Close();
            var restored = GameSettings.Load(Main.SettingsPath);
            bool back = Near(restored.MasterVolume, original.m) && Near(restored.EffectsVolume, original.e) && Near(restored.AmbienceVolume, original.a);

            Report(name, live && saved && back, $"live={live} saved={saved} restored={back}");
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>The minimap fills in the cells around the player, and fills in more when they walk somewhere new.</summary>
    private async Task CheckMiniMapAsync(Main main)
    {
        const string name = "the minimap reveals new cells after the player moves";
        try
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            var site = combat.Site;
            var layout = site.Floors[0];
            main.MovePlayerToFloor(0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var map = combat.Sim.Map;
            int seenBefore = map.SeenCount(0), versionBefore = map.Version;

            // A room that is neither the entrance nor the exit, so the player is not dropped into a stair shaft.
            var room = layout.Rooms.First(r => r != layout.EntranceRoom && r != layout.ExitRoom && layout.CellAt(r.CenterX, r.CenterZ) == CellKind.Floor);
            var (wx, wy, wz) = site.ToWorld(0, room.CenterX, room.CenterZ);
            main.Player.PlaceAt(new Vector3(wx + 0.5f, wy, wz + 0.5f), 0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            int seenAfter = map.SeenCount(0);
            var (cx, cz) = map.CellOf(wx + 0.5, wz + 0.5);
            var minimap = Descendants<MiniMap>(main.GetNode<Hud>("Hud")).FirstOrDefault();
            bool revealed = seenBefore > 0 && seenAfter > seenBefore && map.Version > versionBefore && map.IsSeen(0, cx, cz);
            bool shown = minimap is { Visible: true };
            Report(name, revealed && shown, $"seen {seenBefore} -> {seenAfter}, version {versionBefore} -> {map.Version}, minimap visible={shown}");
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>The pedlar appears on the plaza, opens his pack and sells a cream for coin, then closes.</summary>
    private void CheckPedlar(Main main)
    {
        const string name = "the pedlar trades on the plaza and takes coin for a cream";
        try
        {
            var sim = main.Combat!.Sim;
            var spot = Merchant.PedlarSpot(main.Combat.Site);
            main.Player.PlaceAt(new Vector3((float)spot.X, (float)spot.Y, (float)spot.Z + 1.5f), 0);
            for (int i = 0; i < 30 && !sim.Enemies.Any(e => e.Kind == EnemyKind.Pedlar); i++) sim.Tick(1.0 / 60, main.Player.View, default);
            var pedlar = sim.Enemies.FirstOrDefault(e => e.Kind == EnemyKind.Pedlar);
            if (pedlar == null) { _failed++; GD.PrintErr($"  FAIL {name}: no pedlar on the plaza"); return; }

            main.Player.PlaceAt(new Vector3((float)pedlar.Feet.X, (float)pedlar.Feet.Y, (float)pedlar.Feet.Z + 1.5f), 0);
            var character = main.Menus.CharacterProvider()!;
            character.GainCoin(100);
            int before = character.Coin;

            main.Pedlar!.Filter(new PlayerActions(false, false, false, Interact: true), main.Player.View.Feet, true, sim.Enemies);
            bool opened = main.StoryUi.DialogueOpen;
            bool hasCream = main.StoryUi.DialogueChoices.Any(c => c.Contains("Brownie's cream") && c.Contains("coin"));
            main.StoryUi.PressDialogueChoice(0);
            bool bought = character.Coin == before - Merchant.PriceFor(ItemKind.BrownieCream, ReputationTier.Neutral)!.Value
                && character.Items.Count(ItemKind.BrownieCream) == 1;
            main.StoryUi.Close();

            if (opened && hasCream && bought) { _passed++; GD.Print($"  ok   {name} (coin {before} -> {character.Coin})"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: opened={opened} cream={hasCream} bought={bought} coin={character.Coin}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>Pressing Interact beside a real, unlocked chest opens it (the sim state that turns the lid) and says so.</summary>
    private void CheckChestOpens(Main main)
    {
        Check("interact opens a chest in range and its state changes", () =>
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            var sim = combat.Sim;
            sim.Respawn(); // full health, no enemies, nothing in the way
            var chest = sim.Items.Chests.FirstOrDefault(c => !c.Locked && !c.Opened && LootTable.ItemFor(c.Loot.Kind) != null)
                ?? throw new InvalidOperationException("no unopened item chest in this dungeon");
            var spawn = chest.Spawn;
            var item = LootTable.ItemFor(chest.Loot.Kind)!.Value;
            var character = main.Menus.CharacterProvider() ?? throw new InvalidOperationException("no character");
            int before = character.Items.Count(item);
            var view = Stand(new Vec3(spawn.X + 1.5, spawn.Y, spawn.Z + 0.5)); // a block away from the chest's middle

            combat.Step(1.0 / 60, view, default);
            bool closedWithoutInteract = !chest.Opened;
            combat.Step(1.0 / 60, view, new PlayerActions(false, false, false, Interact: true));
            bool opened = chest.Opened && combat.LastEvents.OfType<ChestOpened>().Any(o => o.SpawnIndex == chest.Index && !o.WasLocked);
            bool stored = character.Items.Count(item) == before + LootTable.ItemCount(chest.Loot);
            if (!(closedWithoutInteract && opened && stored)) GD.PrintErr($"    chest: closedWithoutInteract={closedWithoutInteract} opened={opened} stored={stored} before={before}");
            return closedWithoutInteract && opened && stored;
        });
    }

    /// <summary>Standing on a spike trap, the player loses exactly Interactables.TrapDamage at the strike moment.</summary>
    private void CheckTrapStrikes(Main main)
    {
        Check("a spike trap hurts a player standing on it by the trap damage", () =>
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            var sim = combat.Sim;
            sim.Respawn();
            int index = combat.Site.Spawns.Select((s, i) => (s, i)).First(t => t.s.Kind == SpawnKind.Trap).i;
            var spawn = combat.Site.Spawns[index];
            var view = Stand(new Vec3(spawn.X + 0.5, spawn.Y, spawn.Z + 0.5));

            // Step through at most two trap cycles. The first strike that is the only damage in its step is the one to measure.
            for (int i = 0; i < (int)(2 * Interactables.TrapPeriod * 60) + 60 && !sim.Dead; i++)
            {
                double before = sim.Vitals.Health;
                combat.Step(1.0 / 60, view, default);
                if (combat.LastEvents.OfType<TrapSprung>().FirstOrDefault(t => t.SpawnIndex == index) is not { } sprung) continue;
                if (combat.LastEvents.OfType<PlayerHurt>().Count() != 1) continue; // something else hit in the same step; wait for the next strike
                double lost = before - sim.Vitals.Health;
                bool ok = Near(lost, Interactables.TrapDamage) && Near(sprung.Damage, Interactables.TrapDamage);
                if (!ok) GD.PrintErr($"    trap: lost {lost:F2} health, event says {sprung.Damage:F2}, expected {Interactables.TrapDamage}");
                return ok;
            }
            GD.PrintErr("    trap: no strike was seen");
            return false;
        });
    }

    /// <summary>Finishing a floor is saved to the records file by the real scene, and loads back as the personal best.</summary>
    private void CheckRunRecordsSaveAndReload(Main main)
    {
        Check("a completed floor is saved to the run records and reloads as the personal best", () =>
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            var sim = combat.Sim;
            var site = combat.Site;
            string path = Main.RecordsPath;
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

            sim.Respawn();
            // Walk down the stairs: stand on floor 0, then on floor 1, over cells that hold no encounter.
            var onFloor0 = Stand(new Vec3(site.OriginX + 1.5, DungeonSite.FloorY(0) + 1, site.OriginZ + 1.5));
            var onFloor1 = Stand(new Vec3(site.OriginX + 1.5, DungeonSite.FloorY(1) + 1, site.OriginZ + 1.5));
            combat.Step(1.0 / 60, onFloor0, default);
            combat.Step(1.0 / 60, onFloor1, default);
            var done = combat.LastEvents.OfType<FloorCompleted>().FirstOrDefault() ?? throw new InvalidOperationException("the floor was not completed");

            var expected = new FloorRecord(done.Result.Overall, done.Result.Points, done.Seconds);
            var back = RunRecords.Load(path).Best(main.Settings.Seed, done.Floor);
            bool fileWritten = System.IO.File.Exists(path);
            bool same = back is { } b && b.Grade == expected.Grade && b.Points == expected.Points && Near(b.Seconds, expected.Seconds);
            if (!(fileWritten && same)) GD.PrintErr($"    records: file={fileWritten} expected {expected} got {back}");
            return fileWritten && same;
        });
    }

    /// <summary>The interface scale follows the settings and lands on the real window.</summary>
    private void CheckUiScaling(Main main)
    {
        Check("the interface size follows the settings and is applied to the window", () =>
        {
            var settings = main.Settings;
            double userBefore = settings.UiScale;
            bool autoBefore = settings.AutoUiScale;
            try
            {
                var window = main.GetWindow();
                settings.AutoUiScale = false;
                settings.UiScale = 1.5;
                main.ApplyUiScale();
                bool fixedScale = Near(main.UiScaleApplied, 1.5) && Near(window.ContentScaleFactor, 1.5);

                settings.UiScale = 1.0;
                main.ApplyUiScale();
                bool back = Near(window.ContentScaleFactor, 1.0);

                settings.AutoUiScale = true;
                main.ApplyUiScale();
                double expected = UiScaling.Factor(window.Size.X, window.Size.Y, 1.0, true);
                bool automatic = Near(window.ContentScaleFactor, expected);
                if (!(fixedScale && back && automatic)) GD.PrintErr($"    ui scale: fixed={fixedScale} back={back} automatic={automatic} (window {window.Size}, expected {expected})");
                return fixedScale && back && automatic;
            }
            finally
            {
                settings.UiScale = userBefore;
                settings.AutoUiScale = autoBefore;
                main.ApplyUiScale();
            }
        });
    }

    /// <summary>
    /// The live physics loop records the run: the previous check completed floor 0 in the real scene, so a replay of it, made
    /// from the inputs the physics loop captured, must be on disk beside the records, decode, and belong to this seed.
    /// </summary>
    private Task CheckReplaySavedByARealRunAsync(Main main)
    {
        Check("a floor completed in the real scene saves its replay", () =>
        {
            var saved = ReplayStore.Beside(Main.RecordsPath).Load(main.Settings.Seed, 0);
            bool ok = saved is { } r && r.Floor == 0 && r.TickCount > 30 && r.Seed == main.Settings.Seed;
            if (!ok) GD.PrintErr($"    replay: {(saved == null ? "nothing saved" : $"floor {saved.Floor}, {saved.TickCount} steps, seed {saved.Seed}")}");
            return ok;
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// A scripted player crosses a real portal gap in the real scene: it stands at the lip of the first gap out of the entrance room,
    /// fires the teal portal at the far hallway and the violet one at the wall beside it through the real combat sim, then walks into
    /// the violet portal with the real walking physics and must come out in the far hallway, on its feet and alive. Then it falls
    /// into the pit, and the sim must put it back on its own side without killing it.
    /// </summary>
    private async Task CheckAGapIsCrossedAsync(Main main)
    {
        const string name = "a player crosses a real portal gap, and falling into the pit recovers without dying";
        string forwardAction = InputMapper.Name(GameAction.MoveForward);
        try
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon");
            var sim = combat.Sim;
            var site = combat.Site;
            var layout = site.Floors[0];
            sim.Respawn();
            combat.RefreshPortals();

            var gap = layout.Gaps.First(g => g.Kind == LinkKind.PortalGap && g.Hazard == GapHazard.Bare && (layout.Links[g.LinkIndex].A == layout.EntranceRoom.Id || layout.Links[g.LinkIndex].B == layout.EntranceRoom.Id));
            var plan = GapCrossing.Plan(site, 0, gap, true) ?? throw new InvalidOperationException("no way across the gap was found");

            // Stand a few cells back first, so the sim learns where it is safe, then walk to the lip: the hint appears there.
            var back = gap.Hall(true)[^3];
            var backFeet = GapCrossing.FeetAt(site, 0, back);
            double yaw = Math.Atan2(-gap.DirX, -gap.DirZ);
            main.Player.PlaceAt(new Vector3((float)backFeet.X, (float)backFeet.Y, (float)backFeet.Z), yaw);
            for (int i = 0; i < 25; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            main.Player.PlaceAt(new Vector3((float)plan.Feet.X, (float)plan.Feet.Y, (float)plan.Feet.Z), yaw);
            for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            string hint = main.StoryUi.CaptionText;
            bool hinted = hint.StartsWith("A gap. Fire a portal across it") && hint.Contains(BindingNames.Display(main.Settings.Bindings.Get(GameAction.PortalTeal, Slot.KeyboardMouse)));

            // Fire both portals from the lip: far hallway first, then the wall beside the player.
            var feet = new Vec3(main.Player.Body.X, main.Player.Body.Y, main.Player.Body.Z);
            PlayerView At(Vec3 look) => new(feet, feet + new Vec3(0, 1.62, 0), look, false, false);
            combat.Step(1.0 / 60, At(plan.FarAim), new PlayerActions(false, false, false, PortalTeal: true));
            combat.Step(1.0 / 60, At(plan.NearAim), new PlayerActions(false, false, false, PortalViolet: true));
            bool linked = sim.Portals.Linked;
            var violet = sim.Portals.Violet ?? throw new InvalidOperationException("the violet portal did not place");
            var teal = sim.Portals.Teal ?? throw new InvalidOperationException("the teal portal did not place");

            // Face the violet portal and walk forward into it with the real physics.
            var to = violet.Frame.Center - new Vec3(main.Player.Body.X, violet.Frame.Center.Y, main.Player.Body.Z);
            main.Player.PlaceAt(new Vector3((float)feet.X, (float)feet.Y, (float)feet.Z), Math.Atan2(-to.X, -to.Z));
            bool crossed = false;
            main.Player.Crossed += _ => crossed = true;
            Input.ActionPress(forwardAction);
            try
            {
                for (int i = 0; i < 60 * 6 && !crossed; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                for (int i = 0; i < 60; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); // keep walking away from the exit
            }
            finally { Input.ActionRelease(forwardAction); }
            for (int i = 0; i < 90; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var body = main.Player.Body;
            var cell = ((int)Math.Floor(body.X) - site.OriginX, (int)Math.Floor(body.Z) - site.OriginZ);
            bool farSide = gap.Within(gap.HallTo, cell, 1);
            bool standing = body.OnGround && Math.Abs(body.Y - (DungeonSite.FloorY(0) + 1 + layout.HeightAt(cell.Item1, cell.Item2))) < 0.55;
            bool alive = !sim.Dead;

            // Now fall in: stand safe on the near side first, then drop into the middle of the pit.
            sim.ClosePortals();
            combat.RefreshPortals();
            main.Player.PlaceAt(new Vector3((float)backFeet.X, (float)backFeet.Y, (float)backFeet.Z), yaw);
            for (int i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            bool fell = false, recovered = false;
            combat.EventRaised += e => { if (e is PlayerFell) fell = true; else if (e is PlayerRecovered) recovered = true; };
            var mid = gap.Span[gap.Span.Count / 2];
            double healthBefore = sim.Vitals.Health;
            main.Player.PlaceAt(new Vector3(site.OriginX + mid.X + 0.5f, DungeonSite.FloorY(0) + 1.2f, site.OriginZ + mid.Z + 0.5f), yaw);
            for (int i = 0; i < 60 * 4 && !recovered; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            for (int i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var afterFall = ((int)Math.Floor(main.Player.Body.X) - site.OriginX, (int)Math.Floor(main.Player.Body.Z) - site.OriginZ);
            bool backOnOurSide = gap.Within(gap.HallFrom, afterFall, 1) && main.Player.Body.OnGround && main.Player.Body.Y > DungeonSite.FloorY(0) + 1.5;
            bool hurtNotKilled = sim.Vitals.Health < healthBefore && !sim.Dead;

            bool ok = hinted && linked && crossed && farSide && standing && alive && fell && recovered && backOnOurSide && hurtNotKilled;
            Report(name, ok, $"hinted={hinted} ('{hint}') linked={linked} crossed={crossed} farSide={farSide} cell={cell} standing={standing} y={body.Y:F2} alive={alive} fell={fell} recovered={recovered} backOnOurSide={backOnOurSide} hurtNotKilled={hurtNotKilled}");
            _ = teal;
        }
        catch (Exception e)
        {
            Input.ActionRelease(forwardAction);
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>
    /// A bot plays the whole dungeon in the real scene with no cheats beyond teleporting between rooms: it walks into every room
    /// that holds enemies, aims at the nearest one and fires until the room is clear (respawning when it dies), descends every
    /// floor, kills the boss, and the story's ending must then be reached.
    /// </summary>
    private async Task CheckABotCanPlayTheWholeDungeonAsync(Main main)
    {
        const string name = "a bot plays every room, floor and the boss to the ending";
        try
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon");
            var sim = combat.Sim;
            var site = combat.Site;
            var cleared = new HashSet<(int, int)>();
            var floorsCompleted = new HashSet<int>();
            bool bossKilled = false;
            int deaths = 0, ticks = 0;
            combat.EventRaised += e =>
            {
                if (e is EncounterCleared c) cleared.Add((c.Floor, c.RoomId));
                if (e is FloorCompleted f) floorsCompleted.Add(f.Floor);
                if (e is EnemyKilled k && k.Kind == EnemyKind.Warden) bossKilled = true;
                if (e is PlayerDied) deaths++;
            };

            sim.Respawn();
            var rooms = new List<(int Floor, int RoomId)>();
            foreach (var floor in site.Floors)
                foreach (int roomId in site.Spawns.Where(s => s.Floor == floor.Floor && s.Kind is SpawnKind.Monster or SpawnKind.Boss).Select(s => s.RoomId).Distinct().OrderBy(id => floor.Rooms[id].Depth))
                    rooms.Add((floor.Floor, roomId));

            PlayerView Stand(Vec3 feet, Vec3 look) => new(feet, feet + new Vec3(0, 1.62, 0), look.Normalized(), false, false);

            foreach (var (floorIndex, roomId) in rooms)
            {
                var layout = site.Floors[floorIndex];
                var room = layout.Rooms[roomId];
                var (wx, wy, wz) = site.ToWorld(floorIndex, room.CenterX, room.CenterZ);
                var feet = new Vec3(wx + 0.5, wy, wz + 0.5);
                int attempts = 0;
                while (!cleared.Contains((floorIndex, roomId)) && attempts++ < 12)
                {
                    if (sim.Dead) sim.Respawn();
                    for (int t = 0; t < 60 * 120 && !cleared.Contains((floorIndex, roomId)) && !sim.Dead; t++, ticks++)
                    {
                        var target = sim.Enemies.Where(en => !en.Dead).OrderBy(en => (en.Feet - feet).Length).FirstOrDefault();
                        var look = target == null ? new Vec3(1, 0, 0) : new Vec3(target.Feet.X - feet.X, target.Feet.Y + target.Spec.Height * 0.55 - (feet.Y + 1.62), target.Feet.Z - feet.Z);
                        bool close = target != null && (target.Feet - feet).Length < 7;
                        combat.Step(1.0 / 60, Stand(feet, look), new PlayerActions(target != null && !close, target != null && close, false));
                    }
                }
                if (!cleared.Contains((floorIndex, roomId))) throw new InvalidOperationException($"the bot could not clear floor {floorIndex + 1} room {roomId} (deaths {deaths})");

                // After the floor's last room, descend.
                bool lastOnFloor = rooms.Last(r => r.Floor == floorIndex).RoomId == roomId;
                if (lastOnFloor && floorIndex < site.FloorCount - 1)
                {
                    var below = site.ToWorld(floorIndex + 1, 3, 3);
                    combat.Step(1.0 / 60, Stand(new Vec3(below.X + 0.5, below.Y, below.Z + 0.5), new Vec3(1, 0, 0)), default);
                }
            }

            // The boss fell: give the story its delay to show the ending.
            await ToSignal(GetTree().CreateTimer(4.5), SceneTreeTimer.SignalName.Timeout);

            bool everyFloor = Enumerable.Range(0, site.FloorCount).All(sim.FloorCompleted);
            bool ending = main.Story.EndingShown;
            if (everyFloor && bossKilled && ending) { _passed++; GD.Print($"  ok   {name} ({ticks / 60} s of play, {deaths} deaths)"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: floors={floorsCompleted.Count}/{site.FloorCount} boss={bossKilled} ending={ending} deaths={deaths}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>
    /// A different saved seed builds a different world when the scene is rebuilt, and the same seed builds the same one. This
    /// follows the title screen's Play button: save the seed to the settings file, then build the scene again.
    /// </summary>
    private async Task CheckSeedChangesTheWorldAsync(Main main)
    {
        const string name = "a different seed builds a different world and the same seed builds the same one";
        try
        {
            const string otherSeed = "amber-hollow-914";
            var original = main.Settings;
            var chosen = new GameSettings { Seed = otherSeed };
            if (!chosen.TrySave(Main.SettingsPath)) throw new InvalidOperationException("could not save the settings file");

            var rebuilt = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
            AddChild(rebuilt);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            long originalSeed = main.World.Seed, rebuiltSeed = rebuilt.World.Seed;
            bool followsSettings = rebuilt.Settings.Seed == otherSeed && rebuiltSeed == TerrainGenerator.SeedFromString(otherSeed);
            ulong originalSum = TerrainChecksum(new VoxelWorld(originalSeed)), rebuiltSum = TerrainChecksum(rebuilt.World);
            bool differs = originalSeed != rebuiltSeed && originalSum != rebuiltSum;
            bool sameSeedSameWorld = TerrainChecksum(new VoxelWorld(rebuiltSeed)) == rebuiltSum && TerrainChecksum(new VoxelWorld(originalSeed)) == originalSum;

            rebuilt.QueueFree();
            original.TrySave(Main.SettingsPath); // put the saved settings back

            Report(name, followsSettings && differs && sameSeedSameWorld, $"followsSettings={followsSettings} differs={differs} sameSeedSameWorld={sameSeedSameWorld}");
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>FNV-1a over the blocks of the 5 x 5 chunks around the origin: a fingerprint of the terrain.</summary>
    private static ulong TerrainChecksum(VoxelWorld world)
    {
        ulong hash = 1469598103934665603UL;
        for (int cx = -2; cx <= 2; cx++)
            for (int cz = -2; cz <= 2; cz++)
                foreach (var block in world.GetChunk(cx, cz).RawBlocks)
                {
                    hash ^= (byte)block;
                    hash *= 1099511628211UL;
                }
        return hash;
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

    private void Report(string name, bool ok, string detail)
    {
        if (ok) { _passed++; GD.Print($"  ok   {name}"); }
        else { _failed++; GD.PrintErr($"  FAIL {name}: {detail}"); }
    }

    private bool CheckClipComposition()
    {
        var model = ModelCatalog.ForEnemy(EnemyKind.Brownie);
        var clip = EnemyMotions.For(model).First(c => c.Motion == Motion.Walk);
        int key = clip.Times.Length / 3;
        var rig = ModelFactory.Build(model);
        AddChild(rig.Root);
        bool ok = true;
        foreach (var track in clip.Tracks)
        {
            rig.Animator!.Play(Motions.Name(Motion.Walk), 0, 0);
            rig.Animator.Seek(clip.Times[key], update: true);
            var node = rig.Part(track.Part)!;
            var q = track.Rotations[key];
            var godot = node.Quaternion;
            float dot = godot.X * q.X + godot.Y * q.Y + godot.Z * q.Z + godot.W * q.W;
            var at = ModelFactory.ToVector(track.Translations[key]);
            if (Math.Abs(dot) < 0.999f || node.Position.DistanceTo(at) > 1e-3f)
            {
                GD.PrintErr($"    {track.Part}: rotation dot={dot} position={node.Position} expected {at}");
                ok = false;
            }
        }
        rig.Root.Free();
        return ok;
    }

    private bool CheckEnemyRigs()
    {
        EnemyView Make(EnemyKind kind, Motion pose)
        {
            var view = new EnemyView(kind);
            AddChild(view);
            view.ForcePose(pose);
            for (int i = 0; i < 40; i++) view.Animate(EnemyState.Chasing, 1.0 / 30);
            return view;
        }

        bool ok = true;
        void Expect(bool condition, string what)
        {
            if (condition) return;
            ok = false;
            GD.PrintErr($"    rig: {what}");
        }

        foreach (var kind in new[] { EnemyKind.Husk, EnemyKind.Warden })
        {
            var idle = Make(kind, Motion.Idle);
            var windup = Make(kind, Motion.Windup);
            var walk = Make(kind, Motion.Walk);
            var stunned = Make(kind, Motion.Stunned);

            Expect(windup.Rig.Part("arm_l")!.Basis.Y.Y < 0, $"{kind} raises its arms over its head on a windup");
            Expect(Math.Abs(walk.Rig.Part("leg_l")!.RotationDegrees.X) + Math.Abs(walk.Rig.Part("leg_r")!.RotationDegrees.X) > 5, $"{kind} swings its legs when walking");
            Expect(walk.Rig.Part("leg_l")!.RotationDegrees.X * walk.Rig.Part("leg_r")!.RotationDegrees.X < 0, $"{kind}'s legs swing in opposite directions");
            Expect(stunned.Rig.Part("head")!.RotationDegrees.X > 20, $"{kind} slumps its head when stunned");

            // The head hangs from the torso: leaning the torso carries it along.
            var head = windup.Rig.Part("head")!;
            Expect(head.GetParent() == windup.Rig.Part("torso"), $"{kind}'s head is parented to the torso");
            Expect(head.GlobalPosition.DistanceTo(idle.Rig.Part("head")!.GlobalPosition) > 0.02, $"{kind}'s head moves when the torso leans");
            Expect(windup.Rig.Part("arm_r")!.GetParent() == windup.Rig.Part("torso"), $"{kind}'s arms are parented to the torso");
            foreach (var v in new[] { idle, windup, walk, stunned }) v.QueueFree();
        }

        var s0 = Make(EnemyKind.Spitter, Motion.Idle);
        var s1 = Make(EnemyKind.Spitter, Motion.Windup);
        Expect(s1.Rig.Part("body")!.Scale.X > 1.1f, "the Spitter swells when winding up");
        Expect(s1.Rig.Part("mouth")!.Scale.X > 1.2f, "the Spitter's mouth opens wide when winding up");
        Expect(s1.Rig.Part("tent_0")!.RotationDegrees.Length() > 1, "the Spitter's tentacles sway");
        s0.QueueFree();
        s1.QueueFree();
        return ok;
    }

    private bool CheckCreatureRigs()
    {
        bool ok = true;
        void Expect(bool condition, string what)
        {
            if (condition) return;
            ok = false;
            GD.PrintErr($"    creature rig: {what}");
        }
        EnemyView Make(EnemyKind kind, Motion pose)
        {
            var view = new EnemyView(kind);
            AddChild(view);
            view.ForcePose(pose);
            for (int i = 0; i < 40; i++) view.Animate(EnemyState.Chasing, 1.0 / 30);
            return view;
        }

        foreach (var kind in new[] { EnemyKind.Brownie, EnemyKind.Selkie })
        {
            var windup = Make(kind, Motion.Windup);
            var walk = Make(kind, Motion.Walk);
            var stunned = Make(kind, Motion.Stunned);
            Expect(windup.Rig.Part("arm_l")!.Basis.Y.Y < 0, $"{kind} raises its arms over its head on a windup");
            Expect(walk.Rig.Part("leg_l")!.RotationDegrees.X * walk.Rig.Part("leg_r")!.RotationDegrees.X < 0, $"{kind}'s legs swing in opposite directions");
            Expect(stunned.Rig.Part("head")!.RotationDegrees.X > 20, $"{kind} slumps its head when stunned");
            Expect(walk.Rig.Part("head")!.GetParent() == walk.Rig.Part("torso"), $"{kind}'s head hangs from the torso");
            foreach (var v in new[] { windup, walk, stunned }) v.QueueFree();
        }

        var kelpieWalk = Make(EnemyKind.Kelpie, Motion.Walk);
        var kelpieWindup = Make(EnemyKind.Kelpie, Motion.Windup);
        Expect(kelpieWalk.Rig.Part("leg_fl")!.RotationDegrees.X * kelpieWalk.Rig.Part("leg_fr")!.RotationDegrees.X < 0, "the Kelpie gallops with diagonal pairs of legs");
        Expect(kelpieWindup.Rig.Part("body")!.RotationDegrees.X < -10, "the Kelpie rears for its blow");
        Expect(kelpieWalk.Rig.Part("head")!.GetParent() == kelpieWalk.Rig.Part("neck"), "the Kelpie's head hangs from its neck");

        var nighe = Make(EnemyKind.BeanNighe, Motion.Idle);
        var nigheStunned = Make(EnemyKind.BeanNighe, Motion.Stunned);
        Expect(nigheStunned.Rig.Part("head")!.RotationDegrees.X > 20, "the Bean Nighe slumps when stunned");

        var hare = new EnemyView(EnemyKind.Hare);
        AddChild(hare);
        hare.ForcePose(Motion.Walk);
        float rest = 0.25f, maxY = 0;
        for (int i = 0; i < 40; i++)
        {
            hare.Animate(EnemyState.Chasing, 1.0 / 30);
            maxY = Math.Max(maxY, hare.Rig.Part("body")!.Position.Y);
        }
        Expect(maxY > rest + 0.05f, $"the hare hops (peak {maxY:F2})");
        Expect(hare.Rig.Part("ear_l")!.RotationDegrees.X < -10, "the hare lays its ears back when it runs");

        foreach (var v in new[] { kelpieWalk, kelpieWindup, nighe, nigheStunned, hare }) v.QueueFree();
        return ok;
    }

    /// <summary>
    /// The relationship system in the real scene: creatures are drawn with their own models and pools, a Friendly brownie is not
    /// hostile, and killing one in front of another changes the standing screen, raises a toast and is written to relations.json.
    /// </summary>
    private async Task CheckCreaturesAsync(Main main)
    {
        const string name = "creatures spawn and draw, a Friendly brownie is not hostile, and a witnessed kill changes the standing screen";
        try
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            var sim = combat.Sim;
            var rep = sim.Relations?.Reputation ?? throw new InvalidOperationException("the relationship system is off in the real scene");
            var hud = main.GetNode<Hud>("Hud");
            sim.Respawn();
            foreach (var f in FactionGraphStandable()) rep.Set(f, FactionStart(f));
            main.MovePlayerToFloor(0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            // 1. The dungeon holds creatures, and the view draws each with its own model.
            var natural = new List<(EnemyKind Kind, int Id)>();
            for (int floor = 0; floor < combat.Site.FloorCount; floor++)
            {
                // Reach each floor in the sim itself (the scene may be paused by an earlier check).
                combat.Step(1.0 / 60, Stand(new Vec3(combat.Site.OriginX + 1.5, DungeonSite.FloorY(floor) + 1, combat.Site.OriginZ + 1.5)), default);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            main.MovePlayerToFloor(0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            foreach (var e in sim.Enemies.Where(e => e.Mind is { Species.AlwaysHostile: false })) natural.Add((e.Kind, e.Id));
            int expected = combat.Site.Spawns.Count(s => s.Kind == SpawnKind.Creature);
            bool spawned = expected > 0 && natural.Count == expected;
            if (!spawned) GD.PrintErr($"    creatures by floor: planned {string.Join(",", combat.Site.Spawns.Where(s => s.Kind == SpawnKind.Creature).GroupBy(s => s.Floor).Select(g => $"{g.Key}:{g.Count()}"))} present {string.Join(",", sim.Enemies.Where(e => e.Mind is { Species.AlwaysHostile: false }).GroupBy(e => e.Floor).Select(g => $"{g.Key}:{g.Count()}"))} current floor {sim.CurrentFloor} paused={GetTree().Paused} dead={sim.Dead} playerY={main.Player.Body.Y:F1}");
            bool drawn = natural.All(n => combat.GetNodeOrNull<EnemyView>($"Enemy_{n.Id}") is { } v && v.Rig.Model.Id == ModelCatalog.ForEnemy(n.Kind).Id);
            bool pools = natural.All(n => n.Kind is not (EnemyKind.Kelpie or EnemyKind.Selkie)) || combat.GetNodeOrNull<Node3D>("Pool") != null;

            // 2. A Friendly brownie standing near you does nothing to you.
            sim.Respawn();
            main.MovePlayerToFloor(0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            rep.Set(Faction.Brownies, 40);
            var view = main.Player.View;
            var friend = sim.SpawnEnemy(EnemyKind.Brownie, new Vec3(view.Feet.X + 2.5, view.Feet.Y, view.Feet.Z));
            double hp = sim.Vitals.Health;
            for (int i = 0; i < 180; i++) combat.Step(1.0 / 60, view, default);
            bool friendly = friend.Mind!.Intent != Intent.Attack && friend.Mind.Disposition != Disposition.Aggressive && sim.Vitals.Health >= hp && !friend.Dead;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var friendView = combat.GetNodeOrNull<EnemyView>($"Enemy_{friend.Id}");
            bool pip = friendView?.PipColor is { } c && c.G > c.R; // green: friendly

            // 3. Kill a brownie with another watching: the Brownies fall to Wary, a toast shows, the screen and the file say so.
            rep.Set(Faction.Brownies, 0);
            sim.Relations!.Reputation.Reset();
            string before = TierOf(rep, Faction.Brownies);
            sim.Respawn();
            main.MovePlayerToFloor(0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            view = main.Player.View;
            var victim = sim.SpawnEnemy(EnemyKind.Brownie, new Vec3(view.Feet.X + 3, view.Feet.Y, view.Feet.Z));
            var witness = sim.SpawnEnemy(EnemyKind.Brownie, new Vec3(view.Feet.X + 3, view.Feet.Y, view.Feet.Z + 1.5));
            for (int i = 0; i < 20; i++) combat.Step(1.0 / 60, view, default);
            for (int shot = 0; shot < 8 && !victim.Dead; shot++)
            {
                var at = new Vec3(victim.Feet.X, victim.Feet.Y + victim.Spec.Height * 0.85, victim.Feet.Z);
                combat.Step(1.0 / 60, view with { Forward = (at - view.Eye).Normalized() }, new PlayerActions(true, false, false));
                for (int i = 0; i < 20; i++) combat.Step(1.0 / 60, view, default);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string after = TierOf(rep, Faction.Brownies);
            main.Menus.Open(MenuScreen.Standing);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string line = main.Menus.StandingLines.FirstOrDefault(l => l.StartsWith("the Brownies")) ?? "(missing)";
            main.Menus.Close();
            bool changed = victim.Dead && before == "Neutral" && after == "Wary" && line.Contains("Wary");
            bool toast = hud.ToastText.Contains("The Brownies now regard you as Wary");
            bool hollowUnmoved = rep.Tier(Faction.Husks) == ReputationTier.Hated && rep.Value(Faction.Husks) <= -90;
            bool angry = witness.Mind!.Memory.Anger > 0 || witness.Mind.Memory.Fear > 0;
            var saved = RelationsStore.Load(Main.RelationsPath).Standings(main.SeedName);
            bool persisted = saved != null && saved.TryGetValue(Faction.Brownies, out var v2) && v2 < 0;

            // Leave the world as the later checks expect it.
            sim.Respawn();
            rep.Reset();
            bool ok = spawned && drawn && pools && friendly && pip && changed && toast && hollowUnmoved && angry && persisted;
            if (ok) { _passed++; GD.Print($"  ok   {name} ({natural.Count} creatures; screen: {line})"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: spawned={spawned} ({natural.Count}/{expected}) drawn={drawn} pools={pools} friendly={friendly} pip={pip} changed={changed} ({before}->{after}, '{line}') toast={toast} ('{hud.ToastText}') hollow={hollowUnmoved} witnessMoved={angry} persisted={persisted}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }

        static IEnumerable<Faction> FactionGraphStandable() => FactionGraph.Standable;
        static double FactionStart(Faction f) => FactionGraph.Info(f).StartStanding;
        static string TierOf(Reputation r, Faction f) => ReputationTiers.Label(r.Tier(f));
    }

    private void Check(string name, Func<bool> test)
    {
        bool ok;
        try { ok = test(); }
        catch (Exception e)
        {
            GD.PrintErr($"  FAIL {name}: {e.Message}");
            ok = false;
        }
        if (ok) { _passed++; GD.Print($"  ok   {name}"); }
        else { _failed++; GD.PrintErr($"  FAIL {name}"); }
    }
}
