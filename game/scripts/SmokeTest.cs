using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Input;
using Hollowfall.Core.Ui;
using Hollowfall.Core.Meshing;
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
        // Never touch the real settings file from a test run.
        string settingsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hollowfall-smoke-{System.Environment.ProcessId}.json");
        System.Environment.SetEnvironmentVariable("HOLLOWFALL_SETTINGS", settingsPath);

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

        Check("an all-air chunk converts to no mesh", () =>
            ChunkMeshBuilder.ToArrayMesh(ChunkMesher.Build(0, 0, (_, _, _) => BlockType.Air)) == null);

        Check("every enemy model is rigged: poses move the joints and children follow their parents", CheckEnemyRigs);

        await CheckMainSceneAsync();

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
            if (settings.Bindings.Rebind(GameAction.Parry, Binding.Key("G")) != RebindOutcome.Ok) return false;
            InputMapper.Apply(settings.Bindings, settings.Deadzone);
            bool mapped = InputMap.ActionGetEvents(InputMapper.Name(GameAction.Parry)).OfType<InputEventKey>().Any(k => k.PhysicalKeycode == Key.G);
            bool saved = settings.TrySave(Main.SettingsPath) && GameSettings.Load(Main.SettingsPath).Bindings.Get(GameAction.Parry, Slot.KeyboardMouse) == Binding.Key("G");
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
