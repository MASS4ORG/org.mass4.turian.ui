namespace Turian.Tests;

/// <summary>Tooltips from the <c>tooltip</c> attribute or a <c>&lt;Tooltip&gt;</c> child, shown on hover or focus.</summary>
public sealed class UiTooltipTests
{
    /// <summary>Text is transparent so platform-dependent glyphs never cover a probed pixel.</summary>
    const string sheet =
        "Tooltip { background-color = #00ff00; color = transparent; width = 60; height = 20; delay = 0; }";

    const string owner = """
        <UI><VisualElement tooltip="Hi" style="width: 100; height: 30; background-color: #333333" /></UI>
        """;

    static IInputHandler Over => UiFrameHarness.Pointer(30, 15);

    static IInputHandler Away => UiFrameHarness.Pointer(150, 100);

    /// <summary>A text tooltip shows under its owner once hovered and hides after the pointer leaves.</summary>
    [Fact]
    public void TextTooltipFollowsHover()
    {
        var ui = new UiFrameHarness(owner, sheet);

        Assert.False(ui.IsColor(ui.Frame(Over), 30, 40, 0, 255, 0), "hover is observed before the tooltip draws");
        Assert.True(ui.IsColor(ui.Frame(Over), 30, 40, 0, 255, 0), "the tooltip should sit under its owner");
        ui.Frame(Away);
        Assert.False(UiFrameHarness.Contains(ui.Frame(Away), 0, 255, 0), "the tooltip should hide after leaving");
    }

    /// <summary>The sheet's <c>delay</c> holds the tooltip back until the owner has been hovered that long.</summary>
    [Fact]
    public void DelayComesFromTheSheet()
    {
        var ui = new UiFrameHarness(owner, sheet + "Tooltip { delay = 1; }");

        ui.Frame(Over);
        Assert.False(UiFrameHarness.Contains(ui.Frame(Over), 0, 255, 0), "half a second is shorter than the delay");
        Assert.True(UiFrameHarness.Contains(ui.Frame(Over), 0, 255, 0), "a full second reaches the delay");
    }

    /// <summary>Without a sheet delay the default holds the tooltip for one half-second frame.</summary>
    [Fact]
    public void DefaultDelayApplies()
    {
        var ui = new UiFrameHarness(owner, "Tooltip { background-color = #00ff00; width = 60; height = 20; }");
        ui.Clock.Step = 0.25f;

        ui.Frame(Over);
        Assert.False(UiFrameHarness.Contains(ui.Frame(Over), 0, 255, 0), "a quarter second is under the default");
        ui.Frame(Over);
        Assert.True(UiFrameHarness.Contains(ui.Frame(Over), 0, 255, 0), "three quarters pass the default");
    }

    /// <summary>A <c>&lt;Tooltip&gt;</c> child renders its own elements, only as the tooltip.</summary>
    [Fact]
    public void RichTooltipRendersItsChildrenOnlyWhenShown()
    {
        var ui = new UiFrameHarness("""
            <UI>
              <VisualElement style="width: 100; height: 30; background-color: #333333">
                <Tooltip style="background-color: #ffff00">
                  <VisualElement style="width: 40; height: 20; background-color: #0000ff" />
                </Tooltip>
              </VisualElement>
            </UI>
            """, "Tooltip { delay = 0; padding = 4; }");

        Assert.False(UiFrameHarness.Contains(ui.Frame(Away), 0, 0, 255), "tooltip content must not render inline");
        ui.Frame(Over);
        var shown = ui.Frame(Over);
        Assert.True(UiFrameHarness.Contains(shown, 0, 0, 255), "the tooltip's own elements should render");
        Assert.True(UiFrameHarness.Contains(shown, 255, 255, 0), "the inline background should paint the box");
    }

    sealed class Hints
    {
        /// <summary>The bound tooltip text.</summary>
        public string Hint { get; set; } = "Hungry";
    }

    /// <summary>A bound tooltip shows its text in the sheet's color; an empty value shows nothing.</summary>
    [Fact]
    public void BoundTextUsesTheSheetColor()
    {
        var hints = new Hints();
        var ui = new UiFrameHarness("""
            <UI><VisualElement tooltip="{Hint}" style="width: 100; height: 30" /></UI>
            """, "Tooltip { color = #ff0000; font-size = 18; width = 90; height = 26; delay = 0; }");
        ui.Renderer.Bind(hints);

        ui.Frame(Over);
        Assert.True(UiFrameHarness.Contains(ui.Frame(Over), 255, 0, 0), "the text should use the sheet color");
        hints.Hint = string.Empty;
        Assert.False(UiFrameHarness.Contains(ui.Frame(Over), 255, 0, 0), "an empty hint should show nothing");
    }

    /// <summary>A focused owner keeps its tooltip after the pointer leaves.</summary>
    [Fact]
    public void FocusKeepsTheTooltip()
    {
        var ui = new UiFrameHarness("""
            <UI><TextField name="f" tooltip="Name" width="100" height="30" /></UI>
            """, sheet);

        ui.Frames(UiFrameHarness.Pointer(30, 15, pressed: true), Away, Away);
        Assert.True(ui.IsColor(ui.Frame(Away), 30, 40, 0, 255, 0), "focus should keep the tooltip open");
    }

    /// <summary>Below the owner when it fits, above when it does not, and never past the right edge.</summary>
    [Fact]
    public void PositionStaysOnScreen()
    {
        var bounds = new Rect(0, 0, 200, 120);
        var size = new Vector2(60, 20);

        Assert.Equal(new Vector2(10, 50), TooltipBehavior.Position(new Rect(10, 20, 50, 30), size, bounds, 0, 0));
        Assert.Equal(new Vector2(15, 54), TooltipBehavior.Position(new Rect(10, 20, 50, 30), size, bounds, 5, 4));
        Assert.Equal(new Vector2(10, 70), TooltipBehavior.Position(new Rect(10, 90, 50, 30), size, bounds, 0, 0));
        Assert.Equal(new Vector2(140, 50), TooltipBehavior.Position(new Rect(180, 20, 20, 30), size, bounds, 0, 0));
        Assert.Equal(new Vector2(0, 0), TooltipBehavior.Position(new Rect(0, 0, 10, 110), size, bounds, 0, 0));
    }
}
