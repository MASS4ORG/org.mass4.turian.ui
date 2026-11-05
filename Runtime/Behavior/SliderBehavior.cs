namespace Turian.Engine.UI;

/// <summary>
/// Look-free slider behavior for one slider: a press jumps the value to the pointer and keeps scrubbing until the
/// button is released, even outside the slider; while focused, directional keys or semantic (gamepad) directions
/// step it and Home/End reach the bounds; values snap to the step. Kept in the brick while Guinevere core has no
/// slider behavior.
/// </summary>
sealed class SliderBehavior
{
    /// <summary>Share of the range one key press moves a continuous slider (one with no step).</summary>
    const float continuousKeyStep = 0.01f;

    /// <summary>Whether the pointer captured the slider and is scrubbing.</summary>
    public bool Dragging { get; private set; }

    /// <summary>Stops scrubbing, as when the slider is disabled.</summary>
    public void Release() => Dragging = false;

    /// <summary>
    /// Registers the slider for focus and applies this frame's pointer and keys in the render pass, returning the
    /// new value.
    /// </summary>
    /// <param name="gui">The GUI rendering the slider; its current node is the slider.</param>
    /// <param name="area">The slider's value area in the current node's coordinates.</param>
    /// <param name="value">The value before this frame's input.</param>
    /// <param name="min">Lower bound.</param>
    /// <param name="max">Upper bound, not below <paramref name="min"/>.</param>
    /// <param name="step">Snap interval, or 0 to move continuously.</param>
    /// <param name="vertical">Whether the value grows upwards instead of to the right.</param>
    public float Update(Gui gui, Rect area, float value, float min, float max, float step, bool vertical)
    {
        gui.RegisterFocusable(canReceiveFocus: true, claimsArrowKeys: true);
        if (gui.GetInteractable().OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            Dragging = true;
        }

        if (!gui.Input.IsMouseButtonDown(MouseButton.Left))
        {
            Dragging = false;
        }

        if (Dragging)
        {
            value = ValueAt(gui.LocalMousePosition, area, min, max, step, vertical);
        }

        return gui.HasFocus() ? Stepped(gui, value, min, max, step, vertical) : value;
    }

    /// <summary>The value under <paramref name="pointer"/>, clamped to the bounds and snapped to the step.</summary>
    public static float ValueAt(Vector2 pointer, Rect area, float min, float max, float step, bool vertical)
    {
        var length = vertical ? area.H : area.W;
        if (length <= 0f)
        {
            return min;
        }

        var fraction = vertical ? (area.Y + area.H - pointer.Y) / length : (pointer.X - area.X) / length;
        return Snap(min + (Math.Clamp(fraction, 0f, 1f) * (max - min)), min, max, step);
    }

    /// <summary>The nearest multiple of <paramref name="step"/> above <paramref name="min"/>, within the bounds.</summary>
    public static float Snap(float value, float min, float max, float step)
    {
        if (step > 0f)
        {
            value = min + (MathF.Round((value - min) / step) * step);
        }

        return Math.Clamp(value, min, max);
    }

    static float Stepped(Gui gui, float value, float min, float max, float step, bool vertical)
    {
        var delta = step > 0f ? step : (max - min) * continuousKeyStep;
        if (gui.IsNavigationPressed(vertical ? FocusDirection.Down : FocusDirection.Left))
        {
            return Snap(value - delta, min, max, step);
        }

        if (gui.IsNavigationPressed(vertical ? FocusDirection.Up : FocusDirection.Right))
        {
            return Snap(value + delta, min, max, step);
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Home))
        {
            return min;
        }

        return gui.Input.IsKeyPressed(KeyboardKey.End) ? max : value;
    }
}
