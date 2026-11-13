namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    readonly List<(UiElement Element, string Id)> navigationControls = [];
    readonly HashSet<string> collectionFocusOwners = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> navigationNames = new(StringComparer.Ordinal);
    static readonly (FocusDirection Direction, string Attribute)[] directionAttributes =
    [
        (FocusDirection.Left, "focus-left"), (FocusDirection.Right, "focus-right"),
        (FocusDirection.Up, "focus-up"), (FocusDirection.Down, "focus-down")
    ];

    void RegisterNavigation(Gui gui, UiElement el, bool defaultFocusable = true, bool claimsArrowKeys = false)
    {
        var focusable = UiValue.TryBool(Attr(el, "focusable"), out var value) ? value : defaultFocusable;
        if (!focusable && !defaultFocusable)
        {
            return;
        }
        var enabled = IsEnabled(el) && focusable;
        gui.RegisterFocusable(enabled, enabled, claimsArrowKeys: claimsArrowKeys);
        navigationControls.Add((el, gui.CurrentNode.Id));
        if (el.Name is { Length: > 0 } name)
        {
            navigationNames[name] = gui.CurrentNode.Id;
        }
    }

    bool Activate(Gui gui, UiElement el)
    {
        RegisterNavigation(gui, el);
        if (gui.Pass != Pass.Pass2Render || !IsEnabled(el))
        {
            return false;
        }
        if (gui.GetInteractable().OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            return true;
        }
        return gui.HasFocus() && gui.IsAcceptPressed;
    }

    void FinishNavigation(Gui gui)
    {
        foreach (var (element, id) in navigationControls)
        {
            gui.Focus.SetNavigationLinks(id, FocusTarget(element, "focus-next"), FocusTarget(element, "focus-previous"));
            foreach (var (direction, attribute) in directionAttributes)
            {
                gui.Focus.SetDirectionalNeighbor(id, direction, FocusTarget(element, attribute));
            }
            NavigateExplicitTab(gui, element, id);
        }

        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }
        if (!gui.Focus.HasAnyFocus)
        {
            SetInitialFocus(gui);
        }
        CancelDocument(gui);
    }

    void CancelDocument(Gui gui)
    {
        if (gui.IsCancelPressed && cancelClaims == 0
            && (navigationControls.Exists(control => gui.HasFocus(control.Id))
                || collectionFocusOwners.Any(gui.HasFocusWithin))
            && document.CancelHandler is { } handler && Handlers.TryGetValue(handler, out var cancel))
        {
            cancel();
        }
    }

    void NavigateExplicitTab(Gui gui, UiElement element, string id)
    {
        var explicitTab = Attr(element, "focus-next") is not null || Attr(element, "focus-previous") is not null;
        gui.Focus.SetClaimsTab(id, explicitTab);
        if (gui.Pass != Pass.Pass2Render || !explicitTab || !gui.HasFocus(id)
            || !gui.Input.IsKeyPressed(KeyboardKey.Tab))
        {
            return;
        }
        var reverse = gui.Input.IsKeyDown(KeyboardKey.LeftShift) || gui.Input.IsKeyDown(KeyboardKey.RightShift);
        gui.Focus.Navigate(reverse ? FocusDirection.Previous : FocusDirection.Next);
    }

    void SetInitialFocus(Gui gui)
    {
        var preferred = document.InitialFocus is { } name ? navigationNames.GetValueOrDefault(name) : null;
        var available = navigationControls.Where(control => IsEnabled(control.Element)
            && (!UiValue.TryBool(Attr(control.Element, "focusable"), out var enabled) || enabled)).ToList();
        var initial = available.Find(control => control.Id == preferred);
        if (initial.Id is null)
        {
            initial = available.FirstOrDefault();
        }
        if (initial.Id is { } id)
        {
            gui.RequestFocus(id);
        }
    }

    string? FocusTarget(UiElement el, string attribute) =>
        Attr(el, attribute) is { } name ? navigationNames.GetValueOrDefault(name.TrimStart('#')) : null;

    void ExitSlider(Gui gui, bool vertical)
    {
        if (!gui.HasFocus())
        {
            return;
        }
        var previous = vertical ? FocusDirection.Left : FocusDirection.Up;
        var next = vertical ? FocusDirection.Right : FocusDirection.Down;
        if (gui.IsNavigationPressed(previous))
        {
            focusExit = previous;
        }
        else if (gui.IsNavigationPressed(next))
        {
            focusExit = next;
        }
    }

    void ExitDropdown(Gui gui)
    {
        if (!gui.HasFocus())
        {
            return;
        }
        if (gui.IsNavigationPressed(FocusDirection.Left))
        {
            focusExit = FocusDirection.Previous;
        }
        else if (gui.IsNavigationPressed(FocusDirection.Right))
        {
            focusExit = FocusDirection.Next;
        }
    }
}
