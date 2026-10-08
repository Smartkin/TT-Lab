using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Graphics;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// A texture's and a sound's editors replace the asset's whole data: they set it on the data's custom editor node, which set the asset's
// AssetData property on the data itself and threw ("Object does not match target type")
[Collection(ProjectCollection.Name)]
public sealed class TextureEditorTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;
    private readonly List<string> _files = [];

    public TextureEditorTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose()
    {
        foreach (var file in _files.Where(File.Exists))
        {
            File.Delete(file);
        }

        _project.Dispose();
    }

    private string WritePng(IAsset owner, int size, uint argb)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tt_lab_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, TextureData.CreateSolidColor(owner, size, argb).GetPngBytes());
        _files.Add(path);
        return path;
    }

    [AvaloniaFact]
    public void ReplacingTheTexturesPictureTakesTheImage()
    {
        var texture = _assets.AddTexture("Wood", 0xFF8B5A2B);
        var document = new DocumentViewModel(texture);
        document.Initialize();
        var node = document.PropertyGraph.Find("Root.AssetData[custom_editor]")!;
        var editor = Assert.IsType<TextureViewModel>(EditorDescRegistry.GetDesc(document, node).Construct());
        using var shown = editor.Activator.Activate();

        editor.ReplaceWith(WritePng(texture, 32, 0xFF204060));

        var data = ((IAsset)texture).GetData<TextureData>();
        Assert.Equal(32, data.Bitmap!.PixelSize.Width);
        Assert.Same(data, editor.CurrentValue);
        Assert.Same(data.Bitmap, editor.Texture);
        Assert.Same(data, node.GetValue());
        Assert.True(document.IsDirty);
        // The data can't be taken back by undo, the history starts over
        Assert.False(document.History.CanUndo);
    }

    private static async Task WaitForLevels(TextureViewModel editor)
    {
        for (var waited = 0; waited < 10000 && editor.BuildText.StartsWith("Making"); waited += 20)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Dispatcher.UIThread.RunJobs();
    }

    // The viewer shows what building makes of the picture level by level: quantized when it has more colors than a palette holds, the
    // mips the PS2 version draws in the distance, and the picture as it's stored
    [AvaloniaFact]
    public async Task TheViewerShowsWhatTheGameGetsLevelByLevel()
    {
        var texture = _project.Add(new Texture(), "Gradient");
        var pixels = Enumerable.Range(0, 64 * 64).Select(i => 0xFF000000u | (uint)(i % 64 * 4) << 16 | (uint)(i / 64 * 4) << 8 | 0x20).ToArray();
        texture.SetData(TextureData.FromPixels(texture, pixels, 64, 64));
        texture.UseGameLayout(64, 64);
        var document = new DocumentViewModel(texture);
        document.Initialize();
        var editor = Assert.IsType<TextureViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData[custom_editor]")!).Construct());
        using var shown = editor.Activator.Activate();
        await WaitForLevels(editor);

        Assert.Contains("quantized", editor.BuildText);
        Assert.True(editor.CanPickLevel);
        Assert.True(editor.MaxMipLevel > 1);
        Assert.Equal(64, editor.Texture!.PixelSize.Width);
        editor.MipLevel = 2;
        Assert.Equal(16, editor.Texture!.PixelSize.Width);
        Assert.StartsWith("Mip 2 of", editor.LevelText);

        editor.ShowsOriginal = true;
        Assert.Same(((IAsset)texture).GetData<TextureData>().Bitmap, editor.Texture);
        Assert.False(editor.CanPickLevel);
        editor.ShowsOriginal = false;

        // Without mips there's one level
        document.PropertyGraph.Find("Root.GenerateMipmaps")!.SetValue(false);
        await WaitForLevels(editor);
        Assert.Equal(0, editor.MaxMipLevel);
        Assert.Equal(0, editor.MipLevel);
        Assert.Contains("without mips", editor.BuildText);
    }

    [AvaloniaFact]
    public void ASoundsDataIsReplacedTheSameWay()
    {
        var sound = _project.Add(new SoundEffect(), "Splash");
        sound.SetData(new SoundEffectData(sound));
        var document = new DocumentViewModel(sound);
        document.Initialize();
        var node = document.PropertyGraph.Find("Root.AssetData[custom_editor]")!;
        var replacement = new SoundEffectData(sound);

        node.SetValue(replacement);

        Assert.Same(replacement, ((IAsset)sound).GetData<SoundEffectData>());
        Assert.Same(replacement, node.GetValue());
    }
}
