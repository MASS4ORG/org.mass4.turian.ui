namespace Turian.Tests;

/// <summary>The <c>&lt;Dropdown&gt;</c> and <c>&lt;EnumDropdown&gt;</c> elements: options, input, binding and styling.</summary>
public sealed class UiDropdownTests
{
    const string sheet = """
        Dropdown, EnumDropdown { width = 120; height = 24; background-color = #333333; }
        Dropdown:open { background-color = #0000ff; }
        Dropdown:empty { background-color = #ff00ff; }
        Dropdown arrow { width = 8; height = 8; background-color = #ffff00; }
        Listbox { flex-direction = column; background-color = #222222; }
        Option { width = expand; height = 20; background-color = #00ffff; }
        Option:selected { background-color = #00ff00; }
        Option:highlighted { background-color = #ff0000; }
        """;

    const string qualityOptions = """
        <Option text="Low" value="low" />
        <Option text="Medium" value="medium" />
        <Option>High</Option>
        """;

    static IInputHandler Idle => UiFrameHarness.Pointer(-1, -1);

    static IInputHandler PressButton => UiFrameHarness.Pointer(40, 12, pressed: true);

    /// <summary>Row <paramref name="index"/> of a list opened under the button, which is 24 tall with 20-tall rows.</summary>
    static IInputHandler PressOption(int index) => UiFrameHarness.Pointer(40, 24 + (index * 20) + 10, pressed: true);

    static IInputHandler Key(GKey key) => UiFrameHarness.Keys(-1, -1, key);

    sealed class Graphics
    {
        /// <summary>The bound option value.</summary>
        public string Quality { get; set; } = "low";

        /// <summary>The bound option index.</summary>
        public int QualityIndex { get; set; }
    }

    static (UiFrameHarness Ui, Graphics Model) Quality(string attributes = "")
    {
        var model = new Graphics();
        var ui = new UiFrameHarness($$"""
            <UI><Dropdown name="q" value="{Quality, mode=TwoWay}" selected-index="{QualityIndex, mode=TwoWay}"
                          value-changed="OnChanged" {{attributes}}>{{qualityOptions}}</Dropdown></UI>
            """, sheet);
        ui.Renderer.Bind(model);
        return (ui, model);
    }

    /// <summary>The list goes below the button, or above it when it does not fit, never past the top.</summary>
    [Fact]
    public void ListOpensBelowOrAbove()
    {
        var bounds = new Rect(0, 0, 200, 120);

        Assert.Equal(new Vector2(10, 30), DropdownBehavior.ListPosition(new Rect(10, 10, 50, 20), new(50, 60), bounds));
        Assert.Equal(new Vector2(10, 30), DropdownBehavior.ListPosition(new Rect(10, 90, 50, 20), new(50, 60), bounds));
        Assert.Equal(new Vector2(10, 0), DropdownBehavior.ListPosition(new Rect(10, 40, 50, 20), new(50, 90), bounds));
    }

    /// <summary>A press opens the list over the document; picking a row writes both bindings and closes it.</summary>
    [Fact]
    public void PickingAnOptionWritesBackAndCloses()
    {
        var (ui, model) = Quality();
        var changes = 0;
        ui.Renderer.Handlers["OnChanged"] = () => changes++;

        var open = ui.Frames(PressButton, Idle);
        Assert.True(ui.IsColor(open, 2, 2, 0, 0, 255), "an open button should match :open");
        Assert.True(ui.IsColor(open, 40, 34, 255, 0, 0), "the selected row should start highlighted");
        Assert.True(ui.IsColor(open, 40, 74, 0, 255, 255), "other rows should use the option rule");

        var closed = ui.Frames(PressOption(2), Idle);
        Assert.Equal("High", model.Quality);
        Assert.Equal(2, model.QualityIndex);
        Assert.Equal(1, changes);
        Assert.False(UiFrameHarness.Contains(closed, 0, 255, 255), "the list should close after a pick");
    }

    /// <summary>A press outside the button and the list dismisses it without changing the choice.</summary>
    [Fact]
    public void OutsidePressDismisses()
    {
        var (ui, model) = Quality();

        ui.Frames(PressButton, Idle, UiFrameHarness.Pointer(190, 110, pressed: true));
        var pixels = ui.Frame();

        Assert.False(UiFrameHarness.Contains(pixels, 0, 255, 255), "the list should be dismissed");
        Assert.Equal("low", model.Quality);
    }

    /// <summary>Keys move the highlight while open, pick with Enter, close with Escape and step the choice while closed.</summary>
    [Fact]
    public void KeyboardOperatesTheDropdown()
    {
        var (ui, model) = Quality();

        ui.Frames(PressButton, Key(GKey.Down), Key(GKey.Enter), Idle);
        Assert.Equal(1, model.QualityIndex);
        ui.Frames(Key(GKey.Down), Idle);
        Assert.Equal(2, model.QualityIndex);
        ui.Frames(Key(GKey.Space), Key(GKey.Escape), Idle);
        Assert.False(UiFrameHarness.Contains(ui.Frame(), 0, 255, 255), "Escape should close the list");
        Assert.Equal(2, model.QualityIndex);
    }

    /// <summary>A gamepad opens, moves through and picks options through the GUI's semantic input.</summary>
    [Fact]
    public void GamepadOperatesTheDropdown()
    {
        var (ui, model) = Quality();
        var pad = ui.UseGamepad();

        ui.Frames(PressButton, Idle, Key(GKey.Escape), Idle);
        foreach (var action in new[] { SemanticAction.Accept, SemanticAction.Down, SemanticAction.Down, SemanticAction.Accept })
        {
            pad.Press(action);
            ui.Frame(Idle);
            pad.Release();
            ui.Frame(Idle);
        }

        Assert.Equal(2, model.QualityIndex);
    }

    sealed record Resolution(string Name, int Width);

    sealed class Display
    {
        /// <summary>The bound items.</summary>
        public List<Resolution> Resolutions { get; } = [new("HD", 1280), new("Full HD", 1920)];

        /// <summary>The bound choice, an item itself.</summary>
        public Resolution? Current { get; set; }
    }

    /// <summary>Bound items are labelled by a member and the chosen item itself is written back.</summary>
    [Fact]
    public void BoundItemsWriteTheChosenItem()
    {
        var display = new Display();
        var ui = new UiFrameHarness("""
            <UI><Dropdown items="{Resolutions}" display-member="Name" value="{Current, mode=TwoWay}" /></UI>
            """, sheet);
        ui.Renderer.Bind(display);

        Assert.True(ui.IsColor(ui.Frame(), 2, 2, 255, 0, 255), "nothing chosen should match :empty");
        ui.Frames(PressButton, Idle, PressOption(1), Idle);

        Assert.Same(display.Resolutions[1], display.Current);
    }

    sealed class Names
    {
        /// <summary>Plain items labelled by their text.</summary>
        public List<string> Colonists { get; } = ["Engie", "Hana"];

        /// <summary>Text that is not an item collection.</summary>
        public string Title => "Crew";

        /// <summary>The bound choice.</summary>
        public string? Chosen { get; set; }
    }

    /// <summary>Plain items are labelled by their text; a text bound as items yields no options.</summary>
    [Fact]
    public void ItemsWithoutMemberUseTheirText()
    {
        var names = new Names();
        var ui = new UiFrameHarness("""
            <UI>
              <VisualElement style="flex-direction: column">
                <Dropdown items="{Colonists}" value="{Chosen, mode=TwoWay}" />
                <Dropdown items="{Title}" />
              </VisualElement>
            </UI>
            """, sheet);
        ui.Renderer.Bind(names);

        ui.Frames(PressButton, Idle, PressOption(1), Idle);
        Assert.Equal("Hana", names.Chosen);
        ui.Frames(UiFrameHarness.Pointer(40, 36, pressed: true), Idle);
        Assert.False(UiFrameHarness.Contains(ui.Frame(), 0, 255, 255), "a text binding should give no rows");
    }

    enum Difficulty
    {
        Easy,
        Normal,
        Hard,
    }

    sealed class Game
    {
        /// <summary>The bound enum value.</summary>
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    }

    /// <summary>An enum dropdown lists its value's declared members and writes the chosen one back.</summary>
    [Fact]
    public void EnumDropdownListsDeclaredValues()
    {
        var game = new Game();
        var ui = new UiFrameHarness("""
            <UI><EnumDropdown value="{Difficulty, mode=TwoWay}" /></UI>
            """, sheet + "EnumDropdown:open { background-color = #0000ff; }");
        ui.Renderer.Bind(game);

        var open = ui.Frames(PressButton, Idle);
        Assert.True(ui.IsColor(open, 40, 54, 255, 0, 0), "the current value should be highlighted");
        ui.Frames(PressOption(2), Idle);

        Assert.Equal(Difficulty.Hard, game.Difficulty);
    }

    /// <summary>An unbound enum dropdown has no options and shows its placeholder.</summary>
    [Fact]
    public void UnboundEnumDropdownIsEmpty()
    {
        var ui = new UiFrameHarness("""<UI><EnumDropdown placeholder="Pick" /></UI>""", sheet);

        ui.Frames(PressButton, Idle);

        Assert.False(UiFrameHarness.Contains(ui.Frame(), 0, 255, 255), "there should be no rows");
    }

    /// <summary>A disabled dropdown never opens; the arrow part draws where its rule sizes it.</summary>
    [Fact]
    public void DisabledDropdownStaysClosed()
    {
        var (ui, _) = Quality("enabled=\"false\"");

        var pixels = ui.Frames(PressButton, Idle);

        Assert.False(UiFrameHarness.Contains(pixels, 0, 255, 255), "a disabled dropdown should not open");
        Assert.True(ui.IsColor(pixels, 115, 12, 255, 255, 0), "the arrow should sit at the end of the button");
    }

    /// <summary>An unbound dropdown keeps its choice and starts from a literal index.</summary>
    [Fact]
    public void UnboundDropdownKeepsItsChoice()
    {
        var ui = new UiFrameHarness($$"""
            <UI><Dropdown selected-index="1">{{qualityOptions}}</Dropdown></UI>
            """, sheet);

        Assert.True(ui.IsColor(ui.Frames(PressButton, Idle), 40, 54, 255, 0, 0), "index 1 should start highlighted");
        ui.Frames(PressOption(0), Idle, PressButton, UiFrameHarness.Pointer(-1, -1));
        Assert.True(ui.IsColor(ui.Frame(), 40, 34, 255, 0, 0), "the picked first row should persist");
    }

    /// <summary>A list that does not fit under the button opens above it.</summary>
    [Fact]
    public void ListFlipsAboveNearTheBottom()
    {
        var ui = new UiFrameHarness($$"""
            <UI><VisualElement style="flex-direction: column">
              <VisualElement style="height: 100" />
              <Dropdown>{{qualityOptions}}</Dropdown>
            </VisualElement></UI>
            """, sheet, height: 150);

        var pixels = ui.Frames(UiFrameHarness.Pointer(40, 112, pressed: true), Idle, Idle);

        Assert.True(ui.IsColor(pixels, 40, 70, 0, 255, 255), "the rows should sit above the button");
    }
}
