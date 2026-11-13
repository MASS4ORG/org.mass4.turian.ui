namespace Turian.Engine.UI;

/// <summary>
/// Feeds a <see cref="Gui"/> frame from the engine's screen-space input, bridging
/// <see cref="IInputHandler"/> to an explicitly supplied <see cref="IInputSource"/>.
///
/// <para>
/// Pointer coordinates are in viewport pixels with the origin at the top-left, which
/// is what Guinevere expects for a full-screen or camera-space panel. World-space
/// panels use a separate handler that remaps a camera ray onto the panel surface.
/// </para>
/// </summary>
/// <remarks>
/// <see cref="KeyboardKey"/> and <see cref="MouseButton"/> share
/// GLFW-derived numeric values with <c>Silk.NET.Input</c> for the common range, so the
/// mapping is a direct <see cref="int"/> cast — the same approach Guinevere's own
/// Silk.NET integration uses.
/// </remarks>
/// <summary>
/// An <see cref="IInputHandler"/> the <see cref="UiRuntime"/> can drive per frame: it carries the
/// canvas scale a scaled panel is rasterised at and a per-frame reset hook. Implemented by the
/// screen-space <see cref="EngineInputHandler"/> and the world-space <c>WorldPanelInputHandler</c>.
/// </summary>
public interface IUiFrameInput : IInputHandler
{
    /// <summary>Engine devices available to action navigation, or null for a custom pointer-only handler.</summary>
    IInputSource? InputSource => null;

    /// <summary>Canvas scale the UI is rasterised at (pointer coords are divided by it).</summary>
    float CanvasScale { get; set; }

    /// <summary>Clears per-frame buffers (typed characters). Call after each Guinevere frame.</summary>
    void EndFrame();
}

/// <summary>
/// Screen-space input handler that translates the engine's <see cref="IInputSource"/> state into
/// a pointer/keyboard/mouse input frame for a full-screen or camera-space
/// <see cref="Gui"/>, including buffered text input.
/// </summary>
public sealed class EngineInputHandler : IUiFrameInput
{
    readonly IInputSource? source;
    string typedCharacters = string.Empty;

    /// <summary>Creates a screen-space handler. Without a source all input is neutral.</summary>
    public EngineInputHandler(IInputSource? source = null) => this.source = source;

    /// <inheritdoc/>
    public IInputSource? InputSource => source;

    /// <summary>
    /// The canvas scale the UI is currently rasterized at (<see cref="UiScale"/>). Pointer
    /// coordinates come from the engine in physical viewport pixels; the layout the pointer is
    /// hit-tested against is in logical units (physical ÷ scale), so the reported position is
    /// divided by this. 1 means physical and logical coincide.
    /// </summary>
    public float CanvasScale { get; set; } = 1f;

    /// <summary>
    /// Always <c>false</c>: the engine <see cref="IInputSource"/> reports individual
    /// keys, not a whole-keyboard "any key" flag. Only <c>Dropdown</c> reads this, and
    /// it treats <c>false</c> as "no keyboard activity", which is the safe default.
    /// Revisit alongside full text-input parity.
    /// </summary>
    public bool IsAnyKeyDown => false;

    float Inv => CanvasScale is > 0f and not 1f ? 1f / CanvasScale : 1f;

    // Where a lifted finger's pointer goes: a finger does not hover, so nothing stays highlighted after a tap.
    static readonly Vector2 Away = new(-100_000f, -100_000f);

    bool touchPointer;
    bool leftWasDown;

    // The frame after the press keeps the finger's position, because a click is decided where the finger lifted.
    bool PointerAway => touchPointer && !leftWasDown && source is { Touches.Count: 0 }
        && !source.IsMouseButtonDown(SilkMouseButton.Left);

    /// <inheritdoc/>
    public Vector2 MousePosition => PointerAway ? Away : (source?.MousePosition ?? Vector2.Zero) * Inv;

    /// <inheritdoc/>
    public Vector2 MouseDelta => PointerAway ? Vector2.Zero : (source?.MouseDelta ?? Vector2.Zero) * Inv;

    /// <inheritdoc/>
    public Vector2 PrevMousePosition => MousePosition - MouseDelta;

    /// <inheritdoc/>
    public float MouseWheelDelta => source?.MouseScrollDelta ?? 0f;

    /// <inheritdoc/>
    public bool IsKeyDown(KeyboardKey keyboardKey) => source?.IsKeyDown((SilkKey)(int)keyboardKey) ?? false;

    /// <inheritdoc/>
    public bool IsKeyPressed(KeyboardKey keyboardKey) => source?.WasKeyPressed((SilkKey)(int)keyboardKey) ?? false;

    /// <inheritdoc/>
    public bool IsKeyUp(KeyboardKey keyboardKey) => !IsKeyDown(keyboardKey);

    /// <inheritdoc/>
    public bool IsMouseButtonDown(MouseButton button) => source?.IsMouseButtonDown((SilkMouseButton)(int)button) ?? false;

    /// <inheritdoc/>
    public bool IsMouseButtonPressed(MouseButton button) => source?.WasMouseButtonPressed((SilkMouseButton)(int)button) ?? false;

    /// <inheritdoc/>
    public bool IsMouseButtonUp(MouseButton button) => !IsMouseButtonDown(button);

    /// <inheritdoc/>
    public string GetTypedCharacters() => typedCharacters;

    /// <summary>
    /// Text clipboard access is not yet abstracted by the engine; returns empty.
    /// Wired up with the text-input parity work (paste in <c>TextField</c>).
    /// </summary>
    public string GetClipboardText() => string.Empty;

    /// <inheritdoc cref="GetClipboardText"/>
    public void SetClipboardText(string text) { }

    /// <summary>
    /// Sets the characters typed this frame, to be reported by
    /// <see cref="GetTypedCharacters"/>. Called by the UI runtime once a per-frame
    /// character source exists; today it is only exercised by tests.
    /// </summary>
    /// <param name="text">The characters typed since the previous frame.</param>
    public void SetTypedCharacters(string text) => typedCharacters = text;

    /// <summary>
    /// Clears the per-frame typed-character buffer and notes whether a finger or a mouse drives the pointer.
    /// Call after each Guinevere frame.
    /// </summary>
    public void EndFrame()
    {
        typedCharacters = string.Empty;
        if (source is null)
        {
            return;
        }

        var leftDown = source.IsMouseButtonDown(SilkMouseButton.Left);
        if (source.Touches.Count > 0)
        {
            touchPointer = true;
        }
        else if (source.MouseDelta != Vector2.Zero && !leftDown && !leftWasDown)
        {
            touchPointer = false;
        }

        leftWasDown = leftDown;
    }
}
