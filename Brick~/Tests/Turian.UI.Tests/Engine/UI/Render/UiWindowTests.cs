namespace Turian.Tests;

/// <summary>The <c>&lt;Window&gt;</c> element: dragging, raising, closing, focus and binding.</summary>
public sealed class UiWindowTests
{
    const string sheet = """
        Window { width = 80; height = 60; background-color = #0000ff; }
        Window.b { background-color = #00ff00; }
        Window:focus-within { background-color = #ff00ff; }
        TitleBar { height = 16; background-color = #333333; }
        Window:dragging TitleBar { background-color = #ffff00; }
        CloseButton { width = 16; height = 16; background-color = #ff0000; }
        Slider { width = 60; height = 20; }
        Dropdown { width = 60; height = 20; }
        """;

    static IInputHandler Idle => UiFrameHarness.Pointer(-1, -1);

    static IInputHandler Press(float x, float y) => UiFrameHarness.Pointer(x, y, pressed: true);

    static IInputHandler Key(GKey key) => UiFrameHarness.Keys(-1, -1, key);

    sealed class Desk
    {
        /// <summary>Window A's bound left edge.</summary>
        public float AX { get; set; } = 10;

        /// <summary>Window A's bound top edge.</summary>
        public float AY { get; set; } = 10;

        /// <summary>Window B's bound left edge.</summary>
        public float BX { get; set; } = 50;

        /// <summary>Window B's bound top edge.</summary>
        public float BY { get; set; } = 30;

        /// <summary>Whether window A shows.</summary>
        public bool AOpen { get; set; } = true;

        /// <summary>Whether window B shows.</summary>
        public bool BOpen { get; set; } = true;

        /// <summary>A slider value inside window B.</summary>
        public double Volume { get; set; } = 0.2;

        /// <summary>Window A's bound title.</summary>
        public string Title => "Stock";
    }

    /// <summary>
    /// Window A at (10, 10) and window B at (50, 30), each 80 by 60 with a 16-tall title bar; B overlaps A's lower
    /// right and starts on top. <paramref name="content"/> goes inside B, a slider by default.
    /// </summary>
    static (UiFrameHarness Ui, Desk Model) TwoWindows(string? content = null)
    {
        content ??= """<Slider value="{Volume, mode=TwoWay}" step="0.1" />""";
        var desk = new Desk();
        var ui = new UiFrameHarness($$"""
            <UI>
              <Window name="a" title="{Title}" x="{AX, mode=TwoWay}" y="{AY, mode=TwoWay}" open="{AOpen, mode=TwoWay}"
                      closable="true" close="OnClose" />
              <Window name="b" class="b" title="Work" x="{BX, mode=TwoWay}" y="{BY, mode=TwoWay}"
                      open="{BOpen, mode=TwoWay}" closable="true">{{content}}</Window>
            </UI>
            """, sheet);
        ui.Renderer.Bind(desk);
        return (ui, desk);
    }

    /// <summary>A position stays inside the bounds; a window larger than them keeps its corner inside.</summary>
    [Fact]
    public void ClampKeepsWindowsOnScreen()
    {
        var bounds = new Rect(0, 0, 200, 120);

        Assert.Equal(new Vector2(120, 60), WindowBehavior.Clamp(new(150, 100), new(80, 60), bounds));
        Assert.Equal(new Vector2(0, 0), WindowBehavior.Clamp(new(-5, -5), new(80, 60), bounds));
        Assert.Equal(new Vector2(0, 0), WindowBehavior.Clamp(new(150, 100), new(300, 200), bounds));
        Assert.Equal(new Vector2(30, 20), WindowBehavior.Clamp(new(30, 20), new(80, 60), bounds));
    }

    /// <summary>The later window starts on top; a press inside the lower one raises it.</summary>
    [Fact]
    public void PressRaisesAWindow()
    {
        var (ui, _) = TwoWindows();

        Assert.True(ui.IsColor(ui.Frames(Idle, Idle), 70, 60, 0, 255, 0), "B should start above A");
        var pixels = ui.Frames(Press(20, 50), Idle, Idle);

        Assert.True(ui.IsColor(pixels, 70, 60, 0, 0, 255), "a press inside A should raise it above B");
    }

    /// <summary>Dragging the title bar moves the window, matches <c>:dragging</c> and writes the position.</summary>
    [Fact]
    public void DraggingTheTitleBarMovesTheWindow()
    {
        var (ui, model) = TwoWindows();

        var dragging = ui.Frames(Idle, Press(30, 15), UiFrameHarness.Held(50, 25), UiFrameHarness.Held(50, 25));
        Assert.True(ui.IsColor(dragging, 40, 25, 255, 255, 0), "the moved title bar should match :dragging");

        var dropped = ui.Frames(UiFrameHarness.Pointer(50, 25), Idle);
        Assert.Equal(30f, model.AX);
        Assert.Equal(20f, model.AY);
        Assert.True(ui.IsColor(dropped, 40, 25, 51, 51, 51), "the title bar should stop matching :dragging");
    }

    /// <summary>A window dragged past the screen edge stops at it.</summary>
    [Fact]
    public void DraggingStaysOnScreen()
    {
        var (ui, model) = TwoWindows();

        var pixels = ui.Frames(Idle, Press(30, 15), UiFrameHarness.Held(400, 400), UiFrameHarness.Pointer(400, 400),
            Idle);

        Assert.InRange(model.AX, 110f, 125f);
        Assert.InRange(model.AY, 50f, 65f);
        Assert.True(ui.IsColor(pixels, 198, 110, 0, 0, 255), "the window should reach the bottom right corner");
        Assert.False(ui.IsColor(pixels, 105, 110, 0, 0, 255), "the window should keep its own size");
    }

    /// <summary>A bound position moves the window from the model.</summary>
    [Fact]
    public void BoundPositionMovesTheWindow()
    {
        var (ui, model) = TwoWindows();
        ui.Frames(Idle, Idle);

        model.AX = 100;
        var pixels = ui.Frames(Idle, Idle);

        Assert.True(ui.IsColor(pixels, 150, 50, 0, 0, 255), "A should have moved right");
        Assert.False(ui.IsColor(pixels, 20, 50, 0, 0, 255), "A should have left its old place");
    }

    /// <summary>The close button closes without dragging, writes <c>open</c> and fires <c>close</c>.</summary>
    [Fact]
    public void CloseButtonClosesTheWindow()
    {
        var (ui, model) = TwoWindows();
        var closes = 0;
        ui.Renderer.Handlers["OnClose"] = () => closes++;

        Assert.True(ui.IsColor(ui.Frames(Idle, Idle), 85, 15, 255, 0, 0), "the close button should end the title bar");
        var pixels = ui.Frames(Press(80, 15), UiFrameHarness.Held(120, 40), Idle, Idle);

        Assert.False(model.AOpen);
        Assert.Equal(1, closes);
        Assert.Equal(10f, model.AX);
        Assert.False(ui.IsColor(pixels, 20, 50, 0, 0, 255), "A should be gone");
    }

    /// <summary>Focus inside a window matches <c>:focus-within</c>, and Cancel closes it.</summary>
    [Fact]
    public void CancelClosesTheFocusedWindow()
    {
        var (ui, model) = TwoWindows();

        var focused = ui.Frames(Idle, Press(80, 56), Idle, Idle);
        Assert.True(ui.IsColor(focused, 120, 80, 255, 0, 255), "B should match :focus-within");
        Assert.True(model.AOpen);

        ui.Frames(Key(GKey.Escape), Idle);
        Assert.False(model.BOpen);
        Assert.True(model.AOpen);
    }

    /// <summary>A gamepad's cancel closes the focused window too.</summary>
    [Fact]
    public void GamepadCancelClosesTheFocusedWindow()
    {
        var (ui, model) = TwoWindows();
        var pad = ui.UseGamepad();

        ui.Frames(Idle, Press(80, 56), Idle, Idle);
        pad.Press(SemanticAction.Cancel);
        ui.Frame(Idle);
        pad.Release();
        ui.Frame(Idle);

        Assert.False(model.BOpen);
    }

    /// <summary>Reopening a window moves focus to its first focusable content.</summary>
    [Fact]
    public void OpeningMovesFocusIntoTheContent()
    {
        var (ui, model) = TwoWindows();
        model.BOpen = false;
        ui.Frames(Idle, Idle);

        model.BOpen = true;
        ui.Frames(Idle, Idle);
        var before = model.Volume;
        ui.Frames(Key(GKey.Right), Idle);

        Assert.Equal(before + 0.1, model.Volume, 3);
    }

    /// <summary>Cancel first closes a popup open inside the window, and only then the window.</summary>
    [Fact]
    public void CancelLeavesTheWindowToAnOpenPopup()
    {
        var (ui, model) = TwoWindows("""<Dropdown><Option text="Low" /><Option text="High" /></Dropdown>""");

        ui.Frames(Idle, Press(70, 56), Idle, Idle, Key(GKey.Escape), Idle);
        Assert.True(model.BOpen);

        ui.Frames(Key(GKey.Escape), Idle);
        Assert.False(model.BOpen);
    }

    /// <summary>Unbound windows take their literal position once and keep what dragging gives them.</summary>
    [Fact]
    public void UnboundWindowKeepsItsPosition()
    {
        var ui = new UiFrameHarness("""
            <UI>
              <Window x="20" y="20" title="Plain" />
              <Window class="b" open="false" />
            </UI>
            """, sheet);

        var start = ui.Frames(Idle, Idle);
        Assert.True(ui.IsColor(start, 25, 50, 0, 0, 255), "the window should start at its literal position");
        Assert.False(UiFrameHarness.Contains(start, 0, 255, 0), "a closed window should not draw");

        var moved = ui.Frames(Press(30, 25), UiFrameHarness.Held(40, 25), UiFrameHarness.Pointer(40, 25), Idle, Idle);
        Assert.False(ui.IsColor(moved, 25, 50, 0, 0, 255), "the dragged window should stay where it was dropped");
        Assert.True(ui.IsColor(moved, 105, 50, 0, 0, 255), "the dragged window should cover its new place");
    }
}
