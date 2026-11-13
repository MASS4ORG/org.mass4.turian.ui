namespace Turian.Tests;

/// <summary>The real engine-to-Guinevere action bridge, including rebinding and held navigation.</summary>
public sealed class UiNavigationInputTests
{
    /// <summary>The same action edges can be read in both UI passes and do not fire again while held.</summary>
    [Fact]
    public void SubmitEdgesAreStableAndRebindable()
    {
        var source = new BufferedInputSource();
        var input = new UiNavigationInput(new EngineInputHandler(source));
        Assert.Same(source, input.InputSource);
        source.PushGamepadButtonDown(0, GamepadButton.South);
        input.BeginFrame(0.016f);
        Assert.True(input.IsPressed(SemanticAction.Accept));
        Assert.True(input.IsPressed(SemanticAction.Accept));
        Assert.Equal(InputDeviceKind.Controller, input.ActiveDevice.Kind);
        input.BeginFrame(0.016f);
        Assert.False(input.IsPressed(SemanticAction.Accept));

        var asset = UiNavigationActions.CreateDefault();
        asset.Find("UI/Submit")!.Bindings = [InputBinding.For(InputControl.Keyboard(Key.F))];
        input.Load(asset);
        source.PushKeyDown(Key.Enter);
        input.BeginFrame(0.016f);
        Assert.False(input.IsPressed(SemanticAction.Accept));
        Assert.False(input.IsKeyPressed(GKey.Enter));
        source.PushKeyDown(Key.F);
        input.BeginFrame(0.016f);
        Assert.True(input.IsPressed(SemanticAction.Accept));
        input.BeginFrame(0.016f, textInputFocused: true);
        Assert.True(input.IsKeyPressed(GKey.Enter));
    }

    /// <summary>The left stick uses the engine's positive-up convention and repeats on the UI clock.</summary>
    [Fact]
    public void StickUsesEngineAxesAndRepeatsAfterDelay()
    {
        var source = new BufferedInputSource();
        var input = new UiNavigationInput(new EngineInputHandler(source), source);
        source.PushGamepadAxis(0, GamepadAxis.LeftStickY, 0.8f);
        input.BeginFrame(0.01f);
        Assert.True(input.IsPressed(SemanticAction.Up));
        Assert.False(input.IsPressed(SemanticAction.Down));
        input.BeginFrame(0.2f);
        Assert.False(input.IsPressed(SemanticAction.Up));
        input.BeginFrame(0.21f);
        Assert.True(input.IsPressed(SemanticAction.Up));
        input.BeginFrame(0.04f);
        Assert.False(input.IsPressed(SemanticAction.Up));
        source.PushGamepadDisconnected(0);
        input.BeginFrame(0.1f);
        Assert.False(input.IsPressed(SemanticAction.Up));
        source.PushGamepadButtonDown(0, GamepadButton.DPadDown);
        input.BeginFrame(0.01f);
        Assert.True(input.IsPressed(SemanticAction.Down));
    }

    /// <summary>A disabled or missing custom UI map yields neutral actions; null restores the defaults.</summary>
    [Fact]
    public void MissingAndDisabledMapsDoNotFallBackToHardcodedKeys()
    {
        var source = new BufferedInputSource();
        var input = new UiNavigationInput(new EngineInputHandler(source), source);
        source.PushKeyDown(Key.Space);
        input.Load(new InputActionsAsset());
        input.BeginFrame(0.016f);
        Assert.False(input.IsPressed(SemanticAction.Accept));
        input.Load(null);
        input.BeginFrame(0.016f);
        Assert.True(input.IsPressed(SemanticAction.Accept));
        input.Actions.SetMapEnabled("UI", false);
        input.BeginFrame(0.016f);
        Assert.False(input.IsPressed(SemanticAction.Accept));
        Assert.False(input.IsKeyPressed(GKey.Space));
    }

    /// <summary>World panels expose their engine source for action navigation without pointer hits.</summary>
    [Fact]
    public void WorldPanelNavigationReadsItsOwnEngineSource()
    {
        var source = new BufferedInputSource();
        var input = new UiNavigationInput(new WorldPanelInputHandler(source));
        source.PushGamepadButtonDown(0, GamepadButton.South);
        input.BeginFrame(0.016f);
        Assert.True(input.IsPressed(SemanticAction.Accept));
        Assert.False(input.IsMouseButtonDown(GMouseButton.Left));
    }

    /// <summary>Independent UI surfaces sample a shared definition without sharing held or rebound state.</summary>
    [Fact]
    public void SharedDefinitionsHaveIndependentRuntimeState()
    {
        var source = new BufferedInputSource();
        var asset = UiNavigationActions.CreateDefault();
        var first = new UiNavigationInput(new EngineInputHandler(source), source);
        var second = new UiNavigationInput(new EngineInputHandler(source), source);
        first.Load(asset);
        second.Load(asset);
        source.PushGamepadButtonDown(0, GamepadButton.South);
        first.BeginFrame(0.016f);
        second.BeginFrame(0.016f);
        Assert.True(first.IsPressed(SemanticAction.Accept));
        Assert.True(second.IsPressed(SemanticAction.Accept));
        Assert.False(asset.Find("UI/Submit")!.IsPressed);
        Assert.NotSame(first.Actions.Asset, second.Actions.Asset);
        first.Load(asset);
        first.BeginFrame(0.016f);
        Assert.False(first.IsPressed(SemanticAction.Accept));
    }

    /// <summary>Navigation preserves scaled pointer, text and clipboard contracts for its wrapped handler.</summary>
    [Fact]
    public void NavigationPreservesPointerAndTextEditingInput()
    {
        var pointer = Substitute.For<IUiFrameInput>();
        pointer.InputSource.Returns((IInputSource?)null);
        pointer.MousePosition.Returns(new Vector2(10, 20));
        pointer.MouseDelta.Returns(new Vector2(2, 3));
        pointer.PrevMousePosition.Returns(new Vector2(8, 17));
        pointer.MouseWheelDelta.Returns(2f);
        pointer.IsAnyKeyDown.Returns(true);
        pointer.IsKeyUp(GKey.F).Returns(true);
        pointer.IsMouseButtonUp(GMouseButton.Left).Returns(true);
        pointer.GetTypedCharacters().Returns("hello");
        pointer.GetClipboardText().Returns("clipboard");
        pointer.IsKeyPressed(GKey.Enter).Returns(true);
        var input = new UiNavigationInput(pointer) { CanvasScale = 2 };
        Assert.Equal(2, input.CanvasScale);
        Assert.Equal(new Vector2(10, 20), input.MousePosition);
        Assert.Equal(new Vector2(2, 3), input.MouseDelta);
        Assert.Equal(new Vector2(8, 17), input.PrevMousePosition);
        Assert.Equal(2, input.MouseWheelDelta);
        Assert.True(input.IsAnyKeyDown);
        Assert.True(input.IsKeyUp(GKey.F));
        Assert.True(input.IsMouseButtonUp(GMouseButton.Left));
        Assert.True(input.IsKeyPressed(GKey.Enter));
        Assert.Equal("hello", input.GetTypedCharacters());
        Assert.Equal("clipboard", input.GetClipboardText());
        input.SetClipboardText("changed");
        pointer.Received().SetClipboardText("changed");
        input.EndFrame();
        pointer.Received().EndFrame();
    }

    /// <summary>Existing custom frame handlers can keep their keyboard input without implementing engine devices.</summary>
    [Fact]
    public void LegacyCustomFrameHandlersKeepKeyboardNavigation()
    {
        var input = new UiNavigationInput(new LegacyInput());
        input.BeginFrame(0.016f);
        Assert.Null(input.InputSource);
        Assert.True(input.IsKeyPressed(GKey.Enter));
        Assert.False(input.IsPressed(SemanticAction.Accept));
    }

    sealed class LegacyInput : IUiFrameInput
    {
        /// <inheritdoc/>
        public float CanvasScale { get; set; } = 1;
        /// <inheritdoc/>
        public Vector2 MousePosition => Vector2.Zero;
        /// <inheritdoc/>
        public Vector2 MouseDelta => Vector2.Zero;
        /// <inheritdoc/>
        public Vector2 PrevMousePosition => Vector2.Zero;
        /// <inheritdoc/>
        public float MouseWheelDelta => 0;
        /// <inheritdoc/>
        public bool IsAnyKeyDown => false;
        /// <inheritdoc/>
        public bool IsKeyDown(GKey key) => false;
        /// <inheritdoc/>
        public bool IsKeyUp(GKey key) => true;
        /// <inheritdoc/>
        public bool IsKeyPressed(GKey key) => key == GKey.Enter;
        /// <inheritdoc/>
        public bool IsMouseButtonDown(GMouseButton button) => false;
        /// <inheritdoc/>
        public bool IsMouseButtonPressed(GMouseButton button) => false;
        /// <inheritdoc/>
        public bool IsMouseButtonUp(GMouseButton button) => true;
        /// <inheritdoc/>
        public string GetTypedCharacters() => string.Empty;
        /// <inheritdoc/>
        public string GetClipboardText() => string.Empty;
        /// <inheritdoc/>
        public void SetClipboardText(string text) { }
        /// <inheritdoc/>
        public void EndFrame() { }
    }
}
