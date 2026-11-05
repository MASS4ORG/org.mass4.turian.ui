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

    /// <summary>A tooltip's hover timing and the previous frame's geometry, which positions this frame's box.</summary>
    sealed class TooltipState
    {
        /// <summary>When the owner started being hovered or focused, or <c>null</c> while it is neither.</summary>
        public float? EngagedSince { get; set; }

        /// <summary>The owner's screen rect in the previous frame.</summary>
        public Rect Anchor { get; set; }

        /// <summary>The tooltip's size in the previous frame.</summary>
        public Vector2 Size { get; set; }

        /// <summary>
        /// The owner's node among its parent's children, recorded in the build pass: the render pass reuses that pass's
        /// nodes without appending them again, so only the index identifies the owner there.
        /// </summary>
        public int AnchorIndex { get; set; } = -1;
    }

    /// <summary>
    /// Renders <paramref name="el"/> and, when it has a <c>tooltip</c> attribute or a <c>&lt;Tooltip&gt;</c> child, the
    /// tooltip anchored to the node the element produced. Visibility comes from the previous frame's state, so both
    /// passes build the same tree; the render pass then records hover or focus for the next frame.
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
        var tip = TooltipStateFor(owner);
        var ownerNode = OwnerNode(gui, tip, parent, count);
        var delay = Math.Max(StyleFloat(gui, styleSource, "delay", defaultTooltipDelay), 0f);
        if (IsTooltipShown(gui, tip, delay))
        {
            DrawTooltip(gui, styleSource, content, text, tip);
        }

        if (ownerNode is not null)
        {
            TrackTooltip(gui, tip, ownerNode, delay);
        }
    }

    /// <summary>
    /// Records the owner's child index in the build pass and returns the owner's node in the render pass, where it
    /// is laid out; <c>null</c> in the build pass or when the owner produced no node.
    /// </summary>
    static LayoutNode? OwnerNode(Gui gui, TooltipState tip, LayoutNode parent, int count)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            tip.AnchorIndex = parent.Children.Count > count ? count : -1;
            return null;
        }

        return tip.AnchorIndex >= 0 && tip.AnchorIndex < parent.Children.Count ? parent.Children[tip.AnchorIndex] : null;
    }

    static bool IsTooltipShown(Gui gui, TooltipState tip, float delay) =>
        tip.EngagedSince is { } since && gui.Clock.Elapsed - since >= delay;

    string? TooltipText(UiElement el)
    {
        var (bound, value) = BoundValue(el, "tooltip");
        return bound ? value?.ToString() : Attr(el, "tooltip");
    }

    TooltipState TooltipStateFor(UiElement el)
    {
        var key = StateKey(el, "tooltip");
        if (state.TryGetValue(key, out var existing) && existing is TooltipState tip)
        {
            return tip;
        }

        tip = new TooltipState();
        state[key] = tip;
        return tip;
    }

    void DrawTooltip(Gui gui, UiElement styleSource, UiElement? content, string? text, TooltipState tip)
    {
        var position = TooltipPosition(tip.Anchor, tip.Size, gui.LayoutBounds,
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

    /// <summary>
    /// Below the owner, shifted by the offsets, flipped above it when there is no room underneath and kept inside
    /// <paramref name="bounds"/> horizontally.
    /// </summary>
    internal static Vector2 TooltipPosition(Rect anchor, Vector2 size, Rect bounds, float offsetX, float offsetY)
    {
        var x = Math.Clamp(anchor.X + offsetX, bounds.X, Math.Max(bounds.X, bounds.X + bounds.W - size.X));
        var below = anchor.Y + anchor.H + offsetY;
        var y = below + size.Y <= bounds.Y + bounds.H ? below : anchor.Y - size.Y - offsetY;
        return new Vector2(x, Math.Max(y, bounds.Y));
    }

    static void TrackTooltip(Gui gui, TooltipState tip, LayoutNode anchor, float delay)
    {
        var rect = anchor.LocalToScreen(anchor.Rect);
        tip.Anchor = rect;
        if (!rect.Contains(gui.Input.MousePosition) && !gui.HasFocusWithin(anchor.Id))
        {
            tip.EngagedSince = null;
            return;
        }

        var now = gui.Clock.Elapsed;
        tip.EngagedSince ??= now;
        var waited = now - tip.EngagedSince.Value;
        if (waited < delay)
        {
            gui.RequestFrameIn(delay - waited);
        }
    }
}
