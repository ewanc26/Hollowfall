using Godot;
using Hollowfall.Core.Input;

namespace Hollowfall.Game;

/// <summary>
/// Connects Core's <see cref="BindingMap"/> to Godot's InputMap. Every <see cref="GameAction"/> becomes an input action
/// called <c>hf_&lt;Name&gt;</c> with its keyboard/mouse and gamepad inputs, so game code asks "is Jump pressed" and never
/// mentions a key. Keyboard bindings use physical key positions, so WASD stays under the same fingers on AZERTY.
/// </summary>
public static class InputMapper
{
    public static string Name(GameAction action) => "hf_" + action;

    /// <summary>(Re)creates every action from the bindings. Call after loading settings and after any rebind.</summary>
    public static void Apply(BindingMap bindings, double deadzone)
    {
        foreach (var action in Enum.GetValues<GameAction>())
        {
            string name = Name(action);
            if (InputMap.HasAction(name)) InputMap.EraseAction(name);
            InputMap.AddAction(name, (float)deadzone);
            foreach (var binding in bindings.All(action))
                if (ToEvent(binding) is { } ev) InputMap.ActionAddEvent(name, ev);
        }
    }

    public static InputEvent? ToEvent(Binding b)
    {
        switch (b.Device)
        {
            case InputDevice.Keyboard when Enum.TryParse<Key>(b.Code, out var key):
                return new InputEventKey { PhysicalKeycode = key };
            case InputDevice.Mouse when Enum.TryParse<MouseButton>(b.Code, out var button):
                return new InputEventMouseButton { ButtonIndex = button };
            case InputDevice.GamepadButton when Enum.TryParse<JoyButton>(b.Code, out var joy):
                return new InputEventJoypadButton { ButtonIndex = joy };
            case InputDevice.GamepadAxis when Enum.TryParse<JoyAxis>(b.Code, out var axis):
                return new InputEventJoypadMotion { Axis = axis, AxisValue = b.AxisSign };
            default:
                return null;
        }
    }

    /// <summary>
    /// Turns a real input event into a binding, for "press a key" capture. Returns null for events that are not a
    /// deliberate press (key releases, mouse motion, a stick barely moved).
    /// </summary>
    public static Binding? FromEvent(InputEvent e, bool includeGamepad, bool includeKeyboardMouse)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } key when includeKeyboardMouse:
                var code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
                return code == Key.None ? null : Binding.Key(code.ToString());
            case InputEventMouseButton { Pressed: true } mouse when includeKeyboardMouse:
                return Binding.Mouse(mouse.ButtonIndex.ToString());
            case InputEventJoypadButton { Pressed: true } joy when includeGamepad:
                return Binding.Button(joy.ButtonIndex.ToString());
            case InputEventJoypadMotion motion when includeGamepad && Math.Abs(motion.AxisValue) >= 0.7f:
                return Binding.Axis(motion.Axis.ToString(), motion.AxisValue < 0 ? -1 : 1);
            default:
                return null;
        }
    }

    public static bool Pressed(GameAction a) => Input.IsActionPressed(Name(a));

    public static bool JustPressed(GameAction a) => Input.IsActionJustPressed(Name(a));
}
