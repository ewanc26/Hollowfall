using Godot;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Replay;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;
using Hollowfall.Core.Input;
using Hollowfall.Core.Localization;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Story;
using Hollowfall.Core.Ui;

namespace Hollowfall.Game;

public enum MenuScreen
{
    None,
    Title,
    Pause,
    Controls,
    Settings,
    Journal,

    /// <summary>How each faction regards the player (relationship system). Appended last so earlier values never shift.</summary>
    Standing,

    /// <summary>The character: experience, level and the stat points to spend (progression). Appended after Standing.</summary>
    Character,

    /// <summary>The quests on offer and their progress (progression). Appended after Character.</summary>
    Quests,

    /// <summary>The bag: items carried between runs, and the charm worn. Appended after Quests.</summary>
    Bag,

    /// <summary>Developer tools (debug builds only): test levels, the model showcase and the debug overlay. Appended last.</summary>
    Developer,
}

/// <summary>
/// Title, pause, controls (rebinding) and settings screens, built in code. The game tree is paused while a menu is up
/// and everything is reachable with the keyboard, mouse or a gamepad (buttons take focus, so the d-pad and sticks
/// navigate). Settings and bindings are applied as they change and saved when you leave a screen.
/// </summary>
public partial class Menus : CanvasLayer
{
    private const double ListenSeconds = 6;

    // Gàidhlig labels run longer than English ones, so the action column is wide enough for "Gluais air adhart (Move forward)".
    private const float ActionColumn = 310, BindingColumn = 210;

    private readonly GameSettings _settings;
    private readonly Loc _loc;
    private readonly string _savePath;
    private readonly Dictionary<MenuScreen, Control> _screens = [];
    private readonly Dictionary<(GameAction, Slot), Button> _bindingButtons = [];
    private Label _message = null!;
    private (GameAction Action, Slot Slot)? _listening;
    private double _listenLeft;
    private MenuScreen _current = MenuScreen.None;
    private OptionButton? _promptBox;
    private VBoxContainer _journalList = null!;
    private Label _journalTitle = null!;
    private VBoxContainer _standingList = null!;
    private VBoxContainer _characterList = null!;
    private VBoxContainer _questList = null!;
    private VBoxContainer _bagList = null!;

    /// <summary>Supplies the player's reputation for the Standing screen; null when the relationship system is off. Set by the game.</summary>
    public Func<Hollowfall.Core.Relations.Reputation?> StandingProvider { get; set; } = () => null;

    /// <summary>The labels on the Standing screen's rows, for tests.</summary>
    public IReadOnlyList<string> StandingLines => _standingLines;

    private readonly List<string> _standingLines = [];

    /// <summary>Supplies the character for the Character screen; null when there is none. Set by the game.</summary>
    public Func<Progression?> CharacterProvider { get; set; } = () => null;

    /// <summary>Supplies the bag for the Bag screen; null when there is none. Set by the game.</summary>
    public Func<Inventory?> BagProvider { get; set; } = () => null;

    /// <summary>The player wears a charm from the bag, or takes the worn one off.</summary>
    public event Action<ItemKind>? WearRequested;
    public event Action? TakeOffRequested;

    /// <summary>Raised when the player spends a point on a stat. The host spends it and saves the character.</summary>
    public event Action<Stat>? SpendRequested;

    /// <summary>Raised when the player takes a quest on the Quests screen.</summary>
    public event Action<QuestId>? QuestTakeRequested;

    public Menus(GameSettings settings, string savePath, Loc? loc = null)
    {
        _settings = settings;
        _savePath = savePath;
        _loc = loc ?? new Loc(settings.Language);
    }

    /// <summary>Supplies the controller style to show on binding buttons; set by the game from the connected pad.</summary>
    public Func<ControllerStyle> PromptStyleProvider { get; set; } = () => ControllerStyle.Xbox;

    /// <summary>Supplies the journal: the dungeon's title and the notes read so far, with the floor each was found on. Set by the game.</summary>
    public Func<(string Title, IReadOnlyList<(string Floor, StoryNote Note)> Notes)> JournalProvider { get; set; } = () => ("", []);

    public MenuScreen Current => _current;
    public bool IsOpen => _current != MenuScreen.None;

    /// <summary>Raised with true when a menu opens and false when the last one closes.</summary>
    public event Action<bool>? MenuOpenChanged;

    public event Action? ResumeRequested;
    public event Action? RespawnRequested;
    public event Action? QuitRequested;

    /// <summary>Raised when a different seed was chosen on the title screen: the host builds a fresh world.</summary>
    public event Action? NewGameRequested;

    /// <summary>Raised by the pause menu's main menu button.</summary>
    public event Action? MainMenuRequested;

    /// <summary>Developer menu: start a run in the glued test room.</summary>
    public event Action? GluedRoomRequested;

    /// <summary>Developer menu: open a page of the model showcase, named as HOLLOWFALL_SHOWCASE names them.</summary>
    public event Action<string>? ShowcaseRequested;

    /// <summary>Developer menu: whether the debug overlay is showing, and the change when the player ticks it.</summary>
    public Func<bool> DebugOverlayOn { get; set; } = () => false;
    public event Action<bool>? DebugOverlayChanged;

    private CheckBox? _debugBox;

    /// <summary>Fired whenever a setting or binding changes, so the game can apply it.</summary>
    public event Action? SettingsChanged;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = MakeTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _root = root;
        BuildScreens();
    }

    private Control _root = null!;

    private void BuildScreens()
    {
        _bindingButtons.Clear();
        _screens[MenuScreen.Title] = BuildTitle();
        _screens[MenuScreen.Pause] = BuildPause();
        _screens[MenuScreen.Controls] = BuildControls();
        _screens[MenuScreen.Settings] = BuildSettings();
        _screens[MenuScreen.Journal] = BuildJournal();
        _screens[MenuScreen.Standing] = BuildStanding();
        _screens[MenuScreen.Character] = BuildCharacter();
        _screens[MenuScreen.Quests] = BuildQuests();
        _screens[MenuScreen.Bag] = BuildBag();
        _screens[MenuScreen.Developer] = BuildDeveloper();
        foreach (var s in _screens.Values)
        {
            s.Visible = false;
            _root.AddChild(s);
        }
    }

    /// <summary>Builds every screen again in the current language and shows the one that was open, so a language change is seen at once.</summary>
    public void RebuildScreens()
    {
        var showing = _current;
        CancelListening();
        foreach (var old in _screens.Values.ToList())
        {
            old.Visible = false;
            old.QueueFree();
        }
        BuildScreens();
        if (showing != MenuScreen.None)
        {
            foreach (var (s, c) in _screens) c.Visible = s == showing;
            if (showing == MenuScreen.Journal) RefreshJournal();
            FocusFirst(_screens[showing]);
        }
    }

    // ---- showing and hiding -----------------------------------------------------------------

    public void Open(MenuScreen screen)
    {
        if (_current == MenuScreen.Controls || _current == MenuScreen.Settings) Save();
        CancelListening();
        foreach (var (s, c) in _screens) c.Visible = s == screen;
        _current = screen;
        GetTree().Paused = screen != MenuScreen.None;
        MenuOpenChanged?.Invoke(screen != MenuScreen.None);
        Input.MouseMode = screen == MenuScreen.None ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        if (screen == MenuScreen.Journal) RefreshJournal();
        if (screen == MenuScreen.Standing) RefreshStanding();
        if (screen == MenuScreen.Character) RefreshCharacter();
        if (screen == MenuScreen.Quests) RefreshQuests();
        if (screen == MenuScreen.Bag) RefreshBag();
        if (screen == MenuScreen.Developer && _debugBox != null) _debugBox.SetPressedNoSignal(DebugOverlayOn());
        if (screen != MenuScreen.None) FocusFirst(_screens[screen]);
    }

    public void Close() => Open(MenuScreen.None);

    /// <summary>Gives keyboard and gamepad focus to the first thing on a screen that can take it (buttons, sliders, checkboxes).</summary>
    private static bool FocusFirst(Node n)
    {
        foreach (var child in n.GetChildren())
        {
            if (child is Control { Visible: true, FocusMode: not Control.FocusModeEnum.None } c && !(c is BaseButton { Disabled: true }))
            {
                c.GrabFocus();
                return true;
            }
            if (FocusFirst(child)) return true;
        }
        return false;
    }

    private void Save()
    {
        _settings.TrySave(_savePath);
    }

    public override void _Input(InputEvent e)
    {
        if (_listening is { } target)
        {
            if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                CancelListening();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (InputMapper.FromEvent(e, target.Slot == Slot.Gamepad, target.Slot == Slot.KeyboardMouse) is { } binding)
            {
                CommitBinding(target.Action, binding);
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (e.IsActionPressed(InputMapper.Name(GameAction.Pause)) && !e.IsEcho())
        {
            if (_current == MenuScreen.None) Open(MenuScreen.Pause);
            else if (_current == MenuScreen.Pause) { Close(); ResumeRequested?.Invoke(); }
            else if (_current is MenuScreen.Controls or MenuScreen.Settings or MenuScreen.Journal or MenuScreen.Standing or MenuScreen.Character or MenuScreen.Quests or MenuScreen.Bag or MenuScreen.Developer) Open(ReturnScreen());
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_listening == null) return;
        _listenLeft -= delta;
        if (_listenLeft <= 0) CancelListening();
    }

    // ---- building blocks --------------------------------------------------------------------

    private static Theme MakeTheme()
    {
        var theme = new Theme { DefaultFont = UiFonts.Body, DefaultFontSize = 22 };
        StyleBoxFlat Box(Color c, Color border) => new()
        {
            BgColor = c,
            BorderColor = border,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        theme.SetStylebox("normal", "Button", Box(new Color(0.12f, 0.12f, 0.15f), new Color(0.3f, 0.3f, 0.36f)));
        theme.SetStylebox("hover", "Button", Box(new Color(0.2f, 0.2f, 0.26f), new Color(0.6f, 0.6f, 0.75f)));
        theme.SetStylebox("pressed", "Button", Box(new Color(0.3f, 0.2f, 0.12f), new Color(1f, 0.7f, 0.2f)));
        theme.SetStylebox("focus", "Button", Box(new Color(0.2f, 0.2f, 0.26f), new Color(1f, 0.75f, 0.25f)));
        theme.SetStylebox("disabled", "Button", Box(new Color(0.09f, 0.09f, 0.1f), new Color(0.2f, 0.2f, 0.22f)));
        theme.SetStylebox("panel", "PanelContainer", Box(new Color(0.05f, 0.05f, 0.07f, 0.94f), new Color(0.25f, 0.25f, 0.3f)));
        theme.SetColor("font_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color(0.5f, 0.5f, 0.55f));
        theme.SetFontSize("font_size", "Button", 22);
        theme.SetFontSize("font_size", "Label", 22);
        theme.SetConstant("icon_max_width", "Button", 30);
        return theme;
    }

    private static Control Backdrop(float darkness)
    {
        var back = new ColorRect { Color = new Color(0, 0, 0, darkness), MouseFilter = Control.MouseFilterEnum.Stop };
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return back;
    }

    private static (Control Screen, VBoxContainer Column) Panel(float width, float height)
    {
        var holder = Backdrop(0.55f);
        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(centre);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, height) };
        centre.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        return (holder, column);
    }

    private static Label Heading(string text, int size = 30)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontOverride("font", UiFonts.Display);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
        return l;
    }

    /// <summary>Raised for menu ticks and selections so the host can play them.</summary>
    public static event Action<Hollowfall.Core.Audio.SoundId>? UiSound;

    private static Button MakeButton(string text, Action onPress, float minWidth = 0)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 0) };
        b.Pressed += () => UiSound?.Invoke(Hollowfall.Core.Audio.SoundId.UiSelect);
        b.FocusEntered += () => UiSound?.Invoke(Hollowfall.Core.Audio.SoundId.UiMove);
        b.Pressed += onPress;
        return b;
    }

    // ---- screens ----------------------------------------------------------------------------

    private Control BuildTitle()
    {
        var (screen, col) = Panel(500, 480);
        var logo = Glyphs.LoadTexture("res://assets/logo.svg");
        if (logo != null)
            col.AddChild(new TextureRect { Texture = logo, CustomMinimumSize = new Vector2(400, 90), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        else
            col.AddChild(Heading("HOLLOWFALL", 48));
        col.AddChild(new Label { Text = _loc.Tr("title.tagline"), HorizontalAlignment = HorizontalAlignment.Center });

        // Every seed names its dungeon, the way the old maps do.
        var dungeonName = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        dungeonName.AddThemeFontOverride("font", UiFonts.DisplayMedium);
        dungeonName.AddThemeFontSizeOverride("font_size", 26);
        dungeonName.AddThemeColorOverride("font_color", new Color(0.85f, 0.6f, 0.35f));
        col.AddChild(dungeonName);

        // In Gàidhlig and Bilingual the dungeon also has a Gàidhlig name under its English one.
        var gaelicName = new Label { HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
        gaelicName.AddThemeFontSizeOverride("font_size", 22);
        gaelicName.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
        col.AddChild(gaelicName);

        // The world is named by its seed: type one to share or revisit a dungeon, or roll a random one.
        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", 8);
        seedRow.AddChild(new Label { Text = _loc.Tr("title.seed") });
        var seedEdit = new LineEdit { Text = _settings.Seed, MaxLength = SeedWords.MaxLength, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(240, 0), PlaceholderText = _loc.Tr("title.seedHint") };
        seedRow.AddChild(seedEdit);
        void ShowName()
        {
            long seed = TerrainGenerator.SeedFromString(SeedWords.Clean(seedEdit.Text));
            dungeonName.Text = StoryPlan.TitleFor(seed);
            string? gaelic = _loc.DungeonName(seed);
            gaelicName.Text = gaelic ?? "";
            gaelicName.Visible = gaelic != null;
        }
        seedEdit.TextChanged += _ => ShowName();
        ShowName();
        var dice = MakeButton(_loc.Tr("title.random"), () => seedEdit.Text = SeedWords.From((ulong)System.Random.Shared.NextInt64()));
        seedRow.AddChild(dice);
        // The same dungeon for everyone who plays today (UTC), so a daily run can be compared with the rest of the day's players.
        var daily = MakeButton(_loc.Tr("title.daily"), () => seedEdit.Text = DailySeed.Today(DateTime.UtcNow));
        seedRow.AddChild(daily);
        col.AddChild(seedRow);

        // Today's board for a daily seed: the best run of each floor that re-simulates to the result it claims.
        var boardTitle = new Label { Text = _loc.Tr("title.board"), Visible = false };
        var boardText = new Label { Visible = false };
        col.AddChild(boardTitle);
        col.AddChild(boardText);
        string boardSeed = "";
        void ShowBoard()
        {
            string seed = SeedWords.Clean(seedEdit.Text);
            bool daily = DailyBoard.IsDaily(seed);
            boardTitle.Visible = boardText.Visible = daily;
            if (!daily || seed == boardSeed) return;
            boardSeed = seed;
            boardText.Text = "";
            var store = ReplayStore.Beside(Main.RecordsPath);
            long worldSeed = TerrainGenerator.SeedFromString(seed);
            Task.Run(() =>
            {
                var world = new VoxelWorld(worldSeed);
                var entries = DailyBoard.Build(store.LoadAll(seed), r => ReplayRunner.Verify(r, world, out _));
                string text = entries.Count == 0
                    ? _loc.Tr("title.boardNone")
                    : string.Join("\n", entries.Select(e => _loc.Tr("title.boardRow", e.Floor + 1, e.Result.Grade, e.Result.Points, e.Result.Seconds.ToString("0.0"))));
                Callable.From(() => { if (GodotObject.IsInstanceValid(boardText) && boardSeed == seed) boardText.Text = text; }).CallDeferred();
            });
        }
        seedEdit.TextChanged += _ => ShowBoard();
        ShowBoard();

        col.AddChild(MakeButton(_loc.Tr("menu.play"), () =>
        {
            string seed = SeedWords.Clean(seedEdit.Text);
            seedEdit.Text = seed;
            if (seed != _settings.Seed)
            {
                _settings.Seed = seed;
                _settings.TrySave(_savePath);
                NewGameRequested?.Invoke();
                return;
            }
            Close();
            ResumeRequested?.Invoke();
        }));
        if (OS.IsDebugBuild()) col.AddChild(MakeButton("Developer", () => OpenFrom(MenuScreen.Title, MenuScreen.Developer)));
        col.AddChild(MakeButton(_loc.Tr("menu.controls"), () => OpenFrom(MenuScreen.Title, MenuScreen.Controls)));
        col.AddChild(MakeButton(_loc.Tr("menu.settings"), () => OpenFrom(MenuScreen.Title, MenuScreen.Settings)));
        col.AddChild(MakeButton(_loc.Tr("menu.quit"), () => QuitRequested?.Invoke()));
        return screen;
    }

    /// <summary>Developer tools, reachable from the title screen in debug builds only. Labels are English: these are for the team, not players.</summary>
    private Control BuildDeveloper()
    {
        var (screen, col) = Panel(560, 500);
        col.AddChild(Heading("DEVELOPER", 36));

        col.AddChild(new Label { Text = "Test levels", HorizontalAlignment = HorizontalAlignment.Center });
        col.AddChild(MakeButton("Glued test room", () => GluedRoomRequested?.Invoke()));

        col.AddChild(new Label { Text = "Model showcase", HorizontalAlignment = HorizontalAlignment.Center });
        var pages = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pages.AddThemeConstantOverride("separation", 8);
        foreach (var page in new[] { "enemies", "poses", "weapons", "props", "cat", "effects" })
            pages.AddChild(MakeButton(char.ToUpperInvariant(page[0]) + page[1..], () => ShowcaseRequested?.Invoke(page)));
        col.AddChild(pages);

        col.AddChild(new Label { Text = "Overlay", HorizontalAlignment = HorizontalAlignment.Center });
        _debugBox = new CheckBox { Text = "Debug overlay (F3)", ButtonPressed = DebugOverlayOn() };
        _debugBox.Toggled += on => DebugOverlayChanged?.Invoke(on);
        col.AddChild(_debugBox);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

    private Control BuildPause()
    {
        var (screen, col) = Panel(420, 580);
        col.AddChild(Heading(_loc.Tr("pause.title")));
        col.AddChild(MakeButton(_loc.Tr("pause.resume"), () => { Close(); ResumeRequested?.Invoke(); }));
        col.AddChild(MakeButton(_loc.Tr("pause.respawn"), () => { Close(); RespawnRequested?.Invoke(); }));
        col.AddChild(MakeButton(_loc.Tr("pause.journal"), () => OpenFrom(MenuScreen.Pause, MenuScreen.Journal)));
        col.AddChild(MakeButton(_loc.Tr("pause.standing"), () => OpenFrom(MenuScreen.Pause, MenuScreen.Standing)));
        col.AddChild(MakeButton(_loc.Tr("pause.character"), () => OpenFrom(MenuScreen.Pause, MenuScreen.Character)));
        col.AddChild(MakeButton(_loc.Tr("pause.quests"), () => OpenFrom(MenuScreen.Pause, MenuScreen.Quests)));
        col.AddChild(MakeButton(_loc.Tr("pause.bag"), () => OpenFrom(MenuScreen.Pause, MenuScreen.Bag)));
        col.AddChild(MakeButton(_loc.Tr("pause.mainMenu"), () => MainMenuRequested?.Invoke()));
        col.AddChild(MakeButton(_loc.Tr("menu.controls"), () => OpenFrom(MenuScreen.Pause, MenuScreen.Controls)));
        col.AddChild(MakeButton(_loc.Tr("menu.settings"), () => OpenFrom(MenuScreen.Pause, MenuScreen.Settings)));
        col.AddChild(MakeButton(_loc.Tr("menu.quit"), () => QuitRequested?.Invoke()));
        return screen;
    }

    private Control BuildJournal()
    {
        var (screen, col) = Panel(820, 600);
        col.AddChild(Heading(_loc.Tr("journal.title")));
        _journalTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _journalTitle.AddThemeColorOverride("font_color", new Color(0.85f, 0.6f, 0.35f));
        col.AddChild(_journalTitle);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 390), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        _journalList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(740, 0) };
        _journalList.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(_journalList);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

    private Control BuildStanding()
    {
        var (screen, col) = Panel(820, 640);
        col.AddChild(Heading(_loc.Tr("standing.title")));
        var sub = new Label { Text = _loc.Tr("standing.subtitle"), HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(740, 0) };
        sub.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        sub.AddThemeFontSizeOverride("font_size", 18);
        col.AddChild(sub);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 410), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        _standingList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(740, 0) };
        _standingList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_standingList);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

    private Control BuildCharacter()
    {
        var (screen, col) = Panel(640, 520);
        col.AddChild(Heading(_loc.Tr("character.title")));
        _characterList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(560, 0) };
        _characterList.AddThemeConstantOverride("separation", 12);
        col.AddChild(_characterList);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

    private Control BuildBag()
    {
        var (screen, col) = Panel(640, 520);
        col.AddChild(Heading(_loc.Tr("bag.title")));
        _bagList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(560, 0) };
        _bagList.AddThemeConstantOverride("separation", 12);
        col.AddChild(_bagList);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

    /// <summary>Rebuilds the bag screen from the live bag. Called each time the screen opens and after a charm is worn or taken off.</summary>
    private void RefreshBag()
    {
        foreach (var child in _bagList.GetChildren()) child.QueueFree();
        if (BagProvider() is not { } bag)
        {
            _bagList.AddChild(new Label { Text = _loc.Tr("bag.none") });
            return;
        }

        _bagList.AddChild(new Label { Text = _loc.Tr("bag.slots", bag.SlotsUsed, Inventory.Slots) });
        _bagList.AddChild(BagRow(bag, ItemKind.BrownieCream));
        _bagList.AddChild(BagRow(bag, ItemKind.ThornCharm));
        var note = new Label { Text = _loc.Tr("bag.wearHint"), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
        note.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.66f));
        note.AddThemeFontSizeOverride("font_size", 16);
        _bagList.AddChild(note);
    }

    private HBoxContainer BagRow(Inventory bag, ItemKind kind)
    {
        var (name, use, blurb) = Inventory.Info(kind);
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Glyphs.Icon(kind == ItemKind.BrownieCream ? GameIcons.Cream : GameIcons.Charm, 32));
        bool worn = bag.Charm == kind;
        row.AddChild(new Label { Text = $"{name} x{bag.Count(kind)}" + (worn ? $" ({_loc.Tr("bag.worn")})" : ""), CustomMinimumSize = new Vector2(230, 0) });
        var hint = new Label { Text = kind == ItemKind.BrownieCream ? _loc.Tr("bag.creamHint", Inventory.CreamHeal) : blurb, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.66f));
        hint.AddThemeFontSizeOverride("font_size", 16);
        row.AddChild(hint);
        if (use == ItemUse.Charm)
        {
            if (worn) row.AddChild(MakeButton(_loc.Tr("bag.takeOff"), () => { TakeOffRequested?.Invoke(); RefreshBag(); }));
            else
            {
                var wear = MakeButton(_loc.Tr("bag.wear"), () => { WearRequested?.Invoke(kind); UiSound?.Invoke(Hollowfall.Core.Audio.SoundId.CharmWear); RefreshBag(); });
                wear.Disabled = bag.Count(kind) == 0;
                row.AddChild(wear);
            }
        }
        return row;
    }

    /// <summary>Rebuilds the character screen from the live progression. Called each time the screen opens and after a point is spent.</summary>
    private void RefreshCharacter()
    {
        foreach (var child in _characterList.GetChildren()) child.QueueFree();
        if (CharacterProvider() is not { } c)
        {
            _characterList.AddChild(new Label { Text = _loc.Tr("character.none") });
            return;
        }

        string next = c.Level >= Progression.MaxLevel
            ? _loc.Tr("character.maxLevel")
            : _loc.Tr("character.nextLevel", c.Experience, Progression.ExperienceForLevel(c.Level + 1));
        _characterList.AddChild(new Label { Text = _loc.Tr("character.level", c.Level) + "    " + next });
        _characterList.AddChild(new Label { Text = _loc.Tr("character.points", c.PointsAvailable) });
        _characterList.AddChild(StatRow(c, Stat.Vigour, "character.vigour", "character.vigourHint"));
        _characterList.AddChild(StatRow(c, Stat.Might, "character.might", "character.mightHint"));

        var note = new Label { Text = _loc.Tr("character.nextRun"), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
        note.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.66f));
        note.AddThemeFontSizeOverride("font_size", 16);
        _characterList.AddChild(note);
    }

    private Control BuildQuests()
    {
        var (screen, col) = Panel(640, 520);
        col.AddChild(Heading(_loc.Tr("quest.title")));
        _questList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(560, 0) };
        _questList.AddThemeConstantOverride("separation", 12);
        col.AddChild(_questList);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

    /// <summary>Rebuilds the quest list from the live progression. Called each time the screen opens and after a quest is taken.</summary>
    private void RefreshQuests()
    {
        foreach (var child in _questList.GetChildren()) child.QueueFree();
        if (CharacterProvider() is not { } c)
        {
            _questList.AddChild(new Label { Text = _loc.Tr("quest.none") });
            return;
        }

        foreach (var def in QuestLog.Catalogue.Values) _questList.AddChild(QuestRow(c, def));

        var note = new Label { Text = _loc.Tr("quest.nextRun"), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
        note.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.66f));
        note.AddThemeFontSizeOverride("font_size", 16);
        _questList.AddChild(note);
    }

    private HBoxContainer QuestRow(Progression c, QuestDef def)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        text.AddChild(new Label { Text = _loc.Tr(QuestNameKey(def.Id)) });
        string reward = def.Item is { } item ? Inventory.Info(item).Name : "";
        var progress = new Label { Text = _loc.Tr("quest.progress", c.Quests.Progress(def.Id), def.Count) + "    " + _loc.Tr("quest.reward", def.Experience, reward), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        progress.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.66f));
        progress.AddThemeFontSizeOverride("font_size", 16);
        text.AddChild(progress);
        row.AddChild(text);

        bool done = c.Quests.IsDone(def.Id);
        bool taken = c.Quests.Active.Contains(def.Id);
        var button = MakeButton(_loc.Tr(done ? "quest.done" : taken ? "quest.taken" : "quest.take"), () => { QuestTakeRequested?.Invoke(def.Id); RefreshQuests(); });
        button.Disabled = done || taken;
        row.AddChild(button);
        return row;
    }

    private static string QuestNameKey(QuestId id) => id switch
    {
        QuestId.ClearTheHusks => "quest.husks",
        QuestId.ClearTheSpitters => "quest.spitters",
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private HBoxContainer StatRow(Progression c, Stat stat, string nameKey, string hintKey)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        int value = stat == Stat.Vigour ? c.Vigour : c.Might;
        row.AddChild(new Label { Text = _loc.Tr(nameKey, value), CustomMinimumSize = new Vector2(190, 0) });
        var hint = new Label { Text = _loc.Tr(hintKey), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.66f));
        hint.AddThemeFontSizeOverride("font_size", 16);
        row.AddChild(hint);
        var spend = MakeButton("+", () => { SpendRequested?.Invoke(stat); RefreshCharacter(); });
        spend.Disabled = c.PointsAvailable <= 0;
        row.AddChild(spend);
        return row;
    }

    private static Color TierColour(Hollowfall.Core.Relations.ReputationTier tier) => tier switch
    {
        Hollowfall.Core.Relations.ReputationTier.Hated => new Color(0.85f, 0.15f, 0.15f),
        Hollowfall.Core.Relations.ReputationTier.Hostile => new Color(0.95f, 0.4f, 0.25f),
        Hollowfall.Core.Relations.ReputationTier.Wary => new Color(0.95f, 0.75f, 0.25f),
        Hollowfall.Core.Relations.ReputationTier.Neutral => new Color(0.75f, 0.75f, 0.8f),
        Hollowfall.Core.Relations.ReputationTier.Friendly => new Color(0.55f, 0.85f, 0.45f),
        Hollowfall.Core.Relations.ReputationTier.Trusted => new Color(0.35f, 0.85f, 0.65f),
        _ => new Color(0.4f, 0.7f, 1f),
    };

    /// <summary>Rebuilds the Standing screen from the live reputation. Called each time the screen opens.</summary>
    private void RefreshStanding()
    {
        foreach (var child in _standingList.GetChildren()) child.QueueFree();
        _standingLines.Clear();
        var rep = StandingProvider();
        if (rep == null)
        {
            _standingList.AddChild(new Label { Text = _loc.Tr("standing.nobody") });
            return;
        }

        string? group = null;
        foreach (var row in Hollowfall.Core.Relations.StandingBoard.Rows(rep))
        {
            if (row.Group != group)
            {
                group = row.Group;
                var header = new Label { Text = group.ToUpperInvariant() };
                header.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
                header.AddThemeFontSizeOverride("font_size", 17);
                _standingList.AddChild(header);
            }

            var colour = TierColour(row.Tier);
            string tier = Hollowfall.Core.Relations.ReputationTiers.Label(row.Tier);
            string line = $"{row.Name}: {tier} ({row.Value:0})";
            _standingLines.Add(line);

            var name = new Label { Text = row.Name, CustomMinimumSize = new Vector2(190, 0) };
            var label = new Label { Text = tier, CustomMinimumSize = new Vector2(110, 0) };
            label.AddThemeColorOverride("font_color", colour);
            var bar = new Control { CustomMinimumSize = new Vector2(300, 18), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            bar.AddChild(new ColorRect { Size = new Vector2(300, 18), Color = new Color(0.12f, 0.12f, 0.16f) });
            bar.AddChild(new ColorRect { Position = new Vector2(149, 0), Size = new Vector2(2, 18), Color = new Color(1, 1, 1, 0.35f) }); // the neutral mark
            float x = (float)(row.Fraction * 300);
            bar.AddChild(new ColorRect { Position = new Vector2(Math.Min(x, 150), 3), Size = new Vector2(Math.Abs(x - 150), 12), Color = colour });
            var entry = new HBoxContainer();
            entry.AddThemeConstantOverride("separation", 12);
            entry.AddChild(name);
            entry.AddChild(label);
            entry.AddChild(bar);
            _standingList.AddChild(entry);

            var blurb = new Label { Text = row.Blurb, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(700, 0) };
            blurb.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.66f));
            blurb.AddThemeFontSizeOverride("font_size", 15);
            _standingList.AddChild(blurb);
        }

        var recent = Hollowfall.Core.Relations.StandingBoard.RecentLines(rep);
        var recentHead = new Label { Text = _loc.Tr("standing.recent") };
        recentHead.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
        recentHead.AddThemeFontSizeOverride("font_size", 17);
        _standingList.AddChild(recentHead);
        if (recent.Count == 0) _standingList.AddChild(new Label { Text = _loc.Tr("standing.nothingYet") });
        foreach (var line in recent)
        {
            var l = new Label { Text = line };
            l.AddThemeFontSizeOverride("font_size", 17);
            _standingList.AddChild(l);
        }
    }

    /// <summary>Rebuilds the journal from the notes read so far. Called each time the screen opens.</summary>
    private void RefreshJournal()
    {
        var (title, notes) = JournalProvider();
        _journalTitle.Text = notes.Count == 0 ? title : _loc.Tr("journal.notesRead", title, notes.Count);
        foreach (var child in _journalList.GetChildren()) child.QueueFree();

        if (notes.Count == 0)
        {
            var empty = new Label { Text = _loc.Tr("journal.empty"), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(720, 0) };
            empty.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            _journalList.AddChild(empty);
            return;
        }
        foreach (var (floor, note) in notes)
        {
            var floorLabel = new Label { Text = floor };
            floorLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
            floorLabel.AddThemeFontSizeOverride("font_size", 17);
            var heading = new Label { Text = note.Title };
            heading.AddThemeFontOverride("font", UiFonts.DisplayMedium);
            heading.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
            var text = new Label { Text = note.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(720, 0) };
            text.AddThemeFontSizeOverride("font_size", 24);
            var entry = new VBoxContainer();
            entry.AddThemeConstantOverride("separation", 2);
            entry.AddChild(floorLabel);
            entry.AddChild(heading);
            entry.AddChild(text);
            _journalList.AddChild(entry);
        }
    }

    private Control BuildControls()
    {
        var (screen, col) = Panel(900, 620);
        col.AddChild(Heading(_loc.Tr("controls.title")));
        col.AddChild(new Label { Text = _loc.Tr("controls.help"), HorizontalAlignment = HorizontalAlignment.Center });

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 400), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        col.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        var header = new HBoxContainer();
        header.AddChild(new Label { Text = _loc.Tr("controls.colAction"), CustomMinimumSize = new Vector2(ActionColumn, 0) });
        header.AddChild(new Label { Text = _loc.Tr("controls.colKeys"), CustomMinimumSize = new Vector2(BindingColumn + 8, 0) });
        header.AddChild(new Label { Text = _loc.Tr("controls.colPad") });
        list.AddChild(header);

        foreach (var group in Enum.GetValues<ActionGroup>())
        {
            var title = new Label { Text = _loc.Tr(InterfaceStrings.GroupKey(group)) };
            title.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
            list.AddChild(title);

            foreach (var action in Enum.GetValues<GameAction>().Where(a => ActionInfo.GroupOf(a) == group))
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(new Label { Text = ActionLabel(action), CustomMinimumSize = new Vector2(ActionColumn, 0) });

                foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
                {
                    var button = MakeButton("", () => BeginListening(action, slot), BindingColumn);
                    if (slot == Slot.KeyboardMouse && ActionInfo.AnalogOnlyOnGamepad(action)) button.Disabled = true;
                    _bindingButtons[(action, slot)] = button;
                    row.AddChild(button);
                }
                list.AddChild(row);
            }
        }

        _message = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        col.AddChild(_message);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddThemeConstantOverride("separation", 12);
        footer.AddChild(MakeButton(_loc.Tr("controls.resetAll"), () =>
        {
            _settings.Bindings.ResetToDefaults();
            Changed(_loc.Tr("controls.resetDone"));
        }));
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);

        RefreshBindingButtons();
        return screen;
    }

    private MenuScreen _returnTo = MenuScreen.Pause;

    private MenuScreen ReturnScreen() => _returnTo;

    /// <summary>Remembers whether the title or the pause menu opened Controls and Settings, so Back returns there.</summary>
    public void OpenFrom(MenuScreen from, MenuScreen to)
    {
        _returnTo = from;
        Open(to);
    }

    private Control BuildSettings()
    {
        var (screen, col) = Panel(640, 580);
        col.AddChild(Heading(_loc.Tr("settings.title")));

        AddLanguageChooser(col);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 380), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        col.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(list);

        Slider(list, _loc.Tr("settings.mouseSensitivity"), GameSettings.MouseSensitivityRange.Min, GameSettings.MouseSensitivityRange.Max, 0.0001,
            () => _settings.MouseSensitivity, v => _settings.MouseSensitivity = v, v => $"{v / GameSettings.MouseSensitivityRange.Default:P0}");
        Slider(list, _loc.Tr("settings.padLookSpeed"), GameSettings.PadLookSpeedRange.Min, GameSettings.PadLookSpeedRange.Max, 0.1,
            () => _settings.PadLookSpeed, v => _settings.PadLookSpeed = v, v => $"{v:F1} rad/s");
        Slider(list, _loc.Tr("settings.deadzone"), GameSettings.DeadzoneRange.Min, GameSettings.DeadzoneRange.Max, 0.01,
            () => _settings.Deadzone, v => _settings.Deadzone = v, v => $"{v:P0}");
        Slider(list, _loc.Tr("settings.fov"), GameSettings.FovRange.Min, GameSettings.FovRange.Max, 1,
            () => _settings.FieldOfView, v => _settings.FieldOfView = v, v => $"{v:F0}°");
        Slider(list, _loc.Tr("settings.uiSize"), UiScaling.UserRange.Min, UiScaling.UserRange.Max, 0.05,
            () => _settings.UiScale, v => _settings.UiScale = v, v => $"{v * 100:F0}%");
        CheckBox(list, _loc.Tr("settings.autoScale"), () => _settings.AutoUiScale, v => _settings.AutoUiScale = v);
        Slider(list, _loc.Tr("settings.master"), GameSettings.VolumeRange.Min, GameSettings.VolumeRange.Max, 0.05,
            () => _settings.MasterVolume, v => _settings.MasterVolume = v, v => $"{v * 100:F0}%");
        Slider(list, _loc.Tr("settings.effects"), GameSettings.VolumeRange.Min, GameSettings.VolumeRange.Max, 0.05,
            () => _settings.EffectsVolume, v => _settings.EffectsVolume = v, v => $"{v * 100:F0}%");
        Slider(list, _loc.Tr("settings.ambience"), GameSettings.AmbienceRange.Min, GameSettings.AmbienceRange.Max, 0.05,
            () => _settings.AmbienceVolume, v => _settings.AmbienceVolume = v, v => $"{v * 100:F0}%");
        Slider(list, _loc.Tr("settings.music"), GameSettings.MusicRange.Min, GameSettings.MusicRange.Max, 0.05,
            () => _settings.MusicVolume, v => _settings.MusicVolume = v, v => $"{v * 100:F0}%");
        Slider(list, _loc.Tr("settings.viewDistance"), GameSettings.ViewRadiusRange.Min, GameSettings.ViewRadiusRange.Max, 1,
            () => _settings.ViewRadius, v => _settings.ViewRadius = (int)v, v => _loc.Tr("settings.chunks", $"{v:F0}"));

        var prompts = new HBoxContainer();
        prompts.AddThemeConstantOverride("separation", 10);
        prompts.AddChild(new Label { Text = _loc.Tr("settings.prompts"), CustomMinimumSize = new Vector2(220, 0) });
        var promptBox = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
        foreach (var style in Enum.GetValues<PromptStyle>()) promptBox.AddItem(style == PromptStyle.Auto ? _loc.Tr("settings.promptsAuto") : style.ToString(), (int)style);
        promptBox.Selected = (int)_settings.ControllerPrompts;
        promptBox.ItemSelected += index =>
        {
            _settings.ControllerPrompts = (PromptStyle)(int)promptBox.GetItemId((int)index);
            SettingsChanged?.Invoke();
            RefreshBindingButtons();
        };
        prompts.AddChild(promptBox);
        list.AddChild(prompts);
        _promptBox = promptBox;

        CheckBox(list, _loc.Tr("settings.invertMouse"), () => _settings.InvertMouseY, v => _settings.InvertMouseY = v);
        CheckBox(list, _loc.Tr("settings.invertPad"), () => _settings.InvertPadY, v => _settings.InvertPadY = v);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddThemeConstantOverride("separation", 12);
        footer.AddChild(MakeButton(_loc.Tr("menu.reset"), () =>
        {
            var keep = _settings.Bindings.Clone();
            string seed = _settings.Seed; // Reset is for options; the chosen world stays
            var language = _settings.Language; // and so does the language you chose
            _settings.ResetToDefaults();
            _settings.Seed = seed;
            _settings.Language = language;
            _settings.Bindings.ResetToDefaults();
            foreach (var a in Enum.GetValues<GameAction>())
                foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
                    if (keep.Get(a, slot) is { } b) _settings.Bindings.Rebind(a, b);
            SettingsChanged?.Invoke();
            // The sliders and checkboxes were built from the old values; build the screen again so they show the new ones.
            var old = _screens[MenuScreen.Settings];
            var parent = old.GetParent();
            var fresh = BuildSettings();
            fresh.Visible = false;
            parent.AddChild(fresh);
            _screens[MenuScreen.Settings] = fresh;
            old.Visible = false;
            old.QueueFree();
            Open(MenuScreen.Settings);
        }));
        footer.AddChild(MakeButton(_loc.Tr("menu.back"), () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

    private string ActionLabel(GameAction action) => _loc.Tr(InterfaceStrings.ActionKey(action));

    /// <summary>The first row of Settings: the language, each named in itself. Choosing one rebuilds every screen at once.</summary>
    private void AddLanguageChooser(VBoxContainer parent)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = _loc.Tr("settings.language"), CustomMinimumSize = new Vector2(220, 0) });
        var box = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        foreach (var language in Enum.GetValues<Language>()) box.AddItem(Languages.NativeName(language), (int)language);
        box.Selected = (int)_settings.Language;
        box.ItemSelected += index =>
        {
            var chosen = (Language)box.GetItemId((int)index);
            if (chosen == _settings.Language) return;
            _settings.Language = chosen;
            _loc.Language = chosen;
            SettingsChanged?.Invoke();
            // This box belongs to the screen being rebuilt, so wait until the event has finished.
            Callable.From(RebuildScreens).CallDeferred();
        };
        row.AddChild(box);
        parent.AddChild(row);
        _languageBox = box;
    }

    private OptionButton? _languageBox;

    /// <summary>The Language chooser on the Settings screen (for tests).</summary>
    public OptionButton? LanguageBox => _languageBox;

    private void Slider(VBoxContainer parent, string label, double min, double max, double step, Func<double> get, Action<double> set, Func<double, string> show)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(220, 0) });
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = get(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(200, 28) };
        var value = new Label { Text = show(get()), CustomMinimumSize = new Vector2(110, 0) };
        slider.ValueChanged += v =>
        {
            set(v);
            value.Text = show(get());
            SettingsChanged?.Invoke();
        };
        row.AddChild(slider);
        row.AddChild(value);
        parent.AddChild(row);
    }

    private void CheckBox(VBoxContainer parent, string label, Func<bool> get, Action<bool> set)
    {
        var box = new CheckBox { Text = label, ButtonPressed = get() };
        box.Toggled += on =>
        {
            set(on);
            SettingsChanged?.Invoke();
        };
        parent.AddChild(box);
    }

    // ---- rebinding --------------------------------------------------------------------------

    private void BeginListening(GameAction action, Slot slot)
    {
        CancelListening();
        _listening = (action, slot);
        _listenLeft = ListenSeconds;
        _bindingButtons[(action, slot)].Text = slot == Slot.Gamepad ? _loc.Tr("controls.listenPad") : _loc.Tr("controls.listenKey");
        _message.Text = _loc.Tr("controls.binding", ActionLabel(action));
    }

    private void CancelListening()
    {
        if (_listening == null) return;
        _listening = null;
        RefreshBindingButtons();
        if (_message != null) _message.Text = "";
    }

    private void CommitBinding(GameAction action, Binding binding)
    {
        var conflict = _settings.Bindings.FindConflict(binding, action);
        var outcome = _settings.Bindings.Rebind(action, binding);
        _listening = null;
        string message = outcome switch
        {
            RebindOutcome.Ok => _loc.Tr("controls.now", ActionLabel(action), BindingNames.Display(binding)),
            RebindOutcome.Swapped when conflict is { } other && _settings.Bindings.Get(other, Hollowfall.Core.Input.BindingMap.SlotOf(binding)) == null =>
                _loc.Tr("controls.swapUnbound", BindingNames.Display(binding), ActionLabel(other)),
            RebindOutcome.Swapped => _loc.Tr("controls.swapped", BindingNames.Display(binding), ActionLabel(conflict ?? action)),
            RebindOutcome.InUse => _loc.Tr("controls.inUse", BindingNames.Display(binding), ActionLabel(conflict ?? action)),
            RebindOutcome.Reserved => _loc.Tr("controls.reserved", BindingNames.Display(binding), ActionLabel(action)),
            RebindOutcome.WrongKind => _loc.Tr("controls.wrongKind", ActionLabel(action), BindingNames.Display(binding)),
            _ => _loc.Tr("controls.same"),
        };
        Changed(message);
    }

    private void Changed(string message)
    {
        RefreshBindingButtons();
        _message.Text = message;
        SettingsChanged?.Invoke();
    }

    public void RefreshBindingButtons()
    {
        var style = PromptStyleProvider();
        foreach (var ((action, slot), button) in _bindingButtons)
        {
            if (slot == Slot.KeyboardMouse && ActionInfo.AnalogOnlyOnGamepad(action))
            {
                button.Text = _loc.Tr("controls.mouse");
                button.Icon = Glyphs.Get("mouse");
                continue;
            }
            var binding = _settings.Bindings.Get(action, slot);
            button.Text = BindingNames.Display(binding, style);
            button.Icon = Glyphs.ForBinding(binding, style);
        }
    }
}
