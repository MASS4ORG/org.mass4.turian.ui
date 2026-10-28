namespace Turian.Engine.UI;

/// <summary>Routes a named UI button through designer-assigned gameplay event assets.</summary>
[TypeId("28b94c47-30dd-4ceb-9313-9b3864df7ea1")]
[ComponentContextMenu("UI/Button")]
public sealed class ButtonComponent : Component
{
    /// <summary>The named button in the UI document on this node.</summary>
    public string Element { get; set; } = string.Empty;

    /// <summary>The designer-wired events raised in authored order when clicked.</summary>
    public List<DataAssetEvent> OnClick { get; set; } = [];

    /// <summary>An optional variable controlling whether this button accepts input.</summary>
    public DataAssetVariableBool? EnabledVariable { get; set; }

    /// <summary>An optional variable controlling whether this button is laid out and drawn.</summary>
    public DataAssetVariableBool? VisibleVariable { get; set; }

    /// <summary>The current enabled state, including the component lifecycle.</summary>
    [JsonIgnore]
    public bool IsEnabled => IsActive && !IsDestroyed && (EnabledVariable?.Value ?? true);

    /// <summary>The current visible state, including the component lifecycle.</summary>
    [JsonIgnore]
    public bool IsVisible => IsActive && !IsDestroyed && (VisibleVariable?.Value ?? true);

    /// <summary>Raises the wired channels directly, with no reflective or method-name dispatch.</summary>
    public void Click()
    {
        if (!IsEnabled || !IsVisible) { return; }
        foreach (var channel in OnClick) { channel.Raise(); }
    }
}
