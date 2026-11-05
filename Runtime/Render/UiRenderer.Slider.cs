namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    static readonly string[] draggingModifiers = ["dragging"];
    static readonly string[] verticalDraggingModifiers = ["vertical", "dragging"];

    /// <summary>
    /// A <c>&lt;Slider&gt;</c> with <c>min</c> (0), <c>max</c> (1), <c>step</c> (0, continuous) and a <c>value</c> that a
    /// two-way binding writes back, firing <c>value-changed</c>; <c>orientation="vertical"</c> grows upwards. The
    /// element's rule styles the box (<c>:vertical</c>, <c>:dragging</c>, <c>:disabled</c>) and its drawn parts
    /// <c>track</c>, <c>fill</c> and <c>thumb</c> (<c>:focus</c> while focused), each sized by its own rule.
    /// </summary>
    void RenderSlider(Gui gui, UiElement el)
    {
        var key = StateKey(el, "value");
        var (min, max) = SliderBounds(el);
        var step = Math.Max(NumberValue(el, "step") ?? 0f, 0f);
        var vertical = IsVertical(el);
        var enabled = IsEnabled(el);
        var current = SliderBehavior.Snap(SliderValue(el, key) ?? min, min, max, step);
        var slider = SliderFor(el);
        if (!enabled)
        {
            slider.Release();
        }

        var node = gui.StyledNode(el.Tag, el.Classes, el.Name, modifiers: SliderModifiers(vertical, slider.Dragging),
            disabled: !enabled);
        ApplyAttributeSize(node, el);
        UiStyleApplier.Apply(node, el.InlineStyle);

        var next = current;
        using (node.Enter())
        {
            LogRect(gui, el, node);
            DrawBackground(gui, el);
            if (gui.Pass == Pass.Pass2Render)
            {
                var area = gui.CurrentNode.InnerRect;
                if (enabled)
                {
                    next = slider.Update(gui, area, current, min, max, step, vertical);
                }

                DrawSliderParts(gui, area, ProgressBehavior.Fraction(next, min, max) ?? 0f, vertical);
            }
        }

        if (next != current)
        {
            WriteBinding(el, "value", next);
            Fire(el, "value-changed");
        }

        state[key] = next;
    }

    (float Min, float Max) SliderBounds(UiElement el)
    {
        var min = NumberValue(el, "min") ?? 0f;
        var max = NumberValue(el, "max") ?? 1f;
        return max < min ? (max, min) : (min, max);
    }

    float? SliderValue(UiElement el, string key)
    {
        var (bound, value) = BoundValue(el, "value");
        if (bound)
        {
            return ToNumber(value);
        }

        return state.TryGetValue(key, out var cached) && cached is float stored ? stored : NumberValue(el, "value");
    }

    SliderBehavior SliderFor(UiElement el)
    {
        var key = StateKey(el, "slider");
        if (state.TryGetValue(key, out var existing) && existing is SliderBehavior slider)
        {
            return slider;
        }

        slider = new SliderBehavior();
        state[key] = slider;
        return slider;
    }

    static string[] SliderModifiers(bool vertical, bool dragging) => (vertical, dragging) switch
    {
        (true, true) => verticalDraggingModifiers,
        (true, false) => verticalModifiers,
        (false, true) => draggingModifiers,
        _ => [],
    };

    /// <summary>
    /// The track across the area, the fill up to the value and the thumb centred on it. A part's thickness and the
    /// thumb's size come from its rule's <c>width</c> and <c>height</c>, defaulting to the area's thickness.
    /// </summary>
    static void DrawSliderParts(Gui gui, Rect area, float fraction, bool vertical)
    {
        var thickness = vertical ? area.W : area.H;
        var trackStyle = gui.ResolvePart("track", StyleState.None, null);
        var track = SliderBand(area, PartLength(trackStyle, vertical ? "width" : "height", thickness), vertical);
        gui.DrawStyledBox(trackStyle, track);

        var filled = vertical
            ? new Rect(track.X, track.Y + (track.H * (1f - fraction)), track.W, track.H * fraction)
            : new Rect(track.X, track.Y, track.W * fraction, track.H);
        gui.DrawStyledBox(gui.ResolvePart("fill", StyleState.None, null), filled);

        var thumbStyle = gui.ResolvePart("thumb", gui.HasFocus() ? StyleState.Focus : StyleState.None, null);
        var thumbW = PartLength(thumbStyle, "width", thickness);
        var thumbH = PartLength(thumbStyle, "height", thickness);
        var center = vertical
            ? new Vector2(area.X + (area.W / 2f), filled.Y)
            : new Vector2(filled.X + filled.W, area.Y + (area.H / 2f));
        gui.DrawStyledBox(thumbStyle, new Rect(center.X - (thumbW / 2f), center.Y - (thumbH / 2f), thumbW, thumbH));
    }

    /// <summary>A band of <paramref name="thickness"/> centred across <paramref name="area"/>.</summary>
    static Rect SliderBand(Rect area, float thickness, bool vertical)
    {
        if (vertical)
        {
            var w = Math.Min(thickness, area.W);
            return new Rect(area.X + ((area.W - w) / 2f), area.Y, w, area.H);
        }

        var h = Math.Min(thickness, area.H);
        return new Rect(area.X, area.Y + ((area.H - h) / 2f), area.W, h);
    }

    /// <summary>A part's length property in pixels, a percentage of <paramref name="basis"/>, or the basis itself.</summary>
    static float PartLength(ResolvedStyle style, string property, float basis) =>
        UiValue.TryLength(style.Get(property), out var length, out var percent)
            ? percent ? length * basis : length
            : basis;
}
