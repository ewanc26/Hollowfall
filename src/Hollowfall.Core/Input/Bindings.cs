namespace Hollowfall.Core.Input;

/// <summary>Everything the player can do, independent of which button does it.</summary>
public enum GameAction
{
    MoveForward,
    MoveBack,
    MoveLeft,
    MoveRight,
    LookUp,
    LookDown,
    LookLeft,
    LookRight,
    Jump,
    FirePrimary,
    FireSecondary,
    Parry,
    PortalBlue,
    PortalOrange,
    Respawn,
    FlyToggle,
    Pause,
}

public enum ActionGroup
{
    Movement,
    Look,
    Combat,
    Portals,
    System,
}

public enum InputDevice
{
    Keyboard,
    Mouse,
    GamepadButton,
    GamepadAxis,
}

/// <summary>
/// One physical input. <see cref="Code"/> is the engine's own name for it (Godot's Key, MouseButton, JoyButton or
/// JoyAxis enum names), so the rest of the code never needs a table. For an axis, <see cref="AxisSign"/> says which
/// direction counts as pressing (-1 or +1).
/// </summary>
public readonly record struct Binding(InputDevice Device, string Code, int AxisSign = 0)
{
    public bool IsGamepad => Device is InputDevice.GamepadButton or InputDevice.GamepadAxis;

    public static Binding Key(string code) => new(InputDevice.Keyboard, code);

    public static Binding Mouse(string code) => new(InputDevice.Mouse, code);

    public static Binding Button(string code) => new(InputDevice.GamepadButton, code);

    public static Binding Axis(string code, int sign) => new(InputDevice.GamepadAxis, code, sign);
}

/// <summary>Which of an action's two slots a binding belongs to: keyboard and mouse, or gamepad.</summary>
public enum Slot
{
    KeyboardMouse,
    Gamepad,
}

public static class ActionInfo
{
    public static ActionGroup GroupOf(GameAction a) => a switch
    {
        GameAction.MoveForward or GameAction.MoveBack or GameAction.MoveLeft or GameAction.MoveRight or GameAction.Jump => ActionGroup.Movement,
        GameAction.LookUp or GameAction.LookDown or GameAction.LookLeft or GameAction.LookRight => ActionGroup.Look,
        GameAction.FirePrimary or GameAction.FireSecondary or GameAction.Parry => ActionGroup.Combat,
        GameAction.PortalBlue or GameAction.PortalOrange => ActionGroup.Portals,
        _ => ActionGroup.System,
    };

    public static string Label(GameAction a) => a switch
    {
        GameAction.MoveForward => "Move forward",
        GameAction.MoveBack => "Move back",
        GameAction.MoveLeft => "Move left",
        GameAction.MoveRight => "Move right",
        GameAction.LookUp => "Look up",
        GameAction.LookDown => "Look down",
        GameAction.LookLeft => "Look left",
        GameAction.LookRight => "Look right",
        GameAction.Jump => "Jump",
        GameAction.FirePrimary => "Fire revolver",
        GameAction.FireSecondary => "Fire shotgun",
        GameAction.Parry => "Parry",
        GameAction.PortalBlue => "Blue portal",
        GameAction.PortalOrange => "Orange portal",
        GameAction.Respawn => "Respawn",
        GameAction.FlyToggle => "Free-fly",
        _ => "Pause menu",
    };

    /// <summary>Actions that cannot be left unbound, or the game could become impossible to use or leave.</summary>
    public static bool Required(GameAction a) => a is GameAction.Pause or GameAction.Jump or GameAction.FirePrimary or
        GameAction.MoveForward or GameAction.MoveBack or GameAction.MoveLeft or GameAction.MoveRight;

    /// <summary>Look is analog: only stick axes make sense, and the keyboard binding is a mouse movement, not a key.</summary>
    public static bool AnalogOnlyOnGamepad(GameAction a) => GroupOf(a) == ActionGroup.Look;
}

public enum RebindOutcome
{
    Ok,
    Swapped,
    Reserved,
    WrongKind,
    Unchanged,
}

/// <summary>
/// The player's bindings: for each action, one keyboard-or-mouse input and one gamepad input. Pure data with rules
/// (no two actions share an input in the same slot, Escape stays on the pause menu, required actions stay bound),
/// so it can be tested without an engine and saved to disk.
/// </summary>
public sealed class BindingMap
{
    private readonly Dictionary<GameAction, Binding?[]> _slots = [];

    public static readonly Binding PauseKey = Binding.Key("Escape");

    public BindingMap()
    {
        foreach (var a in Enum.GetValues<GameAction>()) _slots[a] = new Binding?[2];
    }

    public Binding? Get(GameAction action, Slot slot) => _slots[action][(int)slot];

    public IEnumerable<Binding> All(GameAction action) => _slots[action].Where(b => b != null).Select(b => b!.Value);

    public BindingMap Clone()
    {
        var copy = new BindingMap();
        foreach (var (a, slots) in _slots) copy._slots[a] = [.. slots];
        return copy;
    }

    public static Slot SlotOf(Binding b) => b.IsGamepad ? Slot.Gamepad : Slot.KeyboardMouse;

    /// <summary>The action already using this exact input in the same slot, if any.</summary>
    public GameAction? FindConflict(Binding b, GameAction except)
    {
        var slot = (int)SlotOf(b);
        foreach (var (a, slots) in _slots)
            if (a != except && slots[slot] == b) return a;
        return null;
    }

    /// <summary>
    /// Binds an input to an action in the slot it belongs to. If another action already uses it, the two swap, so
    /// nothing is ever left silently unbound. Escape is reserved for the pause menu.
    /// </summary>
    public RebindOutcome Rebind(GameAction action, Binding b)
    {
        if (b == PauseKey && action != GameAction.Pause) return RebindOutcome.Reserved;
        if (!IsValid(action, b)) return RebindOutcome.WrongKind;

        int slot = (int)SlotOf(b);
        if (_slots[action][slot] == b) return RebindOutcome.Unchanged;

        var previous = _slots[action][slot];
        var outcome = RebindOutcome.Ok;
        if (FindConflict(b, action) is { } other)
        {
            // The other action takes over what this one had, or loses the binding if this one had none.
            if (previous != null && IsValid(other, previous.Value)) _slots[other][slot] = previous;
            else if (ActionInfo.Required(other) && previous == null) return RebindOutcome.Reserved;
            else _slots[other][slot] = null;
            outcome = RebindOutcome.Swapped;
        }
        _slots[action][slot] = b;
        return outcome;
    }

    /// <summary>Clears one slot. Required actions keep their binding, and so does the pause key.</summary>
    public bool Unbind(GameAction action, Slot slot)
    {
        if (_slots[action][(int)slot] == null) return false;
        if (action == GameAction.Pause) return false;
        if (ActionInfo.Required(action) && !(Get(action, slot == Slot.Gamepad ? Slot.KeyboardMouse : Slot.Gamepad) != null)) return false;
        _slots[action][(int)slot] = null;
        return true;
    }

    /// <summary>Whether an input can drive an action: look actions need a mouse (keyboard slot) or stick axis.</summary>
    public static bool IsValid(GameAction action, Binding b)
    {
        if (string.IsNullOrWhiteSpace(b.Code)) return false;
        if (b.Device == InputDevice.GamepadAxis && b.AxisSign is not (-1 or 1)) return false;
        if (b.Device != InputDevice.GamepadAxis && b.AxisSign != 0) return false;
        if (ActionInfo.AnalogOnlyOnGamepad(action))
            return b.Device == InputDevice.GamepadAxis && b.Code is "RightX" or "RightY" or "LeftX" or "LeftY";
        return true;
    }

    public void ResetToDefaults()
    {
        foreach (var a in Enum.GetValues<GameAction>()) _slots[a] = [Defaults.Get(a, Slot.KeyboardMouse), Defaults.Get(a, Slot.Gamepad)];
    }

    public static BindingMap CreateDefault()
    {
        var m = new BindingMap();
        m.ResetToDefaults();
        return m;
    }

    internal void SetRaw(GameAction action, Slot slot, Binding? b) => _slots[action][(int)slot] = b;

    public bool EqualsMap(BindingMap other) =>
        Enum.GetValues<GameAction>().All(a => Get(a, Slot.KeyboardMouse) == other.Get(a, Slot.KeyboardMouse) && Get(a, Slot.Gamepad) == other.Get(a, Slot.Gamepad));

    /// <summary>The shipped bindings. Look has no keyboard slot: the mouse moves the view.</summary>
    public static class Defaults
    {
        public static Binding? Get(GameAction a, Slot slot) => slot == Slot.KeyboardMouse ? Keyboard(a) : Pad(a);

        private static Binding? Keyboard(GameAction a) => a switch
        {
            GameAction.MoveForward => Binding.Key("W"),
            GameAction.MoveBack => Binding.Key("S"),
            GameAction.MoveLeft => Binding.Key("A"),
            GameAction.MoveRight => Binding.Key("D"),
            GameAction.Jump => Binding.Key("Space"),
            GameAction.FirePrimary => Binding.Mouse("Left"),
            GameAction.FireSecondary => Binding.Mouse("Right"),
            GameAction.Parry => Binding.Key("V"),
            GameAction.PortalBlue => Binding.Key("Q"),
            GameAction.PortalOrange => Binding.Key("E"),
            GameAction.Respawn => Binding.Key("R"),
            GameAction.FlyToggle => Binding.Key("F"),
            GameAction.Pause => PauseKey,
            _ => null,
        };

        private static Binding? Pad(GameAction a) => a switch
        {
            GameAction.MoveForward => Binding.Axis("LeftY", -1),
            GameAction.MoveBack => Binding.Axis("LeftY", 1),
            GameAction.MoveLeft => Binding.Axis("LeftX", -1),
            GameAction.MoveRight => Binding.Axis("LeftX", 1),
            GameAction.LookUp => Binding.Axis("RightY", -1),
            GameAction.LookDown => Binding.Axis("RightY", 1),
            GameAction.LookLeft => Binding.Axis("RightX", -1),
            GameAction.LookRight => Binding.Axis("RightX", 1),
            GameAction.Jump => Binding.Button("A"),
            GameAction.FirePrimary => Binding.Axis("TriggerRight", 1),
            GameAction.FireSecondary => Binding.Axis("TriggerLeft", 1),
            GameAction.Parry => Binding.Button("X"),
            GameAction.PortalBlue => Binding.Button("LeftShoulder"),
            GameAction.PortalOrange => Binding.Button("RightShoulder"),
            GameAction.Respawn => Binding.Button("Y"),
            GameAction.FlyToggle => Binding.Button("RightStick"),
            GameAction.Pause => Binding.Button("Start"),
            _ => null,
        };
    }
}

/// <summary>Short names for showing bindings on screen and in the HUD hints.</summary>
public static class BindingNames
{
    public static string Display(Binding? b)
    {
        if (b is not { } v) return "unbound";
        return v.Device switch
        {
            InputDevice.Keyboard => Key(v.Code),
            InputDevice.Mouse => v.Code switch
            {
                "Left" => "Left click",
                "Right" => "Right click",
                "Middle" => "Middle click",
                "WheelUp" => "Wheel up",
                "WheelDown" => "Wheel down",
                _ => "Mouse " + v.Code,
            },
            InputDevice.GamepadButton => Button(v.Code),
            _ => Axis(v.Code, v.AxisSign),
        };
    }

    private static string Key(string code) => code switch
    {
        "Space" => "Space",
        "Escape" => "Esc",
        "Shift" => "Shift",
        "Ctrl" => "Ctrl",
        "Tab" => "Tab",
        "Enter" => "Enter",
        "Up" => "Up arrow",
        "Down" => "Down arrow",
        "Left" => "Left arrow",
        "Right" => "Right arrow",
        _ => code,
    };

    private static string Button(string code) => code switch
    {
        "LeftShoulder" => "LB",
        "RightShoulder" => "RB",
        "LeftStick" => "L3",
        "RightStick" => "R3",
        "DpadUp" => "D-pad up",
        "DpadDown" => "D-pad down",
        "DpadLeft" => "D-pad left",
        "DpadRight" => "D-pad right",
        "Start" => "Start",
        "Back" => "Back",
        _ => code,
    };

    private static string Axis(string code, int sign) => code switch
    {
        "TriggerLeft" => "LT",
        "TriggerRight" => "RT",
        "LeftX" => sign < 0 ? "Left stick left" : "Left stick right",
        "LeftY" => sign < 0 ? "Left stick up" : "Left stick down",
        "RightX" => sign < 0 ? "Right stick left" : "Right stick right",
        "RightY" => sign < 0 ? "Right stick up" : "Right stick down",
        _ => code + (sign < 0 ? "-" : "+"),
    };
}
