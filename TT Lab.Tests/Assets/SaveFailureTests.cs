using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Assets;

// An asset whose data doesn't write keeps the file it had: the files were opened (and emptied) before what goes into them was made, and
// saving a scenery with a NaN in it (typed into its bounds) left its model file empty, the scenery unreadable from then on
[Collection(ProjectCollection.Name)]
public sealed class SaveFailureTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public SaveFailureTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    [Fact]
    public void AModelFileThatDoesntWriteStaysAsItWasAndSaysWhatsWrong()
    {
        var path = Path.Combine(_project.AssetsPath, "test.tlm");
        var file = new TlmFile("scenery", "Test") { Root = TlmNodes.Create("scenery", "Test", new JsonObject { ["Intensity"] = 1.0f }) };
        file.Save(path);
        var written = File.ReadAllBytes(path);

        var broken = new TlmFile("scenery", "Test") { Root = TlmNodes.Create("scenery", "Test", new JsonObject { ["Intensity"] = float.NaN }) };
        var error = Assert.Throws<InvalidDataException>(() => broken.Save(path));

        Assert.Contains("$.root.data.Intensity", error.Message);
        Assert.Contains("NaN", error.Message);
        Assert.Equal(written, File.ReadAllBytes(path));
    }

    [Fact]
    public void ASceneryThatDoesntWriteKeepsItsFile()
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var written = File.ReadAllBytes(scenery.FullDataPath);
        Assert.NotEmpty(written);

        ((IAsset)scenery).GetData<SceneryData>().AmbientLights[0].Intensity = float.PositiveInfinity;

        Assert.ThrowsAny<Exception>(() => scenery.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData));
        Assert.Equal(written, File.ReadAllBytes(scenery.FullDataPath));
        Assert.NotNull(TlmFile.Load(scenery.FullDataPath).Root);
    }
}
