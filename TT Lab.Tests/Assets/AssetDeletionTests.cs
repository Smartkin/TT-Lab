using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using Newtonsoft.Json.Linq;
using TT_Lab.Tests.Support;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Assets;

[Collection(ProjectCollection.Name)]
public sealed class AssetDeletionTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;

    public AssetDeletionTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "Graphics"), Path.Combine(_package.Name, "levels"));
    }

    public void Dispose() => _project.Dispose();

    private static string JsonPath(IAsset asset) => Path.Combine(asset.FullPath, $"{asset.Name}.json");

    private T Create<T>(string name, Func<IAsset, AssetCreationStatus> dataCreator, params string[] folder) where T : IAsset
    {
        return (T)AssetFactory.CreateAsset(typeof(T), _project.GetFolder(_package, folder), name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<T>(), dataCreator)!;
    }

    [Fact]
    public async Task DeletedOgiIsRemovedFromDisk()
    {
        var ogi = Create<OGI>("Skeleton", asset =>
        {
            var data = new OGIData(asset);
            data.SetAnimations([new AnimationData { ID = 1, Name = "Walk", TotalFrames = 4, DefaultFPS = 25, MainAnimation = TestAnimations.CreateTwinAnimation(4, 1) }]);
            asset.SetData(data);
            return AssetCreationStatus.Success;
        });
        ogi.Serialize(SerializationFlags.SaveData);
        var files = new[] { JsonPath(ogi), ogi.FullDataPath };
        Assert.All(files, file => Assert.True(File.Exists(file)));

        Assert.True(await AssetDeletion.DeleteAsync(ogi));

        Assert.False(_project.AssetManager.DoesAssetExist(ogi.URI));
        Assert.All(files, file => Assert.False(File.Exists(file)));
    }

    [Fact]
    public async Task GlobalDefaultChunkCantBeDeleted()
    {
        var chunk = new LevelChunk(_package.URI, "default");
        chunk.RegenerateUri();
        _project.AssetManager.AddAsset(chunk);

        Assert.False(await AssetDeletion.DeleteAsync(chunk));
        Assert.True(_project.AssetManager.DoesAssetExist(chunk.URI));
    }

    [Fact]
    public async Task AssetsWithoutPlaceholdersCantBeDeletedWhileRequired()
    {
        var skin = _project.Add(new Skin(), "Body", 0x1);
        var ogi = _project.Add(new OGI(), "Skeleton", 0x1);
        ogi.SetData(new OGIData(ogi) { Skin = skin.URI });
        ogi.References.Add(skin.URI);

        Assert.False(await AssetDeletion.DeleteAsync(skin));

        Assert.True(_project.AssetManager.DoesAssetExist(skin.URI));
        Assert.Equal(skin.URI, ((IAsset)ogi).GetData<OGIData>().Skin);
    }

    [Fact]
    public async Task RequiredAssetsAreReplacedWithPlaceholders()
    {
        var crash = Create<GameObject>("Crash", AssetDataFactory.CreateGameObjectData);
        var instance = new ObjectInstance { Chunk = Path.Combine("levels", "test"), LayoutID = 0 };
        _project.Add(instance, "Instance 0");
        instance.SetData(new ObjectInstanceData(instance) { ObjectId = crash.URI });
        instance.Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);

        Assert.True(await AssetDeletion.DeleteAsync(crash));

        var placeholder = _project.AssetManager.GetAllAssetsOf<GameObject>().Single();
        Assert.Equal(PlaceholderAssets.PlaceholderName, placeholder.Name);
        Assert.True(File.Exists(JsonPath(placeholder)));
        Assert.Equal(placeholder.URI, ((IAsset)instance).GetData<ObjectInstanceData>().ObjectId);
    }

    [Fact]
    public async Task PlaceholderIsReused()
    {
        var first = Create<GameObject>("First", AssetDataFactory.CreateGameObjectData);
        var second = Create<GameObject>("Second", AssetDataFactory.CreateGameObjectData);
        var instance = new ObjectInstance { Chunk = Path.Combine("levels", "test"), LayoutID = 0 };
        _project.Add(instance, "Instance 0");
        var otherInstance = new ObjectInstance { Chunk = Path.Combine("levels", "test"), LayoutID = 0 };
        _project.Add(otherInstance, "Instance 1", 0x1);
        instance.SetData(new ObjectInstanceData(instance) { ObjectId = first.URI });
        otherInstance.SetData(new ObjectInstanceData(otherInstance) { ObjectId = second.URI });
        instance.Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);
        otherInstance.Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);

        Assert.True(await AssetDeletion.DeleteAsync(first));
        Assert.True(await AssetDeletion.DeleteAsync(second));

        var placeholder = Assert.Single(_project.AssetManager.GetAllAssetsOf<GameObject>());
        Assert.Equal(placeholder.URI, ((IAsset)instance).GetData<ObjectInstanceData>().ObjectId);
        Assert.Equal(placeholder.URI, ((IAsset)otherInstance).GetData<ObjectInstanceData>().ObjectId);
    }

    // A chunk's own values are fixed the way the asset's are: the graph goes from the list, which then isn't the chunk's own anymore
    [Fact]
    public async Task ChunksOwnValuesLinkingToDeletedAssetsAreFixed()
    {
        var graph = Create<BehaviourGraph>("COM_CRATE", AssetDataFactory.CreateBehaviourData);
        var crash = Create<GameObject>("Crash", AssetDataFactory.CreateGameObjectData);
        var chunk = new LevelChunk(_package.URI, "hub");
        chunk.RegenerateUri();
        _project.AssetManager.AddAsset(chunk);
        chunk.Overrides.Add(new AssetOverride
        {
            Asset = crash.URI,
            Values =
            {
                ["AssetData.GraphsWithoutStarter"] = JToken.FromObject(new List<LabURI> { graph.URI }),
                ["AssetData.BehaviourPack"] = "SetSurface(0x01FF0008);",
            },
        });
        chunk.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);

        Assert.True(await AssetDeletion.DeleteAsync(graph));

        Assert.Equal(["AssetData.BehaviourPack"], Assert.Single(chunk.Overrides).Values.Keys);
    }

    [Fact]
    public async Task DeletingAFolderDeletesItsContents()
    {
        var folder = _project.GetFolder(_package, "Graphics");
        var skydome = Create<Skydome>("Sky", AssetDataFactory.CreateSkydomeData, "Graphics");
        var directory = Path.Combine(_project.AssetsPath, _package.Name, "Graphics");
        Assert.True(Directory.Exists(directory));
        Assert.True(File.Exists(skydome.FullDataPath));

        Assert.True(await AssetDeletion.DeleteAsync(folder));

        Assert.False(_project.AssetManager.DoesAssetExist(folder.URI));
        Assert.False(_project.AssetManager.DoesAssetExist(skydome.URI));
        Assert.DoesNotContain(_project.AssetManager.GetAssets(), asset => asset.IsInternal && asset.InternalOwner == skydome);
        Assert.False(File.Exists(skydome.FullDataPath));
        Assert.False(Directory.Exists(directory));
        Assert.DoesNotContain(folder.URI, _project.GetFolder(_package).Children);
    }
}
