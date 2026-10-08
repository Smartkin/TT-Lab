using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReactiveUI.Validation.Extensions;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;

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
}
