using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common;
using TT_Lab.Assets.Factory;

namespace TT_Lab.Tests.Editor;

// Lists the game keeps in fixed places stop growing at what it takes (a material's 4 shader slots, a font's 3 pages), and a build
// refuses lists longer than that
[Collection(ProjectCollection.Name)]
public sealed class ListLimitTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (DocumentViewModel Document, MaterialData Data) OpenMaterial(int shaders)
    {
        var material = _project.Add(new Material(), "Glass");
        var data = new MaterialData(material);
        while (data.Shaders.Count < shaders)
        {
            data.Shaders.Add(new LabShader());
        }

        material.SetData(data);
        var document = new DocumentViewModel(material);
        document.Initialize();
        return (document, data);
    }

    private static DocumentCollectionViewModel ShownList(DocumentViewModel document, string path)
    {
        var editor = Assert.IsType<DocumentCollectionViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 400 }.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        return editor;
    }

    [AvaloniaFact]
    public void AMaterialTakesFourShaders()
    {
        var (document, data) = OpenMaterial(3);
        var shaders = ShownList(document, "Root.AssetData.Shaders");
        Assert.True(shaders.CanAddItem);
        Assert.Equal("The game takes at most 4", shaders.AddItemHint);

        shaders.AddCommand.Execute().Subscribe();
        Assert.Equal(4, data.Shaders.Count);
        Assert.False(shaders.CanAddItem);
        shaders.AddCommand.Execute().Subscribe();

        Assert.Equal(4, data.Shaders.Count);
        Assert.True(document.PropertyGraph.Find("Root.AssetData.Shaders")!.IsFull);
        // Taking one out and putting it back is still undone and redone
        var list = document.PropertyGraph.Find("Root.AssetData.Shaders")!;
        list.RemoveElement(list.Children[1]);
        Assert.True(shaders.CanAddItem);
        document.Undo();
        Assert.Equal(4, data.Shaders.Count);
        Assert.False(shaders.CanAddItem);
    }

    [AvaloniaFact]
    public void BuildsRefuseMoreThanTheGameTakes()
    {
        var (_, data) = OpenMaterial(5);

        var failure = Assert.Throws<InvalidOperationException>(() => data.Export(new PS2ItemFactory()));

        Assert.Equal("Glass has 5 shaders, the game takes at most 4", failure.Message);
        var font = _project.Add(new Font { GlobalPath = "Startup" }, "Menu Font");
        var fontData = new FontData(font);
        fontData.FontPages.AddRange([LabURI.Empty, LabURI.Empty, LabURI.Empty, LabURI.Empty]);
        Assert.Contains("4 pages", Assert.Throws<InvalidOperationException>(() => fontData.Export(new PS2ItemFactory())).Message);
    }

    [Fact]
    public void HullsTheFormatCantIndexArentWritten()
    {
        var hull = TwinCollisionHull.CreateBox(new Vector4(-1, -1, -1, 1), new Vector4(1, 1, 1, 1));
        hull.Vertexes.AddRange(Enumerable.Range(0, 300).Select(i => new Vector4(i, 0, 0, 1)));
        using var writer = new BinaryWriter(new MemoryStream());

        Assert.Throws<InvalidOperationException>(() => hull.Write(writer));
    }
}
