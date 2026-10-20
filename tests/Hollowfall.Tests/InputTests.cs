using Hollowfall.Core.Input;

namespace Hollowfall.Tests;

public class BindingMapTests
{
    [Fact]
    public void EveryActionHasASensibleDefault()
    {
        var m = BindingMap.CreateDefault();
        foreach (var a in Enum.GetValues<GameAction>())
        {
            bool any = m.All(a).Any();
            Assert.True(any, $"{a} has no default binding");
            foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
                if (m.Get(a, slot) is { } b)
                {
                    Assert.True(BindingMap.IsValid(a, b), $"{a} default {b} is not valid");
                    Assert.Equal(slot, BindingMap.SlotOf(b));
                }
        }
        Assert.Equal(BindingMap.PauseKey, m.Get(GameAction.Pause, Slot.KeyboardMouse));
        Assert.Equal(Binding.Mouse("Left"), m.Get(GameAction.FirePrimary, Slot.KeyboardMouse));
        Assert.Equal(Binding.Axis("TriggerRight", 1), m.Get(GameAction.FirePrimary, Slot.Gamepad));
    }

    [Fact]
    public void DefaultsNeverShareAnInputWithinASlot()
    {
        var m = BindingMap.CreateDefault();
        foreach (var slot in new[] { Slot.KeyboardMouse, Slot.Gamepad })
        {
            var used = Enum.GetValues<GameAction>().Select(a => m.Get(a, slot)).Where(b => b != null).Select(b => b!.Value).ToList();
            Assert.Equal(used.Count, used.Distinct().Count());
        }
    }

    [Fact]
    public void LookIsOnlyOnTheGamepadSticks()
    {
        var m = BindingMap.CreateDefault();
        Assert.Null(m.Get(GameAction.LookLeft, Slot.KeyboardMouse));
        Assert.Equal(RebindOutcome.WrongKind, m.Rebind(GameAction.LookLeft, Binding.Key("J")));
        Assert.Equal(RebindOutcome.WrongKind, m.Rebind(GameAction.LookLeft, Binding.Button("A")));
        Assert.Equal(RebindOutcome.Unchanged, m.Rebind(GameAction.LookLeft, Binding.Axis("RightX", -1)));
    }

    [Fact]
    public void RebindingReplacesTheBindingInItsOwnSlotOnly()
    {
        var m = BindingMap.CreateDefault();
        Assert.Equal(RebindOutcome.Ok, m.Rebind(GameAction.Parry, Binding.Key("G")));
        Assert.Equal(Binding.Key("G"), m.Get(GameAction.Parry, Slot.KeyboardMouse));
        Assert.Equal(Binding.Button("X"), m.Get(GameAction.Parry, Slot.Gamepad)); // untouched

        Assert.Equal(RebindOutcome.Ok, m.Rebind(GameAction.Parry, Binding.Button("Back")));
        Assert.Equal(Binding.Key("G"), m.Get(GameAction.Parry, Slot.KeyboardMouse));
        Assert.Equal(Binding.Button("Back"), m.Get(GameAction.Parry, Slot.Gamepad));
    }

    [Fact]
    public void BindingSomethingAlreadyInUseSwapsTheTwoActions()
    {
        var m = BindingMap.CreateDefault();
        // Parry is V, portal teal is Q. Put parry on Q: teal takes V.
        Assert.Equal(RebindOutcome.Swapped, m.Rebind(GameAction.Parry, Binding.Key("Q")));
        Assert.Equal(Binding.Key("Q"), m.Get(GameAction.Parry, Slot.KeyboardMouse));
        Assert.Equal(Binding.Key("V"), m.Get(GameAction.PortalTeal, Slot.KeyboardMouse));
        Assert.Null(m.FindConflict(Binding.Key("Q"), GameAction.Parry));
    }

    [Fact]
    public void NoTwoActionsEverEndUpOnOneInput()
    {
        var m = BindingMap.CreateDefault();
        var keys = new[] { "W", "Q", "V", "R", "F", "Space", "E", "G", "H" };
        var rng = new Random(7);
        var actions = Enum.GetValues<GameAction>().Where(a => ActionInfo.GroupOf(a) != ActionGroup.Look).ToArray();
        for (int i = 0; i < 400; i++)
            m.Rebind(actions[rng.Next(actions.Length)], Binding.Key(keys[rng.Next(keys.Length)]));
        var used = actions.Select(a => m.Get(a, Slot.KeyboardMouse)).Where(b => b != null).Select(b => b!.Value).ToList();
        Assert.Equal(used.Count, used.Distinct().Count());
        Assert.NotNull(m.Get(GameAction.Pause, Slot.KeyboardMouse));
    }

    [Fact]
    public void EscapeIsReservedForThePauseMenu()
    {
        var m = BindingMap.CreateDefault();
        Assert.Equal(RebindOutcome.Reserved, m.Rebind(GameAction.Jump, Binding.Key("Escape")));
        Assert.Equal(Binding.Key("Space"), m.Get(GameAction.Jump, Slot.KeyboardMouse));
        Assert.False(m.Unbind(GameAction.Pause, Slot.KeyboardMouse));
        Assert.False(m.Unbind(GameAction.Pause, Slot.Gamepad));
    }

    [Fact]
    public void RequiredActionsCannotBeLeftWithoutAnyBinding()
    {
        var m = BindingMap.CreateDefault();
        Assert.True(m.Unbind(GameAction.Jump, Slot.Gamepad)); // the keyboard binding remains
        Assert.False(m.Unbind(GameAction.Jump, Slot.KeyboardMouse)); // that would leave nothing
        Assert.True(m.Unbind(GameAction.PortalTeal, Slot.KeyboardMouse)); // not required
    }

    [Fact]
    public void AxisBindingsNeedADirectionAndOthersMustNotHaveOne()
    {
        Assert.False(BindingMap.IsValid(GameAction.Jump, new Binding(InputDevice.GamepadAxis, "TriggerRight", 0)));
        Assert.False(BindingMap.IsValid(GameAction.Jump, new Binding(InputDevice.Keyboard, "W", 1)));
        Assert.False(BindingMap.IsValid(GameAction.Jump, new Binding(InputDevice.Keyboard, "", 0)));
        Assert.True(BindingMap.IsValid(GameAction.Jump, Binding.Axis("TriggerLeft", 1)));
    }

    [Fact]
    public void ResetAndCloneBehave()
    {
        var m = BindingMap.CreateDefault();
        var copy = m.Clone();
        m.Rebind(GameAction.Parry, Binding.Key("G"));
        Assert.False(m.EqualsMap(copy));
        Assert.Equal(Binding.Key("V"), copy.Get(GameAction.Parry, Slot.KeyboardMouse));
        m.ResetToDefaults();
        Assert.True(m.EqualsMap(copy));
    }

    [Theory]
    [InlineData("Key", "W", 0, "W")]
    [InlineData("Key", "Escape", 0, "Esc")]
    [InlineData("Mouse", "Left", 0, "Left click")]
    [InlineData("Btn", "LeftShoulder", 0, "LB")]
    [InlineData("Btn", "A", 0, "A")]
    [InlineData("Axis", "TriggerRight", 1, "RT")]
    [InlineData("Axis", "LeftX", -1, "Left stick left")]
    [InlineData("Axis", "RightY", 1, "Right stick down")]
    public void BindingsHaveReadableNames(string kind, string code, int sign, string expected)
    {
        Binding b = kind switch
        {
            "Key" => Binding.Key(code),
            "Mouse" => Binding.Mouse(code),
            "Btn" => Binding.Button(code),
            _ => Binding.Axis(code, sign),
        };
        Assert.Equal(expected, BindingNames.Display(b));
        Assert.Equal("unbound", BindingNames.Display(null));
    }

    [Fact]
    public void EveryActionHasALabelAndAGroup()
    {
        foreach (var a in Enum.GetValues<GameAction>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ActionInfo.Label(a)));
            Assert.True(Enum.IsDefined(ActionInfo.GroupOf(a)));
        }
    }
}

public class GameSettingsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hf-settings").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void DefaultsAreInsideTheirRanges()
    {
        var s = new GameSettings();
        Assert.InRange(s.MouseSensitivity, GameSettings.MouseSensitivityRange.Min, GameSettings.MouseSensitivityRange.Max);
        Assert.Equal(80, s.FieldOfView);
        Assert.Equal(6, s.ViewRadius);
        Assert.False(s.InvertMouseY);
    }

    [Fact]
    public void ValuesAreClampedAndGarbageFallsBackToDefaults()
    {
        var s = new GameSettings { FieldOfView = 500, ViewRadius = 99, MouseSensitivity = -1, Deadzone = 0, PadLookSpeed = double.NaN };
        Assert.Equal(GameSettings.FovRange.Max, s.FieldOfView);
        Assert.Equal(GameSettings.ViewRadiusRange.Max, s.ViewRadius);
        Assert.Equal(GameSettings.MouseSensitivityRange.Min, s.MouseSensitivity);
        Assert.Equal(GameSettings.DeadzoneRange.Min, s.Deadzone);
        Assert.Equal(GameSettings.PadLookSpeedRange.Default, s.PadLookSpeed);
    }

    [Fact]
    public void SettingsRoundTripThroughJson()
    {
        var s = new GameSettings { MouseSensitivity = 0.004, FieldOfView = 95, ViewRadius = 9, InvertMouseY = true, InvertPadY = true, Deadzone = 0.3, PadLookSpeed = 5 };
        s.Bindings.Rebind(GameAction.Parry, Binding.Key("G"));
        s.Bindings.Rebind(GameAction.PortalViolet, Binding.Button("Back"));
        s.Bindings.Unbind(GameAction.FlyToggle, Slot.KeyboardMouse);

        var back = GameSettings.FromJson(s.ToJson());
        Assert.Equal((s.MouseSensitivity, s.FieldOfView, s.ViewRadius, s.InvertMouseY, s.InvertPadY, s.Deadzone, s.PadLookSpeed),
            (back.MouseSensitivity, back.FieldOfView, back.ViewRadius, back.InvertMouseY, back.InvertPadY, back.Deadzone, back.PadLookSpeed));
        Assert.True(s.Bindings.EqualsMap(back.Bindings));
        Assert.Null(back.Bindings.Get(GameAction.FlyToggle, Slot.KeyboardMouse));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"fieldOfView\":\"wide\",\"viewRadius\":null,\"bindings\":5}")]
    public void DamagedFilesGiveDefaultsInsteadOfExceptions(string? json)
    {
        var s = GameSettings.FromJson(json);
        Assert.Equal(80, s.FieldOfView);
        Assert.True(s.Bindings.EqualsMap(BindingMap.CreateDefault()));
    }

    [Fact]
    public void BadEntriesAreIgnoredAndGoodOnesKept()
    {
        string json = """
        {"fieldOfView": 100, "mouseSensitivity": 99,
         "bindings": {
           "Parry": {"keyboardMouse": {"device":"Keyboard","code":"G","axisSign":0}},
           "Jump": {"keyboardMouse": {"device":"Keyboard","code":"","axisSign":0}},
           "PortalTeal": {"keyboardMouse": {"device":"Warp","code":"Q"}},
           "Nonsense": {"keyboardMouse": {"device":"Keyboard","code":"X"}}
         }}
        """;
        var s = GameSettings.FromJson(json);
        Assert.Equal(100, s.FieldOfView);
        Assert.Equal(GameSettings.MouseSensitivityRange.Max, s.MouseSensitivity);
        Assert.Equal(Binding.Key("G"), s.Bindings.Get(GameAction.Parry, Slot.KeyboardMouse));
        Assert.Equal(Binding.Key("Space"), s.Bindings.Get(GameAction.Jump, Slot.KeyboardMouse)); // blank code rejected
        Assert.Equal(Binding.Key("Q"), s.Bindings.Get(GameAction.PortalTeal, Slot.KeyboardMouse)); // unknown device rejected
    }

    [Fact]
    public void LoadedBindingsAreRepairedSoTheGameStaysUsable()
    {
        // Two actions on W, Escape on jump, and pause cleared: the repair must fix all three.
        string json = """
        {"bindings": {
           "MoveForward": {"keyboardMouse": {"device":"Keyboard","code":"W"}},
           "MoveBack": {"keyboardMouse": {"device":"Keyboard","code":"W"}},
           "Jump": {"keyboardMouse": {"device":"Keyboard","code":"Escape"}},
           "Pause": {"keyboardMouse": null}
        }}
        """;
        var s = GameSettings.FromJson(json);
        var m = s.Bindings;
        Assert.Equal(Binding.Key("W"), m.Get(GameAction.MoveForward, Slot.KeyboardMouse));
        Assert.NotEqual(Binding.Key("W"), m.Get(GameAction.MoveBack, Slot.KeyboardMouse));
        Assert.NotEqual(BindingMap.PauseKey, m.Get(GameAction.Jump, Slot.KeyboardMouse));
        Assert.Equal(BindingMap.PauseKey, m.Get(GameAction.Pause, Slot.KeyboardMouse));
        foreach (var a in Enum.GetValues<GameAction>().Where(ActionInfo.Required))
            Assert.True(m.All(a).Any(), $"{a} lost every binding");
    }

    [Fact]
    public void SavingIsAtomicAndLoadingMissingFilesIsFine()
    {
        string path = Path.Combine(_dir, "nested", "settings.json");
        Assert.Equal(80, GameSettings.Load(path).FieldOfView);

        var s = new GameSettings { FieldOfView = 90 };
        Assert.True(s.TrySave(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(90, GameSettings.Load(path).FieldOfView);

        File.WriteAllText(path, "{ broken");
        Assert.Equal(80, GameSettings.Load(path).FieldOfView);
    }

    [Fact]
    public void ResetRestoresEverything()
    {
        var s = new GameSettings { FieldOfView = 100, InvertMouseY = true };
        s.Bindings.Rebind(GameAction.Parry, Binding.Key("G"));
        s.ResetToDefaults();
        Assert.Equal(80, s.FieldOfView);
        Assert.False(s.InvertMouseY);
        Assert.True(s.Bindings.EqualsMap(BindingMap.CreateDefault()));
    }
}

public class InputMathTests
{
    [Fact]
    public void StickDeadzoneIsRadialAndRescaled()
    {
        Assert.Equal((0.0, 0.0), InputMath.Stick(0.1, 0.1, 0.2));
        var (x, y) = InputMath.Stick(1, 0, 0.2);
        Assert.Equal(1.0, x, 6);
        Assert.Equal(0.0, y, 6);

        var (hx, hy) = InputMath.Stick(0.6, 0, 0.2); // halfway through the live range
        Assert.Equal(0.5, hx, 6);
        Assert.Equal(0, hy, 6);

        // A diagonal is limited to length 1, not 1.41.
        var (dx, dy) = InputMath.Stick(1, 1, 0.2);
        Assert.Equal(1.0, Math.Sqrt(dx * dx + dy * dy), 6);
    }

    [Fact]
    public void NoJumpInOutputRightOutsideTheDeadzone()
    {
        var (x, _) = InputMath.Stick(0.2001, 0, 0.2);
        Assert.True(x < 0.001);
    }

    [Fact]
    public void StickLookTurnsTheRightWayAndScalesWithTimeAndSpeed()
    {
        var (yaw, pitch) = InputMath.StickLook(1, 0, 0.2, 3.0, 0.5, invertY: false);
        Assert.Equal(-1.5, yaw, 6); // right turns right: yaw decreases
        Assert.Equal(0, pitch, 6);

        var (_, up) = InputMath.StickLook(0, -1, 0.2, 3.0, 0.5, invertY: false);
        Assert.True(up > 0, "stick up looks up");
        var (_, inverted) = InputMath.StickLook(0, -1, 0.2, 3.0, 0.5, invertY: true);
        Assert.Equal(-up, inverted, 9);

        var (slow, _) = InputMath.StickLook(0.5, 0, 0.2, 3.0, 0.5, false);
        Assert.True(Math.Abs(slow) < Math.Abs(yaw) / 2, "the curve makes small deflections finer than linear");
        Assert.Equal((0.0, 0.0), InputMath.StickLook(0.1, 0.1, 0.2, 3, 1, false));
    }

    [Fact]
    public void MouseLookAndPitchClamp()
    {
        var (yaw, pitch) = InputMath.MouseLook(100, -50, 0.0025, invertY: false);
        Assert.Equal(-0.25, yaw, 9);
        Assert.Equal(0.125, pitch, 9);
        Assert.Equal(-0.125, InputMath.MouseLook(100, -50, 0.0025, true).Pitch, 9);
        Assert.Equal(1.5, InputMath.ClampPitch(9));
        Assert.Equal(-1.5, InputMath.ClampPitch(-9));
    }

    [Theory]
    [InlineData(0, 1, 0, 0, -1)]
    [InlineData(1, 0, 0, 1, 0)]
    [InlineData(0, 1, Math.PI / 2, -1, 0)]
    [InlineData(1, 0, Math.PI / 2, 0, -1)]
    public void MovementFollowsTheViewDirection(double strafe, double forward, double yaw, double expectedX, double expectedZ)
    {
        var (x, z) = InputMath.WorldMove(strafe, forward, yaw);
        Assert.Equal(expectedX, x, 6);
        Assert.Equal(expectedZ, z, 6);
    }

    [Fact]
    public void DiagonalMovementIsNotFaster()
    {
        var (x, z) = InputMath.WorldMove(1, 1, 0.3);
        Assert.Equal(1.0, Math.Sqrt(x * x + z * z), 6);
    }
}

public class NoHardCodedInputTests
{
    [Fact]
    public void GameplayScriptsAskForActionsNeverForKeys()
    {
        // Menus and InputMapper are the only places allowed to name keys: gameplay code reads actions, so rebinding works.
        string scripts = Path.Combine(LanguagePolicy.FindRepoRoot(), "game", "scripts");
        var allowed = new HashSet<string> { "InputMapper.cs", "Menus.cs", "SmokeTest.cs" };
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(scripts, "*.cs").Where(f => !allowed.Contains(Path.GetFileName(f))))
        {
            string text = File.ReadAllText(file);
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"Input\.IsKeyPressed|Input\.IsPhysicalKeyPressed|(?<![\w.])Key\.[A-Z]|MouseButton\.|JoyButton\.|JoyAxis\."))
                offenders.Add(Path.GetFileName(file));
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryActionIsHandledSomewhereInTheGame()
    {
        // A binding nobody reads is a dead setting. Each action must appear in the scripts (outside the mapper and menus).
        string scripts = Path.Combine(LanguagePolicy.FindRepoRoot(), "game", "scripts");
        string all = string.Concat(Directory.EnumerateFiles(scripts, "*.cs")
            .Where(f => Path.GetFileName(f) is not ("InputMapper.cs" or "Menus.cs" or "SmokeTest.cs"))
            .Select(File.ReadAllText));
        foreach (var a in Enum.GetValues<GameAction>().Where(a => a != GameAction.Pause)) // pause is handled by the menus
            Assert.Contains($"GameAction.{a}", all);
    }
}
