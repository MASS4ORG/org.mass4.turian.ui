using System.CommandLine;
using MASS4.Bricks;
using Microsoft.Extensions.Logging;
using Turian.Editor.Core;
using Turian.Engine.Core;

namespace Turian.Cli.UI;

/// <summary>
/// The <c>turian-cli ui</c> command, which renders a <c>.ui</c> document to a PNG with this brick. It is kept with the
/// brick, and not compiled, until the CLI loads commands from bricks.
/// </summary>
public static class UiCommand
{
    /// <summary>Builds the command for a CLI that runs this brick from <paramref name="brickFolder"/>.</summary>
    /// <param name="brickFolder">The installed brick's folder, holding its <c>Precast~</c> assemblies.</param>
    /// <returns>The <c>ui</c> command.</returns>
    public static Command Create(string brickFolder)
    {
        var outputOption = new Option<string>("--out") { Description = "PNG file to write", DefaultValueFactory = _ => "ui.png" };
        var widthOption = new Option<int>("--width") { Description = "Output width in pixels", DefaultValueFactory = _ => 1280 };
        var heightOption = new Option<int>("--height") { Description = "Output height in pixels", DefaultValueFactory = _ => 720 };
        var fileOption = new Option<string?>("--file") { Description = "Render a .ui document", Required = true };
        var dataOption = new Option<string?>("--data") { Description = "JSON object (inline or @path) bound as the document's data context" };

        var command = new Command("ui", "Render a .ui document to a PNG (no scene, no GPU) with the in-game UI brick")
        {
            outputOption, widthOption, heightOption, fileOption, dataOption,
        };
        command.SetAction(result =>
        {
            var brick = new ResolvedPackage("org.mass4.turian.ui",
                PackageManifest.Load(brickFolder, ["gaya", ProjectPackages.HostName]), brickFolder,
                PackageOrigin.File, $"file:{brickFolder}", null, null, 1, false);
            BrickAssemblies.Load([brick], Log.Logger);

            var preview = UiPresenters.FindPreview() ?? throw new PackageException("The in-game UI brick is not available.");
            var data = result.GetValue(dataOption);
            var png = preview.RenderPng(result.GetValue(fileOption)!, result.GetValue(widthOption), result.GetValue(heightOption),
                data is not null && data.StartsWith('@') ? File.ReadAllText(data[1..]) : data);

            var output = Path.GetFullPath(result.GetValue(outputOption)!);
            File.WriteAllBytes(output, png);
            Log.Logger.LogInformation("Wrote {Width}×{Height} UI render: {OutputPath}", result.GetValue(widthOption),
                result.GetValue(heightOption), output);
        });
        return command;
    }
}
