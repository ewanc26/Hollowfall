using Godot;
using Hollowfall.Core.Audio.Music;
using Hollowfall.Core.Storage;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Input;
using Hollowfall.Core.Localization;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Relations;
using Hollowfall.Core.Story;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.Textures;
using Hollowfall.Core.World;
using Hollowfall.Core.Ui;

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
    private Loc _loc = null!;
    private Menus _menus = null!;
    private ViewModels _weapons = null!;
    private PropsView? _props;
    private ReplayDirector? _replay;
    private AudioDirector _audio = null!;
    private MusicDirector _music = null!;
    private bool _endingStingerSent;
    private EffectsDust _dust = null!;
    private RunRecords _records = new();
    private string _seedName = "";
    private Godot.Environment _env = null!;
    private Atmosphere _atmosphere = DungeonThemes.Surface;
    private StoryUi _storyUi = null!;
    private StoryDirector _story = null!;
    private NotesView? _notesView;
    private WulverDirector? _wulver;
    private WulverView? _wulverView;
    private CatDirector? _cat;
    private CatView? _catView;
    private int _deathCount, _killCount, _deepestFloor = -1;
    private double _playSeconds;

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

    /// <summary>
    /// Everything the game saves lives in one folder, ~/.ewanc26/hollowfall (HOLLOWFALL_HOME moves it). The per-file variables
    /// HOLLOWFALL_SETTINGS, HOLLOWFALL_RECORDS, HOLLOWFALL_STORY and HOLLOWFALL_RELATIONS still win, and the smoke test uses them.
    /// </summary>
    public static DataHome Data { get; } = DataHome.ForCurrentUser();

    public static string SettingsPath => Data.SettingsPath;

    public static string RecordsPath => Data.RecordsPath;

    public static string StoryPath => Data.StoryPath;

    public MusicDirector Music => _music;
    public static string RelationsPath => Data.RelationsPath;
    public static string ProgressionPath => Data.ProgressionPath;

    private RelationsStore _relations = new();

    /// <summary>Your character across runs (experience and stat points), kept in progression.json.</summary>
    private Progression _character = new();

    /// <summary>Records where this seed's standings stand now, in relations.json (atomic, bounded). Cheap: it only runs when a tier changes or a floor ends.</summary>
    private void SaveRelations()
    {
        if (Combat?.Sim.Relations is not { } relations) return;
        _relations.Record(_seedName, relations.Reputation);
        _relations.TrySave(RelationsPath);
    }

    /// <summary>Gives experience for a defeated enemy. The character is saved when a level is gained and when a floor ends, not on every kill.</summary>
    private void AwardKill(EnemyKilled killed)
    {
        int before = _character.Level;
        if (_character.Award(killed.Kind) == 0) return;
        if (_character.Level != before) ProgressionStore.TrySave(_character, ProgressionPath);
    }

    public StoryDirector Story => _story;
    public WulverDirector? Wulver => _wulver;
    public WulverView? WulverView => _wulverView;
    public CatDirector? Cat => _cat;
    public CatView? CatView => _catView;
    public PropsView? Props => _props;
    public StoryUi StoryUi => _storyUi;
    public string SeedName => _seedName;

    private Action<Hollowfall.Core.Audio.SoundId>? _uiSound;

    public override void _ExitTree()
    {
        // The event is static and outlives this scene; a handler left on it would call into a freed AudioDirector.
        if (_uiSound != null) Menus.UiSound -= _uiSound;
        _uiSound = null;
        // The window outlives a scene reload too.
        if (GetWindow() is { } window) window.SizeChanged -= ApplyUiScale;
        if (Combat?.Sim.Relations?.Reputation.RecentChanges.Count > 0) SaveRelations();
    }

    public override void _Ready()
    {
        // Pick up anything an earlier version saved in the engine's own user folder, once.
        int adopted = Data.AdoptLegacy(ProjectSettings.GlobalizePath("user://"));
        if (adopted > 0) GD.Print($"Hollowfall: copied {adopted} saved file(s) into {Data.Root}");
        Settings = GameSettings.Load(SettingsPath);
        _loc = new Loc(Settings.Language);
        InputMapper.Apply(Settings.Bindings, Settings.Deadzone);
        ApplyUiScale();
        GetWindow().SizeChanged += ApplyUiScale;

        string seedText = System.Environment.GetEnvironmentVariable("HOLLOWFALL_SEED") is { Length: > 0 and <= 100 } s ? s : Settings.Seed;
        World = new VoxelWorld(TerrainGenerator.SeedFromString(seedText));
        _seedName = seedText;
        _records = RunRecords.Load(RecordsPath);

        _env = SkyBuilder.Create(out _);
        AddChild(new WorldEnvironment { Environment = _env });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = SkyBuilder.SunRotationDegrees,
            LightEnergy = 1.1f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90,
        });

        _audio = new AudioDirector { Name = "Audio", Settings = Settings };
        AddChild(_audio);
        _music = new MusicDirector { Name = "Music", Settings = Settings, Seed = World.Seed, Audio = _audio };
        AddChild(_music);
        _uiSound = id => _audio.Play(id);
        Menus.UiSound += _uiSound;

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
        _dust = new EffectsDust { Name = "Dust", Active = false, Target = () => _player.Camera.GlobalPosition };
        AddChild(_dust);

        if (site != null)
        {
            Combat = new CombatView { Name = "Combat" };
            _character = ProgressionStore.Load(ProgressionPath); // read before the run starts: its stats are fixed for the whole run
            var sim = new CombatSim(World, site, _character.Stats);
            sim.EnableRelations(); // the Folk, wanderers and vermin; standing with each is kept per seed in relations.json
            _relations = RelationsStore.Load(RelationsPath);
            Combat.Initialize(sim, site, _view);
            Combat.EventRaised += OnCombatEvent;
            Combat.EventRaised += e => { if (e is ReputationChanged or FloorCompleted) SaveRelations(); };
            Combat.EventRaised += e => { if (e is EnemyKilled killed) AwardKill(killed); };
            Combat.EventRaised += e => { if (e is FloorCompleted) ProgressionStore.TrySave(_character, ProgressionPath); };
            Combat.EventRaised += e => { if (e is BoonGiven && _character.ReceiveBoon()) ProgressionStore.TrySave(_character, ProgressionPath); };
            Combat.EventRaised += e => _audio.OnEvent(e, Combat.Sim);
            Combat.EventRaised += e => { if (MusicEvents.For(e, site.FloorCount - 1, site.Floors[^1].BossRoom?.Id ?? -1) is { } stinger) _music.Trigger(stinger); };
            Combat.EventRaised += e => _hud?.OnEvent(e);
            Combat.EventRaised += e => _story?.OnEvent(e);
            Combat.EventRaised += e => { if (e is PlayerDied) _deathCount++; else if (e is EnemyKilled) _killCount++; };
            Combat.EventRaised += e => { if (e is ShotFired shot) _weapons?.Fire(shot.Weapon); };
            AddChild(Combat);
            _props = new PropsView { Name = "Props" };
            _props.Initialize(site, Combat.Sim.Items, FirePitPlacer.Place(site, World.Seed));
            _props.CrackleLevel = level => _audio.SetLoopLevel(Hollowfall.Core.Audio.SoundId.FireCrackle, level);
            AddChild(_props);
            _replay = new ReplayDirector { Name = "Replay" };
            _replay.Initialize(Combat.Sim, site, seedText, RecordsPath);
            AddChild(_replay);
        }

        // The story: a plan chosen by the seed, scrolls lying in the dungeon, and the director that reads them out.
        var storyProgress = StoryProgress.Load(StoryPath);
        var plan = StoryPlan.For(World.Seed, site?.FloorCount ?? 3);
        var noteSpots = site != null ? NotePlacer.Place(site, World.Seed, plan) : [];
        _storyUi = new StoryUi { Name = "Story" };
        if (noteSpots.Count > 0)
        {
            _notesView = new NotesView { Name = "Notes" };
            _notesView.Initialize(noteSpots, id => storyProgress.IsRead(_seedName, id));
            AddChild(_notesView);
        }
        _story = new StoryDirector(plan, storyProgress, _seedName, StoryPath, noteSpots, site?.Floors[^1].BossRoom?.Id ?? -1, _storyUi, _notesView, id => _audio.Play(id))
        {
            InteractLabel = () => BindingNames.Display(Settings.Bindings.Get(GameAction.Interact, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse), PromptStyle),
            MenuLabel = () => BindingNames.Display(Settings.Bindings.Get(GameAction.Pause, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse), PromptStyle),
            Loc = _loc,
        };

        // The Wulver: a hidden figure on a hill, found with a little exploring.
        if (Combat != null && WulverPlacer.Place(World, site) is { } wulverSpot)
        {
            _wulverView = new WulverView { Name = "Wulver" };
            _wulverView.Initialize(wulverSpot, false);
            AddChild(_wulverView);
            var wulverEncounter = new WulverEncounter(World.Seed, _seedName, wulverSpot, storyProgress.WulverFlags(_seedName), storyProgress.WulverVisits(_seedName));
            _wulver = new WulverDirector(wulverEncounter, Combat.Sim.Vitals, Combat.Sim.Items.Perks, _storyUi, _wulverView, storyProgress, _seedName, StoryPath, PlayWithMotif)
            {
                InteractLabel = () => BindingNames.Display(Settings.Bindings.Get(GameAction.Interact, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse), PromptStyle),
                MenuLabel = () => BindingNames.Display(Settings.Bindings.Get(GameAction.Pause, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse), PromptStyle),
                Loc = _loc,
                Facts = () => new WulverFacts(_deathCount, _killCount, _deepestFloor, site!.FloorCount, _story.NotesRead, Combat.Sim.Items.KeysHeld > 0, false),
            };
            GD.Print($"Hollowfall: the Wulver stands at ({wulverSpot.X:F1}, {wulverSpot.Y:F1}, {wulverSpot.Z:F1}), {wulverSpot.Distance:F0} blocks from the entrance");
        }

        // The cat: asleep on a little seat beside one fire pit, found by following the warmth.
        if (Combat != null && CatPlacer.Place(site, World.Seed) is { } catSpot)
        {
            _catView = new CatView { Name = "Cat" };
            _catView.Initialize(catSpot);
            _catView.Woke += () => _audio.Play(Hollowfall.Core.Audio.SoundId.CatMeow, new Vector3((float)catSpot.X, (float)catSpot.Y + 0.4f, (float)catSpot.Z), 0.8f);
            AddChild(_catView);
            var catEncounter = new CatEncounter(World.Seed, _seedName, catSpot, storyProgress.CatFlags(_seedName), storyProgress.CatVisits(_seedName));
            _cat = new CatDirector(catEncounter, Combat.Sim.Vitals, _storyUi, storyProgress, _seedName, StoryPath, (id, at) => _audio.Play(id, at))
            {
                InteractLabel = () => BindingNames.Display(Settings.Bindings.Get(GameAction.Interact, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse), PromptStyle),
                MenuLabel = () => BindingNames.Display(Settings.Bindings.Get(GameAction.Pause, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse), PromptStyle),
            };
            GD.Print($"Hollowfall: something warm rests on floor {catSpot.Pit.Floor} at ({catSpot.X:F1}, {catSpot.Y:F1}, {catSpot.Z:F1})");
        }

        // The weapons in your hands, drawn under the HUD.
        _weaponLayer = new CanvasLayer { Layer = 4, Name = "Weapons" };
        _weapons = new ViewModels { Name = "ViewModels" };
        _weaponLayer.AddChild(_weapons);
        AddChild(_weaponLayer);

        _hud = new Hud { Name = "Hud", Loc = _loc };
        AddChild(_hud);

        _menus = new Menus(Settings, SettingsPath, _loc) { Name = "Menus" };
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
        _menus.JournalProvider = () => (plan.Title, _story.Journal());
        _menus.StandingProvider = () => Combat?.Sim.Relations?.Reputation;
        _menus.CharacterProvider = () => _character;
        _menus.SpendRequested += stat => { if (_character.Spend(stat)) ProgressionStore.TrySave(_character, ProgressionPath); };
        AddChild(_menus);
        if (_wulver != null)
        {
            var framing = new ConversationCamera { Name = "ConversationCamera" };
            framing.Initialize(_player, _storyUi);
            AddChild(framing);
            _wulver.Framing = framing;
        }
        AddChild(_storyUi); // after the menus, so it sees a press first and can close a note without opening the pause menu
        _menus.MenuOpenChanged += open => _storyUi.SetMenuOpen(open);

        // The game starts at the title screen, except headless (tests), scripted demos and when asked to skip it.
        bool skipTitle = DisplayServer.GetName() == "headless" || _demo != null || System.Environment.GetEnvironmentVariable("HOLLOWFALL_SKIP_TITLE") == "1" || _skipTitleOnce;
        _skipTitleOnce = false;
        if (!skipTitle) _menus.Open(MenuScreen.Title);

        // The opening crawl plays the first time a seed is played: when the title screen is left, or straight away if it was skipped.
        bool introAllowed = DisplayServer.GetName() != "headless" && _demo == null;
        if (introAllowed)
        {
            _menus.ResumeRequested += () => _story.ShowIntroIfNew();
            if (skipTitle) CallDeferred(MethodName.ShowIntro);
        }

        // For screenshots and videos: HOLLOWFALL_MENU=title|pause|controls|settings opens that screen straight away.
        if (System.Environment.GetEnvironmentVariable("HOLLOWFALL_MENU") is { } menu)
            _menus.CallDeferred(Menus.MethodName.OpenFrom, (int)MenuScreen.Pause, (int)(menu switch
            {
                "title" => MenuScreen.Title,
                "controls" => MenuScreen.Controls,
                "settings" => MenuScreen.Settings,
                "journal" => MenuScreen.Journal,
                "standing" => MenuScreen.Standing,
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
        _loc.Language = Settings.Language;
        InputMapper.Apply(Settings.Bindings, Settings.Deadzone);
        _view.ViewRadius = Settings.ViewRadius;
        ApplyUiScale();
    }

    /// <summary>
    /// Sizes the whole interface from the window: laid out for 1280 by 720, scaled by <see cref="UiScaling"/> so it keeps its share
    /// of the screen on a small laptop, a 4K display or a retina one, times the player's own multiplier. Only 2D is scaled; the
    /// 3D view still renders at the window's real pixels.
    /// </summary>
    public void ApplyUiScale()
    {
        var window = GetWindow();
        if (window == null) return;
        double factor = UiScaling.Factor(window.Size.X, window.Size.Y, Settings.UiScale, Settings.AutoUiScale);
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleSize = Vector2I.Zero; // no base size: the factor alone decides
        window.ContentScaleFactor = (float)factor;
        UiScaleApplied = factor;
    }

    /// <summary>The interface scale in force, for tests and the debug overlay.</summary>
    public double UiScaleApplied { get; private set; } = 1.0;

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

    /// <summary>The player's real binding for an action on the device they last used, as shown in the hints.</summary>
    private string PortalKeyName(GameAction action) =>
        BindingNames.Display(Settings.Bindings.Get(action, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse));

    private List<(Binding? Binding, string Label)> Hints()
    {
        var slot = _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse;
        (Binding?, string) Hint(GameAction a, string label) => (Settings.Bindings.Get(a, slot), label);
        return
        [
            Hint(GameAction.FirePrimary, _loc.Tr("hint.revolver")),
            Hint(GameAction.FireSecondary, _loc.Tr("hint.shotgun")),
            Hint(GameAction.Parry, _loc.Tr("hint.parry")),
            Hint(GameAction.PortalTeal, _loc.Tr("hint.tealPortal")),
            Hint(GameAction.PortalViolet, _loc.Tr("hint.violetPortal")),
            Hint(GameAction.ClosePortals, _loc.Tr("hint.sever")),
            Hint(GameAction.Interact, _loc.Tr("hint.open")),
            Hint(GameAction.Jump, _loc.Tr("hint.jump")),
            Hint(GameAction.Pause, _loc.Tr("hint.menu")),
        ];
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
        _audio.Walk(delta, _player.Body, dead || _player.Flying, World.GetBlock);

        if (Combat != null)
        {
            if (dead && _player.RespawnRequested) Respawn();
            var view = _player.View;
            if (_demo == "notes") actions = RunNotesDemo(actions);
            if (_demo == "ending" && ++_demoFrame == 10) _story.ShowEnding();
            if (_demo == "captions") RunCaptionDemo();
            if (_demo == "floor") RunFloorDemo();
            if (_demo is { } d && d.StartsWith("wulver")) RunWulverDemo(d);
            if (_demo == "folk") RunFolkDemo();
            actions = _story.Filter(actions, view.Feet, !dead);
            if (_wulver != null) actions = _wulver.Filter(actions, view.Feet, !dead);
            if (_demo is { } cd && cd.StartsWith("cat")) RunCatDemo(cd);
            if (_cat != null) actions = _cat.Filter(actions, view.Feet, !dead);
            if (_demo == "portals") (view, actions) = RunPortalDemo(view, actions);
            if (_demo == "gap") (view, actions) = RunGapDemo(view, actions);
            if (_demo == "sever") (view, actions) = RunSeverDemo(view, actions);
            if (_replay != null) (view, actions) = _replay.Capture(delta, view, actions);
            Combat.Step(delta, view, actions);
            foreach (var impact in Combat.LastEvents.OfType<MeteorImpact>()) _player.KeepSpeed(impact.SpeedKept);
            // Fell into a pit: the sim says where it is safe, and the body goes there (the replay records the new place on the next step).
            foreach (var recovered in Combat.LastEvents.OfType<PlayerRecovered>())
                _player.PlaceAt(new Vector3((float)recovered.Position.X, (float)recovered.Position.Y, (float)recovered.Position.Z), _player.Yaw);
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

    /// <summary>
    /// A scripted setup for screenshots (HOLLOWFALL_DEMO=gap): stand at a gap and look across it. HOLLOWFALL_GAP is
    /// <c>floor:kind:hazard[:cellsBack[:portals]]</c>, for example <c>1:portal:spikes</c> (the first spike pit on floor 1, from its lip), <c>0:portal:bare:5</c>
    /// (five cells back from the lip, looking down the hallway) or <c>2:shaft:bare:2:portals</c> (a shaft gap with both portals fired).
    /// </summary>
    private (PlayerView, PlayerActions) RunGapDemo(PlayerView view, PlayerActions actions)
    {
        _demoFrame++;
        var parts = (System.Environment.GetEnvironmentVariable("HOLLOWFALL_GAP") ?? "0:portal:bare").Split(':');
        if (Combat == null || parts.Length < 3 || !int.TryParse(parts[0], out int floor)) return (view, actions);
        var site = Combat.Site;
        var kind = parts[1] == "shaft" ? LinkKind.ShaftGap : LinkKind.PortalGap;
        var hazard = parts[2] switch { "spikes" => GapHazard.Spikes, "water" => GapHazard.Water, _ => GapHazard.Bare };
        int back = parts.Length > 3 && int.TryParse(parts[3], out int b) ? b : 0;
        bool fire = parts.Length > 4 && parts[4] == "portals";
        var gap = site.Floors[floor].Gaps.FirstOrDefault(g => g.Kind == kind && (kind == LinkKind.ShaftGap || g.Hazard == hazard));
        if (gap == null) return (view, actions);
        var plan = GapCrossing.Plan(site, floor, gap, true);
        var hall = gap.Hall(true);
        var feet = GapCrossing.FeetAt(site, floor, hall[Math.Max(0, hall.Count - 1 - back)]);
        double yaw = Math.Atan2(-gap.DirX, -gap.DirZ);
        if (_demoFrame == 5) MovePlayerToFloor(floor);
        if (_demoFrame is 8 or 9 || _demoFrame == 20) _player.PlaceAt(new Vector3((float)feet.X, (float)feet.Y, (float)feet.Z), yaw, kind == LinkKind.ShaftGap && back > 0 ? 0.35 : -0.32);
        if (fire && plan != null)
        {
            var eye = new Vec3(_player.Body.X, _player.Body.Y + 1.62, _player.Body.Z);
            if (_demoFrame == 30) return (view with { Forward = plan.FarAim, Eye = eye }, actions with { PortalTeal = true });
            if (_demoFrame == 32) return (view with { Forward = plan.NearAim, Eye = eye }, actions with { PortalViolet = true });
        }
        return (view, actions);
    }

    /// <summary>
    /// A scripted Sever (HOLLOWFALL_DEMO=sever): link two portals, stand a husk in the teal opening and close the pair on it.
    /// </summary>
    private (PlayerView, PlayerActions) RunSeverDemo(PlayerView view, PlayerActions actions)
    {
        _demoFrame++;
        if (_demoFrame == 5) MovePlayerToFloor(0);
        var east = new Vec3(1, 0, 0);
        if (_demoFrame == 30) return (view with { Forward = east }, actions with { PortalTeal = true });
        if (_demoFrame == 32) return (view with { Forward = new Vec3(0, 0, -1) }, actions with { PortalViolet = true });
        if (_demoFrame == 34 && Combat!.Sim.Portals.Teal is { } teal)
        {
            var at = teal.Frame.Center + teal.Frame.Normal * 3.5;
            _player.PlaceAt(new Vector3((float)at.X, (float)view.Feet.Y, (float)at.Z), -Math.PI / 2);
            var husk = teal.Frame.Center + teal.Frame.Normal * 0.15;
            Combat.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(husk.X, view.Feet.Y, husk.Z));
        }
        if (_demoFrame == 36) return (view, actions with { ClosePortals = true });
        return (view, actions);
    }

    /// <summary>
    /// HOLLOWFALL_DEMO=folk: line the Folk, a Bean Nighe and a hare up in front of the player on the first floor, with the Brownies
    /// warm toward you, so the models, the pools and the disposition pips can be seen. Toward the end a witnessed kill changes standing.
    /// </summary>
    private void RunFolkDemo()
    {
        _demoFrame++;
        var sim = Combat!.Sim;
        if (_demoFrame == 5)
        {
            sim.Relations!.Reputation.Set(Hollowfall.Core.Relations.Faction.Brownies, 40);
            MovePlayerToFloor(0);
        }
        if (_demoFrame == 12)
        {
            // Stand at the near edge of the entrance room, looking across it, with the creatures in an arc beyond.
            var room = Combat.Site.Floors[0].EntranceRoom;
            var (cx, cy, cz) = Combat.Site.ToWorld(0, room.CenterX, room.CenterZ);
            _player.PlaceAt(new Vector3(cx + 0.5f, cy, cz + 0.5f + 7.5f), 0);
            (EnemyKind Kind, double X, double Z)[] line = [(EnemyKind.Brownie, -2.6, 0.6), (EnemyKind.Selkie, -1.3, -1.6), (EnemyKind.BeanNighe, 0.9, 0.2), (EnemyKind.Kelpie, 2.4, -1.4), (EnemyKind.Hare, 0.2, 2.6)];
            foreach (var (kind, x, z) in line) sim.SpawnEnemy(kind, new Vec3(cx + 0.5 + x, cy, cz + 0.5 + z));
        }
        if (_demoFrame == 150)
        {
            var brownie = sim.Enemies.FirstOrDefault(e => e.Kind == EnemyKind.Brownie && !e.Dead);
            if (brownie != null) sim.Relations!.PlayerHurt(brownie, brownie.Spec.Health, true, sim.Enemies, sim.Time);
        }
    }

    /// <summary>
    /// A scripted setup for screenshots (HOLLOWFALL_DEMO=notes): go to the first floor, stand a few blocks from its first note,
    /// then walk up to it and read it. HOLLOWFALL_DEMO=ending shows the ending card.
    /// </summary>
    private PlayerActions RunNotesDemo(PlayerActions actions)
    {
        _demoFrame++;
        if (Combat == null || _story.Spots.Count == 0) return actions;
        var spot = _story.Spots[0];
        double away = _demoFrame < 40 ? 4.2 : 1.4;
        if (_demoFrame is 5 or 40)
        {
            if (_demoFrame == 5) MovePlayerToFloor(spot.Floor);
            var at = new Vector3((float)(spot.X + spot.FaceX * away), (float)spot.Y, (float)(spot.Z + spot.FaceZ * away));
            _player.PlaceAt(at, Math.Atan2(spot.FaceX, spot.FaceZ));
        }
        return _demoFrame == 48 ? actions with { Interact = true } : actions;
    }

    /// <summary>
    /// HOLLOWFALL_DEMO=wulver stands the player a few blocks in front of the Wulver, looking at him; wulver_wide stands well back to show
    /// his hill; wulver_talk walks up and speaks to him.
    /// </summary>
    private void RunWulverDemo(string demo)
    {
        _demoFrame++;
        if (_wulverView == null) return;
        var s = _wulverView.Spot;
        double away = demo switch { "wulver_wide" => 8, "wulver_talk" or "wulver_maker" or "wulver_gift" => 2.4, _ => 4.0 };
        if (_demoFrame is 5 or 40)
        {
            double fx = -Math.Sin(s.Yaw), fz = -Math.Cos(s.Yaw);
            if (demo == "wulver_wide")
            {
                // Off to one side and a little back, so the hill and his outline show together.
                (fx, fz) = (fx * Math.Cos(0.7) - fz * Math.Sin(0.7), fx * Math.Sin(0.7) + fz * Math.Cos(0.7));
            }
            double px = s.X + fx * away, pz = s.Z + fz * away;
            int ground = World.Generator.SampleColumn((int)Math.Floor(px), (int)Math.Floor(pz)).Height;
            _player.PlaceAt(new Vector3((float)px, ground + 1.2f, (float)pz), Math.Atan2(px - s.X, pz - s.Z));
        }
        if (demo == "wulver_gift" && _demoFrame == 55) Combat?.Sim.Vitals.Damage(45); // so the heal shows
        if (demo is "wulver_talk" or "wulver_maker" or "wulver_gift" && _demoFrame == 60)
        {
            _wulver?.Talk();
            if (demo == "wulver_gift")
            {
                // What is this place, then the gift, then take it: the gift is handed over about 1.2 seconds in.
                GetTree().CreateTimer(0.4, processAlways: true).Timeout += () => _storyUi.PressDialogueChoice(1);
                GetTree().CreateTimer(0.8, processAlways: true).Timeout += () => _storyUi.PressDialogueChoice(1);
                GetTree().CreateTimer(1.2, processAlways: true).Timeout += () => _storyUi.PressDialogueChoice(0);
            }
            if (demo == "wulver_maker")
            {
                // The game is paused while the panel is open, so the presses are timed on the real clock: who are you, then claim the hill.
                GetTree().CreateTimer(0.4, processAlways: true).Timeout += () => _storyUi.PressDialogueChoice(0);
                GetTree().CreateTimer(0.8, processAlways: true).Timeout += () => _storyUi.PressDialogueChoice(0);
                GetTree().CreateTimer(1.2, processAlways: true).Timeout += () => _storyUi.PressDialogueChoice(0);
            }
        }
    }

    /// <summary>
    /// HOLLOWFALL_DEMO=cat_pit stands the player by a fire pit; cat_sleep a few steps from the sleeping cat (cat_sleep_close, nearer, kept asleep);
    /// cat_awake stands in front of him, awake; cat_talk walks up and opens his conversation.
    /// </summary>
    private void RunCatDemo(string demo)
    {
        _demoFrame++;
        if (_catView == null || Combat == null) return;
        var s = _catView.Spot;
        _catView.ForceSleep = demo == "cat_sleep_close";
        if (demo == "cat_pit_alone" && _props != null && _props.PitPositions.Where(p => new Vector2(p.X - (float)s.X, p.Z - (float)s.Z).Length() > 6).Cast<Vector3?>().FirstOrDefault() is { } alone)
        {
            // A pit with no cat: stand three blocks off, looking at its flames.
            if (_demoFrame is 5 or 40)
            {
                if (_demoFrame == 5) MovePlayerToFloor((int)Math.Round((DungeonSite.FirstFloorY + 1 - alone.Y) / DungeonSite.FloorSpacing));
                _player.PlaceAt(new Vector3(alone.X + 2.6f, alone.Y, alone.Z + 1.2f), Math.Atan2(2.6, 1.2), -0.25);
            }
            Combat.Sim.Vitals.Reset(); // the teleport can drop you a block or two; the shot should not show it
            return;
        }
        if (_demoFrame is 5 or 40 or 41)
        {
            if (_demoFrame == 5) MovePlayerToFloor(s.Pit.Floor);
            // Stand to the side of the line from the pit to the cat, so both show together.
            double ax = -s.Dz, az = s.Dx;
            double away = demo switch { "cat_pit" => 3.6, "cat_sleep" => 8.8, "cat_sleep_close" => 2.7, "cat_talk" or "cat_talk_accents" => 2.0, _ => 3.0 };
            double px = (s.Pit.X + s.X) / 2 + ax * away + s.Dx * (demo == "cat_pit" ? -1.4 : 0.2), pz = (s.Pit.Z + s.Z) / 2 + az * away + s.Dz * (demo == "cat_pit" ? -1.4 : 0.2);
            double tx = demo == "cat_pit" ? s.Pit.X : s.X, tz = demo == "cat_pit" ? s.Pit.Z : s.Z;
            double pitch = demo switch { "cat_pit" => -0.2, "cat_sleep_close" => -0.45, "cat_talk" or "cat_talk_accents" => -0.9, "cat_sleep" => -0.05, _ => -0.3 };
            _player.PlaceAt(new Vector3((float)px, (float)s.Y, (float)pz), Math.Atan2(px - tx, pz - tz), pitch);
        }
        if (demo is "cat_talk" or "cat_talk_accents" && _demoFrame == 90)
        {
            _cat?.Talk();
            // The game is paused while the panel is open, so the press is timed on the real clock: blink back, which gets the first Irish welcome with its accent.
            if (demo == "cat_talk_accents") GetTree().CreateTimer(0.4, processAlways: true).Timeout += () => _storyUi.PressDialogueChoice(0);
        }
    }

    /// <summary>HOLLOWFALL_DEMO=captions: shows the death line, the boss line and a floor-complete line in turn, for screenshots.</summary>
    private void RunCaptionDemo()
    {
        _demoFrame++;
        if (_demoFrame == 10) _story.OnEvent(new PlayerDied());
        if (_demoFrame == 110) { _story.OnRespawn(); _story.OnEvent(new EncounterStarted(_story.Plan.FloorCount - 1, _story.BossRoomId, 1)); }
        if (_demoFrame == 330) _story.OnEvent(new FloorCompleted(0, new FloorResult(Grade.A, Grade.B, Grade.A, 0, 11, OverallGrade.A), 80));
    }

    /// <summary>
    /// HOLLOWFALL_DEMO=floor: sends a floor-complete event through every listener (banner, result panel, story) once the first
    /// floor's card has gone, then enters the next floor, to see how they share the screen.
    /// </summary>
    private void RunFloorDemo()
    {
        _demoFrame++;
        if (_demoFrame == 380)
        {
            var done = new FloorCompleted(0, new FloorResult(Grade.A, Grade.B, Grade.A, 0, 11, OverallGrade.A), 80);
            OnCombatEvent(done);
            _hud.OnEvent(done);
            _story.OnEvent(done);
        }
        if (_demoFrame == 381) _story.OnFloor(1);
    }

    private void Respawn()
    {
        var combat = Combat!;
        int floor = Math.Max(combat.Sim.CurrentFloor, 0);
        combat.Sim.Respawn();
        combat.RefreshPortals(); // the sim cleared the portals; drop their views too
        _replay?.NoteRespawn();
        var site = combat.Site;
        _player.PlaceAt(combat.Sim.CurrentFloor < 0 ? new Vector3(site.EntranceX + DungeonSite.PlazaRadius + 0.5f, site.SurfaceY + 1, site.EntranceZ + 0.5f) : FloorSpawn(site, floor), 0);
        _hud.ShowBanner("", 0);
        _story.OnRespawn();
    }

    /// <summary>A sound the story asked for; the Wulver's greeting also plays his motif in the music.</summary>
    private void PlayWithMotif(Hollowfall.Core.Audio.SoundId id)
    {
        _audio.Play(id);
        if (MusicEvents.For(id) is { } stinger) _music.Trigger(stinger);
    }

    /// <summary>What the music listens to this frame, read from the sim and the interface and built by Core's <see cref="MusicStateBuilder"/>.</summary>
    private MusicState MusicNow()
    {
        var b = _player.Body;
        var sim = Combat?.Sim;
        int floor = sim?.CurrentFloor ?? -1;
        var probes = sim == null ? [] : sim.Enemies.Where(e => e.Floor == floor).Select(e =>
            new EnemyProbe(!e.Dead, e.Kind == EnemyKind.Warden, e.RoomId >= 0, Math.Sqrt((e.Feet.X - b.X) * (e.Feet.X - b.X) + (e.Feet.Z - b.Z) * (e.Feet.Z - b.Z))));
        double wulver = _wulverView is { } w ? Math.Sqrt((w.Spot.X - b.X) * (w.Spot.X - b.X) + (w.Spot.Z - b.Z) * (w.Spot.Z - b.Z)) : double.MaxValue;
        return MusicStateBuilder.Build(
            floor,
            probes,
            (int)(sim?.Style.Rank ?? StyleRank.None),
            sim == null ? 1 : sim.Vitals.Health / sim.Vitals.Max,
            sim?.Dead ?? false,
            wulver,
            _storyUi.Modal || _storyUi.Speaking,
            GetTree().Paused,
            passiveNearby: false,
            portalLinked: sim != null && sim.Portals.Teal != null && sim.Portals.Violet != null);
    }

    private void ShowIntro() => _story.ShowIntroIfNew();

    private void OnCombatEvent(CombatEvent e)
    {
        switch (e)
        {
            case PlayerDied:
                _hud.ShowBanner(_loc.Tr("banner.died") + "\n" + _loc.Tr("banner.respawn", BindingNames.Display(Settings.Bindings.Get(GameAction.Respawn, _player.UsingGamepad ? Slot.Gamepad : Slot.KeyboardMouse))), 3600);
                break;
            case EncounterStarted s:
                _hud.ShowBanner(_loc.Tr("banner.enemies", s.Enemies), 2.5);
                break;
            case EncounterCleared:
                _hud.ShowBanner(_loc.Tr("banner.clear"), 1.5);
                break;
            case FloorCompleted f:
                {
                    _replay?.OnFloorCompleted(f);
                    var (isBest, previous) = _records.Submit(_seedName, f.Floor, f.Result, f.Seconds);
                    if (isBest) _records.TrySave(RecordsPath);
                    string note = isBest ? (previous == null ? _loc.Tr("banner.firstClear") : _loc.Tr("banner.newBest", previous.Value.Grade)) : _loc.Tr("banner.best", _records.Best(_seedName, f.Floor)!.Value.Grade);
                    int secs = (int)f.Seconds;
                    _hud.ShowBanner(_loc.Tr("banner.floorDone", f.Floor + 1, f.Result.Overall, $"{secs / 60}:{secs % 60:D2}") + "\n" + _loc.Tr("banner.floorStats", f.Result.Time, f.Result.Kills, f.Result.Style) + "\n" + note, 6);
                }
                break;
            case KeyCollected:
                _hud.ShowBanner(_loc.Tr("banner.key"), 1.2);
                break;
            case ChestOpened o:
                _hud.ShowBanner(
                    o.Loot is { } loot
                        ? $"{_loc.Tr(o.WasLocked ? "banner.lockedChest" : "banner.chest")}  {LootTable.Announce(loot, o.Applied)}"
                        : o.WasLocked ? _loc.Tr("banner.lockedChestHealed", $"{o.Healed:F0}") : _loc.Tr("banner.chestHealed", $"{o.Healed:F0}"),
                    2.2);
                break;
            case ChestLocked:
                _hud.ShowBanner(_loc.Tr("banner.locked"), 1.8);
                break;
            case TrapSprung t:
                _hud.ShowBanner(_loc.Tr("banner.spikes", $"{t.Damage:F0}"), 0.8);
                break;
            case PlayerFell fell:
                _hud.ShowBanner(GapHints.FallBanner(fell, _loc), 1.4);
                if (fell.First) _storyUi.ShowCaption(GapHints.AfterTheFirstFall(PortalKeyName(GameAction.PortalTeal), PortalKeyName(GameAction.PortalViolet), _loc), 7);
                break;
            case PitEdgeReached edge:
                _storyUi.ShowCaption(GapHints.AtTheEdge(edge.Kind, PortalKeyName(GameAction.PortalTeal), PortalKeyName(GameAction.PortalViolet), _loc), 9);
                break;
            case PortalPlaced { Success: false } failed:
                _hud.ShowBanner(_loc.Tr("banner.noPortal", failed.Failure), 1.2);
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
        if (_music.FollowGame) _music.State = MusicNow();
        if (_story.EndingShown && !_endingStingerSent) { _endingStingerSent = true; _music.Trigger(Stinger.Ending); }
        _dust.Active = _audio.Underground;

        if (Combat == null) return;
        var sim = Combat.Sim;
        int floor = sim.CurrentFloor;
        // The fog and ambient light ease toward the theme of the floor the player is on (or the open sky).
        _atmosphere = DungeonThemes.Lerp(_atmosphere, DungeonThemes.ForAtmosphere(floor), 1f - MathF.Exp(-2.5f * (float)delta));
        SkyBuilder.Apply(_env, _atmosphere);
        _story.OnFloor(floor);
        _deepestFloor = Math.Max(_deepestFloor, floor);
        _playSeconds += delta;
        _story.Tick(delta, floor);
        _notesView?.Update(delta);
        _wulverView?.Update(delta, new Vector3((float)b.X, (float)b.Y, (float)b.Z));
        if (_catView != null)
        {
            _catView.Update(delta, new Vector3((float)b.X, (float)b.Y, (float)b.Z), -_player.Camera.GlobalTransform.Basis.Z);
            _audio.SetLoopLevel(Hollowfall.Core.Audio.SoundId.CatPurr, _catView.PurrLevel);
        }
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
