using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.Views.Editors;

namespace TT_Lab.Tests.Editor;

// Emitters play the particle system they name, the chunk's own or the default chunk's
[Collection(ProjectCollection.Name)]
public sealed class ParticleSystemFieldTests : IDisposable
{
    private const string EmitterName = "Root.AssetData.ParticleInstances[0].Name";

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (DocumentViewModel Document, ParticleSystemInstance Emitter) OpenChunkParticles(string emitterName)
    {
        var defaults = _project.Add(new DefaultParticles(), "Global Particles", package: _project.Project.GlobalPackagePS2);
        var defaultData = new DefaultParticleData(defaults);
        defaultData.ParticleSystems.AddRange([new ParticleSystem { Name = "Spark" }, new ParticleSystem { Name = "Fire" }]);
        defaults.SetData(defaultData);

        var particles = _project.Add(new Particles(), "Particles");
        var data = new ParticleData(particles);
        data.ParticleSystems.AddRange([new ParticleSystem { Name = "Fire" }, new ParticleSystem { Name = "Smoke" }]);
        var emitter = new ParticleSystemInstance { Name = emitterName };
        data.ParticleInstances.Add(emitter);
        particles.SetData(data);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        return (document, emitter);
    }

    private static ParticleSystemFieldViewModel Construct(DocumentViewModel document)
    {
        var editor = Assert.IsType<ParticleSystemFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(EmitterName)!).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 800, Height = 600 };
        window.Show();
        Pump();
        return editor;
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    // The chunk's Fire is played in place of the default chunk's, which isn't offered
    [AvaloniaFact]
    public void ChunksSystemsComeBeforeTheDefaultOnes()
    {
        var (document, _) = OpenChunkParticles("Fire");
        var editor = Construct(document);

        editor.LoadChoices();

        Assert.Equal([("Fire", false), ("Smoke", false), ("Spark", true)], editor.ShownChoices.Select(choice => (choice.Name, choice.IsDefault)));
        Assert.True(editor.ShownChoices[0].Overrides);
        Assert.Equal("The chunk's, played in place of the default chunk's", editor.LinkState);
        Assert.False(editor.IsLinkBroken);

        editor.Search = "sp";
        Assert.Equal(["Spark"], editor.ShownChoices.Select(choice => choice.Name));
    }

    [AvaloniaFact]
    public void PickingASystemNamesItAndTellsWhereItsFrom()
    {
        var (document, emitter) = OpenChunkParticles("Smoke");
        var editor = Construct(document);
        editor.LoadChoices();

        editor.Pick(editor.ShownChoices.Single(choice => choice.Name == "Spark"));
        Pump();

        Assert.Equal("Spark", emitter.Name);
        Assert.Equal("The default chunk's", editor.LinkState);
        Assert.True(document.IsDirty);
    }

    // Particles opened on their own show the system in their own tree, a chunk's document in its inspector (ChunkOverrideEditingTests)
    [AvaloniaFact]
    public void TheChunksSystemIsRevealedInTheDocument()
    {
        var (document, _) = OpenChunkParticles("Smoke");
        var window = new Window { Content = new DocumentView { DataContext = document }, Width = 800, Height = 700 };
        window.Show();
        Pump();
        var editor = Construct(document);

        editor.GoToSystem();
        Pump();

        var highlighted = window.GetVisualDescendants().OfType<DockPanel>().Where(panel => panel.Classes.Contains("highlighted"))
            .Select(panel => ((DocumentNodeViewModel)panel.DataContext!).Property.Path);
        Assert.Equal(["Root.AssetData.ParticleSystems[1]"], highlighted);
    }

    [AvaloniaFact]
    public void TheDefaultParticlesHaveNoEmitters()
    {
        OpenChunkParticles("Fire");
        var defaults = _project.AssetManager.GetAllAssetsOf<DefaultParticles>().Single();
        var document = new DocumentViewModel(defaults);
        document.Initialize();

        var emitters = EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.ParticleInstances")!).Construct();
        new Window { Content = new ContentControl { Content = emitters }, Width = 800, Height = 600 }.Show();
        Pump();

        Assert.False(emitters.IsVisible);
    }

    // Duplicating an emitter in the viewport puts a copy right after it, the emitters after it keep their data
    [AvaloniaFact]
    public void ElementsGoInBetweenOthers()
    {
        var (document, emitter) = OpenChunkParticles("Fire");
        var particles = document.PropertyGraph.Find("Root.AssetData.ParticleInstances")!;
        var last = new ParticleSystemInstance { Name = "Smoke" };
        particles.InsertElement(1, last);
        var copy = new ParticleSystemInstance { Name = "Fire copy" };

        var node = particles.InsertElement(1, copy)!;

        Assert.Equal("Root.AssetData.ParticleInstances[1]", node.Path);
        Assert.Equal(["Fire", "Fire copy", "Smoke"], particles.Children.Select(child => ((ParticleSystemInstance)child.GetValue()!).Name));
        Assert.Same(last, document.PropertyGraph.Find("Root.AssetData.ParticleInstances[2].Name")!.Target);
        Assert.Same(emitter, document.PropertyGraph.Find("Root.AssetData.ParticleInstances[0].Name")!.Target);
        Assert.True(document.IsDirty);
    }

    [AvaloniaFact]
    public void NamesOfNoSystemAreShownAsBroken()
    {
        var (document, _) = OpenChunkParticles("Missing");

        var editor = Construct(document);

        Assert.True(editor.IsLinkBroken);
    }
}
