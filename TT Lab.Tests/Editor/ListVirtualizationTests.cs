using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.Views.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Editor;

// Every list's rows are made only where the document's scroll viewer shows them, whatever their heights (DocumentListPanel). Avalonia's
// virtualizing panel took the rows it hadn't made for the average height of the ones it had: a wheel's notch at the bottom of Crash's object
// made his behaviour slots 963 pixels taller and jumped the document, and with an expanded struct near the end of the default chunk's 255
// particle systems the height flipped as the panel made and dropped it, which cycled the layout for seconds
[Collection(ProjectCollection.Name)]
public sealed class ListVirtualizationTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Panel? PanelOf(Window window, PropertyNode property)
    {
        var view = window.GetVisualDescendants().OfType<DocumentCompositeView>().Single(view => view.ViewModel?.Property == property);
        return view.EditorsContainer.ItemsPanelRoot;
    }

    private static (DocumentViewModel Document, Window Window) Show(DocumentViewModel document)
    {
        document.Initialize();
        var window = new Window { Content = new DocumentView { DataContext = document }, Width = 800, Height = 700 };
        window.Show();
        Pump();
        return (document, window);
    }

    private static void Render()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void ALongListOfStructsMakesTheRowsShownAndSettlesWithOneExpanded()
    {
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach");
        var defaults = _project.Add(new DefaultParticles { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath }, "Particles");
        defaults.SetData(new DefaultParticleData(defaults)
        {
            ParticleSystems = Enumerable.Range(0, 255).Select(i => new ParticleSystem { Name = $"System{i}" }).ToList(),
            ParticleInstances = [],
            TextureIDs = [LabURI.Empty, LabURI.Empty, LabURI.Empty],
        });
        chunk.ChunkResources.Add(defaults.URI);
        var document = new DocumentViewModel(chunk);
        document.Initialize();
        var asset = document.PropertyGraph.Find("Root.ChunkResources[0][data]")!;
        var target = asset.Find($"AssetData.{nameof(ParticleData.ParticleSystems)}[250]")!;
        document.OpenInspector(asset, target);
        var viewer = new DocumentScrollViewer { Content = document.Inspector };
        var window = new Window { Content = viewer, Width = 800, Height = 700 };
        window.Show();
        Render();

        var systems = Assert.IsType<DocumentListPanel>(PanelOf(window, asset.Find($"AssetData.{nameof(ParticleData.ParticleSystems)}")!));
        Assert.InRange(systems.MadeRows, 1, 100);
        var row = window.GetVisualDescendants().OfType<DocumentModelView>().Single(view => view.ViewModel?.Property == target);
        Assert.True(row.ViewModel!.IsExpanded);
        var top = row.TranslatePoint(new Point(0, 0), viewer)!.Value.Y;
        Assert.InRange(top, -row.Bounds.Height, viewer.Viewport.Height);
        // Settled: nothing moves once it's laid out
        var offset = viewer.Offset;
        Render();
        Assert.Equal(offset, viewer.Offset);

        // The texture pages are links, plain fields
        var pages = asset.Find($"AssetData.{nameof(DefaultParticleData.TextureIDs)}")!;
        window.GetVisualDescendants().OfType<DocumentCompositeView>().Single(view => view.ViewModel?.Property == pages).ViewModel!.IsExpanded = true;
        Render();
        Assert.IsType<DocumentListPanel>(PanelOf(window, pages));
    }

    // Crash's object at the bottom of its properties, the slots list above what's shown: a wheel's notch up moves what's shown by a notch
    [AvaloniaFact]
    public void RowsOfOtherHeightsAboveWhatsShownDontMoveIt()
    {
        var crash = _project.Add(new GameObject(), "CRASH", 0x0);
        crash.SetData(new GameObjectData(crash)
        {
            Type = ITwinObject.ObjectType.Character, BehaviourSlots = Enumerable.Repeat(LabURI.Empty, 111).ToList(),
            ObjectSlots = Enumerable.Repeat(LabURI.Empty, 5).ToList(), SoundSlots = Enumerable.Repeat(LabURI.Empty, 75).ToList(),
        });
        var (document, window) = Show(new DocumentViewModel(crash));
        var slots = (DocumentCompositeViewModel)document.Root.Nodes.Single(node => node.Property.Name == nameof(GameObjectData.BehaviourSlots));
        slots.IsExpanded = true;
        Render();
        var viewer = window.GetVisualDescendants().OfType<DocumentScrollViewer>().First();
        for (var i = 0; i < 20; i++)
        {
            viewer.Offset = new Vector(0, viewer.Extent.Height);
            Render();
        }

        var panel = Assert.IsType<DocumentListPanel>(PanelOf(window, slots.Property));
        Assert.InRange(panel.MadeRows, 0, 110);
        // A row below the list, shown at the bottom
        var sounds = window.GetVisualDescendants().OfType<DocumentCompositeView>().Single(view => view.ViewModel?.Property.Name == nameof(GameObjectData.SoundSlots));
        for (var notch = 0; notch < 8; notch++)
        {
            var before = sounds.TranslatePoint(new Point(0, 0), viewer)!.Value.Y;
            viewer.Offset = new Vector(0, viewer.Offset.Y - 50);
            Render();
            Assert.Equal(before + 50, sounds.TranslatePoint(new Point(0, 0), viewer)!.Value.Y, 1);
        }
    }

    [AvaloniaFact]
    public void AListOfFieldsIsVirtualized()
    {
        var path = _project.Add(new Path(), "Path");
        path.SetData(new PathData(path) { Points = Enumerable.Range(0, 3000).Select(i => new Vector3(i, 0, 0)).ToList(), ArcLengths = [], InverseSteps = [] });
        var (document, window) = Show(new DocumentViewModel(path));
        var points = (DocumentCompositeViewModel)document.Root.Nodes.Single(node => node.Property.Name == nameof(PathData.Points));

        points.IsExpanded = true;
        Pump();

        var panel = Assert.IsType<DocumentListPanel>(PanelOf(window, points.Property));
        Assert.InRange(panel.MadeRows, 1, 200);
    }
}
