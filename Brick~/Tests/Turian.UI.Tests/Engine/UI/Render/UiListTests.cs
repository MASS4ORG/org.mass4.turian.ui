namespace Turian.Tests;

/// <summary>The <c>&lt;List&gt;</c> and <c>&lt;Grid&gt;</c> elements: virtualization, selection and input.</summary>
public sealed class UiListTests
{
    /// <summary>Labels are transparent so platform-dependent glyphs never cover a probed pixel.</summary>
    const string sheet = """
        Label { color = transparent; }
        List, Grid { width = 100; height = 100; background-color = #222222; }
        List { item-height = 20; }
        Grid { cell-height = 25; }
        Item { background-color = #00ffff; }
        Item:selected { background-color = #00ff00; }
        Item:focus { background-color = #ff0000; }
        Slider { width = 60; height = 20; }
        Slider thumb { background-color = #ffffff; width = 6; height = 20; }
        """;

    static IInputHandler Idle => UiFrameHarness.Pointer(-1, -1);

    static IInputHandler Press(float x, float y) => UiFrameHarness.Pointer(x, y, pressed: true);

    static IInputHandler Key(GKey key) => UiFrameHarness.Keys(-1, -1, key);

    sealed class Roster
    {
        /// <summary>A few thousand bound items.</summary>
        public List<string> Names { get; } = [.. Enumerable.Range(0, 3000).Select(static i => $"C{i}")];

        /// <summary>The bound selected index.</summary>
        public int Index { get; set; } = -1;

        /// <summary>The bound selected item.</summary>
        public string? Chosen { get; set; }

        /// <summary>A bound slider value beside the list.</summary>
        public double Volume { get; set; } = 0.2;

        /// <summary>Items that are not a list.</summary>
        public IEnumerable<string> Few => Names.Take(3);

        /// <summary>Text that is not an item collection.</summary>
        public string Title => "Crew";
    }

    static (UiFrameHarness Ui, Roster Model) Bound(string tag = "List", string attributes = "", string? model = null)
    {
        var roster = new Roster();
        var ui = new UiFrameHarness($$"""
            <UI><{{tag}} name="roster" items="{Names}" selected-index="{Index, mode=TwoWay}"
                         selected-item="{Chosen, mode=TwoWay}" selection-changed="OnChanged" {{attributes}}>
              <Label text="{item}" />
            </{{tag}}></UI>
            """, model ?? sheet);
        ui.Renderer.Bind(roster);
        return (ui, roster);
    }

    /// <summary>The viewport shows the rows it overlaps, clamped to the collection.</summary>
    [Fact]
    public void VisibleRowsCoverTheViewport()
    {
        Assert.Equal((0, 5), ListBehavior.VisibleRows(0, 100, 20, 3000));
        Assert.Equal((2, 8), ListBehavior.VisibleRows(50, 100, 20, 3000));
        Assert.Equal((0, 2), ListBehavior.VisibleRows(0, 100, 20, 3));
        Assert.Equal((0, -1), ListBehavior.VisibleRows(0, 100, 20, 0));
        Assert.Equal((0, -1), ListBehavior.VisibleRows(0, 100, 0, 10));
    }

    /// <summary>Revealing a row scrolls the least: up to its top or down to its bottom.</summary>
    [Fact]
    public void RevealScrollsTheLeast()
    {
        Assert.Equal(40f, ListBehavior.Reveal(100, 100, 20, 20, 2));
        Assert.Equal(100f, ListBehavior.Reveal(100, 100, 20, 20, 6));
        Assert.Equal(140f, ListBehavior.Reveal(100, 100, 20, 20, 11));
    }

    /// <summary>Neighbours step through cells and report -1 past each edge.</summary>
    [Fact]
    public void NeighboursStopAtEdges()
    {
        Assert.Equal(-1, ListBehavior.Neighbour(4, 10, 4, FocusDirection.Left));
        Assert.Equal(6, ListBehavior.Neighbour(5, 10, 4, FocusDirection.Right));
        Assert.Equal(-1, ListBehavior.Neighbour(7, 10, 4, FocusDirection.Right));
        Assert.Equal(-1, ListBehavior.Neighbour(9, 10, 4, FocusDirection.Right));
        Assert.Equal(-1, ListBehavior.Neighbour(2, 10, 4, FocusDirection.Up));
        Assert.Equal(1, ListBehavior.Neighbour(5, 10, 4, FocusDirection.Up));
        Assert.Equal(9, ListBehavior.Neighbour(7, 10, 4, FocusDirection.Down));
        Assert.Equal(-1, ListBehavior.Neighbour(8, 10, 4, FocusDirection.Down));
        Assert.Equal(-1, ListBehavior.Neighbour(3, 10, 1, FocusDirection.Right));
        Assert.Equal(-1, ListBehavior.Neighbour(3, 10, 1, FocusDirection.Next));
    }

    /// <summary>Fixed columns win; otherwise as many cells fit as the width allows, at least one.</summary>
    [Fact]
    public void ColumnsFitTheWidth()
    {
        Assert.Equal(4, ListBehavior.Columns(4, 64, 0, 10));
        Assert.Equal(3, ListBehavior.Columns(null, 25, 0, 88));
        Assert.Equal(2, ListBehavior.Columns(null, 30, 10, 80));
        Assert.Equal(1, ListBehavior.Columns(null, 300, 0, 88));
    }

    sealed class Counter
    {
        /// <summary>How many times an item's member was read.</summary>
        public int Reads;
    }

    sealed class Colonist(string name, Counter counter)
    {
        /// <summary>The name, counting reads.</summary>
        public string Name
        {
            get
            {
                counter.Reads++;
                return name;
            }
        }
    }

    sealed class Colony(Counter counter)
    {
        /// <summary>A few thousand colonists.</summary>
        public List<Colonist> Colonists { get; } =
            [.. Enumerable.Range(0, 3000).Select(i => new Colonist($"C{i}", counter))];
    }

    /// <summary>A few thousand items cost only the rows inside the viewport.</summary>
    [Fact]
    public void LargeCollectionsLayOutOnlyVisibleRows()
    {
        var counter = new Counter();
        var ui = new UiFrameHarness("""
            <UI><List items="{Colonists}" as="c"><Label text="{c.Name}" /></List></UI>
            """, sheet);
        ui.Renderer.Bind(new Colony(counter));

        var pixels = ui.Frames(Idle, Idle);

        Assert.InRange(counter.Reads, 1, 40);
        Assert.True(ui.IsColor(pixels, 40, 90, 0, 255, 255), "the last visible row should be laid out");
    }

    /// <summary>A press selects and focuses an item, writing both bindings and firing the event once.</summary>
    [Fact]
    public void PressSelectsAndFocuses()
    {
        var (ui, model) = Bound();
        var changes = 0;
        ui.Renderer.Handlers["OnChanged"] = () => changes++;

        var pixels = ui.Frames(Idle, Press(40, 50), Idle, Idle);

        Assert.Equal(2, model.Index);
        Assert.Equal("C2", model.Chosen);
        Assert.Equal(1, changes);
        Assert.True(ui.IsColor(pixels, 40, 50, 255, 0, 0), "the pressed item should match :focus");
        Assert.True(ui.IsColor(pixels, 40, 10, 0, 255, 255), "other items should use the item rule");
    }

    /// <summary>Directional keys move focus item to item and Enter selects the focused one.</summary>
    [Fact]
    public void KeyboardMovesFocusAndSelects()
    {
        var (ui, model) = Bound();

        var pixels = ui.Frames(Idle, Press(40, 10), Idle, Key(GKey.Down), Idle, Key(GKey.Down), Idle);
        Assert.Equal(0, model.Index);
        Assert.True(ui.IsColor(pixels, 40, 50, 255, 0, 0), "focus should have moved two items down");
        Assert.True(ui.IsColor(pixels, 40, 10, 0, 255, 0), "the selection should stay until accepted");

        ui.Frames(Key(GKey.Enter), Idle);
        Assert.Equal(2, model.Index);
        ui.Frames(Key(GKey.Up), Idle, Key(GKey.Space), Idle);
        Assert.Equal(1, model.Index);
    }

    /// <summary>Jumping to an item outside the viewport scrolls it into view and focuses it there.</summary>
    [Fact]
    public void JumpsScrollTheCursorIntoView()
    {
        var (ui, model) = Bound();

        ui.Frames(Idle, Press(40, 10), Idle, Key(GKey.End), Idle, Idle, Idle, Idle);
        var pixels = ui.Frames(Key(GKey.Enter), Idle);
        Assert.Equal(2999, model.Index);
        Assert.Equal("C2999", model.Chosen);
        Assert.True(ui.IsColor(pixels, 40, 90, 255, 0, 0), "the last item should sit at the bottom, focused");

        ui.Frames(Key(GKey.PageUp), Idle, Idle, Idle, Key(GKey.Enter), Idle);
        Assert.Equal(2994, model.Index);
        ui.Frames(Key(GKey.Home), Idle, Idle, Idle, Key(GKey.Enter), Idle);
        Assert.Equal(0, model.Index);
        ui.Frames(Key(GKey.PageDown), Idle, Idle, Idle, Key(GKey.Enter), Idle);
        Assert.Equal(5, model.Index);
    }

    /// <summary>A gamepad moves focus and accepts through the GUI's semantic input.</summary>
    [Fact]
    public void GamepadMovesFocusAndSelects()
    {
        var (ui, model) = Bound();
        var pad = ui.UseGamepad();

        ui.Frames(Idle, Press(40, 10), Idle);
        foreach (var action in new[] { SemanticAction.Down, SemanticAction.Down, SemanticAction.Accept })
        {
            pad.Press(action);
            ui.Frame(Idle);
            pad.Release();
            ui.Frame(Idle);
        }

        Assert.Equal(2, model.Index);
    }

    /// <summary>The wheel scrolls the rows, and presses then pick from the scrolled rows.</summary>
    [Fact]
    public void WheelScrollsTheRows()
    {
        var (ui, model) = Bound();

        ui.Frames(Idle, UiFrameHarness.Wheel(40, 50, -1), Idle, Press(40, 20), Idle);

        Assert.Equal(3, model.Index);
    }

    /// <summary>A collection that shrinks below the scroll position scrolls back to show what is left.</summary>
    [Fact]
    public void ShrunkCollectionScrollsBack()
    {
        var (ui, model) = Bound();
        ui.Frames(Idle, UiFrameHarness.Wheel(40, 50, -1), Idle);

        model.Names.RemoveRange(3, model.Names.Count - 3);
        var pixels = ui.Frames(Idle, Idle);

        Assert.True(ui.IsColor(pixels, 40, 10, 0, 255, 255), "the first item should be back on top");
        Assert.True(ui.IsColor(pixels, 40, 70, 34, 34, 34), "nothing should follow the last item");
    }

    /// <summary>Past the edge of the collection, a direction moves focus to the neighbouring control.</summary>
    [Fact]
    public void FocusLeavesPastAnEdge()
    {
        var roster = new Roster();
        var ui = new UiFrameHarness("""
            <UI><VisualElement style="flex-direction: row">
              <List items="{Names}" selected-index="{Index, mode=TwoWay}"><Label text="{item}" /></List>
              <Slider value="{Volume, mode=TwoWay}" step="0.1" />
            </VisualElement></UI>
            """, sheet);
        ui.Renderer.Bind(roster);

        ui.Frames(Idle, Press(40, 10), Idle, Key(GKey.Right), Idle, Key(GKey.Right), Idle);

        Assert.Equal(0.3, roster.Volume, 3);
        Assert.Equal(0, roster.Index);
    }

    /// <summary>A grid flows items into fixed columns and moves focus across and down them.</summary>
    [Fact]
    public void GridFlowsItemsIntoColumns()
    {
        var (ui, model) = Bound("Grid", "columns=\"4\"");

        var pixels = ui.Frames(Idle, Idle, Press(30, 35), Idle, Idle);
        Assert.Equal(5, model.Index);
        Assert.True(ui.IsColor(pixels, 30, 35, 255, 0, 0), "the pressed cell should be focused");
        Assert.True(ui.IsColor(pixels, 5, 35, 0, 255, 255), "its left neighbour should be a plain cell");

        ui.Frames(Key(GKey.Right), Idle, Key(GKey.Down), Idle, Key(GKey.Enter), Idle);
        Assert.Equal(10, model.Index);
    }

    /// <summary>A grid waits for its width before laying out cells, so no frame wraps them wrongly.</summary>
    [Fact]
    public void GridWaitsForItsWidth()
    {
        var (ui, _) = Bound("Grid", model: sheet + "Grid { cell-width = 25; }");

        Assert.False(UiFrameHarness.Contains(ui.Frame(), 0, 255, 255), "the first frame should lay out no cells");
        Assert.True(ui.IsColor(ui.Frame(), 10, 10, 0, 255, 255), "the next frame should lay them out");
    }

    /// <summary>Without fixed columns a grid fits as many cells of its cell width as the width allows.</summary>
    [Fact]
    public void GridFitsCellsByWidth()
    {
        var (ui, model) = Bound("Grid", model: sheet + "Grid { cell-width = 25; }");

        ui.Frames(Idle, Idle, Press(60, 35), Idle);

        Assert.Equal(5, model.Index);
    }

    /// <summary>A bound selected item selects the matching row.</summary>
    [Fact]
    public void SelectedItemBindingSelects()
    {
        var roster = new Roster { Chosen = "C1" };
        var ui = new UiFrameHarness("""
            <UI><List items="{Names}" selected-item="{Chosen, mode=TwoWay}"><Label text="{item}" /></List></UI>
            """, sheet);
        ui.Renderer.Bind(roster);

        var pixels = ui.Frames(Idle, Idle);

        Assert.True(ui.IsColor(pixels, 40, 30, 0, 255, 0), "the bound item should be selected");
        ui.Frames(Press(40, 50), Idle);
        Assert.Equal("C2", roster.Chosen);
    }

    /// <summary>An unbound list starts from a literal index and keeps its own selection.</summary>
    [Fact]
    public void UnboundListKeepsItsSelection()
    {
        var ui = new UiFrameHarness("""
            <UI><List items="{Few}" selected-index="2"><Label text="{item}" /></List></UI>
            """, sheet);
        ui.Renderer.Bind(new Roster());

        Assert.True(ui.IsColor(ui.Frames(Idle, Idle), 40, 50, 0, 255, 0), "index 2 should start selected");
        var pixels = ui.Frames(Press(40, 10), Idle, Idle);
        Assert.True(ui.IsColor(pixels, 40, 50, 0, 255, 255), "the selection should have moved to the pressed item");
        Assert.True(ui.IsColor(pixels, 40, 70, 34, 34, 34), "only three items should be laid out");
    }

    /// <summary>A text bound as items lays out nothing; a disabled list ignores presses.</summary>
    [Fact]
    public void TextItemsAndDisabledListsDoNothing()
    {
        var roster = new Roster();
        var ui = new UiFrameHarness("""
            <UI><VisualElement style="flex-direction: row">
              <List items="{Title}"><Label text="{item}" /></List>
              <List items="{Names}" enabled="false" selected-index="{Index, mode=TwoWay}" />
            </VisualElement></UI>
            """, sheet, width: 220);
        ui.Renderer.Bind(roster);

        var pixels = ui.Frames(Idle, Press(140, 10), Idle);

        Assert.True(ui.IsColor(pixels, 40, 10, 34, 34, 34), "a text should give no items");
        Assert.Equal(-1, roster.Index);
    }

    /// <summary>State inside an item's template belongs to that item, not to the row it scrolls through.</summary>
    [Fact]
    public void TemplateStateFollowsItsItem()
    {
        var ui = new UiFrameHarness("""
            <UI><List items="{Names}"><Toggle /></List></UI>
            """, sheet + "List { item-height = 25; } Item { background-color = #222222; }");
        ui.Renderer.Bind(new Roster());

        var on = ui.Frames(Idle, Idle, Press(7, 7), Idle);
        Assert.True(ui.IsColor(on, 7, 7, 255, 255, 255), "the first item's toggle should be on");

        var scrolled = ui.Frames(UiFrameHarness.Wheel(40, 50, -1), Idle);
        Assert.False(ui.IsColor(scrolled, 7, 7, 255, 255, 255), "the item now on top should keep its own state");

        var back = ui.Frames(UiFrameHarness.Wheel(40, 50, 1), Idle);
        Assert.True(ui.IsColor(back, 7, 7, 255, 255, 255), "the first item should still be on");
    }
}
