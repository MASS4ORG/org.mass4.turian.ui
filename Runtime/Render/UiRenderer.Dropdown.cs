namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    static readonly string[] openModifiers = ["open"];
    static readonly string[] emptyModifiers = ["empty"];
    static readonly string[] openEmptyModifiers = ["open", "empty"];
    static readonly string[] selectedModifiers = ["selected"];
    static readonly string[] highlightedModifiers = ["highlighted"];
    static readonly string[] selectedHighlightedModifiers = ["selected", "highlighted"];

    /// <summary>Stacking order of open option lists when their style names no <c>z-index</c>; below tooltips.</summary>
    const int defaultListLayer = 5_000;

    /// <summary>
    /// A <c>&lt;Dropdown&gt;</c> whose options come from <c>&lt;Option&gt;</c> children (<c>text</c> or inner text, optional
    /// <c>value</c>) or a bound <c>items</c> collection (labelled by <c>display-member</c> or <c>ToString</c>). The choice
    /// is two-way bindable as <c>selected-index</c> and as <c>value</c>, firing <c>value-changed</c>; <c>placeholder</c>
    /// shows while nothing is chosen. The button is the element's rule (<c>:open</c>, <c>:empty</c>, <c>:disabled</c>)
    /// with an <c>arrow</c> part sized by its rule at the end; the list is a <c>Listbox</c> (carrying the dropdown's
    /// classes) of <c>Option</c> rows (<c>:selected</c>, <c>:highlighted</c>), below the button or above it when there is
    /// no room.
    /// </summary>
    void RenderDropdown(Gui gui, UiElement el) => RenderChoice(gui, el, DropdownOptions(el));

    /// <summary>An <c>&lt;EnumDropdown&gt;</c>: a dropdown over the declared values of its bound <c>value</c>'s enum type.</summary>
    void RenderEnumDropdown(Gui gui, UiElement el) => RenderChoice(gui, el, EnumOptions(el));

    void RenderChoice(Gui gui, UiElement el, IReadOnlyList<DropdownOption> options)
    {
        var enabled = IsEnabled(el);
        var dropdown = DropdownFor(el);
        var key = StateKey(el, "selected");
        var selected = SelectedIndex(el, options, key);
        if (dropdown.Begin(gui, enabled) is { } chosen && chosen != selected && chosen < options.Count)
        {
            selected = chosen;
            WriteBinding(el, "selected-index", chosen);
            WriteBinding(el, "value", options[chosen].Value);
            Fire(el, "value-changed");
        }

        state[key] = selected;
        DrawDropdownButton(gui, el, options, selected, dropdown, enabled);
        if (dropdown.IsOpen)
        {
            cancelClaims++;
            DrawDropdownList(gui, el, options, selected, dropdown);
        }

        if (gui.Pass == Pass.Pass2Render)
        {
            dropdown.Dismiss(gui);
        }
    }

    DropdownBehavior DropdownFor(UiElement el)
    {
        var key = StateKey(el, "dropdown");
        if (state.TryGetValue(key, out var existing) && existing is DropdownBehavior dropdown)
        {
            return dropdown;
        }

        dropdown = new DropdownBehavior();
        state[key] = dropdown;
        return dropdown;
    }

    void DrawDropdownButton(Gui gui, UiElement el, IReadOnlyList<DropdownOption> options, int selected,
        DropdownBehavior dropdown, bool enabled)
    {
        var chosen = selected >= 0 && selected < options.Count;
        var modifiers = (dropdown.IsOpen, chosen) switch
        {
            (true, false) => openEmptyModifiers,
            (true, true) => openModifiers,
            (false, false) => emptyModifiers,
            _ => [],
        };
        var node = gui.StyledNode(el.Tag, el.Classes, el.Name, modifiers: modifiers, disabled: !enabled);
        ApplyAttributeSize(node, el);
        UiStyleApplier.Apply(node, el.InlineStyle);

        using (node.Enter())
        {
            LogRect(gui, el, node);
            DrawBackground(gui, el);
            if (gui.Pass == Pass.Pass2Render)
            {
                dropdown.Button(gui, enabled, options.Count, selected);
                DrawDropdownArrow(gui);
            }

            DrawLabel(gui, el, chosen ? options[selected].Label : ResolveTextAttribute(el, "placeholder") ?? string.Empty);
        }
    }

    /// <summary>The <c>arrow</c> part at the end of the button, drawn only when its rule gives it a size.</summary>
    static void DrawDropdownArrow(Gui gui)
    {
        var style = gui.ResolvePart("arrow", StyleState.None, null);
        var w = PartLength(style, "width", 0f);
        var h = PartLength(style, "height", 0f);
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var inner = gui.CurrentNode.InnerRect;
        gui.DrawStyledBox(style, new Rect(inner.X + inner.W - w, inner.Y + ((inner.H - h) / 2f), w, h));
    }

    void DrawDropdownList(Gui gui, UiElement el, IReadOnlyList<DropdownOption> options, int selected,
        DropdownBehavior dropdown)
    {
        var position = DropdownBehavior.ListPosition(dropdown.Anchor,
            new Vector2(dropdown.ListRect.W, dropdown.ListRect.H), gui.LayoutBounds);
        var list = gui.StyledNode("Listbox", el.Classes, null)
            .AbsoluteScreen(position.X, position.Y)
            .Width(dropdown.Anchor.W)
            .BlockInput();

        using (list.Enter())
        {
            var layer = gui.ResolveStyle("Listbox", el.Classes, null).Get("z-index");
            gui.SetZIndex(UiValue.TryFloat(layer, out var z) ? (int)z : defaultListLayer);
            gui.SetEscapesAncestorClips();
            gui.ScrollY();
            for (var i = 0; i < options.Count; i++)
            {
                DrawDropdownOption(gui, options[i], i, i == selected, i == dropdown.Highlight, dropdown);
            }
        }

        if (gui.Pass == Pass.Pass2Render)
        {
            dropdown.ListRect = list.LocalToScreen(list.Rect);
        }
    }

    void DrawDropdownOption(Gui gui, DropdownOption option, int index, bool selected, bool highlighted,
        DropdownBehavior dropdown)
    {
        var modifiers = (selected, highlighted) switch
        {
            (true, true) => selectedHighlightedModifiers,
            (true, false) => selectedModifiers,
            (false, true) => highlightedModifiers,
            _ => [],
        };
        var source = option.Style;
        var node = gui.StyledNode(optionTag, source.Classes, null, modifiers: modifiers);
        UiStyleApplier.Apply(node, source.InlineStyle);

        using (node.Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                dropdown.Option(gui, index);
            }

            DrawLabel(gui, source, option.Label);
        }
    }
}
