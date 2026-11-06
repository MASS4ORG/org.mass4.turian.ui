using System.CommandLine;
using MASS4.Bricks;
using Microsoft.Extensions.Logging;
using Turian.Engine.Core;

namespace Turian.Cli.UI;

/// <summary>
/// The <c>turian-cli ui</c> command, which renders a <c>.ui</c> document to a PNG with the UI brick's preview renderer.
/// The CLI loads the brick, and with it the renderer, before it asks the brick for commands.
/// </summary>
public static class UiCommand
{
    /// <summary>Builds the command.</summary>
    /// <param name="logger">The host's logger, which reports the written file.</param>
    /// <returns>The <c>ui</c> command.</returns>
    public static Command Create(ILogger logger)
    {
        var outputOption = new Option<string>("--out")
        {
            Description = "PNG file to write",
            DefaultValueFactory = static _ => "ui.png",
        };
        var widthOption = new Option<int>("--width")
        {
            Description = "Output width in pixels",
            DefaultValueFactory = static _ => 1280,
        };
        var heightOption = new Option<int>("--height")
        {
            Description = "Output height in pixels",
            DefaultValueFactory = static _ => 720,
        };
        var fileOption = new Option<string?>("--file") { Description = "Render a .ui document", Required = true };
        var dataOption = new Option<string?>("--data")
        {
            Description = "Inline JSON object bound as the document's data context",
        };
        var dataFileOption = new Option<string?>("--data-file")
        {
            Description = "JSON file bound as the document's data context",
        };

        var command = new Command("ui", "Render a .ui document to a PNG (no scene, no GPU) with the in-game UI brick")
        {
            outputOption, widthOption, heightOption, fileOption, dataOption, dataFileOption,
        };
        command.SetAction(result =>
        {
            var preview = UiPresenters.FindPreview()
                          ?? throw new PackageException("The in-game UI brick is not available.");
            var dataFile = result.GetValue(dataFileOption);
            var data = dataFile is null ? result.GetValue(dataOption) : File.ReadAllText(dataFile);
            var width = result.GetValue(widthOption);
            var height = result.GetValue(heightOption);
            var png = preview.RenderPng(Path.GetFullPath(result.GetValue(fileOption)!), width, height, data);

            var output = Path.GetFullPath(result.GetValue(outputOption)!);
            File.WriteAllBytes(output, png);
            logger.LogInformation("Wrote {Width}×{Height} UI render: {OutputPath}", width, height, output);
        });
        return command;
    }
}
