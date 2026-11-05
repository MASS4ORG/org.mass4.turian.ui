namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    const string tooltipTag = "Tooltip";

    /// <summary>Seconds of hover or focus before a tooltip shows when its style names no <c>delay</c>.</summary>
    const float defaultTooltipDelay = 0.45f;

    /// <summary>Stacking order of tooltips when their style names no <c>z-index</c>; above popups.</summary>
    const int defaultTooltipLayer = 11_000;

    /// <summary>Style source of plain-text tooltips, which have no element of their own.</summary>
    static readonly UiElement textTooltip = new() { Tag = tooltipTag };

    /// <summary>
    /// Renders <paramref name="el"/> and, when it has a <c>tooltip</c> attribute or a <c>&lt;Tooltip&gt;</c> child, the
    /// tooltip anchored to the node the element produced, styled by the <c>Tooltip</c> rules (<c>delay</c>,
    /// <c>offset-x</c>, <c>offset-y</c> and <c>z-index</c> included).
    /// </summary>
    void RenderWithTooltip(Gui gui, UiElement el)
    {
        var content = el.Children.Find(static child => child.Tag == tooltipTag);
        var text = content is null ? TooltipText(el) : null;
        if (content is null && string.IsNullOrEmpty(text))
        {
            RenderWidget(gui, el);
            return;
        }

        var parent = gui.CurrentNode;
        var count = parent.Children.Count;
        RenderWidget(gui, el);
        RenderTooltip(gui, el, content, text, parent, count);
    }

    void RenderTooltip(Gui gui, UiElement owner, UiElement? content, string? text, LayoutNode parent, int count)
    {
        var styleSource = content ?? textTooltip;
        var tip = TooltipFor(owner);
        var ownerNode = tip.OwnerNode(gui, parent, count);
        var delay = Math.Max(StyleFloat(gui, styleSource, "delay", defaultTooltipDelay), 0f);
        if (tip.IsShown(gui.Clock.Elapsed, delay))
        {
            DrawTooltip(gui, styleSource, content, text, tip);
        }

        if (ownerNode is not null)
        {
            tip.Track(gui, ownerNode, delay);
        }
    }

    string? TooltipText(UiElement el)
    {
        var (bound, value) = BoundValue(el, "tooltip");
        return bound ? value?.ToString() : Attr(el, "tooltip");
    }

    TooltipBehavior TooltipFor(UiElement el)
    {
        var key = StateKey(el, "tooltip");
        if (state.TryGetValue(key, out var existing) && existing is TooltipBehavior tip)
        {
            return tip;
        }

        tip = new TooltipBehavior();
        state[key] = tip;
        return tip;
    }

    void DrawTooltip(Gui gui, UiElement styleSource, UiElement? content, string? text, TooltipBehavior tip)
    {
        var position = TooltipBehavior.Position(tip.Anchor, tip.Size, gui.LayoutBounds,
            StyleFloat(gui, styleSource, "offset-x", 0f), StyleFloat(gui, styleSource, "offset-y", 0f));
        var node = gui.StyledNode(tooltipTag, styleSource.Classes, styleSource.Name)
            .AbsoluteScreen(position.X, position.Y)
            .HitTestVisible(false);
        UiStyleApplier.Apply(node, styleSource.InlineStyle);

        using (node.Enter())
        {
            gui.SetZIndex((int)StyleFloat(gui, styleSource, "z-index", defaultTooltipLayer));
            gui.SetEscapesAncestorClips();
            DrawBackground(gui, styleSource);
            if (content is null)
            {
                DrawLabel(gui, styleSource, text ?? string.Empty);
            }
            else
            {
                foreach (var child in content.Children)
                {
                    RenderElement(gui, child);
                }
            }
        }

        if (gui.Pass == Pass.Pass2Render)
        {
            tip.Size = new Vector2(node.Rect.W, node.Rect.H);
        }
    }
}
