using System.Collections.Concurrent;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Assets;

[Collection(ProjectCollection.Name)]
public sealed class AssetManagerTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void AssetsAreFoundByTypeAndId()
    {
        var walk = _project.Add(new Skin(), "Walk", 0x10);
        _project.Add(new Skin(), "Run", 0x11);
        _project.Add(new OGI(), "Skeleton", 0x10);

        Assert.Equal(walk.URI, _project.AssetManager.GetUriByTwinId<Skin>(walk, 0x10));
        Assert.Equal(LabURI.Empty, _project.AssetManager.GetUriByTwinId<Skin>(walk, 0x12));
    }

    [Fact]
    public void IdLookupSeesAddedAndRemovedAssets()
    {
        var walk = _project.Add(new Skin(), "Walk", 0x10);
        Assert.Equal(LabURI.Empty, _project.AssetManager.GetUriByTwinId<Skin>(walk, 0x20));

        var jump = _project.Add(new Skin(), "Jump", 0x20);
        Assert.Equal(jump.URI, _project.AssetManager.GetUriByTwinId<Skin>(walk, 0x20));

        _project.AssetManager.RemoveAsset(jump);
        Assert.Equal(LabURI.Empty, _project.AssetManager.GetUriByTwinId<Skin>(walk, 0x20));
    }

    [Fact]
    public void RequestersWithoutVariationOnlyFindAssetsWithoutVariation()
    {
        var requester = _project.Add(new Skin(), "Requester", 0x1);
        var varied = new Skin { Variation = "Variant" };
        _project.Add(varied, "Varied", 0x30);

        Assert.Equal(LabURI.Empty, _project.AssetManager.GetUriByTwinId<Skin>(requester, 0x30));
    }

    // Items that differ between chunks get a variant for each chunk after the first, a chunk's instances and variants use its own
    [Fact]
    public void RequestersFindTheVariantOfTheirChunk()
    {
        var first = _project.Add(new Skin(), "Shared", 0x40);
        var second = new Skin { Variation = "levels_ice_highseas_gpa04" };
        _project.Add(second, "Shared", 0x40);
        var instance = _project.Add(new Skin { Chunk = "levels/ice/highseas/gpa04" }, "Instance", 0x41);
        var other = _project.Add(new Skin { Chunk = "levels/ice/highseas/gpa07" }, "Other", 0x42);
        var variant = new Skin { Variation = "levels_ice_highseas_gpa04" };
        _project.Add(variant, "Variant", 0x43);

        Assert.Equal(second.URI, _project.AssetManager.GetUriByTwinId<Skin>(instance, 0x40));
        Assert.Equal(second.URI, _project.AssetManager.GetUriByTwinId<Skin>(variant, 0x40));
        Assert.Equal(first.URI, _project.AssetManager.GetUriByTwinId<Skin>(other, 0x40));
    }

    // The game reuses texture IDs for other pictures in other chunks' sceneries: a shared material finds the shared texture and a
    // scenery's material its own scenery's, whatever order they were added in
    [Fact]
    public void RequestersFindAssetsOfTheirOwnFolderThenTheSharedOnes()
    {
        var totemScenery = _project.Add(new Texture { AdditionalPath = "levels/earth/totem/l03beach" }, "Texture 671573F7", 0x671573F7);
        var shared = _project.Add(new Texture(), "Texture 671573F7", 0x671573F7);
        var hubMaterial = _project.Add(new Material(), "lambert48_A13CC1D3", 0xA13CC1D3);
        var totemMaterial = _project.Add(new Material { AdditionalPath = "levels/earth/totem/l03beach" }, "lambert3_1", 0x1);
        var otherScenery = _project.Add(new Material { AdditionalPath = "levels/earth/hub/hubd" }, "lambert3_2", 0x2);

        Assert.Equal(shared.URI, _project.AssetManager.GetUriByTwinId<Texture>(hubMaterial, 0x671573F7));
        Assert.Equal(totemScenery.URI, _project.AssetManager.GetUriByTwinId<Texture>(totemMaterial, 0x671573F7));
        Assert.Equal(shared.URI, _project.AssetManager.GetUriByTwinId<Texture>(otherScenery, 0x671573F7));

        // Another chunk's folder is the last resort
        _project.AssetManager.RemoveAsset(shared);
        Assert.Equal(totemScenery.URI, _project.AssetManager.GetUriByTwinId<Texture>(hubMaterial, 0x671573F7));
    }

    [Fact]
    public void StorageIndexesAssetsByTypeAndId()
    {
        var storage = new AssetStorage();
        var walk = new Skin { ID = 1, URI = new LabURI("res://Test/Skin/Walk") };
        var run = new Skin { ID = 1, URI = new LabURI("res://Test/Skin/Run") };
        var ogi = new OGI { ID = 1, URI = new LabURI("res://Test/OGI/Skeleton") };

        storage.Add(walk.URI, walk);
        storage.Add(ogi.URI, ogi);
        Assert.Equal([walk], storage.GetValuesByTypeAndId(typeof(Skin), 1));

        storage.Add(run.URI, run);
        Assert.Equal([walk, run], storage.GetValuesByTypeAndId(typeof(Skin), 1));

        storage.Remove(walk.URI);
        Assert.Equal([run], storage.GetValuesByTypeAndId(typeof(Skin), 1));
        Assert.Equal([ogi], storage.GetValuesByTypeAndId(typeof(OGI), 1));
        Assert.Empty(storage.GetValuesByTypeAndId(typeof(Skin), 2));
    }

    [Fact]
    public void StorageRejectsDuplicateUris()
    {
        var storage = new AssetStorage();
        var walk = new Skin { URI = new LabURI("res://Test/Skin/Walk") };
        storage.Add(walk.URI, walk);

        Assert.Throws<InvalidOperationException>(() => storage.Add(walk.URI, new Skin()));
    }

    // Builds use the recorded assets to know when their outputs are stale
    [Fact]
    public async Task AccessedAssetsAreRecordedAcrossTasks()
    {
        var walk = _project.Add(new Skin(), "Walk", 0x10);
        var run = _project.Add(new Skin(), "Run", 0x11);
        var ogi = _project.Add(new OGI(), "Skeleton", 0x12);
        var accessed = new ConcurrentDictionary<IAsset, byte>();

        using (_project.AssetManager.RecordAccessedAssets(accessed))
        {
            _project.AssetManager.GetAsset(walk.URI);
            await Task.Run(() => _project.AssetManager.GetAllAssetsOf<OGI>());
        }

        _project.AssetManager.GetAsset(run.URI);

        Assert.Equal(new HashSet<IAsset> { walk, ogi }, accessed.Keys.ToHashSet());
    }

    // Both versions of the game have assets with the same IDs, each only finds its own
    [Fact]
    public void AssetsAreFoundInTheRequestersVersionOfTheGame()
    {
        var ps2 = _project.Add(new Skin(), "PS2 skin", 0x10);
        var xbox = _project.Add(new Skin(), "Xbox skin", 0x10, _project.Project.GlobalPackageXbox);
        var xboxRequester = _project.Add(new Skin(), "Xbox requester", 0x11, _project.Project.XboxPackage);
        var ps2Requester = _project.Add(new Skin(), "PS2 requester", 0x12, _project.Project.Ps2Package);

        Assert.Equal(xbox.URI, _project.AssetManager.GetUriByTwinId<Skin>(xboxRequester, 0x10));
        Assert.Equal(ps2.URI, _project.AssetManager.GetUriByTwinId<Skin>(ps2Requester, 0x10));
        Assert.Equal([xbox], _project.AssetManager.GetRelatedAssetsOf<Skin>(_project.Project.XboxPackage.URI).Where(skin => skin.ID == 0x10));
    }

    [Fact]
    public void RecordingsCanBeNested()
    {
        var walk = _project.Add(new Skin(), "Walk", 0x10);
        var run = _project.Add(new Skin(), "Run", 0x11);
        var outer = new ConcurrentDictionary<IAsset, byte>();
        var inner = new ConcurrentDictionary<IAsset, byte>();

        using (_project.AssetManager.RecordAccessedAssets(outer))
        {
            using (_project.AssetManager.RecordAccessedAssets(inner))
            {
                _project.AssetManager.GetAsset(walk.URI);
            }

            _project.AssetManager.GetAsset(run.URI);
        }

        Assert.Equal([walk], inner.Keys);
        Assert.Equal([run], outer.Keys);
    }

    // The global default chunk's viewport previews the default particle systems, it has nothing else to show
    [Fact]
    public void OnlyTheGlobalPackagesDefaultChunkIsTheGlobalDefault()
    {
        var globalDefault = new LevelChunk(_project.Project.GlobalPackagePS2.URI, "default");
        var regularDefault = new LevelChunk(_project.Project.Ps2Package.URI, "default");
        var level = new LevelChunk(_project.Project.GlobalPackagePS2.URI, "hub");

        Assert.True(globalDefault.IsGlobalDefaultChunk);
        Assert.False(regularDefault.IsGlobalDefaultChunk);
        Assert.False(level.IsGlobalDefaultChunk);
        Assert.All([globalDefault, regularDefault, level], chunk => Assert.True(chunk.SupportsViewport));
    }
}
