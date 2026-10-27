using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReactiveUI.Validation.Extensions;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.Views;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// Emitters play the system they name, their chunk's own or the default chunk's, so a system's name is its own in its chunk and a level's
// system of a default system's name is played there in its place: renaming a system renames its emitters, in the same particles with the
// rename and in the other chunks once a default chunk's system is saved
[Collection(ProjectCollection.Name)]
public sealed class ParticleSystemNameTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private DefaultParticles AddDefaults(params string[] systems)
    {
        var defaults = _project.Add(new DefaultParticles { Chunk = "startup/default" }, "Global Particles", package: _project.Project.GlobalPackagePS2);
        var data = new DefaultParticleData(defaults);
        data.ParticleSystems.AddRange(systems.Select(name => new ParticleSystem { Name = name }));
        defaults.SetData(data);
        return defaults;
    }

    private Particles AddChunkParticles(string name, string[] systems, string[] emitters, TT_Lab.Assets.Package? package = null)
    {
        var particles = _project.Add(new Particles { Chunk = $"levels/{name.Split(' ')[0].ToLowerInvariant()}" }, name, package: package ?? _project.Project.Ps2Package);
        var data = new ParticleData(particles);
        data.ParticleSystems.AddRange(systems.Select(system => new ParticleSystem { Name = system }));
        data.ParticleInstances.AddRange(emitters.Select(emitter => new ParticleSystemInstance { Name = emitter }));
        particles.SetData(data);
        return particles;
    }

    // Written to its file and let go of, like a chunk nobody has open
    private static void Unload(SerializableAsset asset)
    {
        asset.Serialize(SerializationFlags.SaveData);
        Assert.False(asset.IsLoaded);
    }

    // Shown, its rules and the text it takes are set up when it activates
    private static ParticleSystemNameFieldViewModel NameEditor(DocumentViewModel document, string path)
    {
        var editor = Assert.IsType<ParticleSystemNameFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 200 }.Show();
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

    // A default system's name makes the level's system its own version of that one, another level's is that level's own business
    [AvaloniaFact]
    public void ANameAnotherSystemOfTheChunkHasIsntTaken()
    {
        AddDefaults("FIRE", "SPARK");
        Unload(AddChunkParticles("Cave Particles", ["BUBBLES"], []));
        var particles = AddChunkParticles("Beach Particles", ["SMOKE", "DUST"], []);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var editor = NameEditor(document, "Root.AssetData.ParticleSystems[0].Name");
        var data = ((IAsset)particles).GetData<ParticleData>();

        editor.Text = "DUST";
        Pump();
        Assert.Equal("SMOKE", data.ParticleSystems[0].Name);
        Assert.False(editor.ValidationContext.IsValid);
        Assert.Contains("Beach Particles has another particle system named DUST", editor.ValidationContext.Text.ToSingleLine());

        foreach (var free in new[] { "FIRE", "BUBBLES" })
        {
            editor.Text = free;
            Pump();
            Assert.Equal(free, data.ParticleSystems[0].Name);
            Assert.True(editor.ValidationContext.IsValid);
        }

        Assert.Equal((data.ParticleSystems[0], false), data.FindSystem("BUBBLES"));
    }

    [AvaloniaFact]
    public void TheDefaultChunksSystemsOnlyKeepTheirNamesApartFromEachOther()
    {
        var defaults = AddDefaults("FIRE", "SPARK");
        AddChunkParticles("Beach Particles", ["SMOKE"], []);
        var document = new DocumentViewModel(defaults);
        document.Initialize();
        var editor = NameEditor(document, "Root.AssetData.ParticleSystems[0].Name");
        var data = ((IAsset)defaults).GetData<DefaultParticleData>();

        editor.Text = "SPARK";
        Pump();
        Assert.Equal("FIRE", data.ParticleSystems[0].Name);
        Assert.False(editor.ValidationContext.IsValid);

        editor.Text = "SMOKE";
        Pump();
        Assert.Equal("SMOKE", data.ParticleSystems[0].Name);
        Assert.True(editor.ValidationContext.IsValid);
    }

    // The chunk's emitters can't pick a default system the chunk has its own version of, which they'd play instead
    [AvaloniaFact]
    public void TheChunksOwnVersionOfADefaultSystemIsWhatItsEmittersPick()
    {
        AddDefaults("FIRE", "SPARK");
        var particles = AddChunkParticles("Beach Particles", ["FIRE"], ["FIRE"]);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var editor = Assert.IsType<ParticleSystemFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.ParticleInstances[0].Name")!).Construct());
        new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 200 }.Show();
        Pump();

        editor.LoadChoices();

        Assert.Equal([new ParticleSystemChoice("FIRE", false, true), new ParticleSystemChoice("SPARK", true)], editor.ShownChoices);
        Assert.Equal("The chunk's, played in place of the default chunk's", editor.LinkState);
    }

    [AvaloniaFact]
    public void EmittersOfTheSameParticlesFollowTheRenameInItsStep()
    {
        AddDefaults("SMOKE");
        var particles = AddChunkParticles("Beach Particles", ["SMOKE", "FIRE"], ["SMOKE", "FIRE", "SMOKE"]);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var data = ((IAsset)particles).GetData<ParticleData>();
        var name = document.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].Name")!;

        name.SetValue("SMO");
        name.SetValue("SMOG");

        Assert.Equal(["SMOG", "FIRE", "SMOG"], data.ParticleInstances.Select(emitter => emitter.Name));
        document.Undo();
        Assert.Equal("SMOKE", data.ParticleSystems[0].Name);
        Assert.Equal(["SMOKE", "FIRE", "SMOKE"], data.ParticleInstances.Select(emitter => emitter.Name));
        document.Redo();
        Assert.Equal(["SMOG", "FIRE", "SMOG"], data.ParticleInstances.Select(emitter => emitter.Name));
    }

    [AvaloniaFact]
    public void AddedSystemsGetANameOfTheirOwn()
    {
        AddDefaults("NEW_SYSTEM");
        var particles = AddChunkParticles("Beach Particles", ["FIRE"], []);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var data = ((IAsset)particles).GetData<ParticleData>();
        var systems = document.PropertyGraph.Find("Root.AssetData.ParticleSystems")!;

        systems.InsertElement(1, new ParticleSystem());
        systems.InsertElement(2, new ParticleSystem { Name = "FIRE" });

        Assert.Equal(["FIRE", "NEW_SYSTEM_2", "FIRE_2"], data.ParticleSystems.Select(system => system.Name));
        // Taking one out and putting it back is the one it was
        document.Undo();
        document.Redo();
        Assert.Equal(["FIRE", "NEW_SYSTEM_2", "FIRE_2"], data.ParticleSystems.Select(system => system.Name));
        // Names fill the game's 16 characters at most
        systems.InsertElement(3, new ParticleSystem { Name = "ABCDEFGHIJKLMNOP" });
        systems.InsertElement(4, new ParticleSystem { Name = "ABCDEFGHIJKLMNOP" });
        Assert.Equal("ABCDEFGHIJKLMN_2", data.ParticleSystems[4].Name);
    }

    [AvaloniaFact]
    public async Task SavingARenamedDefaultSystemRenamesTheOtherChunksEmitters()
    {
        var defaults = AddDefaults("FIRE", "SPARK");
        var cave = AddChunkParticles("Cave Particles", [], ["FIRE", "SPARK"]);
        // Its own FIRE is what its emitter plays
        var lab = AddChunkParticles("Lab Particles", ["FIRE"], ["FIRE"]);
        Unload(cave);
        Unload(lab);
        var opened = AddChunkParticles("Beach Particles", [], ["FIRE"]);

        var renamed = await ParticleSystemLinks.RenameInOtherChunksAsync(defaults, new Dictionary<string, string> { ["FIRE"] = "BLAZE" });

        Assert.Equal(2, renamed);
        Assert.Equal(["BLAZE", "SPARK"], ((IAsset)cave).GetData<ParticleData>().ParticleInstances.Select(emitter => emitter.Name));
        Assert.Equal(["FIRE"], ((IAsset)lab).GetData<ParticleData>().ParticleInstances.Select(emitter => emitter.Name));
        Assert.Equal(["BLAZE"], ((IAsset)opened).GetData<ParticleData>().ParticleInstances.Select(emitter => emitter.Name));
        Assert.Contains("\"BLAZE\"", File.ReadAllText(opened.FullDataPath));
    }

    // A chunk with its own system of the new name plays that one with the emitters renamed to it, which the log warns about
    [AvaloniaFact]
    public async Task ALevelsOwnSystemOfTheNewNameIsPlayedByItsRenamedEmitters()
    {
        var panel = new TT_Lab.ViewModels.LogViewModel(new TestProject.NullEventAggregator(), new TT_Lab.Project.ProjectManager(new TestProject.NullEventAggregator()));
        Log.SetViewModel(panel);
        try
        {
            var defaults = AddDefaults("FIRE");
            var cave = AddChunkParticles("Cave Particles", ["BLAZE"], ["FIRE", "BLAZE"]);
            Unload(cave);

            Assert.Equal(1, await ParticleSystemLinks.RenameInOtherChunksAsync(defaults, new Dictionary<string, string> { ["FIRE"] = "BLAZE" }));

            var data = ((IAsset)cave).GetData<ParticleData>();
            Assert.Equal(["BLAZE", "BLAZE"], data.ParticleInstances.Select(emitter => emitter.Name));
            Assert.False(data.FindSystem("BLAZE")!.Value.IsDefault);
            for (var wait = 0; wait < 100 && !panel.Text.Text.Contains("Cave Particles has particle systems of its own named BLAZE"); wait++)
            {
                await Task.Delay(20);
            }

            Assert.Contains("Cave Particles has particle systems of its own named BLAZE", panel.Text.Text);
        }
        finally
        {
            Log.SetViewModel(null);
        }
    }

    [AvaloniaFact]
    public async Task TheDefaultChunksDocumentRenamesTheOtherChunksEmittersWhenSaved()
    {
        var defaults = AddDefaults("FIRE", "SPARK");
        var cave = AddChunkParticles("Cave Particles", [], ["FIRE"]);
        Unload(cave);
        var document = new DocumentViewModel(defaults);
        document.Initialize();
        document.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].Name")!.SetValue("BLAZE");
        // Nothing happens to the other chunks before the rename is saved
        Assert.DoesNotContain("\"BLAZE\"", File.ReadAllText(cave.FullDataPath));

        document.Save();

        for (var wait = 0; wait < 100 && !File.ReadAllText(cave.FullDataPath).Contains("\"BLAZE\""); wait++)
        {
            await Task.Delay(50);
        }

        Assert.Equal(["BLAZE"], ((IAsset)cave).GetData<ParticleData>().ParticleInstances.Select(emitter => emitter.Name));
    }

    // The particles' editor and the default chunk's scene share the data: a rename made in one and saved by the other is followed, the one that
    // made it closed without saving. Only the renaming document's save followed it, and the other chunks' emitters kept a name nothing had
    [AvaloniaFact]
    public async Task ARenameSavedByTheOtherEditorIsFollowed()
    {
        var defaults = AddDefaults("FIRE", "SPARK");
        defaults.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var cave = AddChunkParticles("Cave Particles", [], ["FIRE"]);
        Unload(cave);
        var renaming = new DocumentViewModel(defaults);
        renaming.Initialize();
        var saving = new DocumentViewModel(defaults);
        saving.Initialize();
        renaming.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].Name")!.SetValue("BLAZE");

        saving.Save();
        renaming.Close();
        await ParticleSystemLinks.WhenFollowed();

        Assert.Contains("\"BLAZE\"", File.ReadAllText(defaults.FullDataPath));
        Assert.Equal(["BLAZE"], ((IAsset)cave).GetData<ParticleData>().ParticleInstances.Select(emitter => emitter.Name));
        // What the save wrote is what's kept of the file: saved again, nothing is followed again
        saving.Save();
        Assert.False(ParticleSystemLinks.IsFollowing);
        saving.Close();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    // The editors closing the project asks about (the shell's), answered Save, with the log panel closing the project clears
    private sealed class ShellEditors : IDisposable
    {
        private readonly Window _window;

        public ShellEditors()
        {
            Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), new ProjectManager(new TestProject.NullEventAggregator())));
            var resources = new ResourcesEditorsViewModel();
            Editors = new EditorsViewModel(new ScenesEditorsViewModel(), resources, Locator.Current.GetService<ProjectManager>()!);
            Editors.AskAboutUnsavedChanges = what =>
            {
                Asked.Add(what);
                return Task.FromResult(UnsavedChangesDialogue.AnswerResult.YES);
            };
            // The renames in open chunks go through their documents
            Locator.CurrentMutable.RegisterConstant(Editors);
            _window = new Window { Content = new EditorsViewerView { DataContext = resources }, Width = 1200, Height = 800 };
            _window.Show();
        }

        public EditorsViewModel Editors { get; }
        public List<string> Asked { get; } = [];

        public async Task<TabbedEditorViewModel> Open(IAsset asset)
        {
            Editors.OpenEditor(asset);
            var tab = Editors.AllTabs.Single(tab => tab.EditableResource == asset.URI);
            await WaitUntil(() => tab.IsLoaded);
            return tab;
        }

        public void Dispose()
        {
            _window.Close();
            Locator.CurrentMutable.UnregisterAll<EditorsViewModel>();
            // Clearing the log is posted
            Pump();
            Log.SetViewModel(null);
        }
    }

    // Saved at the question closing the project asks (opening another project and closing TT Lab ask it too), a default system's rename is
    // followed before it returns: the follow ran in the background, the project closed under it, and the other chunks' emitters kept a name
    // nothing had
    [AvaloniaFact]
    public async Task ARenameSavedAtTheClosePromptIsFollowedBeforeTheProjectCloses()
    {
        var defaults = AddDefaults("FIRE", "SPARK");
        defaults.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var cave = AddChunkParticles("Cave Particles", [], ["FIRE"]);
        Unload(cave);
        var (cavePath, defaultsPath) = (cave.FullDataPath, defaults.FullDataPath);
        using var shell = new ShellEditors();
        var tab = await shell.Open(defaults);
        tab.Document!.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].Name")!.SetValue("BLAZE");

        Assert.True(await shell.Editors.SaveOrDiscardUnsavedChanges());
        Locator.Current.GetService<ProjectManager>()!.CloseProject();

        Assert.Single(shell.Asked);
        Assert.Contains("\"BLAZE\"", File.ReadAllText(defaultsPath));
        Assert.Contains("\"BLAZE\"", File.ReadAllText(cavePath));
        Assert.DoesNotContain("\"FIRE\"", File.ReadAllText(cavePath));
    }

    // A level's scene that's open gets the rename into its document, which closing every editor discarded: it's saved once the follow is done,
    // the question asked once
    [AvaloniaFact]
    public async Task ARenameSavedAtTheClosePromptIsSavedInTheOpenScenes()
    {
        var package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(package.Name, "levels"), Path.Combine(package.Name, "Graphics"));
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
        var folder = _project.GetFolder(package, "levels");
        var beach = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, "beach", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(),
            asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        var beachParticles = beach.ChunkResources.Select(_project.AssetManager.GetAsset).OfType<Particles>().Single();
        ((IAsset)beachParticles).GetData<ParticleData>().ParticleInstances.Add(new ParticleSystemInstance { Name = "FIRE" });
        Unload(beachParticles);
        var defaults = AddDefaults("FIRE", "SPARK");
        defaults.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var cave = AddChunkParticles("Cave Particles", [], ["FIRE"]);
        Unload(cave);
        var (beachPath, cavePath) = (beachParticles.FullDataPath, cave.FullDataPath);
        using var shell = new ShellEditors();
        await shell.Open(beach);
        var tab = await shell.Open(defaults);
        tab.Document!.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].Name")!.SetValue("BLAZE");

        // Closing the project
        Assert.True(await shell.Editors.CloseAllEditors());
        shell.Editors.ClearRecentlyClosed();
        Locator.Current.GetService<ProjectManager>()!.CloseProject();

        Assert.Single(shell.Asked);
        Assert.Empty(shell.Editors.AllTabs);
        foreach (var path in new[] { beachPath, cavePath })
        {
            Assert.Contains("\"BLAZE\"", File.ReadAllText(path));
            Assert.DoesNotContain("\"FIRE\"", File.ReadAllText(path));
        }
    }
}
