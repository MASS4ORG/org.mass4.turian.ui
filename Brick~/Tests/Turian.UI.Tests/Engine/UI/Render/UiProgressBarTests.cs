namespace Turian.Tests;

/// <summary>The <c>&lt;ProgressBar&gt;</c> element: value mapping, fill geometry and sheet-driven painting.</summary>
public sealed class UiProgressBarTests
{
    const string sheet = """
        ProgressBar { background-color = #0000ff; }
        ProgressBar fill { background-color = #00ff00; }
        ProgressBar:indeterminate { background-color = #ff0000; }
        """;

    /// <summary>Completion maps the value into 0..1 between min and max; no value means indeterminate.</summary>
    [Theory]
    [InlineData(null, 0f, 1f, null)]
    [InlineData(0.5f, 0f, 1f, 0.5f)]
    [InlineData(2f, 0f, 1f, 1f)]
    [InlineData(-1f, 0f, 1f, 0f)]
    [InlineData(100f, 50f, 150f, 0.5f)]
    [InlineData(5f, 5f, 5f, 1f)]
    [InlineData(4f, 5f, 5f, 0f)]
    public void FractionMapsValueBetweenBounds(float? value, float min, float max, float? expected) =>
        Assert.Equal(expected, UiRenderer.ProgressFraction(value, min, max));

    /// <summary>A known fraction fills from the left, or from the bottom when vertical.</summary>
    [Fact]
    public void FillAnchorsAtTheStartOfTheTrack()
    {
        var track = new Rect(10, 20, 200, 40);

        Assert.Equal(new Rect(10, 20, 50, 40), UiRenderer.ProgressFill(track, 0.25f, false, 0f, 0.3f, 0.7f));
        Assert.Equal(new Rect(10, 50, 200, 10), UiRenderer.ProgressFill(track, 0.25f, true, 0f, 0.3f, 0.7f));
    }

    /// <summary>An indeterminate chunk enters from the start, crosses the track and is clipped at both ends.</summary>
    [Fact]
    public void IndeterminateChunkSweepsAcrossTheTrack()
    {
        var track = new Rect(0, 0, 100, 10);

        Assert.Equal(0f, UiRenderer.ProgressFill(track, null, false, 0f, 0.3f, 1f).W);
        var middle = UiRenderer.ProgressFill(track, null, false, 0.65f, 0.3f, 1f);
        Assert.Equal(35f, middle.X, 3);
        Assert.Equal(30f, middle.W, 3);
        var leaving = UiRenderer.ProgressFill(track, null, false, 1.2f, 0.3f, 1f);
        Assert.Equal(90f, leaving.X, 3);
        Assert.Equal(10f, leaving.W, 3);
        var vertical = UiRenderer.ProgressFill(track, null, true, 0.65f, 0.3f, 1f);
        Assert.Equal(3.5f, vertical.Y, 3);
        Assert.Equal(3f, vertical.H, 3);
    }

    /// <summary>Bound values arrive as any numeric type or as text.</summary>
    [Fact]
    public void BoundNumbersAcceptCommonTypes()
    {
        Assert.Equal(0.5f, UiRenderer.ToNumber(0.5f));
        Assert.Equal(0.5f, UiRenderer.ToNumber(0.5d));
        Assert.Equal(3f, UiRenderer.ToNumber(3));
        Assert.Equal(3f, UiRenderer.ToNumber(3L));
        Assert.Equal(0.5f, UiRenderer.ToNumber(0.5m));
        Assert.Equal(0.5f, UiRenderer.ToNumber("0.5"));
        Assert.Null(UiRenderer.ToNumber("half"));
        Assert.Null(UiRenderer.ToNumber(null));
        Assert.Null(UiRenderer.ToNumber(new object()));
    }

    /// <summary>The track and its fill part are painted from the sheet, split at the value.</summary>
    [Fact]
    public void FillPaintsTheCompletedSpan()
    {
        var ui = new UiFrameHarness("""
            <UI><ProgressBar value="150" min="100" max="200" width="200" height="20" /></UI>
            """, sheet);

        var pixels = ui.Frame();

        Assert.True(ui.IsColor(pixels, 50, 10, 0, 255, 0), "the completed half should be the fill color");
        Assert.True(ui.IsColor(pixels, 150, 10, 0, 0, 255), "the rest should be the track color");
    }

    sealed class Needs
    {
        /// <summary>The bound completion.</summary>
        public double Food { get; set; } = 0.25;
    }

    /// <summary>A bound value moves the fill on the next frame.</summary>
    [Fact]
    public void BoundValueMovesTheFill()
    {
        var needs = new Needs();
        var ui = new UiFrameHarness("""
            <UI><ProgressBar value="{Food}" width="200" height="20" /></UI>
            """, sheet);
        ui.Renderer.Bind(needs);

        Assert.True(ui.IsColor(ui.Frame(), 100, 10, 0, 0, 255), "a quarter should leave the middle unfilled");
        needs.Food = 0.75;
        Assert.True(ui.IsColor(ui.Frame(), 100, 10, 0, 255, 0), "three quarters should fill the middle");
    }

    /// <summary>A vertical bar fills from the bottom.</summary>
    [Fact]
    public void VerticalBarFillsFromTheBottom()
    {
        var ui = new UiFrameHarness("""
            <UI><ProgressBar value="0.5" orientation="vertical" width="20" height="100" /></UI>
            """, sheet);

        var pixels = ui.Frame();

        Assert.True(ui.IsColor(pixels, 10, 75, 0, 255, 0), "the lower half should be filled");
        Assert.True(ui.IsColor(pixels, 10, 25, 0, 0, 255), "the upper half should be track");
    }

    /// <summary>Without a value the bar matches <c>:indeterminate</c> and draws the moving chunk.</summary>
    [Fact]
    public void MissingValueIsIndeterminate()
    {
        var ui = new UiFrameHarness("""
            <UI><ProgressBar width="200" height="20" /></UI>
            """, sheet);
        ui.Clock.Step = 0.65f / 0.7f;

        var pixels = ui.Frame();

        Assert.True(ui.IsColor(pixels, 10, 10, 255, 0, 0), "the indeterminate track should use its own rule");
        Assert.True(ui.IsColor(pixels, 100, 10, 0, 255, 0), "the chunk should be crossing the middle");
    }

    /// <summary>The chunk's size and speed come from the sheet.</summary>
    [Fact]
    public void IndeterminateChunkIsStyled()
    {
        var ui = new UiFrameHarness("""
            <UI><ProgressBar width="200" height="20" /></UI>
            """, sheet + "ProgressBar { indeterminate-size = 0.1; indeterminate-speed = 1; }");
        ui.Clock.Step = 0.15f;

        var pixels = ui.Frame();

        Assert.True(ui.IsColor(pixels, 20, 10, 0, 255, 0), "a 0.1 chunk at 0.15 s should span 5% to 15% of the track");
        Assert.True(ui.IsColor(pixels, 100, 10, 255, 0, 0), "the middle should be bare track");
    }
}
