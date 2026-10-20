using Godot;
using Hollowfall.Core.Input;

namespace Hollowfall.Game;

public enum MenuScreen
{
    None,
    Title,
    Pause,
    Controls,
    Settings,
}

/// <summary>
/// Title, pause, controls (rebinding) and settings screens, built in code. The game tree is paused while a menu is up
/// and everything is reachable with the keyboard, mouse or a gamepad (buttons take focus, so the d-pad and sticks
/// navigate). Settings and bindings are applied as they change and saved when you leave a screen.
/// </summary>
public partial class Menus : CanvasLayer
{
    private const double ListenSeconds = 6;

    private readonly GameSettings _settings;
    private readonly string _savePath;
    private readonly Dictionary<MenuScreen, Control> _screens = [];
    private readonly Dictionary<(GameAction, Slot), Button> _bindingButtons = [];
    private Label _message = null!;
    private (GameAction Action, Slot Slot)? _listening;
    private double _listenLeft;
    private MenuScreen _current = MenuScreen.None;

    public Menus(GameSettings settings, string savePath)
    {
        _settings = settings;
        _savePath = savePath;
    }

    public MenuScreen Current => _current;
    public bool IsOpen => _current != MenuScreen.None;

    /// <summary>Raised with true when a menu opens and false when the last one closes.</summary>
    public event Action<bool>? MenuOpenChanged;

    public event Action? ResumeRequested;
    public event Action? RespawnRequested;
    public event Action? QuitRequested;

    /// <summary>Fired whenever a setting or binding changes, so the game can apply it.</summary>
    public event Action? SettingsChanged;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = MakeTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _screens[MenuScreen.Title] = BuildTitle();
        _screens[MenuScreen.Pause] = BuildPause();
        _screens[MenuScreen.Controls] = BuildControls();
        _screens[MenuScreen.Settings] = BuildSettings();
        foreach (var s in _screens.Values)
        {
            s.Visible = false;
            root.AddChild(s);
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
            else if (_current is MenuScreen.Controls or MenuScreen.Settings) Open(ReturnScreen());
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
        var theme = new Theme();
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
        theme.SetFontSize("font_size", "Button", 18);
        theme.SetFontSize("font_size", "Label", 18);
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
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
        return l;
    }

    private static Button MakeButton(string text, Action onPress, float minWidth = 0)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 0) };
        b.Pressed += onPress;
        return b;
    }

    // ---- screens ----------------------------------------------------------------------------

    private Control BuildTitle()
    {
        var (screen, col) = Panel(460, 360);
        col.AddChild(Heading("HOLLOWFALL", 48));
        col.AddChild(new Label { Text = "A procedural dungeon crawler.\nMove fast. Heal by hurting things.", HorizontalAlignment = HorizontalAlignment.Center });
        col.AddChild(MakeButton("Play", () => { Close(); ResumeRequested?.Invoke(); }));
        col.AddChild(MakeButton("Controls", () => OpenFrom(MenuScreen.Title, MenuScreen.Controls)));
        col.AddChild(MakeButton("Settings", () => OpenFrom(MenuScreen.Title, MenuScreen.Settings)));
        col.AddChild(MakeButton("Quit", () => QuitRequested?.Invoke()));
        return screen;
    }

    private Control BuildPause()
    {
        var (screen, col) = Panel(420, 360);
        col.AddChild(Heading("PAUSED"));
        col.AddChild(MakeButton("Resume", () => { Close(); ResumeRequested?.Invoke(); }));
        col.AddChild(MakeButton("Respawn", () => { Close(); RespawnRequested?.Invoke(); }));
        col.AddChild(MakeButton("Controls", () => OpenFrom(MenuScreen.Pause, MenuScreen.Controls)));
        col.AddChild(MakeButton("Settings", () => OpenFrom(MenuScreen.Pause, MenuScreen.Settings)));
        col.AddChild(MakeButton("Quit", () => QuitRequested?.Invoke()));
        return screen;
    }

    private Control BuildControls()
    {
        var (screen, col) = Panel(760, 620);
        col.AddChild(Heading("CONTROLS"));
        col.AddChild(new Label { Text = "Click a binding, then press the new key, button or stick direction. Esc cancels.", HorizontalAlignment = HorizontalAlignment.Center });

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 400), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        col.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        var header = new HBoxContainer();
        header.AddChild(new Label { Text = "Action", CustomMinimumSize = new Vector2(220, 0) });
        header.AddChild(new Label { Text = "Keyboard / mouse", CustomMinimumSize = new Vector2(230, 0) });
        header.AddChild(new Label { Text = "Gamepad" });
        list.AddChild(header);

        foreach (var group in Enum.GetValues<ActionGroup>())
        {
            var title = new Label { Text = group.ToString().ToUpperInvariant() };
            title.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
            list.AddChild(title);

            foreach (var action in Enum.GetValues<GameAction>().Where(a => ActionInfo.GroupOf(a) == group))
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(new Label { Text = ActionInfo.Label(action), CustomMinimumSize = new Vector2(220, 0) });

                foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
                {
                    var button = MakeButton("", () => BeginListening(action, slot), 220);
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
        footer.AddChild(MakeButton("Reset to defaults", () =>
        {
            _settings.Bindings.ResetToDefaults();
            Changed("Bindings reset to defaults.");
        }));
        footer.AddChild(MakeButton("Back", () => Open(ReturnScreen())));
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
        var (screen, col) = Panel(620, 470);
        col.AddChild(Heading("SETTINGS"));

        Slider(col, "Mouse sensitivity", GameSettings.MouseSensitivityRange.Min, GameSettings.MouseSensitivityRange.Max, 0.0001,
            () => _settings.MouseSensitivity, v => _settings.MouseSensitivity = v, v => $"{v / GameSettings.MouseSensitivityRange.Default:P0}");
        Slider(col, "Gamepad look speed", GameSettings.PadLookSpeedRange.Min, GameSettings.PadLookSpeedRange.Max, 0.1,
            () => _settings.PadLookSpeed, v => _settings.PadLookSpeed = v, v => $"{v:F1} rad/s");
        Slider(col, "Stick dead zone", GameSettings.DeadzoneRange.Min, GameSettings.DeadzoneRange.Max, 0.01,
            () => _settings.Deadzone, v => _settings.Deadzone = v, v => $"{v:P0}");
        Slider(col, "Field of view", GameSettings.FovRange.Min, GameSettings.FovRange.Max, 1,
            () => _settings.FieldOfView, v => _settings.FieldOfView = v, v => $"{v:F0}°");
        Slider(col, "Render distance", GameSettings.ViewRadiusRange.Min, GameSettings.ViewRadiusRange.Max, 1,
            () => _settings.ViewRadius, v => _settings.ViewRadius = (int)v, v => $"{v:F0} chunks");

        CheckBox(col, "Invert mouse Y", () => _settings.InvertMouseY, v => _settings.InvertMouseY = v);
        CheckBox(col, "Invert gamepad Y", () => _settings.InvertPadY, v => _settings.InvertPadY = v);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddThemeConstantOverride("separation", 12);
        footer.AddChild(MakeButton("Reset", () =>
        {
            var keep = _settings.Bindings.Clone();
            _settings.ResetToDefaults();
            _settings.Bindings.ResetToDefaults();
            foreach (var a in Enum.GetValues<GameAction>())
                foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
                    if (keep.Get(a, slot) is { } b) _settings.Bindings.Rebind(a, b);
            SettingsChanged?.Invoke();
            Open(MenuScreen.Settings);
        }));
        footer.AddChild(MakeButton("Back", () => Open(ReturnScreen())));
        col.AddChild(footer);
        return screen;
    }

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
        _bindingButtons[(action, slot)].Text = slot == Slot.Gamepad ? "press a button or move a stick…" : "press a key or click…";
        _message.Text = $"Binding \"{ActionInfo.Label(action)}\". Esc cancels.";
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
            RebindOutcome.Ok => $"{ActionInfo.Label(action)} is now {BindingNames.Display(binding)}.",
            RebindOutcome.Swapped => $"{BindingNames.Display(binding)} was used by {ActionInfo.Label(conflict ?? action)}; the two swapped.",
            RebindOutcome.Reserved => $"{BindingNames.Display(binding)} cannot be used for {ActionInfo.Label(action)}.",
            RebindOutcome.WrongKind => $"{ActionInfo.Label(action)} cannot use {BindingNames.Display(binding)}.",
            _ => "That binding is already set.",
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
        foreach (var ((action, slot), button) in _bindingButtons)
            button.Text = slot == Slot.KeyboardMouse && ActionInfo.AnalogOnlyOnGamepad(action)
                ? "mouse"
                : BindingNames.Display(_settings.Bindings.Get(action, slot));
    }
}
