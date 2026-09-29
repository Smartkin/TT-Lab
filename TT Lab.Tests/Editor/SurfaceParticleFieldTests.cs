using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Tests.Editor;

// Collision surfaces play particle systems by their index in the default chunk's list (the game's table of systems starts with them),
// the inspector names them and picks them from that list
[Collection(ProjectCollection.Name)]
public sealed class SurfaceParticleFieldTests : IDisposable
{
    private const string ImpactParticles = "Root.AssetData.ImpactParticleSystemId";

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (DocumentViewModel Document, CollisionSurfaceData Data) OpenSurface(UInt16 impactParticles)
    {
        var defaults = _project.Add(new DefaultParticles(), "Global Particles", package: _project.Project.GlobalPackagePS2);
        var defaultData = new DefaultParticleData(defaults);
        defaultData.ParticleSystems.AddRange([new ParticleSystem { Name = "CRATE_BREAK" }, new ParticleSystem { Name = "WATER_SPLASH_1A" }]);
        defaults.SetData(defaultData);

        var surface = _project.Add(new CollisionSurface { Chunk = "default" }, "SURF_WATER");
        var data = new CollisionSurfaceData(surface) { ImpactParticleSystemId = impactParticles };
        surface.SetData(data);
        var document = new DocumentViewModel(surface);
        document.Initialize();
        return (document, data);
    }

    private static DefaultParticleSystemFieldViewModel Construct(DocumentViewModel document)
    {
        var editor = Assert.IsType<DefaultParticleSystemFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(ImpactParticles)!).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 800, Height = 600 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        return editor;
    }

    [AvaloniaFact]
    public void TheIndexNamesADefaultSystemAndIsPickedFromThem()
    {
        var (document, data) = OpenSurface(1);
        var editor = Construct(document);

        Assert.Equal("1  WATER_SPLASH_1A", editor.SystemName);
        Assert.Equal("The default chunk's", editor.LinkState);
        Assert.True(editor.HasSystem);
        editor.LoadChoices();
        Assert.Equal(["None", "0  CRATE_BREAK", "1  WATER_SPLASH_1A"], editor.ShownChoices.Select(choice => choice.Label));
        editor.Search = "crate";
        Assert.Equal(["0  CRATE_BREAK"], editor.ShownChoices.Select(choice => choice.Label));

        editor.Pick(editor.ShownChoices[0]);

        Assert.Equal(0, data.ImpactParticleSystemId);
        Assert.Equal("0  CRATE_BREAK", editor.SystemName);
        editor.LoadChoices();
        editor.Pick(editor.ShownChoices[0]);
        Assert.Equal(0xFFFF, data.ImpactParticleSystemId);
        Assert.Equal("None", editor.SystemName);
        Assert.False(editor.HasSystem);
        // Changes of one value in a row are one step
        document.Undo();
        Assert.Equal(1, data.ImpactParticleSystemId);
        Assert.Equal("1  WATER_SPLASH_1A", editor.SystemName);
    }

    [AvaloniaFact]
    public void AnIndexPastTheDefaultSystemsIsShownBroken()
    {
        var (document, _) = OpenSurface(129);
        var editor = Construct(document);

        Assert.Equal("129", editor.SystemName);
        Assert.True(editor.IsLinkBroken);
        Assert.Equal("The default chunk has 2 particle systems", editor.LinkState);
        // New surfaces play none
        Assert.Equal(0xFFFF, new CollisionSurfaceData(new CollisionSurface()).StepParticleSystemId);
    }
}
