using System.Runtime.CompilerServices;

namespace Turian.Engine.UI;

/// <summary>
/// Places a Guinevere UI in a scene. The UI comes from a <c>.ui</c> <see cref="Document"/> asset,
/// or — for user scripts — from an <see cref="OnBuild"/> delegate set in code. The component
/// carries the document reference plus placement and scaling settings; a <see cref="UiManager"/>
/// finds it through the weak component registry.
/// </summary>
[DisallowMultipleComponent]
[ComponentContextMenu("UI/UI Document")]
[TypeId("da4b14b7-dfd9-51a0-afd0-83f1a4c982ac")]
public class UiDocumentComponent : Component
{
    static readonly ConditionalWeakTable<UiDocumentComponent, object> knownPanels = new();
    static readonly object registration = new();

    /// <summary>Tracks this panel without retaining its scene or depending on overridden lifecycle callbacks.</summary>
    public UiDocumentComponent() => knownPanels.Add(this, registration);

    internal static void Collect(Node root, List<UiDocumentComponent> into)
    {
        foreach (var entry in knownPanels)
        {
            var panel = entry.Key;
            if (panel.Node is { } node && ComponentIndex(node, panel) >= 0 && IsUnderRoot(node, root))
            {
                into.Add(panel);
            }
        }
        into.Sort(ComparePanels);
    }

    static bool IsUnderRoot(Node node, Node root)
    {
        for (Node? current = node; current is not null; current = current.Parent)
        {
            if (!current.IsActive) { return false; }
            if (ReferenceEquals(current, root)) { return true; }
        }
        return false;
    }

    static int ComponentIndex(Node node, Component component)
    {
        var components = node.Components;
        for (var i = 0; i < components.Count; i++)
        {
            if (ReferenceEquals(components[i], component)) { return i; }
        }
        return -1;
    }

    static int Depth(Node node)
    {
        var depth = 0;
        while (node.Parent is { } parent) { depth++; node = parent; }
        return depth;
    }

    static int ComparePanels(UiDocumentComponent left, UiDocumentComponent right)
    {
        var a = left.Node!;
        var b = right.Node!;
        if (ReferenceEquals(a, b)) { return ComponentIndex(a, left).CompareTo(ComponentIndex(b, right)); }
        var leftDepth = Depth(a);
        var rightDepth = Depth(b);
        var da = leftDepth;
        var db = rightDepth;
        while (da > db) { a = a.Parent!; da--; }
        while (db > da) { b = b.Parent!; db--; }
        if (ReferenceEquals(a, b)) { return leftDepth.CompareTo(rightDepth); }
        while (!ReferenceEquals(a.Parent, b.Parent)) { a = a.Parent!; b = b.Parent!; }
        return a.Parent!.Children.IndexOf(a).CompareTo(b.Parent!.Children.IndexOf(b));
    }

    /// <summary>The <c>.ui</c> document this panel renders. Empty when the UI is supplied via <see cref="OnBuild"/>.</summary>
    public AssetReference<UiDocumentAsset> Document { get; set; } = new();

    /// <summary>
    /// Extra <c>.uss</c> stylesheets applied on top of the ones the document declares with
    /// <c>&lt;Style src&gt;</c>, lowest priority first.
    /// </summary>
    public List<AssetReference<UiStyleSheetAsset>> StyleSheets { get; set; } = [];

    /// <summary>Where the UI is drawn. Defaults to a full-screen overlay.</summary>
    public UiRenderMode Mode { get; set; } = UiRenderMode.ScreenSpaceOverlay;

    /// <summary>Whether this panel is excluded from editor previews and the Scene viewer.</summary>
    [JsonIgnore, Hide]
    public virtual bool PlayModeOnly => false;

    /// <summary>
    /// Draw order among screen-space panels: lower values are composited first (further back).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>How the panel's logical size tracks the framebuffer size (screen-space modes).</summary>
    public UiScaleMode ScaleMode { get; set; } = UiScaleMode.ConstantPixelSize;

    /// <summary>Design resolution for <see cref="UiScaleMode.ScaleWithScreenSize"/>.</summary>
    public Int2 ReferenceResolution { get; set; } = new(1920, 1080);

    /// <summary>Render-target size in pixels for <see cref="UiRenderMode.WorldSpace"/>.</summary>
    public Int2 PanelSize { get; set; } = new(1024, 640);

    /// <summary>
    /// Builds the UI each frame. Invoked once per layout pass and once per render pass, so it must
    /// be idempotent (the standard immediate-mode contract). Not serialized — set it in code.
    /// </summary>
    [JsonIgnore, Hide]
    public Action<Gui>? OnBuild { get; set; }

    /// <summary>
    /// The asset database the document and its stylesheets are read from, or <c>null</c> outside a project, where
    /// only an <see cref="OnBuild"/> panel can draw.
    /// </summary>
    [InjectService(Optional = true), JsonIgnore, Hide]
    public AssetDatabase? Assets { get; set; }
}
