namespace Turian.Engine.UI;

/// <summary>
/// Look-free progress geometry: completion of a value between bounds and the filled span of a track, including the
/// indeterminate sweep. Kept in the brick while Guinevere core has no progress behavior.
/// </summary>
static class ProgressBehavior
{
    /// <summary>Completion in 0..1 of <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>,
    /// or <c>null</c> for an indeterminate bar.</summary>
    public static float? Fraction(float? value, float min, float max)
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
    public static Rect Fill(Rect track, float? fraction, bool vertical, float elapsed, float chunk, float speed)
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
}
