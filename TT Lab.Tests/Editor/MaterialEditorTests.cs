using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.Controls;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Graphics;
using TT_Lab.Views.Editors;
using TT_Lab.Views.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Editor;

// The material editor ticks the shader types the material has and edits its shaders' animations on a timeline
[Collection(ProjectCollection.Name)]
public sealed class MaterialEditorTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (DocumentViewModel Document, MaterialData Data) Open(params TwinShader.Type[] types) => Open(_ => { }, types);

    private (DocumentViewModel Document, MaterialData Data) Open(Action<MaterialData> setUp, params TwinShader.Type[] types)
    {
        var material = _project.Add(new Material(), "Mat");
        var data = new MaterialData(material) { Shaders = types.Select(type => new LabShader { ShaderType = type }).ToList() };
        setUp(data);
        material.SetData(data);
        var document = new DocumentViewModel(material);
        document.Initialize();
        return (document, data);
    }

    private static T Editor<T>(DocumentViewModel document, string path) where T : DocumentNodeViewModel
    {
        var editor = Assert.IsType<T>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        editor.Activator.Activate();
        return editor;
    }

    private static IEnumerable<string> Ticked(ActivatedShadersFieldViewModel field) => field.Types.Where(type => type.IsUsed).Select(type => type.Name);

    // A material's preview is its own, at the document's root: putting a shader in or taking one out makes the material's objects again,
    // which read the root as a link to a chunk's resource and made nothing, and the preview stayed empty through undo and new shaders
    [AvaloniaFact]
    public void ThePreviewIsMadeAgainForTheMaterialItself()
    {
        var texture = new TestAssets(_project).AddTexture("Tex", 0xFF8080FF);
        var material = _project.Add(new Material(), "Mat");
        material.SetData(new MaterialData(material) { Shaders = [new LabShader { ShaderType = TwinShader.Type.StandardUnlit, TextureId = texture.URI }] });
        var viewport = new ViewportViewModel();
        var document = new DocumentViewModel(material, viewport);
        document.Initialize();
        viewport.Init(document);

        Assert.Equal(material.URI, viewport.ResourceOf(document.PropertyGraph.Root));
        Assert.Equal(texture.URI, viewport.ResourceOf(document.PropertyGraph.Find("Root.AssetData.Shaders[0].TextureId")!));
        Assert.Null(viewport.ResourceOf(document.PropertyGraph.Find("Root.AssetData.Shaders")!));
        viewport.Close();
    }

    [AvaloniaFact]
    public void ActivatedShadersAreTheShadersTypes()
    {
        var (document, data) = Open(TwinShader.Type.StandardUnlit);
        // What the game's bits would have ticked: StandardUnlit and UnlitBillboard share one
        data.ActivatedShaders = Twinsanity.TwinsanityInterchange.Enumerations.Enums.AppliedShaders.UnlitBillboard;

        var field = Editor<ActivatedShadersFieldViewModel>(document, "Root.AssetData.ActivatedShaders");

        Assert.Equal(["StandardUnlit"], Ticked(field));
        document.PropertyGraph.Find("Root.AssetData.Shaders[0].ShaderType")!.SetValue(TwinShader.Type.UnlitBillboard);
        Assert.Equal(["UnlitBillboard"], Ticked(field));
        document.Undo();
        Assert.Equal(["StandardUnlit"], Ticked(field));
        Assert.Contains("sky", field.Types.Single(item => item.Name == nameof(TwinShader.Type.UnlitSkydome)).Hint);
    }

    private static IEnumerable<DocumentNodeViewModel> EditorsUnder(DocumentNodeViewModel editor) =>
        editor is DocumentCompositeViewModel composite ? composite.Nodes.SelectMany(node => EditorsUnder(node).Prepend(node)) : [];

    // A shader's integer and floats only show for the types that read them, captioned by what they are: they were "Int Param" and four
    // "Float Param"s on every shader, grayed out on the types that read none. Shown first for a type reading none, they showed until the
    // type changed: the inspector made their editors after it had looked at what the nodes show
    [AvaloniaFact]
    public void TheParamsShowWhatTheShadersTypeReads()
    {
        var (document, _) = Open(TwinShader.Type.StandardLit);
        document.OpenInspector(document.PropertyGraph.Root, document.PropertyGraph.Find("Root.AssetData.Shaders[0].FloatParam"));
        var window = new Window { Content = new DocumentScrollViewer { Content = document.Inspector }, Width = 800, Height = 2000 };
        window.Show();
        void Pump()
        {
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.UIThread.RunJobs();
            }
        }

        DocumentNodeViewModel Shown(string path) => EditorsUnder(document.Inspector!).Single(editor => editor.Property.Path == $"Root.AssetData.Shaders[0].{path}");
        string[] FloatCaptions() => Enumerable.Range(0, 4).Select(index => Shown($"FloatParam[{index}]")).Where(editor => editor.IsVisible).Select(editor => editor.Caption).ToArray();
        bool Showing(string caption) => window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == caption && text.IsEffectivelyVisible);
        Pump();

        Assert.False(Shown("IntParam").IsVisible);
        Assert.False(Shown("FloatParam").IsVisible);
        Assert.False(Showing("Int Param"));
        Assert.False(Showing("Float Param"));

        var type = document.PropertyGraph.Find("Root.AssetData.Shaders[0].ShaderType")!;
        type.SetValue(TwinShader.Type.UnlitClothDeformation2);
        Pump();
        Assert.True(Shown("IntParam").IsVisible);
        Assert.Equal("Wave Mode", Shown("IntParam").Caption);
        Assert.Contains("2.3 times", Shown("IntParam").Hint);
        Assert.Equal("Waves", Shown("FloatParam").Caption);
        Assert.Equal(["Speed", "Amplitude X", "Amplitude Y", "Amplitude Z"], FloatCaptions());
        Assert.True(Showing("Amplitude Y"));

        type.SetValue(TwinShader.Type.LitReflectionSurface);
        Pump();
        Assert.False(Shown("IntParam").IsVisible);
        Assert.True(Shown("FloatParam").IsVisible);
        Assert.Equal("Reflection", Shown("FloatParam").Caption);
        Assert.Equal(["Offset"], FloatCaptions());
        Assert.False(Showing("Amplitude Y"));

        // Changes of one value in a row are one step: undo puts the type before them back and what it shows with it, redo the last
        document.Undo();
        Pump();
        Assert.Equal(TwinShader.Type.StandardLit, type.GetValue());
        Assert.False(Shown("IntParam").IsVisible);
        Assert.False(Shown("FloatParam").IsVisible);
        document.Redo();
        Pump();
        Assert.Equal(TwinShader.Type.LitReflectionSurface, type.GetValue());
        Assert.True(Shown("FloatParam").IsVisible);
        Assert.Equal(["Offset"], FloatCaptions());
        window.Close();
    }

    [AvaloniaFact]
    public void TheEditorsShowInTheInspector()
    {
        var (document, _) = Open(data => data.Shaders[0].Animation = ShaderAnimationTracks.SetAnimated(ShaderAnimationTracks.InsertFrame(ShaderAnimationTracks.Create(), 0), 2, true, 0),
            TwinShader.Type.StandardLit);
        document.OpenInspector(document.PropertyGraph.Root, document.PropertyGraph.Find("Root.AssetData.Shaders[0].Animation"));
        var window = new Window { Content = new DocumentScrollViewer { Content = document.Inspector }, Width = 800, Height = 1200 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var timeline = window.GetVisualDescendants().OfType<ShaderAnimationTimeline>().Single();
        Assert.Equal(6, timeline.Tracks!.Count);
        Assert.Equal(2, timeline.Tracks[2].Values.Count);
        Assert.Single(window.GetVisualDescendants().OfType<ShaderAnimationEditorView>());
        Assert.Single(window.GetVisualDescendants().OfType<ActivatedShadersFieldView>());
        Assert.NotNull(window.CaptureRenderedFrame());
        window.Close();
    }

    [AvaloniaFact]
    public void TheSwitchesSetWhatTheShaderTakesFromTheAnimation()
    {
        var (document, data) = Open(setUp => setUp.Shaders[0].Animation = ShaderAnimationTracks.Create(), TwinShader.Type.StandardUnlit);
        var editor = Editor<ShaderAnimationEditorViewModel>(document, "Root.AssetData.Shaders[0].Animation");
        Assert.False(editor.MovesU);
        Assert.False(editor.Rows[0].IsUsed);

        editor.MovesU = true;
        editor.DrivesColor = true;

        Assert.Equal(TwinShader.XScrollFormula.FromAnimation, data.Shaders[0].XScrollSettings);
        Assert.True(data.Shaders[0].AnimationDrivesColor);
        Assert.Equal([true, false, true, true, true, true], editor.Rows.Select(row => row.IsUsed));
        Assert.Equal([true, false, true, true, true, true], editor.Tracks.Select(track => track.Used));
        Assert.Null(editor.Rows[0].UnusedHint);
        Assert.Contains("Y Scroll Settings", editor.Rows[1].UnusedHint);
        // A step each
        document.Undo();
        Assert.False(data.Shaders[0].AnimationDrivesColor);
        Assert.False(editor.DrivesColor);
        document.Undo();
        Assert.Equal(TwinShader.XScrollFormula.Disabled, data.Shaders[0].XScrollSettings);
        Assert.False(editor.MovesU);

        // Set in the shader's own field, the switch follows
        document.PropertyGraph.Find("Root.AssetData.Shaders[0].YScrollSettings")!.SetValue(TwinShader.YScrollFormula.FromAnimation);
        Assert.True(editor.MovesV);
        Assert.True(editor.Rows[1].IsUsed);
    }

    // Tracks the shader doesn't take are grayed out: their rows disabled, their keys gray and not dragged until their switch is on
    [AvaloniaFact]
    public void UnusedTracksAreGrayedOutAndKeepTheirKeys()
    {
        var (document, data) = Open(setUp => setUp.Shaders[0].Animation = ShaderAnimationTracks.SetAnimated(ShaderAnimationTracks.InsertFrame(ShaderAnimationTracks.Create(), 0), 2, true, 0),
            TwinShader.Type.StandardUnlit);
        var editor = Editor<ShaderAnimationEditorViewModel>(document, "Root.AssetData.Shaders[0].Animation");
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 700 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var rows = window.GetVisualDescendants().OfType<Grid>().Where(grid => grid.Classes.Contains("track")).ToList();
        Assert.Equal(6, rows.Count);
        Assert.All(rows, row => Assert.False(row.IsEffectivelyEnabled));
        var timeline = window.GetVisualDescendants().OfType<ShaderAnimationTimeline>().Single();
        editor.SelectedTrack = 2;
        Dispatcher.UIThread.RunJobs();

        void DragFirstRedKey()
        {
            var key = Avalonia.VisualExtensions.TranslatePoint(timeline, new Avalonia.Point(timeline.FrameX(0), timeline.ValueY(ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 0))), window)!.Value;
            window.MouseDown(key, MouseButton.Left);
            window.MouseMove(key + new Avalonia.Point(0, 40));
            window.MouseUp(key + new Avalonia.Point(0, 40), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        DragFirstRedKey();
        Assert.Equal(1.0f, ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 0));
        Assert.False(document.CanUndo);

        editor.DrivesColor = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([false, false, true, true, true, true], rows.Select(row => row.IsEffectivelyEnabled));
        DragFirstRedKey();
        Assert.True(ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 0) < 1.0f);
        window.Close();
    }

    [AvaloniaFact]
    public void AnimationsAreMadeAndEditedOnTheTimeline()
    {
        var (document, data) = Open(TwinShader.Type.StandardUnlit);
        var editor = Editor<ShaderAnimationEditorViewModel>(document, "Root.AssetData.Shaders[0].Animation");
        Assert.False(editor.HasAnimation);

        editor.AddAnimationCommand.Execute().Subscribe();

        Assert.True(editor.HasAnimation);
        Assert.NotNull(data.Shaders[0].Animation);
        Assert.Equal(1, editor.FrameCount);
        editor.Rows[2].IsAnimated = true;
        editor.InsertFrameCommand.Execute().Subscribe();
        Assert.Equal((2, 1), (editor.FrameCount, editor.Frame));
        editor.Rows[2].ValueText = "0.5";
        Assert.Equal(0.5f, ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 1));
        Assert.Equal(1.0f, ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 0));
        Assert.Equal([1.0f, 0.5f], editor.Tracks[2].Values);

        // A drag of a key is one step
        editor.BeginDrag();
        editor.DragKey(2, 0, 0.2f);
        editor.DragKey(2, 0, 0.3f);
        editor.EndDrag();
        Assert.Equal(0.3f, ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 0), 1e-3f);
        document.Undo();
        Assert.Equal(1.0f, ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 0));
        Assert.Equal(0.5f, ShaderAnimationTracks.ValueAt(data.Shaders[0].Animation!, 2, 1));
        Assert.Equal([1.0f, 0.5f], editor.Tracks[2].Values);

        editor.RemoveAnimationCommand.Execute().Subscribe();
        Assert.Null(data.Shaders[0].Animation);
        Assert.False(editor.HasAnimation);
        document.Undo();
        Assert.NotNull(data.Shaders[0].Animation);
    }
}
