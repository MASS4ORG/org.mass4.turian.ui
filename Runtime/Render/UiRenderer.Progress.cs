namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    static readonly string[] indeterminateModifiers = ["indeterminate"];
    static readonly string[] verticalModifiers = ["vertical"];
    static readonly string[] indeterminateVerticalModifiers = ["indeterminate", "vertical"];

    /// <summary>Fraction of the track an indeterminate bar's moving chunk covers when its style names none.</summary>
    const float defaultIndeterminateSize = 0.3f;

    /// <summary>Track lengths an indeterminate chunk travels per second when its style names none.</summary>
    const float defaultIndeterminateSpeed = 0.7f;

    /// <summary>
    /// A <c>&lt;ProgressBar&gt;</c>: the element's rule styles the track and its <c>fill</c> part the filled span, as
    /// in <c>ProgressBar fill { background-color = #4caf50; }</c>. Without a value the bar is <c>:indeterminate</c> and
    /// animates a chunk sized and paced by the <c>indeterminate-size</c> and <c>indeterminate-speed</c> properties.
    /// </summary>
    void RenderProgressBar(Gui gui, UiElement el)
    {
        var fraction = ProgressFraction(NumberValue(el, "value"), NumberValue(el, "min") ?? 0f,
            NumberValue(el, "max") ?? 1f);
        var vertical = string.Equals(Attr(el, "orientation"), "vertical", StringComparison.OrdinalIgnoreCase);
        var node = gui.StyledNode(el.Tag, el.Classes, el.Name, modifiers: ProgressModifiers(fraction is null, vertical));
        ApplyAttributeSize(node, el);
        UiStyleApplier.Apply(node, el.InlineStyle);

        using (node.Enter())
        {
            LogRect(gui, el, node);
            DrawBackground(gui, el);
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            if (fraction is null)
            {
                gui.RequestFrame();
            }

            var chunk = StyleFloat(gui, el, "indeterminate-size", defaultIndeterminateSize);
            var speed = StyleFloat(gui, el, "indeterminate-speed", defaultIndeterminateSpeed);
            var fill = ProgressFill(gui.CurrentNode.InnerRect, fraction, vertical, gui.Clock.Elapsed, chunk, speed);
            if (fill.W > 0f && fill.H > 0f)
            {
                gui.DrawStyledBox(gui.ResolvePart("fill", StyleState.None, null), fill);
            }
        }
    }

    static string[] ProgressModifiers(bool indeterminate, bool vertical) => (indeterminate, vertical) switch
    {
        (true, true) => indeterminateVerticalModifiers,
        (true, false) => indeterminateModifiers,
        (false, true) => verticalModifiers,
        _ => [],
    };

    /// <summary>Completion in 0..1 of <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>,
    /// or <c>null</c> for an indeterminate bar.</summary>
    internal static float? ProgressFraction(float? value, float min, float max)
    {
        if (value is not { } v)
        {
            return null;
        }

        var range = max - min;
        return range > 0f ? Math.Clamp((v - min) / range, 0f, 1f) : v >= max ? 1f : 0f;
    }

    /// <summary>
    /// The filled span of <paramref name="track"/>: anchored left (or bottom when <paramref name="vertical"/>) for a
    /// known fraction, or a chunk of <paramref name="chunk"/> track lengths that sweeps across, clipped at both ends.
    /// </summary>
    internal static Rect ProgressFill(Rect track, float? fraction, bool vertical, float elapsed, float chunk,
        float speed)
    {
        var (start, end) = fraction is { } f ? (0f, f) : SweepSpan(elapsed, Math.Clamp(chunk, 0f, 1f), speed);
        if (!vertical)
        {
            return new Rect(track.X + (track.W * start), track.Y, track.W * (end - start), track.H);
        }

        return new Rect(track.X, track.Y + (track.H * (1f - end)), track.W, track.H * (end - start));
    }

    static (float Start, float End) SweepSpan(float elapsed, float chunk, float speed)
    {
        var span = 1f + chunk;
        var head = (elapsed * speed % span) - chunk;
        var start = Math.Max(head, 0f);
        return (start, Math.Max(Math.Min(head + chunk, 1f), start));
    }

    /// <summary>A numeric attribute, literal or bound, or <c>null</c> when absent or not a number.</summary>
    float? NumberValue(UiElement el, string attribute)
    {
        var (bound, value) = BoundValue(el, attribute);
        return bound ? ToNumber(value) : UiValue.TryFloat(Attr(el, attribute), out var parsed) ? parsed : null;
    }

    internal static float? ToNumber(object? value) => value switch
    {
        float f => f,
        double d => (float)d,
        int i => i,
        long l => l,
        decimal m => (float)m,
        string s => UiValue.TryFloat(s, out var parsed) ? parsed : null,
        _ => null,
    };

    float StyleFloat(Gui gui, UiElement el, string name, float fallback) =>
        UiValue.TryFloat(Style(gui, el, name), out var value) ? value : fallback;
}
