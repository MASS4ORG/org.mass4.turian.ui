namespace Turian.Tests;

/// <summary>The in-game UI shipped as the brick <c>org.mass4.turian.ui</c>.</summary>
public sealed class UiBrickTests : IDisposable
{
    const string brickId = TestBrick.Id;

    readonly string project = Path.Combine(Path.GetTempPath(), $"turian-ui-brick-{Guid.NewGuid():N}");

    /// <summary>Creates an empty project.</summary>
    public UiBrickTests()
    {
        Directory.CreateDirectory(Path.Combine(project, "Assets"));
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(project, recursive: true);

    /// <summary>The brick ships its own assembly prebuilt and the editor's importers apart; its packages come from the map.</summary>
    [Fact]
    public void BrickShipsPrebuiltAssemblies()
    {
        var brick = Install(brickId);

        var runtime = BrickAssemblies.RuntimeAssemblies(brick).Select(Path.GetFileNameWithoutExtension).ToList();
        Assert.Contains("Turian.Engine.UI", runtime);
        Assert.DoesNotContain("Turian.Editor.UI", runtime);
        Assert.DoesNotContain("Turian.Cli.UI", runtime);
        Assert.DoesNotContain("System.CommandLine", runtime);
        Assert.Equal(["Turian.Cli.UI", "Turian.Editor.UI"],
            BrickAssemblies.EditorAssemblies(brick).Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("System.CommandLine", brick.Manifest.Nuget.Keys);
        Assert.Contains(typeof(UiDocumentComponent).Assembly.GetReferencedAssemblies(), static r => r.Name == "Guinevere");
        Assert.Contains("SkiaSharp", brick.Manifest.Nuget.Keys);
        Assert.Contains("MASS4.Guinevere", brick.Manifest.Nuget.Keys);
    }

    /// <summary>
    /// The manifest pins the NuGet versions the brick's own prebuilt assemblies were compiled against, so a game
    /// restoring them can load what the brick references. Third-party assemblies copied beside them reference what
    /// their packages ship with.
    /// </summary>
    [Fact]
    public void ManifestPackagesMatchPrecastReferences()
    {
        var brick = Install(brickId);

        foreach (var path in BrickAssemblies.RuntimeAssemblies(brick)
                     .Where(static p => Path.GetFileName(p).StartsWith("Turian.", StringComparison.Ordinal)))
        {
            using var pe = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(path));
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.AssemblyReferences)
            {
                var reference = metadata.GetAssemblyReference(handle);
                var name = metadata.GetString(reference.Name);
                if (!brick.Manifest.Nuget.TryGetValue(name, out var pinned))
                {
                    continue;
                }

                var package = Version.Parse(pinned);
                Assert.True(reference.Version.Major == package.Major && reference.Version.Minor == package.Minor,
                    $"{Path.GetFileName(path)} references {name} {reference.Version}; the manifest pins {pinned}");
            }
        }
    }

    /// <summary>
    /// The prebuilt assemblies and the suite itself run on exactly the Guinevere the manifest pins, so a game never
    /// loads a different version than the one the brick was compiled and tested against.
    /// </summary>
    [Theory]
    [InlineData(typeof(Gui), "MASS4.Guinevere")]
    [InlineData(typeof(StylingExtensions), "MASS4.Guinevere.Styling")]
    public void GuinevereMatchesThePin(Type anchor, string package)
    {
        var brick = Install(brickId);
        var pinned = Version.Parse(brick.Manifest.Nuget[package]);
        var assembly = anchor.Assembly.GetName().Name;

        foreach (var path in BrickAssemblies.RuntimeAssemblies(brick).Concat(BrickAssemblies.EditorAssemblies(brick)))
        {
            using var pe = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(path));
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.AssemblyReferences)
            {
                var reference = metadata.GetAssemblyReference(handle);
                if (metadata.GetString(reference.Name) == assembly)
                {
                    Assert.Equal(pinned, Truncate(reference.Version));
                }
            }
        }

        Assert.Equal(pinned, Truncate(anchor.Assembly.GetName().Version!));
    }

    static Version Truncate(Version version) => new(version.Major, version.Minor, version.Build);

    /// <summary>The generated project of a game references the brick's assemblies and NuGet packages, and only when installed.</summary>
    [Fact]
    public void GeneratedProjectsReferenceTheBrick()
    {
        var settings = new BuildAppSettings { Title = "Game", ProjectAbsoluteDir = project };

        Assert.DoesNotContain(References(settings), static name => name == "Turian.Engine.UI");

        Install(brickId);
        var references = References(settings);
        Assert.Contains("Turian.Engine.UI", references);
        var packages = References(settings, "PackageReference");
        Assert.Contains("MASS4.Guinevere", packages);
        Assert.Contains("SkiaSharp.NativeAssets.Win32", packages);
    }

    /// <summary>A game lists the prebuilt assemblies in its type manifest so it registers their types at start.</summary>
    [Fact]
    public void TypeManifestListsPrecastAssemblies()
    {
        Install(brickId);

        var manifest = UserCodeTypeManifestGenerator.Generate(Path.Combine(project, "Assets"), NullLogger.Instance);

        Assert.Contains("Turian.Engine.UI", manifest.PrecastAssemblies);
        Assert.Contains("Turian.Engine.Vulkan", manifest.PrecastAssemblies);
        // The DataAsset-Driven dependency ships a precast assembly only after a Release build; a source checkout, as in
        // CI, has none to list.
        if (File.Exists(Path.Combine(TestBrick.Folder, "..", "org.mass4.turian.ddd", "Brick~", "Precast", "lib",
                "Turian.Engine.DataAssetDriven.dll")))
        {
            Assert.Contains("Turian.Engine.DataAssetDriven", manifest.PrecastAssemblies);
        }

        Assert.DoesNotContain("Turian.Editor.UI", manifest.PrecastAssemblies);
    }

    /// <summary>Loading a brick registers its types once, and the presenter factory is then found without naming the UI.</summary>
    [Fact]
    public void LoadedBrickProvidesThePresenterFactory()
    {
        var brick = Install(brickId);

        BrickAssemblies.Load([brick], NullLogger.Instance);

        Assert.NotNull(UiPresenters.Find());
        Assert.Equal(typeof(UiDocumentComponent), TypeRegistry.GetTypeOrThrow(new Guid("da4b14b7-dfd9-51a0-afd0-83f1a4c982ac")));
    }

    /// <summary>The brick renders a single document to a PNG on the CPU, which the command line's <c>ui</c> command uses.</summary>
    [Fact]
    public void BrickRendersDocumentPreviews()
    {
        var file = Path.Combine(project, "hud.ui");
        File.WriteAllText(file, "<UI xmlns=\"https://turian.mass4.org/ui\"><VisualElement><Label text=\"Score {Score}\"/></VisualElement></UI>");

        var png = new UiManagerFactory().RenderPng(file, 128, 64, "{ \"Score\": 7 }");

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
    }

    ResolvedPackage Install(string id)
    {
        var runtime = Path.GetFullPath(Path.Combine(TestBrick.Folder, "..", "org.mass4.turian.ddd"));
        new ProjectManifest
        {
            Dependencies =
            {
                [id] = $"file:{TestBrick.Folder}",
                ["org.mass4.turian.ddd"] = $"file:{runtime}"
            }
        }.Save(project);
        return ProjectPackages.Resolve(project).Packages.Single(p => p.Id == id);
    }

    static string[] References(IBuildAppSettings settings, string item = "Reference") =>
        [.. CsProjectGenerator.GenerateUserCode(settings, NullLogger.Instance).Items
            .Where(i => i.ItemType == item).Select(static i => i.Include)];
}
