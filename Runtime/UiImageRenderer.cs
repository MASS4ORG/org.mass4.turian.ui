namespace Turian.Engine.UI;

/// <summary>
/// Renders a Guinevere UI straight to an image with no GPU involvement — for the Studio preview
/// panel, the <c>ui</c> CLI verb, tests, and golden-image comparisons. The realtime path is
/// <see cref="UiRuntime"/>.
/// </summary>
public static class UiImageRenderer
{
    /// <summary>
    /// Frames rendered at least: layout that measures the previous frame, such as a grid's columns or a scroll
    /// container's scrollbar space, settles on the second.
    /// </summary>
    const int minimumFrames = 2;

    /// <summary>Frames rendered at most, so an animation that always asks for another frame still ends.</summary>
    const int maximumFrames = 4;

    /// <summary>
    /// Runs two-pass UI frames until the layout settles (at least two, more while a frame is requested, at most four)
    /// and returns the last as a PNG. The clock advances once, so every frame shows the same moment.
    /// </summary>
    /// <param name="build">
    /// Builds the UI; invoked once per pass, so it must be idempotent.
    /// </param>
    /// <param name="width">Image width in pixels, greater than zero.</param>
    /// <param name="height">Image height in pixels, greater than zero.</param>
    /// <param name="background">Fill color; transparent by default.</param>
    /// <param name="deltaTime">Seconds since a notional previous frame, for time-based animation.</param>
    /// <param name="inputSource">Input source, or <c>null</c> for a neutral headless preview.</param>
    /// <returns>PNG-encoded bytes.</returns>
    public static byte[] RenderPng(
        Action<Gui> build,
        int width,
        int height,
        SKColor? background = null,
        float deltaTime = 0f,
        IInputSource? inputSource = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;

        var gui = new Gui { Input = new EngineInputHandler(inputSource) };
        var font = Font.FromFamilyName("sans-serif", 16);
        gui.Time.Update(deltaTime);

        for (var frame = 1; frame <= maximumFrames; frame++)
        {
            canvas.Clear(background ?? SKColors.Transparent);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(canvas, font, font);
            build(gui);
            gui.CalculateLayout();

            gui.SetStage(Pass.Pass2Render);
            build(gui);
            gui.Render();
            gui.EndFrame();

            if (frame >= minimumFrames && gui.FrameWaitSeconds > 0)
            {
                break;
            }
        }

        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
