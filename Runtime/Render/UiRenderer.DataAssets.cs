namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    readonly Dictionary<Guid, DataAsset> dataAssets = [];
    readonly Dictionary<UiElement, ButtonComponent> buttons = [];

    /// <summary>Binds hydrated asset identities for asset://GUID variables and event channels in a .ui document.</summary>
    public void BindDataAssets(IEnumerable<DataAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        dataAssets.Clear();
        foreach (var asset in assets) { dataAssets.Add(asset.Id, asset); }
        FoldExplicitBindings();
    }

    /// <summary>Connects named UI elements to component event lists using compiled calls.</summary>
    public void BindButtons(IEnumerable<ButtonComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        buttons.Clear();
        foreach (var button in components)
        {
            if (document.Find(button.Element) is { } element) { buttons.Add(element, button); }
        }
    }

    static bool AssetId(string path, out Guid id)
    {
        id = default;
        return path.StartsWith("asset://", StringComparison.Ordinal) && Guid.TryParse(path.AsSpan(8), out id);
    }

    bool ReadDataAsset(string path, out object? value)
    {
        value = null;
        if (!path.StartsWith("asset://", StringComparison.Ordinal)) { return false; }
        value = AssetId(path, out var id) && dataAssets.GetValueOrDefault(id) is IDataAssetValue variable
            ? variable.ReadValue() : null;
        return true;
    }

    bool WriteDataAsset(string path, object? value)
    {
        if (!path.StartsWith("asset://", StringComparison.Ordinal)) { return false; }
        if (AssetId(path, out var id) && dataAssets.GetValueOrDefault(id) is IDataAssetValue variable)
        {
            variable.TryWriteValue(value);
        }
        return true;
    }
}
