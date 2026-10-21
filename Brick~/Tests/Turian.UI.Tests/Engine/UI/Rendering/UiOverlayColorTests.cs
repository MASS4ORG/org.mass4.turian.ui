namespace Turian.Tests;

/// <summary>Checks the CPU backend's texture as the renderer's overlay.</summary>
[Collection(SerialTests.Name)]
public sealed class UiOverlayColorTests(VulkanFixture fixture) : IClassFixture<VulkanFixture>
{
    const int size = 16;

    /// <summary>Skia colors survive texture sampling and framebuffer encoding unchanged in editor readback.</summary>
    [Fact]
    public void UiOverlay_PreservesSrgbColors()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);
        using var viewer = new SceneViewerService(fixture.Vulkan, new AssetDatabase(), size, size);
        using var backend = new CpuSkiaVulkanBackend(fixture.Vulkan, size, size);
        var color = new SKColor(31, 36, 48);
        backend.Render(canvas => canvas.Clear(color));
        viewer.OverlayTexture = backend.Texture;

        viewer.Render(new Node(), 0.016);
        var pixels = new byte[size * size * 4];
        viewer.CopyPixels(pixels);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            Assert.InRange(pixels[i], color.Blue - 1, color.Blue + 1);
            Assert.InRange(pixels[i + 1], color.Green - 1, color.Green + 1);
            Assert.InRange(pixels[i + 2], color.Red - 1, color.Red + 1);
        }
    }
}
