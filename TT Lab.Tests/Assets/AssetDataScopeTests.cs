using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Assets;

// Chunks build in parallel, each in a scope of its own that keeps the data it loads and the internal assets that data makes. A scenery's
// data makes its chunk's collision and dynamic scenery and deletes them when it's disposed
[Collection(ProjectCollection.Name)]
public sealed class AssetDataScopeTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private Scenery AddScenery() => _project.Add(new Scenery { Chunk = "levels/test" }, "Scenery", package: _project.Project.Ps2Package);

    // The way loading a scenery's data makes its chunk's collision
    private Collision MakeCollision(Scenery owner)
    {
        var collision = new Collision
        {
            Package = owner.Package,
            Chunk = owner.Chunk,
            InvariantName = $"{owner.Chunk}_COLLISION",
            Alias = "Collision",
            IsInternal = true,
            InternalOwner = owner,
        };
        collision.SetData(new CollisionData(collision));
        _project.AssetManager.AddAsset(collision);
        return collision;
    }

    // Building a chunk releases a scenery's data once it's written
    [Fact]
    public void DeletingAnInternalAssetOfTheScopeTakesItOutOfTheScope()
    {
        var scenery = AddScenery();
        using var scope = new AssetDataScope();
        var collision = MakeCollision(scenery);
        scope.SetData(scenery, new SceneryData(scenery) { Collision = collision.URI });

        scope.ReleaseData(scenery);

        Assert.True(collision.MarkedForDeletion);
        Assert.False(_project.AssetManager.DoesAssetExist(collision.URI));
        // Loading the scenery again in the same chunk makes its collision again
        var again = MakeCollision(scenery);
        Assert.Equal(collision.URI, again.URI);
        Assert.Same(again, _project.AssetManager.GetAsset(collision.URI));
    }

    [Fact]
    public void EndingAScopeLeavesTheInternalAssetsOfTheDataEditorsHaveOpen()
    {
        var scenery = AddScenery();
        var editors = MakeCollision(scenery);
        var scope = new AssetDataScope();
        var builds = MakeCollision(scenery);
        scope.SetData(scenery, new SceneryData(scenery) { Collision = builds.URI });

        scope.Dispose();

        Assert.True(builds.MarkedForDeletion);
        Assert.False(editors.MarkedForDeletion);
        Assert.True(editors.IsLoaded);
        Assert.Same(editors, _project.AssetManager.GetAsset(editors.URI));
    }
}
