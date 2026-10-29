namespace Turian.Tests;

/// <summary>
/// Covers <see cref="UiManager"/>'s world-space panels and <c>.ui</c> document panels on a headless Vulkan device.
/// Skipped when no usable Vulkan device.
/// </summary>
[Collection(SerialTests.Name)]
public sealed class UiManagerTests : IClassFixture<VulkanFixture>, IDisposable
{
    readonly VulkanFixture fixture;
    readonly string directory = Directory.CreateTempSubdirectory("turian-ui-").FullName;
    readonly AssetDatabase database;

    /// <summary>Receives the shared headless Vulkan device fixture.</summary>
    public UiManagerTests(VulkanFixture fixture)
    {
        this.fixture = fixture;
        UiDocumentAsset.ClearCache();
        database = new AssetDatabase();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        UiDocumentAsset.ClearCache();
        Directory.Delete(directory, recursive: true);
    }

    /// <summary>A controller the test document binds, counting its updates.</summary>
    public sealed class CountingController : UiController
    {
        /// <summary>How many frames ticked this controller.</summary>
        public static int Updates { get; set; }

        /// <inheritdoc />
        public override void OnUpdate(float deltaTime) => Updates++;
    }

    /// <summary>Controllers resolve from the loaded assemblies by full name; an unknown name yields none.</summary>
    [Fact]
    public void ControllersResolveFromLoadedAssembliesOnly()
    {
        Assert.IsType<CountingController>(UiManager.CreateController(typeof(CountingController).FullName));
        Assert.Null(UiManager.CreateController("Missing.Controller"));
        Assert.Null(UiManager.CreateController(typeof(UiManagerTests).FullName));
        Assert.Null(UiManager.CreateController(null));
    }

    static (Node Root, UiDocumentComponent Panel) Scene(UiDocumentComponent panel, params Component[] extra)
    {
        var root = new Node { Name = "Root" };
        var child = new Node { Name = "Panel" };
        child.AddComponent(panel);
        foreach (var component in extra)
        {
            child.AddComponent(component);
        }

        child.Parent = root;
        root.Children.Add(child);
        root.Awake(null);
        return (root, panel);
    }

    static WorldUiFrame Frame()
    {
        var camera = Substitute.For<ICamera>();
        camera.GetViewMatrix().Returns(Matrix4x4.Identity);
        camera.GetProjectionMatrix().Returns(Matrix4x4.Identity);
        return new WorldUiFrame(camera, 256, 256, 0.016f);
    }

    Guid RegisterDocument(string xml)
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Directory.CreateDirectory(Path.Combine(directory, "Assets")).FullName, "panel.ui");
        File.WriteAllText(path, xml);
        Assert.True(database.RegisterAsset(new UiDocumentAsset { Id = id, RelativePath = path }, path));
        return id;
    }

    /// <summary>Each drawable world-space panel becomes a quad; screen-space and inactive panels do not.</summary>
    [Fact]
    public void RenderWorldPanels_RendersEachActiveWorldPanel()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var manager = new UiManager(fixture.Vulkan);
        var (root, panel) = Scene(
            new UiDocumentComponent { Mode = UiRenderMode.WorldSpace, PanelSize = new Int2(64, 32), OnBuild = static _ => { } },
            new UiRaycasterComponent { MaxDistance = 5f });
        var screen = new UiDocumentComponent { OnBuild = static _ => { } };
        root.AddComponent(screen);

        var quads = manager.RenderWorldPanels(root, Frame());
        Assert.Single(quads);
        Assert.Single(manager.RenderWorldPanels(root, Frame()));

        panel.IsActive = false;
        Assert.Empty(manager.RenderWorldPanels(root, Frame()));
    }

    /// <summary>A <c>.ui</c> document panel resolves once, binds its controller and ticks it every frame.</summary>
    [Fact]
    public void RenderOverlay_ResolvesDocumentPanels()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var documentId = RegisterDocument($"""
            <UI xmlns="https://turian.mass4.org/ui" controller="{typeof(CountingController).FullName}">
              <Style src="missing.uss" />
              <Label text="Hello" />
            </UI>
            """);
        using var manager = new UiManager(fixture.Vulkan);
        var (root, _) = Scene(new UiDocumentComponent
        {
            Document = new AssetReference<UiDocumentAsset>(documentId),
            Assets = database,
        });
        CountingController.Updates = 0;

        Assert.NotNull(manager.RenderOverlay(root, 128, 64, 0.016f));
        Assert.NotNull(manager.RenderOverlay(root, 128, 64, 0.016f));
        Assert.Equal(2, CountingController.Updates);
    }

    /// <summary>Play copies keep component IDs but clicks must target the new session's hydrated assets.</summary>
    [Fact]
    public void PlayCopyRebindsButtonsAcrossSessions()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);
        var documentId = RegisterDocument("<UI><Button name=\"go\" text=\"Go\" width=\"120\" height=\"36\" /></UI>");
        using var manager = new UiManager(fixture.Vulkan);
        var panelId = Guid.NewGuid();
        UiRenderer Resolve(DataAssetEvent channel)
        {
            var (_, panel) = Scene(new UiDocumentComponent
            {
                Id = panelId, Document = new(documentId), Assets = database,
            }, new ButtonComponent { Element = "go", OnClick = [channel] });
            var method = typeof(UiManager).GetMethod("ResolveDocumentBuilder",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var cached = method.Invoke(manager, [panel])!;
            return (UiRenderer)cached.GetType().GetProperty("Renderer")!.GetValue(cached)!;
        }
        _ = Resolve(new DataAssetEvent());
        for (var i = 0; i < 2; i++)
        {
            using var session = new DataAssetSession();
            var channel = new DataAssetEvent();
            session.Attach(channel);
            var calls = 0;
            using var listener = channel.Subscribe(() => calls++);
            var renderer = Resolve(channel);
            Click(renderer);
            Assert.Equal(1, calls);
        }
        _ = Resolve(new DataAssetEvent());
    }

    static void Click(UiRenderer renderer)
    {
        using var surface = SKSurface.Create(new SKImageInfo(160, 100));
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(30, 15));
        input.PrevMousePosition.Returns(new Vector2(30, 15));
        input.IsMouseButtonPressed(Arg.Any<GMouseButton>()).Returns(true);
        var gui = new Gui { Input = input };
        var font = Font.FromFamilyName("sans-serif", 14);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas, font, font);
        renderer.Render(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        renderer.Render(gui);
        gui.Render();
        gui.EndFrame();
    }

    /// <summary>Missing asset metadata or unreadable documents produce no renderer and can be retried.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableDocumentDoesNotCreateRenderer(bool registered)
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);
        var id = registered ? RegisterDocument("<UI>") : Guid.NewGuid();
        var panel = new UiDocumentComponent { Document = new(id), Assets = database };
        using var manager = new UiManager(fixture.Vulkan);
        var resolve = typeof(UiManager).GetMethod("ResolveDocumentBuilder",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Null(resolve.Invoke(manager, [panel]));
    }

    /// <summary>A document reference that resolves to nothing draws nothing.</summary>
    [Fact]
    public void RenderOverlay_UnresolvedDocumentDrawsNothing()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var manager = new UiManager(fixture.Vulkan);
        var (root, _) = Scene(new UiDocumentComponent
        {
            Mode = UiRenderMode.WorldSpace,
            Document = new AssetReference<UiDocumentAsset>(Guid.NewGuid()),
        });

        Assert.Empty(manager.RenderWorldPanels(root, Frame()));
    }
}
