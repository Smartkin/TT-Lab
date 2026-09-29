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

// A particle system's name is its own in its version of the game, emitters play the system they name: renaming a system renames its
// emitters, in the same particles with the rename and in the other chunks once a default chunk's system is saved
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

    [AvaloniaFact]
    public void ANameAnotherSystemOfTheVersionHasIsntTaken()
    {
        AddDefaults("FIRE", "SPARK");
        Unload(AddChunkParticles("Cave Particles", ["BUBBLES"], []));
        // The Xbox version's systems are another game's
        AddChunkParticles("Xbox Particles", ["STEAM"], [], _project.Project.XboxPackage);
        var particles = AddChunkParticles("Beach Particles", ["SMOKE", "DUST"], []);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var editor = NameEditor(document, "Root.AssetData.ParticleSystems[0].Name");
        var data = ((IAsset)particles).GetData<ParticleData>();

        foreach (var taken in new[] { "FIRE", "BUBBLES", "DUST" })
        {
            editor.Text = taken;
            Pump();
            Assert.Equal("SMOKE", data.ParticleSystems[0].Name);
            Assert.False(editor.ValidationContext.IsValid);
            Assert.Contains("has a particle system named", editor.ValidationContext.Text.ToSingleLine());
        }

        editor.Text = "STEAM";
        Pump();
        Assert.Equal("STEAM", data.ParticleSystems[0].Name);
        Assert.True(editor.ValidationContext.IsValid);
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
