namespace Turian.Engine.UI;

/// <summary>Creates the rebindable UI action map used when a panel supplies no custom input asset.</summary>
public static class UiNavigationActions
{
    /// <summary>Builds a fresh UI map with keyboard, D-pad, left-stick and face/shoulder button bindings.</summary>
    public static InputActionsAsset CreateDefault()
    {
        var navigate = new InputAction { Name = "Navigate", Type = InputActionType.Vector2 };
        Bind(navigate, InputControl.Keyboard(SilkKey.Left), -Vector2.UnitX);
        Bind(navigate, InputControl.Keyboard(SilkKey.Right), Vector2.UnitX);
        Bind(navigate, InputControl.Keyboard(SilkKey.Up), -Vector2.UnitY);
        Bind(navigate, InputControl.Keyboard(SilkKey.Down), Vector2.UnitY);
        Bind(navigate, InputControl.Gamepad(GamepadButton.DPadLeft), -Vector2.UnitX);
        Bind(navigate, InputControl.Gamepad(GamepadButton.DPadRight), Vector2.UnitX);
        Bind(navigate, InputControl.Gamepad(GamepadButton.DPadUp), -Vector2.UnitY);
        Bind(navigate, InputControl.Gamepad(GamepadButton.DPadDown), Vector2.UnitY);
        Bind(navigate, InputControl.GamepadStick(GamepadAxis.LeftStickX), Vector2.UnitX);
        Bind(navigate, InputControl.GamepadStick(GamepadAxis.LeftStickY), -Vector2.UnitY);
        return new InputActionsAsset
        {
            Maps = [new InputActionMap
            {
                Name = "UI",
                Actions =
                [
                    navigate,
                    Button("Submit", InputControl.Keyboard(SilkKey.Enter), InputControl.Keyboard(SilkKey.Space),
                        InputControl.Gamepad(GamepadButton.South)),
                    Button("Cancel", InputControl.Keyboard(SilkKey.Escape), InputControl.Gamepad(GamepadButton.East)),
                    Button("PreviousTab", InputControl.Gamepad(GamepadButton.LeftShoulder)),
                    Button("NextTab", InputControl.Gamepad(GamepadButton.RightShoulder))
                ]
            }]
        };
    }

    static void Bind(InputAction action, InputControl control, Vector2 value) =>
        action.Bindings.Add(InputBinding.For(control, value));

    static InputAction Button(string name, params InputControl[] controls) => new()
    {
        Name = name, Bindings = [.. controls.Select(static control => InputBinding.For(control))]
    };
}
