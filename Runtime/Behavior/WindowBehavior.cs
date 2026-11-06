namespace Turian.Engine.UI;

/// <summary>
/// Look-free movable window behavior for one window: dragging its title bar moves it within the bounds, a press inside
/// it or focus arriving in it raises it, Cancel closes it while focus is inside, and opening it moves focus to its
/// first focusable content. Kept in the brick while Guinevere core has no floating window behavior.
/// </summary>
/// <remarks>Close and raise requests read in the render pass are applied at the next build pass.</remarks>
sealed class WindowBehavior
{
    bool closeRequested;
    bool raiseRequested;
    bool? wasOpen;
    Vector2 dragStart;

    /// <summary>The window's top-left corner in screen coordinates.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Whether <see cref="Position"/> has been set from the document or by dragging.</summary>
    public bool Placed { get; set; }

    /// <summary>The window's size as last laid out.</summary>
    public Vector2 Size { get; private set; }

    /// <summary>Whether the title bar is being dragged.</summary>
    public bool Dragging { get; private set; }

    /// <summary>Whether the focused control is inside the window.</summary>
    public bool FocusWithin { get; private set; }

    /// <summary>In the build pass returns whether a close was requested last frame, clearing the request.</summary>
    public bool TakeClose(Gui gui)
    {
        if (gui.Pass != Pass.Pass1Build || !closeRequested)
        {
            return false;
        }

        closeRequested = false;
        return true;
    }

    /// <summary>In the build pass returns whether last frame asked to raise the window, clearing the request.</summary>
    public bool TakeRaise(Gui gui)
    {
        if (gui.Pass != Pass.Pass1Build || !raiseRequested)
        {
            return false;
        }

        raiseRequested = false;
        return true;
    }

    /// <summary>Asks for the window to close at the next build pass.</summary>
    public void Close() => closeRequested = true;

    /// <summary>Records that the window is closed, so opening it again moves focus into it.</summary>
    public void Closed()
    {
        wasOpen = false;
        Dragging = false;
    }

    /// <summary>
    /// Drags the window by its title bar in the render pass; its current node is the title bar. A press that starts on
    /// <paramref name="closeHovered"/> (the close button) does not start a drag.
    /// </summary>
    public void TitleBar(Gui gui, bool closeHovered, Rect bounds)
    {
        if (!Dragging && closeHovered)
        {
            return;
        }

        if (!gui.GetInteractable().OnDrag(out var drag))
        {
            Dragging = false;
            return;
        }

        if (!Dragging)
        {
            Dragging = true;
            dragStart = Position;
        }

        Position = Clamp(dragStart + drag.CurrentPosition - drag.Origin, Size, bounds);
    }

    /// <summary>Handles the window in the render pass after its content; its current node is the window.</summary>
    /// <param name="gui">The GUI rendering the window.</param>
    /// <param name="closable">Whether Cancel may close it.</param>
    /// <param name="popupOpen">Whether a popup inside it is open and owns Cancel.</param>
    public void Update(Gui gui, bool closable, bool popupOpen)
    {
        var node = gui.CurrentNode;
        Size = new Vector2(node.Rect.W, node.Rect.H);
        var within = TrackFocus(gui, node);
        if (wasOpen == false && !within)
        {
            FocusFirst(gui, node);
        }

        wasOpen = true;
        if (closable && within && !popupOpen && gui.IsCancelPressed)
        {
            closeRequested = true;
        }
    }

    /// <summary>
    /// Updates <see cref="FocusWithin"/> and asks to raise the window on a press inside it or when focus arrives in it;
    /// returns whether focus is inside.
    /// </summary>
    bool TrackFocus(Gui gui, LayoutNode node)
    {
        var pressed = gui.Input.IsMouseButtonPressed(MouseButton.Left) && gui.GetInteractable().OnHover();
        var focused = gui.Focus.CurrentFocusedId;
        var within = focused is not null && Contains(node, focused);
        raiseRequested |= pressed || (within && !FocusWithin);
        FocusWithin = within;
        return within;
    }

    /// <summary>
    /// Focuses the first focusable control inside <paramref name="window"/>. Requests run last to first because the
    /// focus manager keeps the last request it accepts and ignores nodes that are not focusable this frame.
    /// </summary>
    static void FocusFirst(Gui gui, LayoutNode window)
    {
        var ids = new List<string>();
        Collect(window, ids);
        for (var i = ids.Count - 1; i >= 0; i--)
        {
            gui.Focus.RequestFocus(ids[i], FocusReason.Keyboard);
        }
    }

    static void Collect(LayoutNode node, List<string> ids)
    {
        foreach (var child in node.Children)
        {
            ids.Add(child.Id);
            Collect(child, ids);
        }
    }

    static bool Contains(LayoutNode node, string id)
    {
        foreach (var child in node.Children)
        {
            if (child.Id == id || Contains(child, id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <paramref name="position"/> moved so a window of <paramref name="size"/> stays inside <paramref name="bounds"/>;
    /// a window larger than the bounds keeps its top-left corner inside them.
    /// </summary>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Rect bounds) => new(
        Math.Max(bounds.X, Math.Min(position.X, bounds.X + bounds.W - size.X)),
        Math.Max(bounds.Y, Math.Min(position.Y, bounds.Y + bounds.H - size.Y)));
}
