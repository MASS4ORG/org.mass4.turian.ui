namespace Turian.Engine.UI;

/// <summary>
/// Look-free tooltip behavior for one owner: engagement by hover or focus, the show delay, and placement below the
/// owner, flipped above it and kept on screen. Kept in the brick while Guinevere core has no tooltip behavior.
/// </summary>
/// <remarks>
/// Visibility is decided from the previous frame so both passes of a frame build the same tree; the render pass then
/// records engagement and geometry for the next frame.
/// </remarks>
sealed class TooltipBehavior
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

    /// <summary>Whether the owner has been engaged for at least <paramref name="delay"/> seconds by <paramref name="now"/>.</summary>
    public bool IsShown(float now, float delay) => EngagedSince is { } since && now - since >= delay;

    /// <summary>
    /// Records the owner's child index in the build pass and returns the owner's node in the render pass, where it
    /// is laid out; <c>null</c> in the build pass or when the owner produced no node.
    /// </summary>
    /// <param name="gui">The GUI rendering the owner.</param>
    /// <param name="parent">The node the owner was rendered under.</param>
    /// <param name="count">The parent's child count before the owner rendered.</param>
    public LayoutNode? OwnerNode(Gui gui, LayoutNode parent, int count)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            AnchorIndex = parent.Children.Count > count ? count : -1;
            return null;
        }

        return AnchorIndex >= 0 && AnchorIndex < parent.Children.Count ? parent.Children[AnchorIndex] : null;
    }

    /// <summary>Records the owner's rect and whether the pointer or focus engages it, for the next frame.</summary>
    public void Track(Gui gui, LayoutNode owner, float delay)
    {
        var rect = owner.LocalToScreen(owner.Rect);
        Anchor = rect;
        if (!rect.Contains(gui.Input.MousePosition) && !gui.HasFocusWithin(owner.Id))
        {
            EngagedSince = null;
            return;
        }

        var now = gui.Clock.Elapsed;
        EngagedSince ??= now;
        var waited = now - EngagedSince.Value;
        if (waited < delay)
        {
            gui.RequestFrameIn(delay - waited);
        }
    }

    /// <summary>
    /// Below the owner, shifted by the offsets, flipped above it when there is no room underneath and kept inside
    /// <paramref name="bounds"/> horizontally.
    /// </summary>
    public static Vector2 Position(Rect anchor, Vector2 size, Rect bounds, float offsetX, float offsetY)
    {
        var x = Math.Clamp(anchor.X + offsetX, bounds.X, Math.Max(bounds.X, bounds.X + bounds.W - size.X));
        var below = anchor.Y + anchor.H + offsetY;
        var y = below + size.Y <= bounds.Y + bounds.H ? below : anchor.Y - size.Y - offsetY;
        return new Vector2(x, Math.Max(y, bounds.Y));
    }
}
