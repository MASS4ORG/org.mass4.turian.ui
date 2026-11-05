namespace Turian.Tests;

/// <summary>The <c>&lt;Slider&gt;</c> element: snapping, pointer, keyboard and gamepad input, binding and parts.</summary>
public sealed class UiSliderTests
{
    const string sheet = """
        Slider { width = 200; height = 20; }
        Slider:vertical { width = 20; height = 100; }
        Slider track { background-color = #0000ff; height = 6; }
        Slider:vertical track { width = 6; }
        Slider fill { background-color = #00ff00; height = 6; }
        Slider thumb { background-color = #ff0000; width = 10; height = 20; }
        """;

    sealed class Settings
    {
        /// <summary>The bound slider value.</summary>
        public double Volume { get; set; } = 0.2;
    }

    static (UiFrameHarness Ui, Settings Model) Bound(string attributes = "")
    {
        var model = new Settings();
        var ui = new UiFrameHarness($$"""
            <UI><Slider name="s" value="{Volume, mode=TwoWay}" {{attributes}} /></UI>
            """, sheet);
        ui.Renderer.Bind(model);
        return (ui, model);
    }

    /// <summary>Values snap to multiples of the step above the minimum and stay within the bounds.</summary>
    [Theory]
    [InlineData(0.34f, 0f, 1f, 0f, 0.34f)]
    [InlineData(0.34f, 0f, 1f, 0.25f, 0.25f)]
    [InlineData(0.4f, 0f, 1f, 0.25f, 0.5f)]
    [InlineData(7f, 2f, 10f, 3f, 8f)]
    [InlineData(-1f, 0f, 1f, 0f, 0f)]
    [InlineData(2f, 0f, 1f, 0.3f, 1f)]
    public void SnapKeepsStepsWithinBounds(float value, float min, float max, float step, float expected) =>
        Assert.Equal(expected, SliderBehavior.Snap(value, min, max, step), 3);

    /// <summary>The pointer maps along the area: left to right, or bottom to top when vertical.</summary>
    [Fact]
    public void PointerMapsAlongTheArea()
    {
        var area = new Rect(10, 20, 100, 50);

        Assert.Equal(0.25f, SliderBehavior.ValueAt(new Vector2(35, 0), area, 0, 1, 0, false), 3);
        Assert.Equal(1f, SliderBehavior.ValueAt(new Vector2(500, 0), area, 0, 1, 0, false), 3);
        Assert.Equal(0.8f, SliderBehavior.ValueAt(new Vector2(0, 30), area, 0, 1, 0, true), 3);
        Assert.Equal(5f, SliderBehavior.ValueAt(new Vector2(0, 0), new Rect(0, 0, 0, 0), 5, 9, 0, false));
    }

    /// <summary>The track, the fill up to the value and the thumb on it are painted from the sheet.</summary>
    [Fact]
    public void PartsPaintFromTheSheet()
    {
        var ui = new UiFrameHarness("""<UI><Slider value="0.5" /></UI>""", sheet);

        var pixels = ui.Frame();

        Assert.True(ui.IsColor(pixels, 50, 10, 0, 255, 0), "the filled half should use the fill rule");
        Assert.True(ui.IsColor(pixels, 150, 10, 0, 0, 255), "the rest should be track");
        Assert.True(ui.IsColor(pixels, 100, 2, 255, 0, 0), "the thumb should sit at the value, taller than the track");
        Assert.False(ui.IsColor(pixels, 150, 2, 0, 0, 255), "the track should be only as thick as its rule");
    }

    /// <summary>A press jumps to the pointer and writes back through the two-way binding.</summary>
    [Fact]
    public void ClickJumpsAndWritesBack()
    {
        var (ui, model) = Bound("step=\"0.05\"");

        ui.Frame(UiFrameHarness.Pointer(150, 10, pressed: true));

        Assert.Equal(0.75, model.Volume, 3);
    }

    /// <summary>Dragging keeps scrubbing past the slider and stops when the button is released.</summary>
    [Fact]
    public void DragScrubsUntilRelease()
    {
        var (ui, model) = Bound();

        ui.Frames(UiFrameHarness.Pointer(50, 10, pressed: true), UiFrameHarness.Held(180, 60));
        Assert.Equal(0.9, model.Volume, 2);
        ui.Frames(UiFrameHarness.Held(500, 10), UiFrameHarness.Pointer(20, 10));
        Assert.Equal(1, model.Volume, 3);
    }

    /// <summary>A focused slider steps with the arrow keys and jumps to the bounds with Home and End.</summary>
    [Fact]
    public void KeysStepAFocusedSlider()
    {
        var (ui, model) = Bound("step=\"0.1\"");

        ui.Frames(UiFrameHarness.Pointer(100, 10, pressed: true), UiFrameHarness.Keys(100, 10, GKey.Right));
        Assert.Equal(0.6, model.Volume, 3);
        ui.Frame(UiFrameHarness.Keys(100, 10, GKey.Left));
        Assert.Equal(0.5, model.Volume, 3);
        ui.Frame(UiFrameHarness.Keys(100, 10, GKey.End));
        Assert.Equal(1, model.Volume, 3);
        ui.Frame(UiFrameHarness.Keys(100, 10, GKey.Home));
        Assert.Equal(0, model.Volume, 3);
    }

    /// <summary>Gamepad directions step a focused slider through the GUI's semantic input.</summary>
    [Fact]
    public void GamepadStepsAFocusedSlider()
    {
        var (ui, model) = Bound("step=\"0.1\"");
        var pad = ui.UseGamepad();

        ui.Frame(UiFrameHarness.Pointer(100, 10, pressed: true));
        pad.Press(SemanticAction.Right);
        ui.Frame(UiFrameHarness.Pointer(100, 10));
        pad.Release();
        Assert.Equal(0.6, model.Volume, 3);
    }

    /// <summary>A vertical slider grows upwards and steps with up and down.</summary>
    [Fact]
    public void VerticalSliderGrowsUpwards()
    {
        var (ui, model) = Bound("orientation=\"vertical\" step=\"0.25\"");

        ui.Frame(UiFrameHarness.Pointer(10, 20, pressed: true));
        Assert.Equal(0.75, model.Volume, 3);
        ui.Frame(UiFrameHarness.Keys(10, 20, GKey.Up));
        Assert.Equal(1, model.Volume, 3);
        var pixels = ui.Frame(UiFrameHarness.Keys(10, 20, GKey.Down));
        Assert.Equal(0.75, model.Volume, 3);
        Assert.True(ui.IsColor(pixels, 10, 90, 0, 255, 0), "the fill should grow from the bottom");
    }

    /// <summary>A disabled slider ignores input.</summary>
    [Fact]
    public void DisabledSliderIgnoresInput()
    {
        var (ui, model) = Bound("enabled=\"false\"");

        ui.Frames(UiFrameHarness.Pointer(150, 10, pressed: true), UiFrameHarness.Keys(150, 10, GKey.End));

        Assert.Equal(0.2, model.Volume, 3);
    }

    /// <summary>An unbound slider keeps its value between frames, fires <c>value-changed</c> and swaps reversed bounds.</summary>
    [Fact]
    public void UnboundSliderKeepsItsValue()
    {
        var ui = new UiFrameHarness("""
            <UI><Slider value="20" min="100" max="0" step="10" value-changed="OnChanged" /></UI>
            """, sheet);
        var changes = 0;
        ui.Renderer.Handlers["OnChanged"] = () => changes++;

        Assert.True(ui.IsColor(ui.Frame(), 30, 10, 0, 255, 0), "20 of 100 should fill a fifth");
        ui.Frames(UiFrameHarness.Pointer(150, 10, pressed: true), UiFrameHarness.Pointer(-1, -1));
        Assert.True(ui.IsColor(ui.Frame(), 140, 10, 0, 255, 0), "the clicked 80 should persist");
        Assert.Equal(1, changes);
    }
}
