namespace Turian.Tests;

public sealed partial class UiBindingTests
{
    /// <summary>Disabled and hidden variable-bound buttons reject input, while visibility also removes their drawing.</summary>
    [Fact]
    public void ButtonVariablesControlInteractionAndVisibility()
    {
        using var session = new DataAssetSession();
        var channel = new DataAssetEvent();
        var enabled = new DataAssetVariableBool();
        var visible = new DataAssetVariableBool { DefaultValue = true };
        foreach (var asset in new DataAsset[] { channel, enabled, visible }) { session.Attach(asset); }
        var clicks = 0;
        using var listener = channel.Subscribe(() => clicks++);
        var button = new ButtonComponent { Element = "go", OnClick = [channel], EnabledVariable = enabled, VisibleVariable = visible };
        var document = UiXmlParser.Parse("<UI><Button name=\"go\" text=\"Go\" width=\"120\" height=\"36\" /></UI>");
        var renderer = new UiRenderer(document);
        renderer.BindButtons([button]);
        var shown = RenderFrame(renderer, Mouse(30, 15, pressed: true));
        Assert.Equal(0, clicks);
        button.Click();
        Assert.Equal(0, clicks);
        enabled.Value = true;
        RenderFrame(renderer, Mouse(30, 15, pressed: true));
        Assert.Equal(1, clicks);
        visible.Value = false;
        var hidden = RenderFrame(renderer, Mouse(30, 15, pressed: true));
        Assert.Equal(1, clicks);
        Assert.False(shown.SequenceEqual(hidden));
        button.Click();
        Assert.Equal(1, clicks);
        button.EnabledVariable = null;
        button.VisibleVariable = null;
        Assert.True(button.IsEnabled);
        Assert.True(button.IsVisible);
        button.IsActive = false;
        Assert.False(button.IsEnabled);
        Assert.False(button.IsVisible);
    }
}
