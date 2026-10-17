using TT_Lab.Assets;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Tests.Rendering;

// A context's meshes by URI: forgotten ones are handed back to be released once nothing draws them, and the parts of data replaced
// whole stay while the new data's objects draw them. Forgetting only took them out of the dictionary, a model made again kept every copy
// of its meshes on the GPU until the tab closed
public sealed class RenderCacheTests
{
    private sealed class FakeMesh(string name)
    {
        public override string ToString() => name;
    }

    private static readonly LabURI First = new("res://Test/Mesh/First");
    private static readonly LabURI Second = new("res://Test/Mesh/Second");
    private static readonly LabURI Never = new("res://Test/Mesh/Never");

    [Fact]
    public void ForgottenMeshesAreHandedBackOnceNothingDrawsThem()
    {
        var cache = new RenderCache<FakeMesh>();
        var mesh = new FakeMesh("first");
        cache.Add(First, mesh);
        cache.Add(Never, null);
        var version = cache.Version;

        Assert.Equal([mesh], cache.Forget([First, Never, Second]));
        Assert.False(cache.TryGet(First, out _));
        Assert.True(cache.HasForgotten);
        // The renderer only looks again once more got forgotten or a mesh left the scene
        Assert.NotEqual(version, cache.Version);
        version = cache.Version;
        Assert.Empty(cache.Forget([Second]));
        Assert.Equal(version, cache.Version);

        // An object of the old mesh is still in the scene (made again once a drag is over)
        var drawn = true;
        Assert.Empty(cache.TakeUndrawn(_ => drawn));
        drawn = false;
        Assert.Equal([mesh], cache.TakeUndrawn(_ => drawn));
        Assert.Empty(cache.TakeUndrawn(_ => drawn));
        Assert.False(cache.HasForgotten);

        // Forgotten again, nothing is handed back twice
        Assert.Empty(cache.Forget([First]));
    }

    // A part's URI is of its content: the new data's objects draw the parts it still has, which stay, the others go once undrawn
    [Fact]
    public void PartsOfReplacedDataStayWhileTheyreDrawn()
    {
        var cache = new RenderCache<FakeMesh>();
        var kept = new FakeMesh("kept");
        var replaced = new FakeMesh("replaced");
        cache.Add(First, kept);
        cache.Add(Second, replaced);
        var drawn = new HashSet<FakeMesh> { kept, replaced };

        cache.ForgetWhenUndrawn([First, Second, Never]);
        Assert.Empty(cache.TakeUndrawn(drawn.Contains));
        Assert.True(cache.TryGet(Second, out _));

        drawn.Remove(replaced);
        Assert.Equal([replaced], cache.TakeUndrawn(drawn.Contains));
        Assert.True(cache.TryGet(First, out var stays));
        Assert.Same(kept, stays);
        Assert.False(cache.TryGet(Second, out _));
        Assert.True(cache.HasForgotten);

        drawn.Clear();
        Assert.Equal([kept], cache.TakeUndrawn(drawn.Contains));
        Assert.False(cache.HasForgotten);
    }

    // Forgotten right away while waiting to be undrawn, it's handed back once, as a forgotten mesh
    [Fact]
    public void AMeshForgottenWhileWaitingIsHandedBackOnce()
    {
        var cache = new RenderCache<FakeMesh>();
        var mesh = new FakeMesh("first");
        cache.Add(First, mesh);
        cache.ForgetWhenUndrawn([First]);

        Assert.Equal([mesh], cache.Forget([First]));
        Assert.Equal([mesh], cache.TakeUndrawn(_ => false));
        Assert.False(cache.HasForgotten);
    }
}
