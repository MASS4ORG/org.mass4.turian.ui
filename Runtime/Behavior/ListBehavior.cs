namespace Turian.Engine.UI;

/// <summary>
/// Look-free list and grid behavior for one collection: lays out only the rows inside the viewport, moves focus item to
/// item with directional keys or semantic (gamepad) directions, Home/End and Page Up/Down, scrolling the focused item
/// into view, selects with a press or Accept, and hands focus to the neighbouring control past an edge. Kept in the
/// brick while Guinevere core has no virtualized collection behavior.
/// </summary>
/// <remarks>
/// Geometry is fixed in the build pass and reused by the render pass, so both passes build the same rows; a selection
/// read in the render pass is applied at the next build pass.
/// </remarks>
sealed class ListBehavior
{
    int? requestedSelection;
    int? focusTarget;
    string? focusedItem;

    /// <summary>The item keyboard and gamepad focus is on, or last was.</summary>
    public int Cursor { get; private set; }

    /// <summary>This frame's layout of the collection.</summary>
    public ListGeometry Geometry { get; private set; } = ListGeometry.Empty;

    /// <summary>The row to scroll into view after this frame's input, if any.</summary>
    public int? RevealRow { get; private set; }

    /// <summary>The direction focus leaves the collection in, if an edge was crossed this frame.</summary>
    public FocusDirection? Exit { get; private set; }

    /// <summary>In the build pass returns the selection made by last frame's press or Accept, else null.</summary>
    public int? Begin(Gui gui)
    {
        if (gui.Pass != Pass.Pass1Build)
        {
            return null;
        }

        var chosen = requestedSelection;
        requestedSelection = null;
        return chosen;
    }

    /// <summary>Fixes this frame's geometry in the build pass; the render pass keeps it.</summary>
    public void Measure(Gui gui, ListGeometry geometry)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            Geometry = geometry;
            RevealRow = null;
            Exit = null;
        }
    }

    /// <summary>
    /// The item that takes focus from Tab or a neighbouring control: the cursor while it is laid out, else the first
    /// laid-out item.
    /// </summary>
    public int TabStop =>
        Cursor >= Geometry.FirstItem && Cursor <= Geometry.LastItem ? Cursor : Geometry.FirstItem;

    /// <summary>Handles one item in the render pass; its current node is the item.</summary>
    /// <param name="gui">The GUI rendering the item.</param>
    /// <param name="index">The item's index in the collection.</param>
    /// <param name="parentId">The collection's node id, the item's focus parent.</param>
    /// <param name="enabled">Whether the collection may be operated.</param>
    public void Item(Gui gui, int index, string parentId, bool enabled)
    {
        var focused = gui.HasFocus();
        gui.RegisterFocusable(enabled && (index == TabStop || focused), parentId: parentId, claimsArrowKeys: true);
        if (!enabled)
        {
            return;
        }

        if (gui.GetInteractable().OnClick())
        {
            Cursor = index;
            requestedSelection = index;
            gui.RegisterFocusable(parentId: parentId, claimsArrowKeys: true);
            gui.RequestFocus(FocusReason.Mouse);
            return;
        }

        if (focused)
        {
            focusedItem = gui.CurrentNode.Id;
            if (focusTarget is null)
            {
                Cursor = index;
            }

            Keys(gui, Cursor);
        }

        if (index == focusTarget)
        {
            Refocus(gui, focused);
        }
    }

    /// <summary>
    /// Requests focus for the cursor item until it has it; focus that moved elsewhere in the meantime cancels the
    /// request. The item a key left may be gone by now, which clears focus for a frame, so the request repeats.
    /// </summary>
    void Refocus(Gui gui, bool focused)
    {
        var current = gui.Focus.CurrentFocusedId;
        if (focused || (current is not null && current != focusedItem))
        {
            focusTarget = null;
            return;
        }

        gui.RequestFocus(FocusReason.Keyboard);
    }

    void Keys(Gui gui, int index)
    {
        var geometry = Geometry;
        if (gui.IsAcceptPressed)
        {
            requestedSelection = index;
            return;
        }

        if (Jump(gui.Input, index, geometry) is { } jump)
        {
            MoveTo(jump);
        }
        else if (Direction(gui) is { } direction)
        {
            var target = Neighbour(index, geometry.Count, geometry.Columns, direction);
            if (target < 0) { Exit = direction; }
            else { MoveTo(target); }
        }
    }

    /// <summary>Where a pressed Home, End, Page Up or Page Down moves from <paramref name="index"/>, if any.</summary>
    static int? Jump(IInputHandler input, int index, ListGeometry geometry)
    {
        var page = Math.Max(1, geometry.PageRows) * geometry.Columns;
        if (input.IsKeyPressed(KeyboardKey.Home)) { return 0; }
        if (input.IsKeyPressed(KeyboardKey.End)) { return geometry.Count - 1; }
        if (input.IsKeyPressed(KeyboardKey.PageUp)) { return Math.Max(index - page, 0); }
        return input.IsKeyPressed(KeyboardKey.PageDown) ? Math.Min(index + page, geometry.Count - 1) : null;
    }

    static FocusDirection? Direction(Gui gui)
    {
        foreach (var direction in (ReadOnlySpan<FocusDirection>)
                 [FocusDirection.Up, FocusDirection.Down, FocusDirection.Left, FocusDirection.Right])
        {
            if (gui.IsNavigationPressed(direction))
            {
                return direction;
            }
        }

        return null;
    }

    void MoveTo(int index)
    {
        if (index < 0)
        {
            return;
        }

        Cursor = index;
        focusTarget = index;
        RevealRow = index / Geometry.Columns;
    }

    /// <summary>
    /// The item next to <paramref name="index"/> in <paramref name="direction"/> on a grid of
    /// <paramref name="columns"/>, or -1 past an edge. Down onto a shorter last row lands on its last item.
    /// </summary>
    public static int Neighbour(int index, int count, int columns, FocusDirection direction) => direction switch
    {
        FocusDirection.Left => index % columns == 0 ? -1 : index - 1,
        FocusDirection.Right => index % columns == columns - 1 || index + 1 >= count ? -1 : index + 1,
        FocusDirection.Up => index < columns ? -1 : index - columns,
        FocusDirection.Down => index / columns >= (count - 1) / columns ? -1 : Math.Min(index + columns, count - 1),
        _ => -1,
    };

    /// <summary>The first and last rows a viewport at <paramref name="offset"/> shows, or (0, -1) for none.</summary>
    public static (int First, int Last) VisibleRows(float offset, float viewport, float stride, int rows)
    {
        if (rows <= 0 || stride <= 0f)
        {
            return (0, -1);
        }

        var first = Math.Clamp((int)(offset / stride), 0, rows - 1);
        var last = Math.Clamp((int)MathF.Ceiling((offset + viewport) / stride), first, rows - 1);
        return (first, last);
    }

    /// <summary>The scroll offset that brings <paramref name="row"/> fully into view with the least movement.</summary>
    public static float Reveal(float offset, float viewport, float stride, float rowHeight, int row)
    {
        var top = row * stride;
        if (top < offset)
        {
            return top;
        }

        var bottom = top + rowHeight;
        return bottom > offset + viewport ? bottom - viewport : offset;
    }

    /// <summary>
    /// Columns of a grid: <paramref name="fixedColumns"/> when given, else as many cells of
    /// <paramref name="cellWidth"/> as fit <paramref name="width"/>, at least one.
    /// </summary>
    public static int Columns(int? fixedColumns, float cellWidth, float gap, float width) =>
        fixedColumns is > 0 ? fixedColumns.Value : Math.Max(1, (int)((width + gap) / Math.Max(cellWidth + gap, 1f)));
}

/// <summary>One frame's layout of a list or grid: cell size, columns and the rows inside the viewport.</summary>
/// <param name="Count">How many items there are.</param>
/// <param name="Columns">Items per row; 1 for a list.</param>
/// <param name="CellWidth">Width of a grid cell; a list's items span the list.</param>
/// <param name="RowHeight">Height of a row.</param>
/// <param name="Gap">Space between rows and between a grid's cells.</param>
/// <param name="FirstRow">The first row laid out.</param>
/// <param name="LastRow">The last row laid out, below <paramref name="FirstRow"/> when there are none.</param>
/// <param name="Viewport">Height of the visible area.</param>
readonly record struct ListGeometry(int Count, int Columns, float CellWidth, float RowHeight, float Gap,
    int FirstRow, int LastRow, float Viewport)
{
    /// <summary>A collection with nothing to lay out.</summary>
    public static ListGeometry Empty { get; } = new(0, 1, 0f, 0f, 0f, 0, -1, 0f);

    /// <summary>Distance from one row's top to the next one's.</summary>
    public float Stride => RowHeight + Gap;

    /// <summary>How many rows the items fill.</summary>
    public int Rows => (Count + Columns - 1) / Columns;

    /// <summary>The first item laid out.</summary>
    public int FirstItem => FirstRow * Columns;

    /// <summary>The last item laid out.</summary>
    public int LastItem => Math.Min(((LastRow + 1) * Columns) - 1, Count - 1);

    /// <summary>How many whole rows fit the viewport.</summary>
    public int PageRows => Stride > 0f ? (int)(Viewport / Stride) : 1;
}
