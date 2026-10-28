namespace Turian.Tests;

public sealed partial class UiBindingTests
{
    /// <summary>A button component delivers its designer-assigned channels without controller discovery.</summary>
    [Fact]
    public void ButtonComponentRaisesWiredAsset()
    {
        using var session = new DataAssetSession();
        var channel = new DataAssetEvent();
        session.Attach(channel);
        var calls = 0;
        using var listener = channel.Subscribe(() => calls++);
        var button = new ButtonComponent { Element = "go", OnClick = [channel] };
        var document = UiXmlParser.Parse("<UI><Button name=\"go\" text=\"Go\" width=\"120\" height=\"36\" /></UI>");
        var renderer = new UiRenderer(document);
        renderer.BindButtons([button]);
        RenderFrame(renderer, Mouse(30, 15, pressed: true));
        Assert.Equal(1, calls);
        button.IsActive = false;
        button.Click();
        Assert.Equal(1, calls);
    }

    /// <summary>A .ui GUID binding reads the live shared value after gameplay changes it.</summary>
    [Fact]
    public void DataAssetGuidBindingReadsLiveVariable()
    {
        using var session = new DataAssetSession();
        var variable = new DataAssetVariableInt { DefaultValue = 10 };
        session.Attach(variable);
        var document = UiXmlParser.Parse($"<UI><Label name=\"value\" text=\"{{asset://{variable.Id}}}\" /></UI>");
        var renderer = new UiRenderer(document);
        renderer.BindDataAssets([variable]);
        variable.Value = 10;
        var before = RenderFrame(renderer, Mouse(-1, -1));
        variable.Value = 200;
        var after = RenderFrame(renderer, Mouse(-1, -1));
        Assert.False(before.SequenceEqual(after));
    }

    /// <summary>A mismatched DataAsset write never falls through into the legacy binding context.</summary>
    [Fact]
    public void DataAssetTypeMismatchDoesNotUseLegacyBinding()
    {
        using var session = new DataAssetSession();
        var variable = new DataAssetVariableInt();
        session.Attach(variable);
        var identity = $"asset://{variable.Id}";
        var fallback = new Dictionary<string, object?> { [identity] = 7 };
        var document = UiXmlParser.Parse($"<UI><Toggle name=\"value\" value=\"{{{identity}, mode=TwoWay}}\" /></UI>");
        var renderer = new UiRenderer(document);
        renderer.Bind(fallback);
        renderer.BindDataAssets([variable]);
        RenderFrame(renderer, Mouse(20, 10, pressed: true));
        Assert.Equal(0, variable.Value);
        Assert.Equal(7, fallback[identity]);
    }

    /// <summary>A .ui event GUID raises the channel without a controller or method-name dispatch.</summary>
    [Fact]
    public void DataAssetGuidEventRaisesDirectly()
    {
        using var session = new DataAssetSession();
        var channel = new DataAssetEvent();
        session.Attach(channel);
        var calls = 0;
        using var handle = channel.Subscribe(() => calls++);
        var document = UiXmlParser.Parse($"<UI><Button name=\"go\" text=\"Go\" width=\"120\" height=\"36\" click=\"asset://{channel.Id}\" /></UI>");
        var renderer = new UiRenderer(document);
        renderer.BindDataAssets([channel]);
        RenderFrame(renderer, Mouse(30, 15, pressed: true));
        Assert.Equal(1, calls);
    }
}
