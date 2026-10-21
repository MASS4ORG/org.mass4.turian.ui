namespace Turian.Tests;

/// <summary>This brick's checkout, which tests install into scratch projects as a <c>file:</c> source.</summary>
public static class TestBrick
{
    /// <summary>The brick id.</summary>
    public const string Id = "org.mass4.turian.ui";

    /// <summary>The brick's root folder: the nearest ancestor of the test output that holds its manifest.</summary>
    public static string Folder { get; } = Find();

    /// <summary>Renders one GUI frame in both passes, as the editor does.</summary>
    /// <param name="gui">The GUI.</param>
    /// <param name="surface">The surface drawn on.</param>
    /// <param name="font">The font of the frame.</param>
    /// <param name="render">Builds the frame.</param>
    public static void Frame(Gui gui, SKSurface surface, Font font, Action<Gui> render)
    {
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas, font, font);
        render(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        render(gui);
        gui.Render();
        gui.EndFrame();
    }

    static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var manifest = Path.Combine(directory.FullName, PackageManifest.FileName);
            if (File.Exists(manifest) && File.ReadAllText(manifest).Contains($"\"{Id}\"", StringComparison.Ordinal))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"The {Id} checkout is not an ancestor of {AppContext.BaseDirectory}.");
    }
}
