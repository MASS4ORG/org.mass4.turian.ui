namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    static readonly string[] focusWithinModifiers = ["focus-within"];
    static readonly string[] draggingFocusWithinModifiers = ["dragging", "focus-within"];

    /// <summary>Stacking order of the lowest window when its style names no <c>z-index</c>; below popups.</summary>
    const int defaultWindowLayer = 1_000;

    /// <summary>Style sources of the title text and the close button, which have no element of their own.</summary>
    static readonly UiElement windowTitle = new() { Tag = "Title" };
    static readonly UiElement windowClose = new() { Tag = "CloseButton" };

    /// <summary>Windows from bottom to top; a raised window moves to the end.</summary>
    readonly List<WindowBehavior> windowOrder = [];

    /// <summary>How many dropdown lists are open this pass, so a window leaves Cancel to a popup inside it.</summary>
    int openPopups;

    /// <summary>
    /// A movable <c>&lt;Window&gt;</c> at screen position <c>x</c>, <c>y</c> (two-way bindable) with a <c>TitleBar</c>
    /// holding its <c>Title</c> (the literal or bound <c>title</c>) and, when <c>closable</c>, a <c>CloseButton</c>
    /// (its <c>close-text</c>, × by default), above its children. Dragging the title bar moves it within the screen; a
    /// press or focus inside raises it above other windows. Closing (the button, or Cancel while focus is inside) sets
    /// a two-way bindable <c>open</c> to false and fires <c>close</c>. The element's rule styles the window
    /// (<c>:dragging</c>, <c>:focus-within</c>) and <c>z-index</c> sets the lowest window's layer.
    /// </summary>
    void RenderWindow(Gui gui, UiElement el)
    {
        var window = WindowFor(el);
        var openKey = StateKey(el, "open");
        var open = WindowOpen(el, openKey);
        if (open && window.TakeClose(gui))
        {
            open = false;
            WriteBinding(el, "open", false);
            Fire(el, "close");
        }

        state[openKey] = open;
        if (!open)
        {
            window.Closed();
            return;
        }

        PlaceWindow(gui, el, window);
        if (window.TakeRaise(gui))
        {
            windowOrder.Remove(window);
            windowOrder.Add(window);
        }

        var node = gui.StyledNode(el.Tag, el.Classes, el.Name, modifiers: WindowModifiers(window))
            .AbsoluteScreen(window.Position.X, window.Position.Y)
            .BlockInput();
        ApplyAttributeSize(node, el);
        UiStyleApplier.Apply(node, el.InlineStyle);

        var moved = window.Position;
        using (node.Enter())
        {
            LogRect(gui, el, node);
            gui.SetZIndex((int)StyleFloat(gui, el, "z-index", defaultWindowLayer) + windowOrder.IndexOf(window));
            DrawBackground(gui, el);
            RenderTitleBar(gui, el, window);

            var popups = openPopups;
            foreach (var child in el.Children)
            {
                RenderElement(gui, child);
            }

            if (gui.Pass == Pass.Pass2Render)
            {
                window.Update(gui, IsClosable(el), openPopups > popups);
            }
        }

        if (window.Position != moved)
        {
            WriteBinding(el, "x", window.Position.X);
            WriteBinding(el, "y", window.Position.Y);
        }
    }

    WindowBehavior WindowFor(UiElement el)
    {
        var key = StateKey(el, "window");
        if (state.TryGetValue(key, out var existing) && existing is WindowBehavior window)
        {
            return window;
        }

        window = new WindowBehavior();
        state[key] = window;
        windowOrder.Add(window);
        return window;
    }

    /// <summary>Whether it shows: a bound <c>open</c>, else its last state, else the literal, else open.</summary>
    bool WindowOpen(UiElement el, string key)
    {
        var (bound, value) = BoundValue(el, "open");
        if (bound)
        {
            return value is true;
        }

        if (state.TryGetValue(key, out var cached) && cached is bool open)
        {
            return open;
        }

        return !UiValue.TryBool(Attr(el, "open"), out var literal) || literal;
    }

    /// <summary>
    /// Takes the position from bound <c>x</c>/<c>y</c> every frame and from literal ones once, except while dragging,
    /// and keeps it within the screen.
    /// </summary>
    void PlaceWindow(Gui gui, UiElement el, WindowBehavior window)
    {
        if (gui.Pass != Pass.Pass1Build || window.Dragging)
        {
            return;
        }

        var position = new Vector2(WindowCoordinate(el, "x", window.Position.X, window.Placed),
            WindowCoordinate(el, "y", window.Position.Y, window.Placed));
        window.Placed = true;
        window.Position = WindowBehavior.Clamp(position, window.Size, gui.LayoutBounds);
    }

    /// <summary>A bound coordinate, else a literal one until first placed, else <paramref name="current"/>.</summary>
    float WindowCoordinate(UiElement el, string attribute, float current, bool placed)
    {
        var (bound, value) = BoundValue(el, attribute);
        if (bound)
        {
            return ToNumber(value) ?? current;
        }

        return placed ? current : NumberValue(el, attribute) ?? current;
    }

    static string[] WindowModifiers(WindowBehavior window) => (window.Dragging, window.FocusWithin) switch
    {
        (true, true) => draggingFocusWithinModifiers,
        (true, false) => draggingModifiers,
        (false, true) => focusWithinModifiers,
        _ => [],
    };

    static bool IsClosable(UiElement el) => UiValue.TryBool(Attr(el, "closable"), out var closable) && closable;

    void RenderTitleBar(Gui gui, UiElement el, WindowBehavior window)
    {
        using (gui.StyledNode("TitleBar").Direction(Axis.Horizontal).Enter())
        {
            using (gui.StyledNode(windowTitle.Tag).ExpandWidth().Enter())
            {
                DrawLabel(gui, windowTitle, WindowTitle(el) ?? string.Empty);
            }

            var closeHovered = IsClosable(el) && RenderCloseButton(gui, el, window);
            if (gui.Pass == Pass.Pass2Render)
            {
                window.TitleBar(gui, closeHovered, gui.LayoutBounds);
            }
        }
    }

    string? WindowTitle(UiElement el)
    {
        var (bound, value) = BoundValue(el, "title");
        return bound ? value?.ToString() : ResolveTextAttribute(el, "title");
    }

    /// <summary>The close button; returns whether the pointer is over it.</summary>
    bool RenderCloseButton(Gui gui, UiElement el, WindowBehavior window)
    {
        using (gui.StyledNode(windowClose.Tag).Enter())
        {
            DrawLabel(gui, windowClose, Attr(el, "close-text") ?? "×");
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            var interactable = gui.GetInteractable();
            if (interactable.OnClick())
            {
                window.Close();
            }

            return interactable.OnHover();
        }
    }
}
