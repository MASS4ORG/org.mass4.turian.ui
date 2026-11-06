using Turian.Cli.Sdk;

namespace Turian.Cli.UI;

/// <summary>Contributes the UI brick's headless commands to <c>turian-cli</c>.</summary>
public sealed class UiCliPlugin : ICliPlugin
{
    /// <inheritdoc/>
    public IEnumerable<CliCommand> CreateCommands(CliPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return [new CliCommand(UiCommand.Create(context.Logger))];
    }
}
