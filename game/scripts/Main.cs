using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Input;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// Entry scene: builds the world, light, sky, player, combat and HUD in code, and steps them in a fixed order each
/// physics frame (player moves, then combat resolves). Game logic lives in Hollowfall.Core; the scripts here only
/// connect the engine to it. Set the seed with HOLLOWFALL_SEED.
/// </summary>
public partial class Main : Node3D
{
    private WorldView _view = null!;
    private PlayerController _player = null!;
    private Hud _hud = null!;
    private Menus _menus = null!;
    private ViewModels _weapons = null!;
    private PropsView? _props;
    private AudioDirector _audio = null!;
    private RunRecords _records = new();
    private string _seedName = "";

    /// <summary>Set when the scene is rebuilt for a new world, so the player is not shown the title screen they just left.</summary>
    private static bool _skipTitleOnce;
    private CanvasLayer _weaponLayer = null!;
    private string _where = "";
    private int _demoFrame;
    private readonly string? _demo = System.Environment.GetEnvironmentVariable("HOLLOWFALL_DEMO");

    public VoxelWorld World { get; private set; } = null!;
    public CombatView? Combat { get; private set; }
    public PlayerController Player => _player;
    public GameSettings Settings { get; private set; } = new();
    public Menus Menus => _menus;

    /// <summary>Where settings and bindings are saved. Override with HOLLOWFALL_SETTINGS (used by tests).</summary>
    public static string SettingsPath =>
        System.Environment.GetEnvironmentVariable("HOLLOWFALL_SETTINGS") is { Length: > 0 } custom ? custom : ProjectSettings.GlobalizePath("user://settings.json");

    /// <summary>Where personal bests are saved; HOLLOWFALL_RECORDS overrides it (tests and screenshots use that).</summary>
    public static string RecordsPath =>
        System.Environment.GetEnvironmentVariable("HOLLOWFALL_RECORDS") is { Length: > 0 } custom ? custom : ProjectSettings.GlobalizePath("user://records.json");

    public override void _Ready()
    {
        Settings = GameSettings.Load(SettingsPath);
        InputMapper.Apply(Settings.Bindings, Settings.Deadzone);

        string seedText = System.Environment.GetEnvironmentVariable("HOLLOWFALL_SEED") is { Length: > 0 and <= 100 } s ? s : Settings.Seed;
        World = new VoxelWorld(TerrainGenerator.SeedFromString(seedText));
        _seedName = seedText;
        _records = RunRecords.Load(RecordsPath);

        AddChild(new WorldEnvironment { Environment = MakeEnvironment() });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, 35, 0),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90,
        });

        _audio = new AudioDirector { Name = "Audio", Settings = Settings };
        AddChild(_audio);
        Menus.UiSound += id => _audio.Play(id);

        var site = FindNearestSite(World);
        var (feet, yaw, where) = Spawn(World, site);
        _where = where;

        _view = new WorldView { Name = "World" };
        _view.Initialize(World);
        _view.ViewRadius = Settings.ViewRadius;
        _view.Focus = feet;
        AddChild(_view);

        _player = new PlayerController { Name = "Player" };
        _player.Settings = Settings;
        _player.Initialize(World, feet, yaw);
        AddChild(_player);

        if (site != null)
        {
            Combat = new CombatView { Name = "Combat" };
            Combat.Initialize(new CombatSim(World, site), site, _view);
            Combat.EventRaised += OnCombatEvent;
            Combat.EventRaised += e => _audio.OnEvent(e, Combat.Sim);
            Combat.EventRaised += e => _hud?.OnEvent(e);
            Combat.EventRaised += e => { if (e is ShotFired shot) _weapons?.Fire(shot.Weapon); };
            AddChild(Combat);
            _props = new PropsView { Name = "Props" };
            _props.Initialize(site, Combat.Sim.Items);
            AddChild(_props);
        }

        // The weapons in your hands, drawn under the HUD.
        _weaponLayer = new CanvasLayer { Layer = 4, Name = "Weapons" };
        _weapons = new ViewModels { Name = "ViewModels" };
        _weaponLayer.AddChild(_weapons);
        AddChild(_weaponLayer);

        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);

        _menus = new Menus(Settings, SettingsPath) { Name = "Menus" };
        _menus.SettingsChanged += ApplySettings;
        _menus.PromptStyleProvider = () => PromptStyle;
        _menus.MenuOpenChanged += open =>
        {
            _hud.Visible = !open;
            _weaponLayer.Visible = !open;
        };
        _menus.RespawnRequested += () => { if (Combat != null) Respawn(); };
        _menus.QuitRequested += () => GetTree().Quit();
        _menus.NewGameRequested += () => Restart(skipTitle: true);
        _menus.MainMenuRequested += () => Restart(skipTitle: false);
        AddChild(_menus);

        // The game starts at the title screen, except headless (tests), scripted demos and when asked to skip it.
        bool skipTitle = DisplayServer.GetName() == "headless" || _demo != null || System.Environment.GetEnvironmentVariable("HOLLOWFALL_SKIP_TITLE") == "1" || _skipTitleOnce;
        _skipTitleOnce = false;
        if (!skipTitle) _menus.Open(MenuScreen.Title);

        // For screenshots and videos: HOLLOWFALL_MENU=title|pause|controls|settings opens that screen straight away.
        if (System.Environment.GetEnvironmentVariable("HOLLOWFALL_MENU") is { } menu)
            _menus.CallDeferred(Menus.MethodName.OpenFrom, (int)MenuScreen.Pause, (int)(menu switch
            {
                "title" => MenuScreen.Title,
                "controls" => MenuScreen.Controls,
                "settings" => MenuScreen.Settings,
                _ => MenuScreen.Pause,
            }));

        GD.Print($"Hollowfall: seed \"{seedText}\", spawn {where} at ({feet.X:F1}, {feet.Y:F1}, {feet.Z:F1})");
    }

    /// <summary>Rebuilds the whole scene: a new world for a new seed, or back to the title screen.</summary>
    private void Restart(bool skipTitle)
    {
        _skipTitleOnce = skipTitle;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    /// <summary>Applies changed settings and bindings to the running game.</summary>
    private void ApplySettings()
    {
        InputMapper.Apply(Settings.Bindings, Settings.Deadzone);
        _view.ViewRadius = Settings.ViewRadius;
    }

    /// <summary>The button labels to show: for a gamepad, those of the connected controller unless the player chose a style.</summary>
    public ControllerStyle PromptStyle
    {
        get
        {
            var pads = Input.GetConnectedJoypads();
            string? name = pads.Count > 0 ? Input.GetJoyName(pads[0]) : null;
            return ControllerStyles.Resolve(Settings.ControllerPrompts, name);
        }
    }

    private List<(Binding? Binding, string Label)> Hints()
    {
        var slot = _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse;
        (Binding?, string) Hint(GameAction a, string label) => (Settings.Bindings.Get(a, slot), label);
        return
        [
            Hint(GameAction.FirePrimary, "revolver"),
            Hint(GameAction.FireSecondary, "shotgun"),
            Hint(GameAction.Parry, "parry"),
            Hint(GameAction.PortalTeal, "teal portal"),
            Hint(GameAction.PortalViolet, "violet portal"),
            Hint(GameAction.Interact, "open"),
            Hint(GameAction.Jump, "jump"),
            Hint(GameAction.Pause, "menu"),
        ];
    }

    private static Godot.Environment MakeEnvironment()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.30f, 0.52f, 0.85f),
            SkyHorizonColor = new Color(0.72f, 0.82f, 0.92f),
            GroundHorizonColor = new Color(0.72f, 0.82f, 0.92f),
            GroundBottomColor = new Color(0.25f, 0.28f, 0.32f),
        };
        return new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.6f, 0.7f),
            AmbientLightEnergy = 0.35f,
            FogEnabled = true,
            FogLightColor = new Color(0.72f, 0.82f, 0.92f),
            FogDensity = 0.003f,
        };
    }

    private static DungeonSite? FindNearestSite(VoxelWorld world)
    {
        DungeonSite? best = null;
        double bestDist = double.MaxValue;
        for (int rx = -3; rx <= 2; rx++)
            for (int rz = -3; rz <= 2; rz++)
                if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site)
                {
                    double d = (double)site.EntranceX * site.EntranceX + (double)site.EntranceZ * site.EntranceZ;
                    if (d < bestDist) { best = site; bestDist = d; }
                }
        return best;
    }

    /// <summary>Spawns beside the nearest dungeon entrance, facing it; falls back to the surface at the origin.</summary>
    private static (Vector3 Feet, double Yaw, string Where) Spawn(VoxelWorld world, DungeonSite? site)
    {
        if (site != null && System.Environment.GetEnvironmentVariable("HOLLOWFALL_SPAWN_FLOOR") is { } f && int.TryParse(f, out int floor) && floor >= 0 && floor < site.FloorCount)
            return (FloorSpawn(site, floor), 0, $"inside dungeon floor {floor}");

        if (site != null)
        {
            // Stand on the plaza edge looking toward the stair shaft (which lies to the -X side of the spawn).
            var feet = new Vector3(site.EntranceX + DungeonSite.PlazaRadius + 0.5f, site.SurfaceY + 1, site.EntranceZ + 0.5f);
            return (feet, Math.PI / 2, $"at the dungeon entrance in region ({site.RegionX},{site.RegionZ})");
        }

        int y = world.Generator.SampleColumn(0, 0).Height + 1;
        return (new Vector3(0.5f, y, 0.5f), 0, "on the surface at the origin");
    }

    /// <summary>A standing spot a few cells from a floor's entrance stairs, inside the entrance room.</summary>
    private static Vector3 FloorSpawn(DungeonSite site, int floor)
    {
        var layout = site.Floors[floor];
        for (int r = 3; r <= 8; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    int x = layout.EntranceX + dx, z = layout.EntranceZ + dz;
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r || layout.CellAt(x, z) != CellKind.Floor || layout.RoomIdAt(x, z) != layout.EntranceRoom.Id) continue;
                    var (wx, wy, wz) = site.ToWorld(floor, x, z);
                    return new Vector3(wx + 0.5f, wy, wz + 0.5f);
                }
        return new Vector3(site.EntranceX + 0.5f, DungeonSite.FloorY(floor) + 1, site.EntranceZ + 0.5f);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dead = Combat?.Sim.Dead ?? false;
        var actions = _player.Step(delta, Combat?.Sim.Portals, dead);

        if (Combat != null)
        {
            if (dead && _player.RespawnRequested) Respawn();
            var view = _player.View;
            if (_demo == "portals") (view, actions) = RunPortalDemo(view, actions);
            Combat.Step(delta, view, actions);
            foreach (var impact in Combat.LastEvents.OfType<MeteorImpact>()) _player.KeepSpeed(impact.SpeedKept);
        }
    }

    /// <summary>Moves the player to a spot inside a dungeon floor (debugging, tests and scripted setups).</summary>
    public void MovePlayerToFloor(int floor)
    {
        if (Combat == null) return;
        _player.PlaceAt(FloorSpawn(Combat.Site, Math.Clamp(floor, 0, Combat.Site.FloorCount - 1)), 0);
    }

    /// <summary>
    /// A scripted setup for screenshots and videos (HOLLOWFALL_DEMO=portals): drop to floor 0, put a teal portal on the
    /// east wall and an violet one on the north wall, and look at the teal one.
    /// </summary>
    private (PlayerView, PlayerActions) RunPortalDemo(PlayerView view, PlayerActions actions)
    {
        _demoFrame++;
        if (_demoFrame == 5) MovePlayerToFloor(0);
        var east = new Vec3(1, 0, 0);
        if (_demoFrame == 30) return (view with { Forward = east }, actions with { PortalTeal = true });
        if (_demoFrame == 32) return (view with { Forward = new Vec3(0, 0, -1) }, actions with { PortalViolet = true });
        if (_demoFrame == 34 && Combat!.Sim.Portals.Teal is { } teal)
        {
            // Stand a few blocks in front of the teal portal, facing it.
            var at = teal.Frame.Center + teal.Frame.Normal * 3.5;
            _player.PlaceAt(new Vector3((float)at.X, (float)view.Feet.Y, (float)at.Z), -Math.PI / 2);
        }
        return (view, actions);
    }

    private void Respawn()
    {
        var combat = Combat!;
        int floor = Math.Max(combat.Sim.CurrentFloor, 0);
        combat.Sim.Respawn();
        var site = combat.Site;
        _player.PlaceAt(combat.Sim.CurrentFloor < 0 ? new Vector3(site.EntranceX + DungeonSite.PlazaRadius + 0.5f, site.SurfaceY + 1, site.EntranceZ + 0.5f) : FloorSpawn(site, floor), 0);
        _hud.ShowBanner("", 0);
    }

    private void OnCombatEvent(CombatEvent e)
    {
        switch (e)
        {
            case PlayerDied:
                _hud.ShowBanner($"YOU DIED\n{BindingNames.Display(Settings.Bindings.Get(GameAction.Respawn, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse))} to respawn", 3600);
                break;
            case EncounterStarted s:
                _hud.ShowBanner($"{s.Enemies} enemies: kill them to open the doors", 2.5);
                break;
            case EncounterCleared:
                _hud.ShowBanner("CLEAR", 1.5);
                break;
            case FloorCompleted f:
                {
                    var (isBest, previous) = _records.Submit(_seedName, f.Floor, f.Result, f.Seconds);
                    if (isBest) _records.TrySave(RecordsPath);
                    string note = isBest ? (previous == null ? "first clear" : $"NEW BEST  (was {previous.Value.Grade})") : $"best {_records.Best(_seedName, f.Floor)!.Value.Grade}";
                    int secs = (int)f.Seconds;
                    _hud.ShowBanner($"FLOOR {f.Floor + 1} COMPLETE  {f.Result.Overall}  {secs / 60}:{secs % 60:D2}\ntime {f.Result.Time}  kills {f.Result.Kills}  style {f.Result.Style}\n{note}", 6);
                }
                break;
            case KeyCollected:
                _hud.ShowBanner("KEY", 1.2);
                break;
            case ChestOpened o:
                _hud.ShowBanner(o.WasLocked ? $"LOCKED CHEST OPENED  +{o.Healed:F0} health" : $"CHEST  +{o.Healed:F0} health", 1.6);
                break;
            case ChestLocked:
                _hud.ShowBanner("locked: find the key on this floor", 1.8);
                break;
            case TrapSprung t:
                _hud.ShowBanner($"SPIKES  -{t.Damage:F0}", 0.8);
                break;
            case PortalPlaced { Success: false } failed:
                _hud.ShowBanner($"can't place a portal there ({failed.Failure})", 1.2);
                break;
        }
    }

    public override void _Process(double delta)
    {
        var b = _player.Body;
        _view.Focus = new Vector3((float)b.X, (float)b.Y, (float)b.Z);
        Combat?.UpdatePortalViews(_player.Camera);

        if (InputMapper.JustPressed(GameAction.DebugInfo)) _hud.SetDebug(!_hud.DebugOn);

        _weapons.Pose(delta, b.Speed, b.OnGround, _player.LookDelta);
        _props?.Update(delta, _view.Focus);
        _audio.Underground = Combat != null && Combat.Sim.CurrentFloor >= 0;

        if (Combat == null) return;
        var sim = Combat.Sim;
        int floor = sim.CurrentFloor;
        var openings = Combat.PortalScreenRects(_player.Camera);
        _hud.SetOpenings(openings);
        _weapons.SetOpenings(openings);
        _weapons.SetPortals(sim.Portals.Teal != null, sim.Portals.Violet != null);
        if (floor >= 0) _hud.SetFloorInfo(floor, Combat.Site.FloorCount, sim.KillsOn(floor), sim.EnemiesOn(floor), sim.ElapsedOn(floor), sim.TargetsFor(floor).ParTimeSeconds, sim.Items.KeysHeld);
        else _hud.SetFloorInfo(-1, 0, 0, 0, 0, 0);
        {
            _hud.SetMap(sim.Map, floor, new Vector2((float)(b.X - Combat.Site.OriginX), (float)(b.Z - Combat.Site.OriginZ)), (float)_player.Yaw);
        }

        string info =
            $"{Engine.GetFramesPerSecond()} fps   x {b.X:F1}  y {b.Y:F1}  z {b.Z:F1}   speed {b.Speed:F1}   " +
            $"{(_player.Flying ? "FLYING" : b.OnGround ? "walking" : "airborne")}   in {World.GetBlock((int)Math.Floor(b.X), (int)Math.Floor(b.Y), (int)Math.Floor(b.Z))}";
        _hud.Update(delta, sim, sim.Portals, info, Hints(), PromptStyle);
    }
}
