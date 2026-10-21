using System.Text.Json;
using Hollowfall.Core.Terrain;
using System.Text.Json.Nodes;

namespace Hollowfall.Core.Input;

/// <summary>Player-adjustable options. Setting a value clamps it to its range, so a bad file can never break the game.</summary>
public sealed class GameSettings
{
    public const int CurrentVersion = 1;

    public static readonly (double Min, double Max, double Default) MouseSensitivityRange = (0.0005, 0.01, 0.0025);
    public static readonly (double Min, double Max, double Default) PadLookSpeedRange = (1.0, 8.0, 3.2);
    public static readonly (double Min, double Max, double Default) DeadzoneRange = (0.05, 0.5, 0.2);
    public static readonly (double Min, double Max, double Default) FovRange = (60, 110, 80);
    public static readonly (double Min, double Max, double Default) VolumeRange = (0, 1, 0.8);
    public static readonly (double Min, double Max, double Default) AmbienceRange = (0, 1, 0.5);
    public static readonly (double Min, double Max, double Default) MusicRange = (0, 1, 0.6);
    public static readonly (int Min, int Max, int Default) ViewRadiusRange = (3, 12, 6);

    private double _mouseSensitivity = MouseSensitivityRange.Default;
    private double _padLookSpeed = PadLookSpeedRange.Default;
    private double _deadzone = DeadzoneRange.Default;
    private double _fov = FovRange.Default;
    private int _viewRadius = ViewRadiusRange.Default;
    private double _uiScale = Ui.UiScaling.UserRange.Default;
    private string _seed = SeedWords.Default;
    private double _master = VolumeRange.Default;
    private double _effects = 1.0;
    private double _ambience = AmbienceRange.Default;
    private double _music = MusicRange.Default;

    /// <summary>Radians of turn per pixel of mouse movement.</summary>
    public double MouseSensitivity
    {
        get => _mouseSensitivity;
        set => _mouseSensitivity = Clamp(value, MouseSensitivityRange.Min, MouseSensitivityRange.Max, MouseSensitivityRange.Default);
    }

    /// <summary>Radians per second at full stick deflection.</summary>
    public double PadLookSpeed
    {
        get => _padLookSpeed;
        set => _padLookSpeed = Clamp(value, PadLookSpeedRange.Min, PadLookSpeedRange.Max, PadLookSpeedRange.Default);
    }

    public double Deadzone
    {
        get => _deadzone;
        set => _deadzone = Clamp(value, DeadzoneRange.Min, DeadzoneRange.Max, DeadzoneRange.Default);
    }

    public double FieldOfView
    {
        get => _fov;
        set => _fov = Clamp(value, FovRange.Min, FovRange.Max, FovRange.Default);
    }

    public int ViewRadius
    {
        get => _viewRadius;
        set => _viewRadius = (int)Clamp(value, ViewRadiusRange.Min, ViewRadiusRange.Max, ViewRadiusRange.Default);
    }

    /// <summary>Your own size multiplier for the interface, on top of the automatic one (see <see cref="Ui.UiScaling"/>).</summary>
    public double UiScale
    {
        get => _uiScale;
        set => _uiScale = Clamp(value, Ui.UiScaling.UserRange.Min, Ui.UiScaling.UserRange.Max, Ui.UiScaling.UserRange.Default);
    }

    /// <summary>Whether the interface also grows and shrinks with the window. Off pins it to your multiplier alone.</summary>
    public bool AutoUiScale { get; set; } = true;

    /// <summary>The world to play, as typed: any text, or a number. Cleaned on the way in so it is never empty.</summary>
    public string Seed
    {
        get => _seed;
        set => _seed = SeedWords.Clean(value);
    }

    /// <summary>Overall loudness, 0 (silent) to 1.</summary>
    public double MasterVolume
    {
        get => _master;
        set => _master = Clamp(value, VolumeRange.Min, VolumeRange.Max, VolumeRange.Default);
    }

    /// <summary>Gunfire, hits, pickups and the rest of the sound effects, relative to the master volume.</summary>
    public double EffectsVolume
    {
        get => _effects;
        set => _effects = Clamp(value, VolumeRange.Min, VolumeRange.Max, 1.0);
    }

    /// <summary>The low dungeon drone, relative to the master volume.</summary>
    public double AmbienceVolume
    {
        get => _ambience;
        set => _ambience = Clamp(value, AmbienceRange.Min, AmbienceRange.Max, AmbienceRange.Default);
    }

    /// <summary>The adaptive music, relative to the master volume. 0 turns it off.</summary>
    public double MusicVolume
    {
        get => _music;
        set => _music = Clamp(value, MusicRange.Min, MusicRange.Max, MusicRange.Default);
    }

    /// <summary>Which button labels to show for a gamepad. Auto follows the connected controller.</summary>
    public PromptStyle ControllerPrompts { get; set; } = PromptStyle.Auto;

    public bool InvertMouseY { get; set; }
    public bool InvertPadY { get; set; }

    public BindingMap Bindings { get; private set; } = BindingMap.CreateDefault();

    private static double Clamp(double v, double min, double max, double fallback) =>
        double.IsNaN(v) || double.IsInfinity(v) ? fallback : Math.Clamp(v, min, max);

    public void ResetToDefaults()
    {
        MouseSensitivity = MouseSensitivityRange.Default;
        PadLookSpeed = PadLookSpeedRange.Default;
        Deadzone = DeadzoneRange.Default;
        FieldOfView = FovRange.Default;
        ViewRadius = ViewRadiusRange.Default;
        UiScale = Ui.UiScaling.UserRange.Default;
        AutoUiScale = true;
        Seed = SeedWords.Default;
        MasterVolume = VolumeRange.Default;
        EffectsVolume = 1.0;
        AmbienceVolume = AmbienceRange.Default;
        MusicVolume = MusicRange.Default;
        InvertMouseY = false;
        InvertPadY = false;
        ControllerPrompts = PromptStyle.Auto;
        Bindings = BindingMap.CreateDefault();
    }

    // ---- saving ---------------------------------------------------------------------------

    public string ToJson()
    {
        var bindings = new JsonObject();
        foreach (var a in Enum.GetValues<GameAction>())
        {
            var entry = new JsonObject();
            foreach (var (slot, name) in new[] { (Slot.KeyboardMouse, "keyboardMouse"), (Slot.Gamepad, "gamepad") })
                entry[name] = Bindings.Get(a, slot) is { } b
                    ? new JsonObject { ["device"] = b.Device.ToString(), ["code"] = b.Code, ["axisSign"] = b.AxisSign }
                    : null;
            bindings[a.ToString()] = entry;
        }

        var root = new JsonObject
        {
            ["version"] = CurrentVersion,
            ["mouseSensitivity"] = MouseSensitivity,
            ["padLookSpeed"] = PadLookSpeed,
            ["deadzone"] = Deadzone,
            ["fieldOfView"] = FieldOfView,
            ["viewRadius"] = ViewRadius,
            ["uiScale"] = UiScale,
            ["autoUiScale"] = AutoUiScale,
            ["seed"] = Seed,
            ["masterVolume"] = MasterVolume,
            ["effectsVolume"] = EffectsVolume,
            ["ambienceVolume"] = AmbienceVolume,
            ["musicVolume"] = MusicVolume,
            ["invertMouseY"] = InvertMouseY,
            ["invertPadY"] = InvertPadY,
            ["controllerPrompts"] = ControllerPrompts.ToString(),
            ["bindings"] = bindings,
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads settings from JSON, never throwing: anything missing, out of range or malformed falls back to the default,
    /// so a damaged or hand-edited file costs the player only the bad entries.
    /// </summary>
    public static GameSettings FromJson(string? json)
    {
        var s = new GameSettings();
        if (string.IsNullOrWhiteSpace(json)) return s;

        JsonObject? root;
        try
        {
            // A repeated property name would otherwise throw later, when the tree is first read, so refuse it here.
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false }) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or FormatException) { return s; }
        if (root == null) return s;

        s.MouseSensitivity = Number(root, "mouseSensitivity", MouseSensitivityRange.Default);
        s.PadLookSpeed = Number(root, "padLookSpeed", PadLookSpeedRange.Default);
        s.Deadzone = Number(root, "deadzone", DeadzoneRange.Default);
        s.FieldOfView = Number(root, "fieldOfView", FovRange.Default);
        s.ViewRadius = (int)Number(root, "viewRadius", ViewRadiusRange.Default);
        s.Seed = root["seed"] is JsonValue sv && sv.TryGetValue<string>(out var seedText) ? seedText : SeedWords.Default;
        s.UiScale = Number(root, "uiScale", Ui.UiScaling.UserRange.Default);
        s.AutoUiScale = root["autoUiScale"] is not JsonValue au || !au.TryGetValue<bool>(out var autoScale) || autoScale;
        s.MasterVolume = Number(root, "masterVolume", VolumeRange.Default);
        s.EffectsVolume = Number(root, "effectsVolume", 1.0);
        s.AmbienceVolume = Number(root, "ambienceVolume", AmbienceRange.Default);
        s.MusicVolume = Number(root, "musicVolume", MusicRange.Default);
        s.InvertMouseY = Flag(root, "invertMouseY");
        s.InvertPadY = Flag(root, "invertPadY");
        s.ControllerPrompts = Enum.TryParse<PromptStyle>(root["controllerPrompts"] is JsonValue v && v.TryGetValue<string>(out var cp) ? cp : null, out var prompts) && Enum.IsDefined(prompts)
            ? prompts
            : PromptStyle.Auto;

        if (root["bindings"] is JsonObject saved)
            foreach (var a in Enum.GetValues<GameAction>())
            {
                if (saved[a.ToString()] is not JsonObject entry) continue;
                foreach (var (slot, name) in new[] { (Slot.KeyboardMouse, "keyboardMouse"), (Slot.Gamepad, "gamepad") })
                {
                    if (!entry.ContainsKey(name)) continue;
                    if (entry[name] is null) { s.Bindings.SetRaw(a, slot, null); continue; }
                    if (ReadBinding(entry[name] as JsonObject) is { } b && BindingMap.IsValid(a, b) && BindingMap.SlotOf(b) == slot)
                        s.Bindings.SetRaw(a, slot, b);
                }
            }

        s.RepairBindings();
        return s;
    }

    private static double Number(JsonObject o, string key, double fallback)
    {
        try { return o[key] is JsonValue v ? v.GetValue<double>() : fallback; }
        catch (Exception e) when (e is InvalidOperationException or FormatException) { return fallback; }
    }

    private static bool Flag(JsonObject o, string key)
    {
        try { return o[key] is JsonValue v && v.GetValue<bool>(); }
        catch (Exception e) when (e is InvalidOperationException or FormatException) { return false; }
    }

    private static Binding? ReadBinding(JsonObject? o)
    {
        if (o == null) return null;
        try
        {
            if (!Enum.TryParse<InputDevice>(o["device"]?.GetValue<string>(), out var device) || !Enum.IsDefined(device)) return null;
            string code = o["code"]?.GetValue<string>() ?? "";
            int sign = o["axisSign"]?.GetValue<int>() ?? 0;
            return new Binding(device, code, sign);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException) { return null; }
    }

    /// <summary>
    /// After loading, makes the map safe: Escape stays on pause (and nowhere else), duplicates within a slot are dropped
    /// in favour of the first action, and required actions that ended up unbound get their default back.
    /// </summary>
    internal void RepairBindings()
    {
        var seen = new Dictionary<(Slot, Binding), GameAction>();
        foreach (var a in Enum.GetValues<GameAction>())
            foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
            {
                if (Bindings.Get(a, slot) is not { } b) continue;
                if ((b == BindingMap.PauseKey && a != GameAction.Pause) || seen.ContainsKey((slot, b)))
                {
                    Bindings.SetRaw(a, slot, null);
                    continue;
                }
                seen[(slot, b)] = a;
            }

        foreach (var a in Enum.GetValues<GameAction>().Where(ActionInfo.Required))
            if (Bindings.Get(a, Slot.KeyboardMouse) == null && Bindings.Get(a, Slot.Gamepad) == null)
                foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
                    if (BindingMap.Defaults.Get(a, slot) is { } d && !seen.ContainsKey((slot, d)))
                    {
                        Bindings.SetRaw(a, slot, d);
                        seen[(slot, d)] = a;
                    }

        if (Bindings.Get(GameAction.Pause, Slot.KeyboardMouse) == null) Bindings.SetRaw(GameAction.Pause, Slot.KeyboardMouse, BindingMap.PauseKey);
    }

    // ---- files ----------------------------------------------------------------------------

    /// <summary>Writes the settings atomically (temp file, then rename), so a crash never leaves half a file.</summary>
    public bool TrySave(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson());
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static GameSettings Load(string path)
    {
        try { return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new GameSettings(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new GameSettings(); }
    }
}
