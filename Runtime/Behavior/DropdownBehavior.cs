namespace Turian.Engine.UI;

/// <summary>
/// Look-free dropdown behavior for one dropdown: the focused button opens and closes its option list with a press,
/// Accept or Cancel; while open, directional keys or semantic (gamepad) directions move a highlight that Accept picks,
/// and while closed they change the selection directly; a press outside the button and the list dismisses it. Kept in
/// the brick while Guinevere core has no popup or listbox behavior.
/// </summary>
/// <remarks>
/// Input is read in the render pass and applied at the next build pass, so both passes of a frame build the same
/// tree and the list is placed from one stable anchor.
/// </remarks>
sealed class DropdownBehavior
{
    bool? requestedOpen;
    int? requestedSelection;

    /// <summary>Whether the option list shows this frame.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The option the keyboard or gamepad cursor is on while open.</summary>
    public int Highlight { get; private set; } = -1;

    /// <summary>The button's screen rect as last laid out.</summary>
    public Rect ButtonRect { get; private set; }

    /// <summary>The list's screen rect as last laid out.</summary>
    public Rect ListRect { get; set; }

    /// <summary>The button rect both passes of this frame place the list from.</summary>
    public Rect Anchor { get; private set; }

    /// <summary>
    /// Starts a frame: in the build pass applies the previous frame's requests and fixes the anchor, returning the
    /// newly chosen option; <c>null</c> otherwise.
    /// </summary>
    public int? Begin(Gui gui, bool enabled)
    {
        if (gui.Pass != Pass.Pass1Build)
        {
            return null;
        }

        Anchor = ButtonRect;
        IsOpen = enabled && (requestedOpen ?? IsOpen);
        var chosen = requestedSelection;
        requestedOpen = null;
        requestedSelection = null;
        return chosen;
    }

    /// <summary>Handles the button in the render pass; its current node is the button.</summary>
    /// <param name="gui">The GUI rendering the dropdown.</param>
    /// <param name="enabled">Whether the dropdown may be operated.</param>
    /// <param name="count">How many options there are.</param>
    /// <param name="selected">The selected option, or -1.</param>
    public void Button(Gui gui, bool enabled, int count, int selected)
    {
        gui.RegisterFocusable(canReceiveFocus: enabled, claimsArrowKeys: true);
        ButtonRect = gui.CurrentNode.LocalToScreen(gui.CurrentNode.Rect);
        if (!enabled)
        {
            return;
        }

        if (gui.GetInteractable().OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            Toggle(selected);
        }
        else if (gui.HasFocus())
        {
            Keys(gui, count, selected);
        }
    }

    /// <summary>Handles one option row in the render pass; its current node is the row.</summary>
    public void Option(Gui gui, int index)
    {
        var interactable = gui.GetInteractable();
        if (interactable.OnHover())
        {
            Highlight = index;
        }

        if (interactable.OnClick())
        {
            Choose(index);
        }
    }

    /// <summary>Closes the list on a press outside the button and the list, in the render pass.</summary>
    public void Dismiss(Gui gui)
    {
        if (IsOpen && gui.Input.IsMouseButtonPressed(MouseButton.Left)
            && !ButtonRect.Contains(gui.Input.MousePosition) && !ListRect.Contains(gui.Input.MousePosition))
        {
            requestedOpen = false;
        }
    }

    /// <summary>
    /// Below the anchor, or above it when <paramref name="size"/> does not fit underneath within
    /// <paramref name="bounds"/>.
    /// </summary>
    public static Vector2 ListPosition(Rect anchor, Vector2 size, Rect bounds)
    {
        var below = anchor.Y + anchor.H;
        var y = below + size.Y <= bounds.Y + bounds.H ? below : Math.Max(bounds.Y, anchor.Y - size.Y);
        return new Vector2(anchor.X, y);
    }

    void Keys(Gui gui, int count, int selected)
    {
        if (gui.IsAcceptPressed)
        {
            Accept(count, selected);
        }
        else if (IsOpen && gui.IsCancelPressed)
        {
            requestedOpen = false;
        }
        else
        {
            Move(NavigationStep(gui), count, selected);
        }
    }

    void Accept(int count, int selected)
    {
        if (IsOpen && Highlight >= 0 && Highlight < count)
        {
            Choose(Highlight);
        }
        else
        {
            Toggle(selected);
        }
    }

    static int NavigationStep(Gui gui) =>
        gui.IsNavigationPressed(FocusDirection.Down) ? 1 : gui.IsNavigationPressed(FocusDirection.Up) ? -1 : 0;

    /// <summary>Moves the highlight while open, or the selection itself while closed.</summary>
    void Move(int step, int count, int selected)
    {
        if (step == 0 || count == 0)
        {
            return;
        }

        if (IsOpen)
        {
            Highlight = Math.Clamp(Highlight + step, 0, count - 1);
        }
        else
        {
            requestedSelection = Math.Clamp(selected + step, 0, count - 1);
        }
    }

    void Toggle(int selected)
    {
        requestedOpen = !IsOpen;
        Highlight = selected;
    }

    void Choose(int index)
    {
        requestedSelection = index;
        requestedOpen = false;
    }
}
