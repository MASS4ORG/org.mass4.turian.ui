using System.CommandLine;
using Turian.Cli.Sdk;
using Turian.Cli.UI;

namespace Turian.Tests;

/// <summary>The UI brick contributes its headless <c>ui</c> command through the shared CLI contract.</summary>
public sealed class UiCliPluginTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), $"turian-ui-cli-{Guid.NewGuid():N}");

    /// <summary>Creates a scratch folder.</summary>
    public UiCliPluginTests() => Directory.CreateDirectory(folder);

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(folder, recursive: true);

    static Command UiCommandOf(UiCliPlugin plugin) =>
        Assert.Single(plugin.CreateCommands(new CliPluginContext("unused", null, NullLogger.Instance))).Command;

    /// <summary>The plugin registers <c>ui</c> at the root, requiring a document and taking the render options.</summary>
    [Fact]
    public void PluginContributesTheUiCommand()
    {
        var registration = Assert.Single(new UiCliPlugin()
            .CreateCommands(new CliPluginContext("unused", null, NullLogger.Instance)));

        Assert.Empty(registration.ParentPath);
        Assert.Equal("ui", registration.Command.Name);
        var root = new RootCommand { registration.Command };
        Assert.NotEmpty(root.Parse(["ui"]).Errors);
        Assert.Empty(root.Parse(["ui", "--file", "menu.ui", "--width", "640", "--height", "480", "--out", "menu.png"])
            .Errors);
    }

    /// <summary>
    /// Running the command renders the document with bound data, from a file or inline, and writes a PNG. The file
    /// has its own option because the CLI host expands <c>@path</c> tokens as response files.
    /// </summary>
    [Fact]
    public void CommandRendersTheDocumentToAPng()
    {
        // The CLI loads the brick before asking it for commands; here the renderer's assembly is already referenced.
        Assert.NotNull(typeof(UiManagerFactory).Assembly);
        var document = Path.Combine(folder, "hud.ui");
        File.WriteAllText(document, """
            <UI xmlns="https://turian.mass4.org/ui"><VisualElement><Label text="Score {Score}"/></VisualElement></UI>
            """);
        var data = Path.Combine(folder, "hud.json");
        File.WriteAllText(data, """{ "Score": 7 }""");
        var output = Path.Combine(folder, "hud.png");

        var exit = new RootCommand { UiCommandOf(new UiCliPlugin()) }
            .Parse(["ui", "--file", document, "--data-file", data, "--width", "128", "--height", "64", "--out", output])
            .Invoke();

        Assert.Equal(0, exit);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], File.ReadAllBytes(output)[..4]);

        var inline = Path.Combine(folder, "inline.png");
        Assert.Equal(0, new RootCommand { UiCommandOf(new UiCliPlugin()) }
            .Parse(["ui", "--file", document, "--data", """{ "Score": 3 }""", "--out", inline]).Invoke());
        Assert.True(File.Exists(inline));
    }
}
