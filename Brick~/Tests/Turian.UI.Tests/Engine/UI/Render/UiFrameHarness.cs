namespace Turian.Tests;

/// <summary>
/// Renders one document headlessly over several frames against a single <see cref="Gui"/>, so state kept on the Gui
/// or the renderer survives between them, with a clock that advances a fixed step per frame.
/// </summary>
sealed class UiFrameHarness
{
    readonly Gui gui = new();
    readonly Font font = Font.FromFamilyName("sans-serif", 14);

    /// <summary>Parses <paramref name="ui"/> and applies <paramref name="sheet"/> (PSS) when given.</summary>
    public UiFrameHarness(string ui, string? sheet = null, int width = 200, int height = 120)
    {
        Renderer = new UiRenderer(UiXmlParser.Parse(ui));
        if (sheet is not null)
        {
            Renderer.StyleSheets.Add(StyleSheet.Parse(sheet, UiStyleSheetAsset.ParseOptions));
        }

        Width = width;
        Height = height;
        gui.Platform.Register<ITimeCapability>(Clock);
    }

    /// <summary>The renderer under test.</summary>
    public UiRenderer Renderer { get; }

    /// <summary>The clock the Gui reads; advanced by <see cref="ManualClock.Step"/> before each frame.</summary>
    public ManualClock Clock { get; } = new();

    /// <summary>Surface width in pixels.</summary>
    public int Width { get; }

    /// <summary>Surface height in pixels.</summary>
    public int Height { get; }

    /// <summary>Renders one frame per input and returns the last frame's RGBA pixels.</summary>
    public byte[] Frames(params IInputHandler[] frames)
    {
        byte[] pixels = [];
        foreach (var input in frames)
        {
            pixels = Frame(input);
        }

        return pixels;
    }

    /// <summary>Renders one frame, both passes, and returns its RGBA pixels.</summary>
    public byte[] Frame(IInputHandler? input = null)
    {
        Clock.Elapsed += Clock.Step;
        Clock.Frames++;

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        gui.Input = input ?? Pointer(-1, -1);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas, font, font);
        Renderer.Render(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Renderer.Render(gui);
        gui.Render();
        gui.EndFrame();
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        return [.. pixmap.GetPixelSpan()];
    }

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public (byte R, byte G, byte B, byte A) At(byte[] pixels, int x, int y)
    {
        var i = ((y * Width) + x) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    /// <summary>Whether the pixel at (<paramref name="x"/>, <paramref name="y"/>) is close to an opaque color.</summary>
    public bool IsColor(byte[] pixels, int x, int y, byte r, byte g, byte b)
    {
        var (pr, pg, pb, pa) = At(pixels, x, y);
        return Math.Abs(pr - r) < 40 && Math.Abs(pg - g) < 40 && Math.Abs(pb - b) < 40 && pa > 200;
    }

    /// <summary>Whether any pixel is close to an opaque color.</summary>
    public static bool Contains(byte[] pixels, byte r, byte g, byte b)
    {
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (Math.Abs(pixels[i] - r) < 40 && Math.Abs(pixels[i + 1] - g) < 40 && Math.Abs(pixels[i + 2] - b) < 40
                && pixels[i + 3] > 200)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A still pointer at (<paramref name="x"/>, <paramref name="y"/>), optionally pressing.</summary>
    public static IInputHandler Pointer(float x, float y, bool pressed = false)
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(x, y));
        input.PrevMousePosition.Returns(new Vector2(x, y));
        input.IsMouseButtonPressed(Arg.Any<GMouseButton>()).Returns(pressed);
        input.GetTypedCharacters().Returns(string.Empty);
        return input;
    }
}

/// <summary>A clock the test moves by hand.</summary>
sealed class ManualClock : ITimeCapability
{
    /// <summary>Seconds added before each harness frame.</summary>
    public float Step { get; set; } = 0.5f;

    /// <inheritdoc/>
    public float DeltaTime => Step;

    /// <inheritdoc/>
    public float Elapsed { get; set; }

    /// <inheritdoc/>
    public ulong Frames { get; set; }
}
