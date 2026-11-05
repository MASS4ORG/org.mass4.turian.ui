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
        var fraction = ProgressBehavior.Fraction(NumberValue(el, "value"), NumberValue(el, "min") ?? 0f,
            NumberValue(el, "max") ?? 1f);
        var vertical = IsVertical(el);
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
            var fill = ProgressBehavior.Fill(gui.CurrentNode.InnerRect, fraction, vertical, gui.Clock.Elapsed, chunk,
                speed);
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

    static bool IsVertical(UiElement el) =>
        string.Equals(Attr(el, "orientation"), "vertical", StringComparison.OrdinalIgnoreCase);

    float StyleFloat(Gui gui, UiElement el, string name, float fallback) =>
        UiValue.TryFloat(Style(gui, el, name), out var value) ? value : fallback;
}
