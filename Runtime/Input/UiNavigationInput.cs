namespace Turian.Engine.UI;

/// <summary>Bridges authored UI actions to Guinevere and keeps their edges stable across both rendering passes.</summary>
public sealed class UiNavigationInput : IUiFrameInput, ISemanticInputCapability
{
    readonly IUiFrameInput pointer;
    readonly IInputSource? source;
    readonly InputActionsAsset defaults = UiNavigationActions.CreateDefault();
    readonly bool[] pressed = new bool[Enum.GetValues<SemanticAction>().Length];
    readonly bool[] held = new bool[4];
    readonly double[] repeatAt = new double[4];
    double elapsed;
    bool editingText;
    InputActionsAsset? definition;

    /// <summary>Creates a navigation adapter around a screen-space or world-panel pointer handler.</summary>
    public UiNavigationInput(IUiFrameInput pointer, IInputSource? source = null)
    {
        this.pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
        this.source = source ?? pointer.InputSource;
        Actions = new InputActionService(this.source ?? new BufferedInputSource());
        Actions.Load(defaults);
        definition = defaults;
    }

    /// <summary>The UI-owned action service; its UI map can be rebound without changing gameplay maps.</summary>
    public InputActionService Actions { get; }

    /// <inheritdoc/>
    public IInputSource? InputSource => source;

    /// <summary>Seconds before a held direction repeats.</summary>
    public float RepeatDelay { get; set; } = 0.4f;

    /// <summary>Seconds between subsequent direction repeats.</summary>
    public float RepeatInterval { get; set; } = 0.08f;

    /// <inheritdoc/>
    public InputDevice ActiveDevice { get; private set; } = new("keyboard-mouse", InputDeviceKind.KeyboardMouse);

    /// <summary>Loads custom UI actions, or restores defaults for null; loading the current asset preserves edges.</summary>
    public void Load(InputActionsAsset? asset)
    {
        asset ??= defaults;
        if (ReferenceEquals(definition, asset))
        {
            return;
        }

        definition = asset;
        Actions.Load(Copy(asset));
        Array.Clear(held);
        Array.Clear(pressed);
    }

    static InputActionsAsset Copy(InputActionsAsset asset) => new()
    {
        Maps = [.. asset.Maps.Select(static map => new InputActionMap
        {
            Name = map.Name, Enabled = map.Enabled,
            Actions = [.. map.Actions.Select(static action => new InputAction
            {
                Name = action.Name, Type = action.Type,
                Bindings = [.. action.Bindings.Select(static binding => new InputBinding
                {
                    Path = binding.Path, Value = binding.Value, PlayerIndex = binding.PlayerIndex
                })]
            })]
        })]
    };

    /// <summary>Samples actions once before Guinevere begins a frame, with an unscaled delta in seconds.</summary>
    public void BeginFrame(float deltaTime, bool textInputFocused = false)
    {
        editingText = textInputFocused;
        elapsed += float.IsFinite(deltaTime) && deltaTime > 0f ? deltaTime : 0;
        Array.Clear(pressed);
        Actions.Update();
        pressed[(int)SemanticAction.Accept] = Actions.WasPressed("UI/Submit");
        pressed[(int)SemanticAction.Cancel] = Actions.WasPressed("UI/Cancel");
        pressed[(int)SemanticAction.PreviousTab] = Actions.WasPressed("UI/PreviousTab");
        pressed[(int)SemanticAction.NextTab] = Actions.WasPressed("UI/NextTab");
        var direction = Actions.ReadVector("UI/Navigate");
        Direction(0, SemanticAction.Left, direction.X < -0.5f);
        Direction(1, SemanticAction.Right, direction.X > 0.5f);
        Direction(2, SemanticAction.Up, direction.Y < -0.5f);
        Direction(3, SemanticAction.Down, direction.Y > 0.5f);
        UpdateDevice();
    }

    void Direction(int index, SemanticAction action, bool down)
    {
        if (down && (!held[index] || elapsed >= repeatAt[index]))
        {
            pressed[(int)action] = true;
            repeatAt[index] = elapsed + (held[index]
                ? Math.Max(0.01f, RepeatInterval) : Math.Max(0.01f, RepeatDelay));
        }
        held[index] = down;
    }

    void UpdateDevice()
    {
        if (source is null || Actions.Asset is not { } asset)
        {
            return;
        }
        foreach (var binding in asset.Maps.Where(static map => map.Enabled)
                     .SelectMany(static map => map.Actions).SelectMany(static action => action.Bindings))
        {
            UpdateDevice(source, binding);
        }
    }

    void UpdateDevice(IInputSource devices, InputBinding binding)
    {
        if (MathF.Abs(devices.Read(binding.Control, binding.PlayerIndex)) < 0.5f)
        {
            return;
        }
        var controller = binding.Control.Kind is InputControlKind.GamepadAxis or InputControlKind.GamepadButton;
        ActiveDevice = controller
            ? new InputDevice("gamepad", InputDeviceKind.Controller)
            : new InputDevice("keyboard-mouse", InputDeviceKind.KeyboardMouse);
    }

    /// <inheritdoc/>
    public bool IsPressed(SemanticAction action) => pressed[(int)action];

    /// <inheritdoc/>
    public float CanvasScale { get => pointer.CanvasScale; set => pointer.CanvasScale = value; }

    /// <inheritdoc/>
    public Vector2 MousePosition => pointer.MousePosition;

    /// <inheritdoc/>
    public Vector2 MouseDelta => pointer.MouseDelta;

    /// <inheritdoc/>
    public Vector2 PrevMousePosition => pointer.PrevMousePosition;

    /// <inheritdoc/>
    public float MouseWheelDelta => pointer.MouseWheelDelta;

    /// <inheritdoc/>
    public bool IsAnyKeyDown => pointer.IsAnyKeyDown;

    /// <inheritdoc/>
    public bool IsKeyDown(KeyboardKey keyboardKey) => pointer.IsKeyDown(keyboardKey);

    /// <inheritdoc/>
    public bool IsKeyUp(KeyboardKey keyboardKey) => pointer.IsKeyUp(keyboardKey);

    /// <inheritdoc/>
    public bool IsKeyPressed(KeyboardKey keyboardKey) =>
        (source is null || editingText || !IsNavigationKey(keyboardKey)) && pointer.IsKeyPressed(keyboardKey);

    static bool IsNavigationKey(KeyboardKey key) => key is KeyboardKey.Enter or KeyboardKey.Space
        or KeyboardKey.Escape or KeyboardKey.Left or KeyboardKey.Right or KeyboardKey.Up or KeyboardKey.Down;

    /// <inheritdoc/>
    public bool IsMouseButtonDown(MouseButton button) => pointer.IsMouseButtonDown(button);

    /// <inheritdoc/>
    public bool IsMouseButtonPressed(MouseButton button) => pointer.IsMouseButtonPressed(button);

    /// <inheritdoc/>
    public bool IsMouseButtonUp(MouseButton button) => pointer.IsMouseButtonUp(button);

    /// <inheritdoc/>
    public string GetTypedCharacters() => pointer.GetTypedCharacters();

    /// <inheritdoc/>
    public string GetClipboardText() => pointer.GetClipboardText();

    /// <inheritdoc/>
    public void SetClipboardText(string text) => pointer.SetClipboardText(text);

    /// <inheritdoc/>
    public void EndFrame() => pointer.EndFrame();
}
