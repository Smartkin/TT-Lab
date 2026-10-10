using Avalonia.Headless.XUnit;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Build;

// The tools made the graphics items' IDs of what the files don't keep (their scenes), so TT Lab makes them of the items' data: the disc's
// textures, materials and skies are built with the game's ID while they're made of what the disc's were
[Collection(ProjectCollection.Name)]
public sealed class RetailIdsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public RetailIdsTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    // The ID a file being built gives the asset
    private static UInt32 Built(IAsset asset)
    {
        using (new RetailIds().Use())
        {
            return asset.ExportTwinID;
        }
    }

    [AvaloniaFact]
    public void TheDiscsGraphicsKeepTheirIdsUntilTheyAreEdited()
    {
        var texture = _assets.AddTexture("Sand", 0xFF806040);
        texture.ID = 0x671573F7;
        var material = _assets.AddMaterial("lambert2", texture.URI);
        material.ID = 0x95DE8AC7;
        var made = _assets.AddMaterial("Glass");
        made.ID = 0x95DE8AC7;
        RetailIds.Record([texture, material], []);

        Assert.Equal(0x671573F7U, Built(texture));
        Assert.Equal(0x95DE8AC7U, Built(material));
        // Not built in a file, the same
        Assert.Equal(0x95DE8AC7U, ((IAsset)material).ExportTwinID);
        // Made in TT Lab, the ID of its data
        Assert.Equal(((IAsset)made).GetDataHash(), Built(made));

        // A value the game reads that the old data hash left out
        ((IAsset)material).GetData<MaterialData>().Shaders[0].FloatParam[0] = 0.35f;
        Assert.NotEqual(0x95DE8AC7U, Built(material));
        Assert.Equal(((IAsset)material).GetDataHash(), Built(material));

        // What building the texture reads of the asset itself
        texture.GenerateMipmaps = !texture.GenerateMipmaps;
        Assert.Equal(((IAsset)texture).GetDataHash(), Built(texture));
        texture.GenerateMipmaps = !texture.GenerateMipmaps;
        Assert.Equal(0x671573F7U, Built(texture));
    }

    // The game reuses IDs for other pictures in other chunks, and a texture brought over from one of them would take the place of the chunk's
    // own: the first asset of an ID in a file keeps it, the next gets its data's
    [AvaloniaFact]
    public void TheFirstAssetOfAnIdInAFileKeepsIt()
    {
        var hub = _assets.AddTexture("Hub", 0xFF102030);
        var beach = _assets.AddTexture("Beach", 0xFF405060);
        hub.ID = 0x671573F7;
        beach.ID = 0x671573F7;
        var material = _assets.AddMaterial("lambert2");
        material.ID = 0x671573F7;
        RetailIds.Record([hub, beach, material], []);

        using (new RetailIds().Use())
        {
            Assert.Equal(0x671573F7U, ((IAsset)beach).ExportTwinID);
            Assert.Equal(((IAsset)hub).GetDataHash(), ((IAsset)hub).ExportTwinID);
            Assert.Equal(0x671573F7U, ((IAsset)beach).ExportTwinID);
            // A material is another kind of item
            Assert.Equal(0x671573F7U, ((IAsset)material).ExportTwinID);
        }

        // Another file
        Assert.Equal(0x671573F7U, Built(hub));
    }

    // A chunk's own version of a material (its overrides, made of the disc's variants of the ID) keeps the ID while its values are the disc's
    [Fact]
    public void AChunksOwnVersionKeepsTheIdWhileItsValuesAreTheDiscs()
    {
        var material = _assets.AddMaterial("lambert2");
        material.ID = 0x95DE8AC7;
        var chunk = new LevelChunk(_project.Project.Ps2Package.URI, "beach") { AdditionalPath = "levels/earth/hub/beach" };
        chunk.Overrides.Add(new AssetOverride { Asset = material.URI, Values = { ["AssetData.DmaChainIndex"] = 7 } });
        RetailIds.Record([material], [chunk]);

        Assert.Equal(2, material.RetailFingerprints!.Count);
        using (new ChunkOverrides(chunk.Overrides).Use())
        {
            Assert.Equal(0x95DE8AC7U, Built(material));
        }

        chunk.Overrides[0].Values["AssetData.DmaChainIndex"] = 9;
        using (new ChunkOverrides(chunk.Overrides).Use())
        {
            Assert.NotEqual(0x95DE8AC7U, Built(material));
        }

        // Other chunks build the asset itself
        Assert.Equal(0x95DE8AC7U, Built(material));
    }

    // Default.rm2's meshes, which the game's code finds by their IDs, keep them in every build whatever their data (the build sets them,
    // PostResolveResources). Meshes record nothing: the others are parts of sceneries, LODs and skies made again of their model files
    [Fact]
    public void TheDefaultChunksMeshesKeepTheirIds()
    {
        var mesh = _assets.AddMesh("Shadow");
        mesh.ID = 0x1D;
        RetailIds.Record([mesh], []);
        Assert.Null(mesh.RetailFingerprints);

        ((IAsset)mesh).GetData<MeshData>().Materials.Reverse();
        var factory = new PS2ItemFactory();
        using (new RetailIds().Use())
        {
            var item = ((IAsset)mesh).GetData<MeshData>().Export(factory);
            mesh.PostResolveResources(factory, factory.GenerateDefault(), item);
            Assert.Equal(0x1DU, item.GetID());
        }
    }

    // The parts in an OGI's model file (its rigid model and the model it's made of, its skin and blend skin) are built with the disc's IDs
    // creation recorded with the OGI while they're made of what the disc's were: reading the file makes them again without their IDs
    [Fact]
    public void ModelFilePartsKeepTheDiscsIdsUntilTheyAreEdited()
    {
        var ogi = _assets.AddOgi();
        var data = ((IAsset)ogi).GetData<OGIData>();
        var rigid = (SerializableAsset)_project.AssetManager.GetAsset(data.RigidModelIds[0]);
        var model = (SerializableAsset)_project.AssetManager.GetAsset(((IAsset)rigid).GetData<RigidModelData>().Model);
        var skin = (SerializableAsset)_project.AssetManager.GetAsset(data.Skin);
        var blendSkin = (SerializableAsset)_project.AssetManager.GetAsset(data.BlendSkin);
        (rigid.ID, model.ID, skin.ID, blendSkin.ID) = (0x5E1, 0x30D, 0x5C1, 0xB1E);
        RetailIds.RecordParts(ogi, []);
        foreach (var part in new[] { rigid, model, skin, blendSkin })
        {
            part.ID = 0;
            part.IsInternal = true;
            part.InternalOwner = ogi;
        }

        Assert.Equal([0x5E1U, 0x30DU, 0x5C1U, 0xB1EU], new IAsset[] { rigid, model, skin, blendSkin }.Select(Built));

        // Drawn with another material, the model it's made of stays as it is
        var lacquer = _assets.AddMaterial("Lacquer");
        lacquer.ID = 0x77;
        ((IAsset)rigid).GetData<RigidModelData>().Materials[0] = lacquer.URI;
        ((IAsset)skin).GetData<SkinData>().SubSkins[0].Material = lacquer.URI;
        Assert.Equal(((IAsset)rigid).GetDataHash(), Built(rigid));
        Assert.Equal(((IAsset)skin).GetDataHash(), Built(skin));
        Assert.Equal(0x30DU, Built(model));
        Assert.Equal(0xB1EU, Built(blendSkin));
    }

    // The disc has parts made of the same under IDs of their own (a scenery's models): each takes one nothing took while there are, then
    // they share one
    [Fact]
    public void PartsMadeOfTheSameTakeAnIdEachWhileThereAre()
    {
        var owner = _assets.AddMesh("Rock");
        var source = ((IAsset)owner).GetData<MeshData>();
        var geometry = AssetManager.Get().GetAssetData<ModelData>(source.Model);
        var models = Enumerable.Range(0, 3).Select(i =>
        {
            var model = _project.Add(new Model(), $"Rock Model {i}");
            model.SetData(new ModelData(model) { Vertexes = geometry.Vertexes, Faces = geometry.Faces, Layouts = geometry.Layouts });
            model.IsInternal = true;
            model.InternalOwner = owner;
            return model;
        }).ToList();
        owner.RetailPartIds = new() { [$"Model:{models[0].Fingerprint():X8}"] = [0xA1, 0xB2] };

        using (new RetailIds().Use())
        {
            Assert.Equal([0xA1U, 0xB2U, 0xA1U], models.Select(model => ((IAsset)model).ExportTwinID));
        }
    }

    // Creation reads a rigid model two OGIs share from the file it kept it in for the second, which makes its model anew without an ID:
    // it's what the same was in a model file written before
    [Fact]
    public void APartMadeAgainWithoutItsIdIsWhatTheSameWasBefore()
    {
        var known = new Dictionary<string, List<uint>>();
        var first = _assets.AddMesh("Rock");
        var firstData = ((IAsset)first).GetData<MeshData>();
        var model = (SerializableAsset)_project.AssetManager.GetAsset(firstData.Model);
        model.ID = 0x4D1;
        RetailIds.RecordParts(first, known);

        var geometry = ((IAsset)model).GetData<ModelData>();
        var again = _project.Add(new Model(), "Rock Model Again");
        again.SetData(new ModelData(again) { Vertexes = geometry.Vertexes, Faces = geometry.Faces, Layouts = geometry.Layouts });
        var second = _project.Add(new Mesh(), "Rock Again");
        second.SetData(new MeshData(second) { Model = again.URI, Materials = [..firstData.Materials] });
        RetailIds.RecordParts(second, known);

        Assert.Equal([0x4D1U], second.RetailPartIds![$"Model:{again.Fingerprint():X8}"]);
    }

    // The files written already get the fingerprints added, assets read them back. What TT Lab made has none in its file
    [Fact]
    public void TheFingerprintsAreKeptInTheAssetsFile()
    {
        var material = _assets.AddMaterial("lambert2");
        var made = _assets.AddMaterial("Glass");
        material.Serialize();
        made.Serialize();
        RetailIds.Record([material], []);

        var json = JObject.Parse(File.ReadAllText(Path.Combine(material.FullPath, $"{material.Name}.json")));
        Assert.Equal(material.RetailFingerprints, json[nameof(IAsset.RetailFingerprints)]!.ToObject<List<UInt32>>());
        var read = new Material();
        read.Deserialize(json.ToString());
        Assert.Equal(material.RetailFingerprints, read.RetailFingerprints);
        Assert.False(JObject.Parse(File.ReadAllText(Path.Combine(made.FullPath, $"{made.Name}.json"))).ContainsKey(nameof(IAsset.RetailFingerprints)));
    }
}
