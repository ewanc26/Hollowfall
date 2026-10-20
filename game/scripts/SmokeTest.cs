using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Input;
using Hollowfall.Core.Ui;
using Hollowfall.Core.Meshing;
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
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_SEED", null);

        Check("both fonts load as real fonts, not the fallback", () =>
            UiFonts.Display != ThemeDB.FallbackFont && UiFonts.Body != ThemeDB.FallbackFont && UiFonts.Display.HasChar('H') && UiFonts.Body.HasChar('9'));

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

        foreach (var file in new[] { settingsPath, settingsPath + ".tmp", recordsPath, recordsPath + ".tmp" })
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
            await CheckInputAndMenusAsync(main);
            await CheckStoryAsync(main);
            CheckWulver(main);
            CheckVolumeSlidersPersist(main);
            await CheckMiniMapAsync(main);
            CheckChestOpens(main);
            CheckTrapStrikes(main);
            CheckRunRecordsSaveAndReload(main);
            await CheckReplaySavedByARealRunAsync(main);
            CheckUiScaling(main);
            await CheckABotCanPlayTheWholeDungeonAsync(main);
            await CheckSeedChangesTheWorldAsync(main);
            main.QueueFree();
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

    /// <summary>Pressing Interact beside a real, unlocked chest opens it (the sim state that turns the lid) and says so.</summary>
    private void CheckChestOpens(Main main)
    {
        Check("interact opens a chest in range and its state changes", () =>
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            var sim = combat.Sim;
            sim.Respawn(); // full health, no enemies, nothing in the way
            var chest = sim.Items.Chests.FirstOrDefault(c => !c.Locked && !c.Opened) ?? throw new InvalidOperationException("no unlocked chest in this dungeon");
            var spawn = chest.Spawn;
            var view = Stand(new Vec3(spawn.X + 1.5, spawn.Y, spawn.Z + 0.5)); // a block away from the chest's middle

            combat.Step(1.0 / 60, view, default);
            bool closedWithoutInteract = !chest.Opened;
            combat.Step(1.0 / 60, view, new PlayerActions(false, false, false, Interact: true));
            bool opened = chest.Opened && combat.LastEvents.OfType<ChestOpened>().Any(o => o.SpawnIndex == chest.Index && !o.WasLocked);
            if (!(closedWithoutInteract && opened)) GD.PrintErr($"    chest: closedWithoutInteract={closedWithoutInteract} opened={opened}");
            return closedWithoutInteract && opened;
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

    private bool CheckEnemyRigs()
    {
        EnemyView Make(EnemyKind kind, string pose)
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
            var idle = Make(kind, "idle");
            var windup = Make(kind, "windup");
            var walk = Make(kind, "walk");
            var stunned = Make(kind, "stunned");

            Expect(windup.Rig.Part("arm_l")!.RotationDegrees.X < -60, $"{kind} raises its arms on a windup");
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

        var s0 = Make(EnemyKind.Spitter, "idle");
        var s1 = Make(EnemyKind.Spitter, "windup");
        Expect(s1.Rig.Part("body")!.Scale.X > 1.1f, "the Spitter swells when winding up");
        Expect(s1.Rig.Part("mouth")!.Scale.X > 1.2f, "the Spitter's mouth opens wide when winding up");
        Expect(s1.Rig.Part("tent_0")!.RotationDegrees.Length() > 1, "the Spitter's tentacles sway");
        s0.QueueFree();
        s1.QueueFree();
        return ok;
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
