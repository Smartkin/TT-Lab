using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.Tests.Editor;

// A scenery's fog is picked from the game's 8 tables by name, the value stays the word the game reads
[Collection(ProjectCollection.Name)]
public sealed class SceneryFogTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [AvaloniaFact]
    public void FogIsPickedFromTheGamesTables()
    {
        var scenery = _project.Add(new Scenery { Chunk = "levels/test" }, "Scenery");
        var data = new SceneryData(scenery) { FogColor = 3 };
        scenery.SetData(data);
        var document = new DocumentViewModel(scenery);
        document.Initialize();
        var field = Assert.IsType<ChoiceFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.FogColor")!).Construct());
        field.Activator.Activate();

        Assert.Equal(8, field.Choices.Count);
        Assert.Equal("Green", field.SelectedChoice!.Name);

        field.SelectedChoice = SceneryFog.Find(4);
        Assert.Equal(4u, data.FogColor);
        Assert.Equal("White", field.SelectedChoice!.Name);

        document.Undo();
        Assert.Equal(3u, data.FogColor);
        Assert.Equal("Green", field.SelectedChoice!.Name);
        // One the game has no table for is still shown
        Assert.Equal("Not a table", SceneryFog.Find(9).Name);
    }
}
