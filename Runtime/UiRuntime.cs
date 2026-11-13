namespace Turian.Engine.UI;

/// <summary>
/// Drives one Guinevere <see cref="Gui"/> instance from the engine loop: runs the two-pass
/// build → layout → render cycle into a <see cref="IUiRenderBackend"/> and hands back the frame
/// as an engine <see cref="Texture"/>.
/// </summary>
/// <remarks>
/// One runtime owns one surface. The screen-space compositor uses a single runtime for every
/// overlay panel; a world-space panel gets its own. Fonts are resolved from the system family
/// list for now — loading fonts as project assets is a later step, so icon glyphs from the
/// FontAwesome range do not render yet.
/// </remarks>
public sealed class UiRuntime : IDisposable
{
    readonly UiNavigationInput input;
    readonly IUiRenderBackend backend;
    readonly Font font;
    readonly Font iconFont;
    readonly CanvasDisplay display = new();
    bool disposed;

    /// <summary>Creates a runtime with an initial surface size.</summary>
    /// <param name="vulkan">The shared Vulkan context.</param>
    /// <param name="width">Initial surface width in pixels, greater than zero.</param>
    /// <param name="height">Initial surface height in pixels, greater than zero.</param>
    /// <param name="backendMode">Render backend to use, or <c>null</c> for the factory default.</param>
    /// <param name="input">
    /// Custom frame handler for world-space panels; otherwise a screen-space handler is created.
    /// </param>
    /// <param name="inputSource">Screen-space input source, or <c>null</c> for neutral input.</param>
    public UiRuntime(
        Vulkan vulkan, int width, int height,
        UiRenderBackendFactory.Mode? backendMode = null,
        IUiFrameInput? input = null,
        IInputSource? inputSource = null)
    {
        ArgumentNullException.ThrowIfNull(vulkan);
        backend = UiRenderBackendFactory.Create(vulkan, width, height, backendMode);
        this.input = new UiNavigationInput(input ?? new EngineInputHandler(inputSource), inputSource);
        Gui.Input = this.input;
        Gui.Platform.Register<ISemanticInputCapability>(this.input);
        Gui.Platform.Register<IDisplayCapability>(display);
        font = Font.FromFamilyName("sans-serif", 16);
        iconFont = font;
    }

    /// <summary>The Guinevere context, for callers that need to read input/time or set scope values.</summary>
    public Gui Gui { get; } = new();

    /// <summary>The action-driven keyboard/gamepad bridge; bindings may be customized through its action service.</summary>
    public UiNavigationInput Navigation => input;

    /// <summary>The most recently rendered frame, or <c>null</c> before the first <see cref="Render"/>.</summary>
    public Texture? Texture => backend.Texture;

    /// <summary>Current surface size in pixels.</summary>
    public (int Width, int Height) Size => backend.Size;

    /// <summary>
    /// Runs one UI frame and publishes it to <see cref="Texture"/>.
    /// </summary>
    /// <param name="build">
    /// Builds the UI. Invoked twice — once for the layout pass and once for the render pass — so it
    /// must be idempotent, which is the normal immediate-mode contract.
    /// </param>
    /// <param name="width">Surface width in pixels for this frame, greater than zero.</param>
    /// <param name="height">Surface height in pixels for this frame, greater than zero.</param>
    /// <param name="deltaTime">Seconds since the previous frame, for <see cref="Gui"/> animations.</param>
    /// <param name="canvasScale">
    /// Uniform scale applied to the canvas before building, so a layout authored at a reference
    /// resolution fills a differently-sized surface. 1 means no scaling.
    /// </param>
    /// <returns>The rendered frame as a texture.</returns>
    public Texture Render(Action<Gui> build, int width, int height, float deltaTime, float canvasScale = 1f)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(build);

        backend.Resize(width, height);
        Gui.Time.Update(deltaTime);
        input.CanvasScale = canvasScale;
        input.BeginFrame(deltaTime, Gui.Focus.IsTextInputFocused);
        display.Update(width, height, canvasScale);

        backend.Render(canvas =>
        {
            if (canvasScale is not 1f)
            {
                canvas.Scale(canvasScale);
            }

            Gui.SetStage(Pass.Pass1Build);
            Gui.BeginFrame(canvas, font, iconFont);
            build(Gui);
            Gui.CalculateLayout();

            Gui.SetStage(Pass.Pass2Render);
            build(Gui);
            Gui.Render();
            Gui.EndFrame();
        });

        input.EndFrame();
        return backend.Texture!;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        backend.Dispose();
    }

    // Guinevere divides font sizes by the canvas scale unless the display reports the same factor, so text would
    // keep its pixel size while everything else grows with the reference resolution.
    sealed class CanvasDisplay : IDisplayCapability
    {
        public float ScaleFactor { get; private set; } = 1f;

        public Vector2 LogicalSize { get; private set; }

        public Vector2 FramebufferSize { get; private set; }

        public void Update(int width, int height, float scale)
        {
            ScaleFactor = float.IsFinite(scale) && scale > 0f ? scale : 1f;
            FramebufferSize = new Vector2(width, height);
            LogicalSize = FramebufferSize / ScaleFactor;
        }
    }
}
