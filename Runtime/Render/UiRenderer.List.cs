namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    const string itemTag = "Item";

    /// <summary>Row height of a list when its style names no <c>item-height</c>.</summary>
    const float defaultItemHeight = 24f;

    /// <summary>Cell width of a grid when its style names no <c>cell-width</c>.</summary>
    const float defaultCellWidth = 64f;

    /// <summary>
    /// A <c>&lt;List&gt;</c> (or <c>&lt;Grid&gt;</c>, see <see cref="RenderGrid"/>) over a bound <c>items</c>
    /// collection whose children are the item template, with the current item named by <c>as</c> (<c>item</c>). Only
    /// rows inside the viewport are laid out, so every row is <c>item-height</c> tall and rows are <c>gap</c> apart.
    /// The single selection is two-way bindable as <c>selected-index</c> and <c>selected-item</c>, firing
    /// <c>selection-changed</c>. Each item is an <c>Item</c> node (<c>:selected</c>, <c>:hover</c>, <c>:focus</c>,
    /// <c>:disabled</c>), styled as <c>List Item</c>.
    /// </summary>
    void RenderList(Gui gui, UiElement el) => RenderCollection(gui, el, grid: false);

    /// <summary>
    /// A <c>&lt;Grid&gt;</c>: a list whose items flow into <c>columns</c> per row, or as many <c>cell-width</c> cells
    /// as fit, each <c>cell-height</c> tall (square by default).
    /// </summary>
    void RenderGrid(Gui gui, UiElement el) => RenderCollection(gui, el, grid: true);

    void RenderCollection(Gui gui, UiElement el, bool grid)
    {
        var items = CollectionItems(el);
        var enabled = IsEnabled(el);
        var list = ListFor(el, out var listKey);
        var key = StateKey(el, "selected");
        var selected = ListSelection(el, items, key);
        if (list.Begin(gui) is { } chosen && chosen != selected && chosen < items.Count)
        {
            selected = chosen;
            WriteBinding(el, "selected-index", chosen);
            WriteBinding(el, "selected-item", items[chosen]);
            Fire(el, "selection-changed");
        }

        state[key] = selected;
        var node = gui.StyledNode(el.Tag, el.Classes, el.Name, disabled: !enabled).Direction(Axis.Vertical);
        ApplyAttributeSize(node, el);
        UiStyleApplier.Apply(node, el.InlineStyle);

        using (node.Enter())
        {
            collectionFocusOwners.Add(node.Id);
            LogRect(gui, el, node);
            gui.ScrollY();
            DrawBackground(gui, el);
            var scroll = gui.GetScrollState(node.Id);
            list.Measure(gui, CollectionGeometry(gui, el, node, scroll, items.Count, grid));
            RenderRows(gui, el, items, new ItemContext(list, listKey, node.Id, selected, enabled, grid));
            if (gui.Pass == Pass.Pass2Render)
            {
                RevealCursor(list, scroll);
                focusExit = list.Exit ?? focusExit;
            }
        }
    }

    /// <summary>What every item of one collection shares while it renders.</summary>
    readonly record struct ItemContext(ListBehavior List, string Key, string NodeId, int Selected, bool Enabled,
        bool Grid);

    ListBehavior ListFor(UiElement el, out string key)
    {
        key = StateKey(el, "list");
        if (state.TryGetValue(key, out var existing) && existing is ListBehavior list)
        {
            return list;
        }

        list = new ListBehavior();
        state[key] = list;
        return list;
    }

    IList CollectionItems(UiElement el) => BoundValue(el, "items").Value switch
    {
        IList list => list,
        string => Array.Empty<object>(),
        IEnumerable sequence => sequence.Cast<object?>().ToList(),
        _ => Array.Empty<object>(),
    };

    /// <summary>
    /// The selected item: a bound <c>selected-index</c>, else the position of a bound <c>selected-item</c>, else the
    /// last selection, else the literal <c>selected-index</c>; -1 when none.
    /// </summary>
    int ListSelection(UiElement el, IList items, string key)
    {
        if (BoundSelection(el, items) is { } bound)
        {
            return bound;
        }

        if (state.TryGetValue(key, out var cached) && cached is int stored)
        {
            return stored;
        }

        return UiValue.TryFloat(Attr(el, "selected-index"), out var literal) ? (int)literal : -1;
    }

    /// <summary>The selection a bound <c>selected-index</c> or <c>selected-item</c> names, else null.</summary>
    int? BoundSelection(UiElement el, IList items)
    {
        var (indexBound, index) = BoundValue(el, "selected-index");
        if (indexBound)
        {
            return ToNumber(index) is { } i ? (int)i : -1;
        }

        var (itemBound, item) = BoundValue(el, "selected-item");
        return !itemBound ? null : item is null ? -1 : items.IndexOf(item);
    }

    /// <summary>
    /// Cell size, columns and visible rows from the style and last frame's scroll state, keeping the offset within the
    /// content so a shrunk collection does not scroll past its end. Before its first layout a list assumes the layout
    /// bounds, while a grid, whose columns depend on its width, lays out nothing and asks for another frame.
    /// </summary>
    ListGeometry CollectionGeometry(Gui gui, UiElement el, LayoutNode node, ScrollState? scroll, int count, bool grid)
    {
        if (gui.Pass != Pass.Pass1Build)
        {
            return default;
        }

        var (width, viewport) = ViewportSize(gui, scroll);
        var gap = Math.Max(node.Style.Gap, 0f);
        var (columns, cellWidth, rowHeight) = grid
            ? GridCells(gui, el, gap, width)
            : (1, width, StyleFloat(gui, el, "item-height", defaultItemHeight));
        var geometry = new ListGeometry(count, columns, cellWidth, Math.Max(rowHeight, 1f), gap, 0, -1, viewport);
        if (grid && scroll is not { ViewportSize.X: > 0f })
        {
            gui.RequestFrame();
            return geometry;
        }

        var offset = ClampOffset(scroll, Math.Max(0f, (geometry.Rows * geometry.Stride) - gap - viewport));
        var (first, last) = ListBehavior.VisibleRows(offset, viewport, geometry.Stride, geometry.Rows);
        return geometry with { FirstRow = first, LastRow = last };
    }

    /// <summary>Last frame's viewport, or the layout bounds before the first one is known.</summary>
    static (float Width, float Height) ViewportSize(Gui gui, ScrollState? scroll)
    {
        var bounds = gui.LayoutBounds;
        var viewport = scroll?.ViewportSize ?? Vector2.Zero;
        return (viewport.X > 0f ? viewport.X : bounds.W, viewport.Y > 0f ? viewport.Y : bounds.H);
    }

    /// <summary>The vertical scroll offset, pulled back to <paramref name="end"/> if content shrank.</summary>
    static float ClampOffset(ScrollState? scroll, float end)
    {
        if (scroll is null)
        {
            return 0f;
        }

        if (scroll.ScrollOffset.Y > end)
        {
            scroll.ScrollOffset = scroll.ScrollOffset with { Y = end };
        }

        return scroll.ScrollOffset.Y;
    }

    (int Columns, float CellWidth, float RowHeight) GridCells(Gui gui, UiElement el, float gap, float width)
    {
        var fixedColumns = NumberValue(el, "columns") is { } c ? (int)c : (int?)null;
        var cellWidth = StyleFloat(gui, el, "cell-width", defaultCellWidth);
        var columns = ListBehavior.Columns(fixedColumns, cellWidth, gap, width);
        if (fixedColumns is > 0)
        {
            cellWidth = Math.Max(1f, (width - (gap * (columns - 1))) / columns);
        }

        return (columns, cellWidth, StyleFloat(gui, el, "cell-height", cellWidth));
    }

    /// <summary>The laid-out rows between spacers that stand in for the rows above and below the viewport.</summary>
    void RenderRows(Gui gui, UiElement el, IList items, ItemContext context)
    {
        var geometry = context.List.Geometry;
        if (geometry.FirstRow > 0)
        {
            using (gui.Node(1f, (geometry.FirstRow * geometry.Stride) - geometry.Gap).Enter()) { }
        }

        for (var row = geometry.FirstRow; row <= geometry.LastRow; row++)
        {
            if (!context.Grid)
            {
                RenderItem(gui, el, items, row, context);
                continue;
            }

            using (gui.ItemKey(row))
            using (gui.Node(-1f, geometry.RowHeight).Direction(Axis.Horizontal).Gap(geometry.Gap).Enter())
            {
                var end = Math.Min((row + 1) * geometry.Columns, items.Count);
                for (var index = row * geometry.Columns; index < end; index++)
                {
                    RenderItem(gui, el, items, index, context);
                }
            }
        }

        var below = geometry.Rows - 1 - geometry.LastRow;
        if (below > 0)
        {
            using (gui.Node(1f, (below * geometry.Stride) - geometry.Gap).Enter()) { }
        }
    }

    /// <summary>
    /// One item: an <c>Item</c> node holding the template with the item in scope. Element state inside it is keyed by
    /// the item's index, so a template's fields and toggles keep their own values as rows scroll in and out.
    /// </summary>
    void RenderItem(Gui gui, UiElement el, IList items, int index, ItemContext context)
    {
        var geometry = context.List.Geometry;
        using var itemKey = gui.ItemKey(index);
        var node = gui.StyledNode(itemTag, null, null, modifiers: index == context.Selected ? selectedModifiers : [],
            disabled: !context.Enabled).Height(geometry.RowHeight);
        if (context.Grid)
        {
            node.Width(geometry.CellWidth);
        }
        else
        {
            node.ExpandWidth();
        }

        var (outerScope, outerKey) = (keyScope, autoKey);
        keyScope = $"{context.Key}[{index}]/";
        autoKey = 0;
        repeatScope.Push((Attr(el, "as") ?? "item", items[index]));
        try
        {
            using (node.Enter())
            {
                if (gui.Pass == Pass.Pass2Render)
                {
                    context.List.Item(gui, index, context.NodeId, context.Enabled);
                }

                foreach (var child in el.Children)
                {
                    RenderElement(gui, child);
                }
            }
        }
        finally
        {
            repeatScope.Pop();
            (keyScope, autoKey) = (outerScope, outerKey);
        }
    }

    /// <summary>Scrolls the row the cursor moved to into view for the next frame.</summary>
    static void RevealCursor(ListBehavior list, ScrollState? scroll)
    {
        if (list.RevealRow is not { } row || scroll is null)
        {
            return;
        }

        var geometry = list.Geometry;
        var offset = ListBehavior.Reveal(scroll.ScrollOffset.Y, geometry.Viewport, geometry.Stride, geometry.RowHeight,
            row);
        scroll.ScrollOffset = scroll.ScrollOffset with { Y = offset };
        scroll.ClampScrollPosition();
    }
}
