using Avalonia.Headless.XUnit;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Assets;

// Chunks keep their own values of the assets they share instead of copies of them
[Collection(ProjectCollection.Name)]
public sealed class AssetOverrideTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private GameObject AddObject(string name, string variation = "", UInt32 id = 0x10, Action<GameObjectData>? edit = null)
    {
        var gameObject = new GameObject { Variation = variation };
        _project.Add(gameObject, name, id, _project.Project.Ps2Package);
        var data = new GameObjectData(gameObject) { Name = "|Hub|act_BIRD", BehaviourPack = "SetObject(0x60050000, 0x000000C0);\n" };
        data.InstFlags.AddRange([1, 2, 3]);
        edit?.Invoke(data);
        gameObject.SetData(data);
        gameObject.Serialize(SerializationFlags.SaveData);
        return gameObject;
    }

    private LevelChunk AddChunk(string path)
    {
        var chunk = new LevelChunk(_project.Project.Ps2Package.URI, Path.GetFileName(path)) { AdditionalPath = path };
        chunk.RegenerateUri();
        _project.AssetManager.AddAsset(chunk);
        return chunk;
    }

    [Fact]
    public void DiffsFindTheValuesThatChanged()
    {
        var @base = JObject.Parse("""{ "Name": "Bird", "Flags": [1, 2, 3], "Slot": { "_uri": "res://a" }, "Pos": { "X": 1, "Y": 2 } }""");
        var changed = JObject.Parse("""{ "Name": "Bird", "Flags": [1, 5, 3], "Slot": { "_uri": "res://b" }, "Pos": { "X": 1, "Y": 7 } }""");

        var values = AssetOverrides.Diff(@base, changed);

        Assert.Equal(["Flags[1]", "Pos.Y", "Slot"], values.Keys);
        AssetOverrides.Apply(@base, values);
        Assert.True(JToken.DeepEquals(@base, changed));
    }

    [Fact]
    public void ListsOfAnotherLengthAreOneValue()
    {
        var values = AssetOverrides.Diff(JObject.Parse("""{ "Flags": [1, 2] }"""), JObject.Parse("""{ "Flags": [1, 2, 3] }"""));

        Assert.Equal("[1,2,3]", Assert.Single(values).Value.ToString(Newtonsoft.Json.Formatting.None));
    }

    [Fact]
    public void ViewsHaveTheChunksValuesAndLeaveTheAssetAlone()
    {
        var bird = AddObject("Bird");
        var values = new SortedDictionary<string, JToken>(StringComparer.Ordinal)
        {
            ["AssetData.InstFlags[1]"] = 9
        };

        var view = AssetOverrides.CreateView(bird, values);

        var viewData = ((IAsset)view).GetData<GameObjectData>();
        Assert.Equal("|Hub|act_BIRD", viewData.Name);
        Assert.Equal([1u, 9u, 3u], viewData.InstFlags);
        Assert.Equal(bird.URI, view.URI);
        Assert.Same(bird, view.OverriddenAsset);
        var birdData = ((IAsset)bird).GetData<GameObjectData>();
        Assert.Equal("|Hub|act_BIRD", birdData.Name);
        Assert.Equal([1u, 2u, 3u], birdData.InstFlags);

        // Views share the asset's file, saving one would overwrite the asset
        var written = File.GetLastWriteTimeUtc(bird.FullDataPath);
        view.Serialize(SerializationFlags.SaveData);
        Assert.Equal(written, File.GetLastWriteTimeUtc(bird.FullDataPath));
    }

    [Fact]
    public void ChunksBuildTheirViewOnce()
    {
        var bird = AddObject("Bird");
        var other = AddObject("Other", id: 0x11);
        var overrides = new ChunkOverrides([new AssetOverride { Asset = bird.URI, Values = { ["AssetData.Name"] = "Changed" } }]);

        var view = overrides.GetView(bird);

        Assert.NotNull(view);
        Assert.Same(view, overrides.GetView(bird));
        Assert.Null(overrides.GetView(other));
    }

    [Fact]
    public void VariantsBecomeTheChunksOverrides()
    {
        var hub = AddChunk("levels/earth/hub/hubb");
        var bird = AddObject("Bird");
        var variant = AddObject("Bird", "levels_earth_hub_hubb", edit: data =>
        {
            data.Name = "|HubB|act_BIRDCLUMP";
            data.BehaviourPack = "SetObject(0x60050000, 0x00000180);\n";
        });
        hub.ItemVersions.Add(variant.URI);
        // What refers to the variant refers to the asset afterwards
        var spawner = AddObject("Spawner", id: 0x12, edit: data => data.ObjectSlots.Add(variant.URI));

        var merged = new VariantMerger(_project.AssetManager, _project.AssetsPath).Merge();

        Assert.Equal(1, merged);
        Assert.False(_project.AssetManager.DoesAssetExist(variant.URI));
        Assert.False(File.Exists(variant.FullDataPath));
        var @override = Assert.Single(hub.Overrides);
        Assert.Equal(bird.URI, @override.Asset);
        // Chunks' names of objects aren't kept
        Assert.Equal(["AssetData.BehaviourPack"], @override.Values.Keys);
        Assert.Equal([bird.URI], hub.ItemVersions);
        Assert.Contains(bird.URI.ToString(), File.ReadAllText(spawner.FullDataPath));
        Assert.DoesNotContain(variant.URI.ToString(), File.ReadAllText(spawner.FullDataPath));
    }

    // A later chunk with the same content as a variant found it by its hash and uses it too
    [Fact]
    public void EveryChunkUsingAVariantGetsItsValues()
    {
        var cavern = AddChunk("levels/earth/cavern/cavent");
        var school = AddChunk("levels/school/crash/crgpa01");
        var other = AddChunk("levels/school/crash/crgpa02");
        var wumpa = AddObject("Wumpa");
        var variant = AddObject("Wumpa", "levels_earth_cavern_cavent", edit: data => data.InstFlags[0] = 7);
        cavern.ItemVersions.Add(variant.URI);
        school.ItemVersions.Add(variant.URI);
        other.ItemVersions.Add(wumpa.URI);

        new VariantMerger(_project.AssetManager, _project.AssetsPath).Merge();

        Assert.Equal(["AssetData.InstFlags[0]"], Assert.Single(cavern.Overrides).Values.Keys);
        Assert.Equal(["AssetData.InstFlags[0]"], Assert.Single(school.Overrides).Values.Keys);
        Assert.Empty(other.Overrides);
        Assert.Equal([wumpa.URI], school.ItemVersions);
    }

    [Fact]
    public void VariantsNoChunkUsesGoAwayWithoutValues()
    {
        var hub = AddChunk("levels/earth/hub/hubb");
        AddObject("Bird");
        var variant = AddObject("Bird", "levels_earth_hub_hubb", edit: data => data.InstFlags[0] = 7);

        Assert.Equal(1, new VariantMerger(_project.AssetManager, _project.AssetsPath).Merge());
        Assert.Empty(hub.Overrides);
        Assert.False(_project.AssetManager.DoesAssetExist(variant.URI));
    }

    // The game reuses texture IDs for other pictures: a chunk's own picture stays a variant and its materials have to point at it.
    // Textures hold bitmaps, which need the headless application
    [AvaloniaFact]
    public void ChunksGetTheirOwnPictureOfATextureThroughTheirMaterials()
    {
        var hub = AddChunk("levels/earth/hub/hubd");
        var other = AddChunk("levels/earth/hub/huba");
        var shared = _project.Add(new Texture(), "Texture 3B69A5CF", 0x3B69A5CF, _project.Project.Ps2Package);
        shared.SetData(TextureData.CreateSolidColor(shared, 32, 0xFF808080));
        shared.Serialize(SerializationFlags.SaveData);
        var own = _project.Add(new Texture { Variation = "levels_earth_hub_hubd" }, "Texture 3B69A5CF", 0x3B69A5CF, _project.Project.Ps2Package);
        own.SetData(TextureData.CreateSolidColor(own, 16, 0xFF204060));
        own.Serialize(SerializationFlags.SaveData);
        var material = AddMaterial("lambert20_24FFD1E7", 0x24FFD1E7, shared);
        // A scenery of another chunk has a picture of its own of the ID in its folder, with its own material: not the base of anything
        var scenery = _project.Add(new Texture { AdditionalPath = "levels/earth/docamok/docamok1" }, "Texture 3B69A5CF", 0x3B69A5CF, _project.Project.Ps2Package);
        scenery.SetData(TextureData.CreateSolidColor(scenery, 32, 0xFF808080));
        scenery.Serialize(SerializationFlags.SaveData);
        var sceneryMaterial = AddMaterial("lambert245_5F34070", 0x5F34070, scenery, "levels/earth/docamok/docamok1");

        var merged = new VariantMerger(_project.AssetManager, _project.AssetsPath).Merge();

        Assert.Equal(0, merged);
        Assert.True(_project.AssetManager.DoesAssetExist(own.URI));
        var @override = Assert.Single(hub.Overrides);
        Assert.Equal(material.URI, @override.Asset);
        var (path, value) = Assert.Single(@override.Values);
        Assert.Equal("AssetData.Shaders[0].TextureId", path);
        Assert.Equal(own.URI.ToString(), (string)value["_uri"]!);
        Assert.Empty(other.Overrides);
        Assert.Equal(shared.URI, ((IAsset)material).GetData<MaterialData>().Shaders[0].TextureId);
        Assert.Equal(scenery.URI, ((IAsset)sceneryMaterial).GetData<MaterialData>().Shaders[0].TextureId);
    }

    private Material AddMaterial(string name, UInt32 id, Texture texture, string folder = "")
    {
        var material = _project.Add(new Material { AdditionalPath = folder }, name, id, _project.Project.Ps2Package);
        var data = new MaterialData(material);
        data.Shaders[0].TextureId = texture.URI;
        material.SetData(data);
        material.Serialize(SerializationFlags.SaveData);
        material.References = [texture.URI];
        return material;
    }

    [Fact]
    public void VariantsTheSameWhereItMattersGoAway()
    {
        var hub = AddChunk("levels/earth/hub/hubb");
        AddObject("Bird");
        AddObject("Bird", "levels_earth_hub_hubb");

        Assert.Equal(1, new VariantMerger(_project.AssetManager, _project.AssetsPath).Merge());
        Assert.Empty(hub.Overrides);
    }
}
