namespace Turian.Tests;

/// <summary>The editor finds this brick's importers in a project that installs it.</summary>
public sealed class BrickImporterTests : IDisposable
{
    readonly string project = Path.Combine(Path.GetTempPath(), $"turian-ui-importers-{Guid.NewGuid():N}");

    /// <summary>Creates an empty project.</summary>
    public BrickImporterTests() => Directory.CreateDirectory(Path.Combine(project, "Assets"));

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(project, recursive: true);

    /// <summary>An importer built before the project opened still finds the importers of the project's bricks.</summary>
    [Fact]
    public void ImporterBuiltEarlyGainsBrickImporters()
    {
        using var build = new BuildManager(new AppSettings(), NullLogger.Instance);
        var settings = new SettingsService();
        using var importer = new AssetImporter(NullLogger.Instance, new AssetDatabase(), settings);

        new ProjectManifest { Dependencies = { [TestBrick.Id] = $"file:{TestBrick.Folder}" } }.Save(project);
        settings.Set(new AppSettings { Title = "Game", ProjectAbsoluteDir = project });
        File.WriteAllText(Path.Combine(project, "Assets", "hud.ui"), "<ui/>");
        importer.GenerateMetaFiles(Path.Combine(project, "Assets"));

        Assert.IsType<UiDocumentAssetImporter>(importer.ImporterFor(Path.Combine(project, "Assets", "hud.ui")));
    }
}
