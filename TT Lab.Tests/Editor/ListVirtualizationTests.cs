using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.Views.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Editor;

// A virtualizing panel takes the rows it hasn't made for the average size of the ones it has. With a list of structs that's way off once
// one of them is expanded, and near the end of the default chunk's 255 particle systems the extent flipped as the panel made and dropped
// the expanded one, which cycled the layout for seconds. Only lists of plain fields, whose rows are all the same size, get virtualized
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

    [AvaloniaFact]
    public void ALongListOfStructsIsNotVirtualizedWhileAListOfFieldsIs()
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
        document.OpenInspector(asset, asset.Find($"AssetData.{nameof(ParticleData.ParticleSystems)}[250]"));
        var window = new Window { Content = new DocumentScrollViewer { Content = document.Inspector }, Width = 800, Height = 700 };
        window.Show();
        Pump();

        var systems = PanelOf(window, asset.Find($"AssetData.{nameof(ParticleData.ParticleSystems)}")!);
        Assert.IsType<StackPanel>(systems);
        Assert.Equal(255, systems!.Children.Count);
        // The texture pages are links, plain fields, rows of one size
        var pages = asset.Find($"AssetData.{nameof(DefaultParticleData.TextureIDs)}")!;
        window.GetVisualDescendants().OfType<DocumentCompositeView>().Single(view => view.ViewModel?.Property == pages).ViewModel!.IsExpanded = true;
        Pump();
        Assert.IsType<VirtualizingStackPanel>(PanelOf(window, pages));
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

        Assert.IsType<VirtualizingStackPanel>(PanelOf(window, points.Property));
    }
}
