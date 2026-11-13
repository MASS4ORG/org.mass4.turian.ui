namespace Turian.Tests;

/// <summary>Document navigation driven by real engine keyboard/gamepad events without a pointer or device.</summary>
public sealed class UiNavigationTests
{
    const string sheet = """
        VisualElement { width = 200; height = 200; }
        Button, Toggle, Slider, Dropdown, Tab { width = 180; height = 30; }
        Button { background-color = #0000ff; }
        Button:focus { background-color = #00ff00; }
        Listbox { width = 180; }
        Option { height = 25; }
        """;

    /// <summary>Initial focus, spatial navigation, submit and cancel work using only a gamepad.</summary>
    [Fact]
    public void GamepadNavigatesAndActivatesMenuWithoutMouse()
    {
        var source = new BufferedInputSource();
        source.PushMouseMove(new Vector2(-100, -100));
        var ui = new UiFrameHarness("""
            <UI initial-focus="play" cancel="Back"><VisualElement>
              <Button name="disabled" enabled="false" click="Disabled">Disabled</Button>
              <Button name="play" click="Play">Play</Button>
              <Button name="options" click="Options">Options</Button>
            </VisualElement></UI>
            """, sheet, height: 200);
        ui.UseEngineInput(source);
        var plays = 0;
        var options = 0;
        var cancels = 0;
        ui.Renderer.Handlers["Play"] = () => plays++;
        ui.Renderer.Handlers["Options"] = () => options++;
        ui.Renderer.Handlers["Back"] = () => cancels++;
        ui.Renderer.Handlers["Disabled"] = () => Assert.Fail("A disabled button was activated.");
        ui.Frame();
        ui.Frame();
        var initial = ui.FocusedId;
        var pixels = ui.Frame();
        Assert.True(ui.IsColor(pixels, 150, 45, 0, 255, 0));
        Press(source, ui, GamepadButton.South);
        Assert.Equal(1, plays);
        Press(source, ui, GamepadButton.DPadDown);
        Assert.NotEqual(initial, ui.FocusedId);
        Press(source, ui, GamepadButton.South);
        Assert.Equal(1, options);
        Press(source, ui, GamepadButton.East);
        Assert.Equal(1, cancels);
    }

    /// <summary>Explicit directional links and forward/reverse Tab links resolve named controls.</summary>
    [Fact]
    public void KeyboardHonorsExplicitFocusLinksAndSkipsDisabledControls()
    {
        var source = new BufferedInputSource();
        source.PushMouseMove(new Vector2(-100, -100));
        var ui = new UiFrameHarness("""
            <UI initial-focus="a"><VisualElement>
              <Button name="a" focus-down="c" focus-next="c" click="A">A</Button>
              <Button name="b" enabled="false">B</Button>
              <Button name="c" focus-previous="a" click="C">C</Button>
            </VisualElement></UI>
            """, sheet, height: 200);
        ui.UseEngineInput(source);
        var selected = "";
        ui.Renderer.Handlers["A"] = () => selected = "a";
        ui.Renderer.Handlers["C"] = () => selected = "c";
        ui.Frame();
        Press(source, ui, Key.Down);
        Press(source, ui, Key.Enter);
        Assert.Equal("c", selected);
        source.PushKeyDown(Key.ShiftLeft);
        Press(source, ui, Key.Tab);
        source.PushKeyUp(Key.ShiftLeft);
        Press(source, ui, Key.Enter);
        Assert.Equal("a", selected);
        Press(source, ui, Key.Tab);
        Press(source, ui, Key.Enter);
        Assert.Equal("c", selected);
    }

    /// <summary>A gamepad operates toggles, sliders and dropdowns and can leave their directional controls.</summary>
    [Fact]
    public void GamepadOperatesBoundControlsAndDismissesDropdownBeforeDocument()
    {
        var source = new BufferedInputSource();
        source.PushMouseMove(new Vector2(-100, -100));
        var ui = new UiFrameHarness("""
            <UI initial-focus="toggle" cancel="Back"><VisualElement>
              <Toggle name="toggle" value="{Enabled, mode=TwoWay}" />
              <Slider name="slider" value="{Volume, mode=TwoWay}" step="0.1" />
              <Dropdown name="choice" selected-index="{Choice, mode=TwoWay}">
                <Option>A</Option><Option>B</Option>
              </Dropdown>
            </VisualElement></UI>
            """, sheet, height: 200);
        ui.UseEngineInput(source);
        var model = new Settings();
        ui.Renderer.Bind(model);
        var cancelled = 0;
        ui.Renderer.Handlers["Back"] = () => cancelled++;
        ui.Frame();
        Press(source, ui, GamepadButton.South);
        Assert.True(model.Enabled);
        Press(source, ui, GamepadButton.DPadDown);
        Assert.Equal("slider", ui.FocusedId);
        Press(source, ui, GamepadButton.DPadRight);
        Assert.Equal(0.3, model.Volume, 3);
        Press(source, ui, GamepadButton.DPadDown);
        Assert.Equal("choice", ui.FocusedId);
        Press(source, ui, GamepadButton.South);
        Press(source, ui, GamepadButton.DPadDown);
        Press(source, ui, GamepadButton.South);
        Assert.Equal(1, model.Choice);
        Press(source, ui, GamepadButton.South);
        Press(source, ui, GamepadButton.East);
        Assert.Equal(0, cancelled);
        Press(source, ui, GamepadButton.East);
        Assert.Equal(1, cancelled);
        Press(source, ui, GamepadButton.DPadLeft);
        Assert.Equal("slider", ui.FocusedId);
        Press(source, ui, GamepadButton.DPadDown);
        Press(source, ui, GamepadButton.DPadRight);
        Assert.Equal("toggle", ui.FocusedId);
    }

    /// <summary>Focused tab headers respond to submit and the shoulder actions.</summary>
    [Fact]
    public void GamepadSelectsTabs()
    {
        var source = new BufferedInputSource();
        source.PushMouseMove(new Vector2(-100, -100));
        var ui = new UiFrameHarness("""
            <UI initial-focus="first"><Tabs>
              <Tab name="first" header="First"><Button click="A">A</Button></Tab>
              <Tab name="second" header="Second"><Button click="B">B</Button></Tab>
            </Tabs></UI>
            """, sheet, width: 400, height: 200);
        ui.UseEngineInput(source);
        var a = 0;
        var b = 0;
        ui.Renderer.Handlers["A"] = () => a++;
        ui.Renderer.Handlers["B"] = () => b++;
        ui.Frame();
        Press(source, ui, GamepadButton.DPadRight);
        Press(source, ui, GamepadButton.South);
        Press(source, ui, GamepadButton.DPadDown);
        Press(source, ui, GamepadButton.South);
        Assert.Equal(0, a);
        Assert.Equal(1, b);
        Press(source, ui, GamepadButton.LeftShoulder);
        Press(source, ui, GamepadButton.South);
        Assert.Equal(1, a);
        Press(source, ui, GamepadButton.RightShoulder);
        Press(source, ui, GamepadButton.South);
        Assert.Equal(2, b);
    }

    /// <summary>A disabled preferred focus falls back, and missing explicit neighbors use spatial navigation.</summary>
    [Fact]
    public void DisabledInitialFocusAndMissingNeighborsFallBack()
    {
        var source = new BufferedInputSource();
        var ui = new UiFrameHarness("""
            <UI initial-focus="disabled"><VisualElement>
              <Button name="disabled" enabled="false" />
              <Button name="a" focus-down="missing" />
              <Button name="skip" focusable="false" />
              <Button name="b" />
            </VisualElement></UI>
            """, sheet, height: 200);
        ui.UseEngineInput(source);
        ui.Frame();
        ui.Frame();
        Assert.Equal("a", ui.FocusedId);
        Press(source, ui, GamepadButton.DPadDown);
        Assert.Equal("b", ui.FocusedId);
    }

    /// <summary>An image button activates from focus, including a custom submit binding instead of Enter.</summary>
    [Fact]
    public void ImageButtonUsesTheAuthoredSubmitBinding()
    {
        var source = new BufferedInputSource();
        source.PushMouseMove(new Vector2(-100, -100));
        var actions = UiNavigationActions.CreateDefault();
        actions.Find("UI/Submit")!.Bindings = [InputBinding.For(InputControl.Keyboard(Key.F))];
        var ui = new UiFrameHarness("""
            <UI><ImageButton name="image" width="50" height="30" image-normal="button.png" click="Go" /></UI>
            """);
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        ui.Renderer.ImageResolver = _ => image;
        var clicks = 0;
        ui.Renderer.Handlers["Go"] = () => clicks++;
        ui.UseEngineInput(source, actions);
        ui.Frame();
        Press(source, ui, Key.Enter);
        Assert.Equal(0, clicks);
        Press(source, ui, Key.F);
        Assert.Equal(1, clicks);
    }

    /// <summary>Escape and controller Cancel bubble out of a focused virtualized row.</summary>
    [Theory]
    [InlineData("List", false)]
    [InlineData("Grid", false)]
    [InlineData("List", true)]
    [InlineData("Grid", true)]
    public void CollectionRowsReturnCancelToTheirOwnDocument(string tag, bool gamepad)
    {
        var source = new BufferedInputSource();
        var ui = new UiFrameHarness($$"""
            <UI cancel="Back"><{{tag}} name="items" items="{Names}">
              <Label text="{item}" />
            </{{tag}}></UI>
            """, "List, Grid { width = 180; height = 100; item-height = 30; cell-height = 30; }");
        ui.Renderer.Bind(new { Names = new[] { "First", "Second" } });
        ui.UseEngineInput(source);
        var cancelled = 0;
        ui.Renderer.Handlers["Back"] = () => cancelled++;
        ui.Frame();
        source.PushMouseMove(new Vector2(20, 15));
        source.PushMouseDown(SilkMouseButton.Left);
        ui.Frame();
        source.NewFrame();
        source.PushMouseUp(SilkMouseButton.Left);
        ui.Frame();
        Assert.NotNull(ui.FocusedId);
        if (gamepad)
        {
            Press(source, ui, GamepadButton.East);
        }
        else
        {
            Press(source, ui, Key.Escape);
        }
        Assert.Equal(1, cancelled);
    }

    sealed class Settings
    {
        /// <summary>The bound toggle value.</summary>
        public bool Enabled { get; set; }

        /// <summary>The bound slider value.</summary>
        public double Volume { get; set; } = 0.2;

        /// <summary>The selected dropdown item.</summary>
        public int Choice { get; set; }
    }

    static void Press(BufferedInputSource source, UiFrameHarness ui, GamepadButton button)
    {
        source.NewFrame();
        source.PushGamepadButtonDown(0, button);
        ui.Frame();
        source.NewFrame();
        source.PushGamepadButtonUp(0, button);
        ui.Frame();
    }

    static void Press(BufferedInputSource source, UiFrameHarness ui, Key key)
    {
        source.NewFrame();
        source.PushKeyDown(key);
        ui.Frame();
        source.NewFrame();
        source.PushKeyUp(key);
        ui.Frame();
    }
}
