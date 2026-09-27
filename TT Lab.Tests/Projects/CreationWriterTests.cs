using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using static TT_Lab.Tests.Support.TestGeometry;

namespace TT_Lab.Tests.Projects;

// Creating a project writes every asset as soon as it's imported and lets go of its data, internal assets once no write uses them
[Collection(ProjectCollection.Name)]
public sealed class CreationWriterTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // Resolvers make a model internal and give it the game's item, like creating a project does
    private Model AddInternalModel(UInt32 id)
    {
        var original = new PS2AnyModel { SubModels = [RigidSubModel(new Random(31), TwinVifPadding.QuadWord, [12], false)] };
        original.Compile();
        var item = Deserialize(new PS2AnyModel(), Serialize(original));
        var model = new Model(_project.Project.Ps2Package.URI, false, string.Empty, id, $"Model {id:X}", item) { IsInternal = true };
        _project.AssetManager.AddAsset(model);
        return model;
    }

    private RigidModel AddRigidModel(string name, UInt32 id, UInt32 model)
    {
        var rigidModel = new RigidModel(_project.Project.Ps2Package.URI, false, string.Empty, id, name, new PS2AnyRigidModel { Model = model, Materials = [] });
        _project.AssetManager.AddAsset(rigidModel);
        return rigidModel;
    }

    private static int WrittenVertexCount(IAsset asset)
    {
        var file = TlmFile.Load(asset.FullDataPath);
        return TlmMeshes.ReadMesh(file, file.Root![TlmNodes.MeshKey] as JsonObject, false).Sum(part => part.Part.Vertexes.Count);
    }

    // One write at a time, the second one needs the model after the first let go of it
    [Fact]
    public void InternalAssetsUsedByTwoWritesComeBackForTheSecond()
    {
        var model = AddInternalModel(0x20);
        var first = AddRigidModel("First", 0x10, 0x20);
        var second = AddRigidModel("Second", 0x11, 0x20);
        using var gate = new MemoryGate(1);
        var writer = new CreationWriter(_project.AssetManager, gate);

        writer.ImportAndWrite();

        Assert.True(writer.IsWritten(first));
        Assert.True(writer.IsWritten(second));
        Assert.False(writer.IsWritten(model));
        Assert.True(WrittenVertexCount(first) > 0);
        Assert.Equal(WrittenVertexCount(first), WrittenVertexCount(second));
        Assert.True(File.Exists(model.FullDataPath), "The model wasn't kept for the second write");

        writer.RemoveInternalAssets();

        Assert.False(_project.AssetManager.DoesAssetExist(model.URI));
        Assert.False(File.Exists(model.FullDataPath));
        Assert.True(File.Exists(first.FullDataPath));
    }
}
