namespace Turian.Tests;

/// <summary>UI discovery follows component membership and hierarchy without per-node allocations.</summary>
public class UiDiscoveryTests
{
    static readonly Action<Node, List<UiDocumentComponent>> Collect = typeof(UiManager)
        .GetMethod("Collect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
        .CreateDelegate<Action<Node, List<UiDocumentComponent>>>();

    /// <summary>Discovery sees newly attached panels, removals, reparenting, activity and hierarchy reordering.</summary>
    [Fact]
    public void DiscoveryTracksMembershipAndHierarchyOrder()
    {
        var root = new Node();
        var other = new Node();
        var left = new Node();
        var right = new Node();
        root.Children = [left, right];
        var b = right.AddComponent(new UiDocumentComponent());
        var a = left.AddComponent(new UiDocumentComponent());
        root.Awake(null);
        var found = new List<UiDocumentComponent>();
        Collect(root, found);
        Assert.Equal([a, b], found);
        root.Children.Move(1, 0);
        found.Clear();
        Collect(root, found);
        Assert.Equal([b, a], found);
        left.IsActive = false;
        found.Clear();
        Collect(root, found);
        Assert.Equal([b], found);
        left.IsActive = true;
        other.Children.Add(right);
        found.Clear();
        Collect(root, found);
        Assert.Equal([a], found);
        var added = left.AddComponent(new FrameStatisticsHudComponent());
        found.Clear();
        Collect(root, found);
        Assert.Equal([a, added], found);
        left.Components.Remove(added);
        left.Components.Insert(0, added);
        found.Clear();
        Collect(root, found);
        Assert.Equal([added, a], found);
        left.Components.Remove(added);
        found.Clear();
        Collect(root, found);
        Assert.Equal([a], found);
        Assert.True(left.RemoveComponent(a));
        found.Clear();
        Collect(root, found);
        Assert.Empty(found);
    }

    /// <summary>Unrelated scene components cannot add allocation work to a warmed discovery pass.</summary>
    [Fact]
    public void DiscoveryAllocationDoesNotGrowWithNonUiNodes()
    {
        var root = new Node();
        var panel = root.AddComponent(new UiDocumentComponent());
        for (var i = 0; i < 2048; i++)
        {
            var child = new Node();
            child.Components.Add(new LightComponent());
            root.Children.Add(child);
        }
        root.Awake(null);
        var found = new List<UiDocumentComponent>();
        for (var i = 0; i < 10; i++) { found.Clear(); Collect(root, found); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) { found.Clear(); Collect(root, found); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal([panel], found);
        Assert.True(allocated < 16384, $"UI discovery allocated {allocated} bytes after warm-up.");
    }
}
