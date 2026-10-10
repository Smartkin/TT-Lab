using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.Views.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// Right clicking a property's name copies its values and everything under them as JSON on the clipboard and pastes copied ones as one
// step: values of the same kind go over as they were copied, lists ask whether they go after the last item or over the items from the one
// pasted onto, values of another kind ask to be overwritten, and what the property's rules limit is listed before anything changes
[Collection(ProjectCollection.Name)]
public sealed class CopyPasteValuesTests : IDisposable
{
    // The new chunk's resources are its scenery, links, particles and the instance of Crash
    private const string CrashObject = "Root.ChunkResources[3][data].AssetData.ObjectId[data].AssetData";

    private readonly TestProject _project = new();
    private readonly Func<string, string, IReadOnlyList<string>, Task<int?>> _ask = ValuesPaste.Ask;
    private readonly List<string> _asked = [];
    private readonly Queue<int?> _answers = new();

    public CopyPasteValuesTests()
    {
        ValuesPaste.Ask = (title, message, _) =>
        {
            _asked.Add($"{title}: {message}");
            return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
        };
    }

    public void Dispose()
    {
        ValuesPaste.Ask = _ask;
        _project.Dispose();
    }

    private static DocumentViewModel Open(IAsset asset)
    {
        var document = new DocumentViewModel(asset);
        document.Initialize();
        return document;
    }

    private static PropertyNode Node(DocumentViewModel document, string path) => document.PropertyGraph.Find(path)!;

    private static Task Copy(DocumentViewModel document, string path) => ValuesClipboard.WriteTextAsync(CopiedValues.Of(Node(document, path)));

    private static Task CopyAsset(IAsset asset) => ValuesClipboard.WriteTextAsync(CopiedValues.OfAsset(Open(asset).PropertyGraph.Root));

    private static Task<bool> Paste(DocumentViewModel document, string path) => ValuesPaste.PasteAsync(document, Node(document, path), false, false);

    private static Task<bool> PasteAsset(DocumentViewModel document) => ValuesPaste.PasteAsync(document, document.PropertyGraph.Root, true, false);

    private static T Data<T>(IAsset asset) where T : TT_Lab.AssetData.AbstractAssetData => asset.GetData<T>();

    private Camera AddCamera(string name, Action<CameraData> values, int layout = 4)
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = layout }, name);
        var data = new CameraData(camera);
        values(data);
        camera.SetData(data);
        return camera;
    }

    private GameObject AddObject(string name, UInt32 id, Action<GameObjectData> values)
    {
        var gameObject = _project.Add(new GameObject(), name, id);
        var data = new GameObjectData(gameObject) { Name = name };
        values(data);
        gameObject.SetData(data);
        return gameObject;
    }

    private Material AddMaterial(string name, int shaders)
    {
        var material = _project.Add(new Material(), name);
        var data = new MaterialData(material);
        while (data.Shaders.Count < shaders)
        {
            data.Shaders.Add(new LabShader());
        }

        material.SetData(data);
        return material;
    }

    // A new chunk of the package, whose instance of Crash is of the global package's Crash object
    private (LevelChunk Chunk, GameObject Crash) ChunkWithCrash(Package package)
    {
        _project.BuildProjectTree(Path.Combine(package.Name, "levels"), Path.Combine(package.Name, "Graphics"));
        var crash = AddObject("Crash", 0x0, data => data.FloatProperties.Add(1.5f));
        crash.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
        var folder = _project.GetFolder(package, "levels");
        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, "hub", string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        return (chunk, crash);
    }

    private static T Activated<T>(DocumentViewModel document, string path) where T : DocumentNodeViewModel
    {
        var editor = Assert.IsAssignableFrom<T>(EditorDescRegistry.GetDesc(document, Node(document, path)).Construct());
        editor.Activator.Activate();
        return editor;
    }

    [AvaloniaFact]
    public async Task CopiedValuesAreTheirJsonOnTheClipboardExactly()
    {
        // A shader's leftover bits and floats no shorter text gives back
        var leftover = BitConverter.UInt32BitsToSingle(0x7FC00123);
        var from = AddCamera("From", data => data.BlendTime = leftover);
        var to = AddCamera("To", data => data.BlendTime = 1.0f);
        var toDocument = Open(to);

        await Copy(Open(from), "Root.AssetData.BlendTime");
        using (var json = JsonDocument.Parse((await ValuesClipboard.ReadTextAsync())!))
        {
            Assert.Equal("From › BlendTime", json.RootElement.GetProperty("From").GetString());
            Assert.Equal("bits:0x7FC00123", json.RootElement.GetProperty("Values").GetString());
        }

        Assert.True(await Paste(toDocument, "Root.AssetData.BlendTime"));
        Assert.Equal(0x7FC00123u, BitConverter.SingleToUInt32Bits(Data<CameraData>(to).BlendTime));
        // One step, named the way the history names what changed
        Assert.Equal("Pasted values into 'To › BlendTime'", toDocument.History.Current.Description);
        toDocument.Undo();
        Assert.Equal(1.0f, Data<CameraData>(to).BlendTime);

        Single[] floats = [-0.0f, 1e-45f, 0.1f, Single.MaxValue, 16777217.0f];
        var source = AddObject("Source", 0x10, data => data.FloatProperties = [..floats]);
        var target = AddObject("Target", 0x11, data => data.FloatProperties = [5, 6, 7, 8, 9]);
        await Copy(Open(source), "Root.AssetData.FloatProperties");
        _answers.Enqueue(1);
        Assert.True(await Paste(Open(target), "Root.AssetData.FloatProperties"));
        Assert.Equal(floats.Select(BitConverter.SingleToUInt32Bits), Data<GameObjectData>(target).FloatProperties.Select(BitConverter.SingleToUInt32Bits));
    }

    [AvaloniaFact]
    public async Task ValuesOnlyGoWhereTheirTypeDoes()
    {
        var camera = AddCamera("Camera", data =>
        {
            data.BlendTime = 2.0f;
            data.TargetBoxMin = new Vector4(1, 2, 3, 1);
        });
        var document = Open(camera);

        await Copy(document, "Root.AssetData.BlendTime");
        var copied = CopiedValues.Read(await ValuesClipboard.ReadTextAsync());
        Assert.Null(ValuesPaste.WhyNot(copied, Node(document, "Root.AssetData.BlendTime"), false, false));
        // Numbers only go where the same type of number does
        Assert.Equal("The copied values are a Single, this is a UInt32", ValuesPaste.WhyNot(copied, Node(document, "Root.AssetData.FovStart"), false, false));
        Assert.Equal("This value is grayed out", ValuesPaste.WhyNot(copied, Node(document, "Root.AssetData.BlendTime"), false, true));
        // A whole asset's values only go into an asset's header, and an asset's header only takes them
        Assert.Contains("not a whole asset's", ValuesPaste.WhyNot(copied, document.PropertyGraph.Root, true, false));
        await CopyAsset(camera);
        var asset = CopiedValues.Read(await ValuesClipboard.ReadTextAsync());
        Assert.Contains("they go into an asset's header", ValuesPaste.WhyNot(asset, Node(document, "Root.AssetData.BlendTime"), false, false));
        Assert.Null(ValuesPaste.WhyNot(asset, document.PropertyGraph.Root, true, false));

        await Copy(document, "Root.AssetData.TargetBoxMin");
        Assert.Null(ValuesPaste.WhyNot(CopiedValues.Read(await ValuesClipboard.ReadTextAsync()), Node(document, "Root.AssetData.TargetBoxMax"), false, false));
    }

    [AvaloniaFact]
    public async Task ListsTakeItemsAfterTheirLastOrOverTheirsFromTheOnePastedOnto()
    {
        var source = AddObject("Source", 0x10, data => data.FloatProperties = [7, 8]);
        var target = AddObject("Target", 0x11, data => data.FloatProperties = [1, 2, 3]);
        await Copy(Open(source), "Root.AssetData.FloatProperties");
        var document = Open(target);
        List<Single> Floats() => Data<GameObjectData>(target).FloatProperties;

        _answers.Enqueue(1);
        await Paste(document, "Root.AssetData.FloatProperties");
        Assert.Contains("Append adds them after its last item", _asked[^1]);
        Assert.Equal([7, 8, 3], Floats());
        document.Undo();
        Assert.Equal([1, 2, 3], Floats());

        // Over the items from the one pasted onto, on past the list's last
        _answers.Enqueue(1);
        await Paste(document, "Root.AssetData.FloatProperties[2]");
        Assert.Equal([1, 2, 7, 8], Floats());
        document.Undo();

        _answers.Enqueue(0);
        await Paste(document, "Root.AssetData.FloatProperties[1]");
        Assert.Equal([1, 2, 3, 7, 8], Floats());
        document.Undo();
        Assert.Equal([1, 2, 3], Floats());

        // Cancelled, nothing changes
        Assert.False(await Paste(document, "Root.AssetData.FloatProperties"));
        Assert.Equal([1, 2, 3], Floats());
    }

    // An asset's name stays, its layout goes with the values, and its lists are gone over from their start like a list pasted onto
    [AvaloniaFact]
    public async Task AWholeAssetsValuesKeepTheTargetsNameAndGoOverItsListsFromTheirStart()
    {
        var from = AddCamera("From", data => data.BlendTime = 3.0f, layout: 4);
        var to = AddCamera("To", data => data.BlendTime = 1.0f, layout: 1);
        await CopyAsset(from);
        var document = Open(to);

        Assert.True(await PasteAsset(document));
        Assert.Equal("To", to.Alias);
        Assert.Equal(4, to.LayoutID);
        Assert.Equal(3.0f, Data<CameraData>(to).BlendTime);
        Assert.Equal("Pasted values into 'To'", document.History.Current.Description);
        document.Undo();
        Assert.Equal(1, to.LayoutID);
        Assert.Equal(1.0f, Data<CameraData>(to).BlendTime);

        var source = AddObject("Source", 0x10, data => data.IntProperties = [5, 6]);
        var target = AddObject("Target", 0x11, data => data.IntProperties = [1, 2, 3]);
        await CopyAsset(source);
        Assert.True(await PasteAsset(Open(target)));
        Assert.Equal([5, 6, 3], Data<GameObjectData>(target).IntProperties);
        Assert.Equal("Target", target.Alias);
        Assert.Empty(_asked);
    }

    // Another kind of value has other values in it
    [AvaloniaFact]
    public async Task AValueOfAnotherKindOverwritesWhatsThereWhenAsked()
    {
        var boss = AddCamera("Boss", data => data.MainCamera1 = new BossCamera { Orbit = new Vector4(35, 65, 2, 0) });
        var line = AddCamera("Line", data => data.MainCamera1 = new CameraLine2());
        await Copy(Open(boss), "Root.AssetData.MainCamera1");
        var document = Open(line);

        Assert.False(await Paste(document, "Root.AssetData.MainCamera1"));
        Assert.Contains("CameraLine2 becomes BossCamera", _asked[^1]);
        Assert.IsType<CameraLine2>(Data<CameraData>(line).MainCamera1);

        _answers.Enqueue(0);
        Assert.True(await Paste(document, "Root.AssetData.MainCamera1"));
        var pasted = Assert.IsType<BossCamera>(Data<CameraData>(line).MainCamera1);
        Assert.Equal(65.0f, pasted.Orbit.Y);
        Assert.NotSame(Data<CameraData>(boss).MainCamera1, pasted);
        document.Undo();
        Assert.IsType<CameraLine2>(Data<CameraData>(line).MainCamera1);
    }

    // The rules of what's pasted onto apply unless the paste changes what they go by: an object of another type pasted whole brings the
    // sub types of its type
    [AvaloniaFact]
    public async Task ASubTypeTheObjectsTypeDoesntHaveIsLimitedUnlessThePasteChangesTheType()
    {
        var pickup = AddObject("Pickup", 0x10, data =>
        {
            data.Type = ITwinObject.ObjectType.Pickup;
            data.SubType = ObjectTypes.CustomPickupSubType;
        });
        var crate = AddObject("Crate", 0x11, data =>
        {
            data.Type = ITwinObject.ObjectType.Crate;
            data.SubType = 9;
        });
        var crateDocument = Open(crate);
        await Copy(Open(pickup), "Root.AssetData.SubType");

        Assert.False(await Paste(crateDocument, "Root.AssetData.SubType"));
        Assert.Contains("isn't one a Crate has, it becomes 1", _asked[^1]);
        Assert.Equal(9, Data<GameObjectData>(crate).SubType);

        _answers.Enqueue(0);
        Assert.True(await Paste(crateDocument, "Root.AssetData.SubType"));
        Assert.Equal(ObjectTypes.PlainSubType, Data<GameObjectData>(crate).SubType);

        _asked.Clear();
        await CopyAsset(pickup);
        Assert.True(await PasteAsset(crateDocument));
        Assert.Empty(_asked);
        Assert.Equal(ITwinObject.ObjectType.Pickup, Data<GameObjectData>(crate).Type);
        Assert.Equal(ObjectTypes.CustomPickupSubType, Data<GameObjectData>(crate).SubType);
    }

    [AvaloniaFact]
    public async Task ListsStopAtTheMostItemsTheyTake()
    {
        var glass = AddMaterial("Glass", 3);
        var metal = AddMaterial("Metal", 3);
        await Copy(Open(glass), "Root.AssetData.Shaders");
        var document = Open(metal);

        _answers.Enqueue(0);
        Assert.False(await Paste(document, "Root.AssetData.Shaders"));
        Assert.Contains("takes 4 items at most, 2 copied items aren't pasted", _asked[^1]);
        Assert.Equal(3, Data<MaterialData>(metal).Shaders.Count);

        _answers.Enqueue(0);
        _answers.Enqueue(0);
        Assert.True(await Paste(document, "Root.AssetData.Shaders"));
        Assert.Equal(4, Data<MaterialData>(metal).Shaders.Count);
    }

    [AvaloniaFact]
    public async Task PastedParticleSystemsGetANameNoOtherSystemHas()
    {
        var particles = _project.Add(new Particles { Chunk = "levels/beach" }, "Beach Particles", package: _project.Project.Ps2Package);
        var data = new ParticleData(particles);
        data.ParticleSystems.AddRange([new ParticleSystem { Name = "FIRE" }, new ParticleSystem { Name = "SMOKE" }]);
        particles.SetData(data);
        var document = Open(particles);
        await Copy(document, "Root.AssetData.ParticleSystems");

        _answers.Enqueue(0);
        _answers.Enqueue(0);
        Assert.True(await Paste(document, "Root.AssetData.ParticleSystems"));
        Assert.Contains("another particle system is named FIRE, the pasted one becomes FIRE_2", _asked[^1]);
        Assert.Equal(["FIRE", "SMOKE", "FIRE_2", "SMOKE_2"], data.ParticleSystems.Select(system => system.Name));
    }

    // A default system's values pasted into a level's system make it the level's own version of that system, played there in its place:
    // the name stays unless another of the level's systems has it
    [AvaloniaFact]
    public async Task ADefaultSystemPastedIntoALevelKeepsItsName()
    {
        var defaults = _project.Add(new DefaultParticles { Chunk = "startup/default" }, "Global Particles", package: _project.Project.GlobalPackagePS2);
        var defaultData = new DefaultParticleData(defaults);
        defaultData.ParticleSystems.Add(new ParticleSystem { Name = "FIRE", GenRate = 7 });
        defaults.SetData(defaultData);
        var particles = _project.Add(new Particles { Chunk = "levels/beach" }, "Beach Particles", package: _project.Project.Ps2Package);
        var data = new ParticleData(particles);
        data.ParticleSystems.Add(new ParticleSystem { Name = "SMOKE" });
        particles.SetData(data);
        await Copy(Open(defaults), "Root.AssetData.ParticleSystems[0]");
        var document = Open(particles);

        _answers.Enqueue(1);
        Assert.True(await Paste(document, "Root.AssetData.ParticleSystems[0]"));
        Assert.DoesNotContain(_asked, asked => asked.Contains("another particle system"));
        Assert.Equal(["FIRE"], data.ParticleSystems.Select(system => system.Name));
        Assert.Equal(7, (int)data.ParticleSystems[0].GenRate);
        Assert.Equal((data.ParticleSystems[0], false), data.FindSystem("FIRE"));

        _answers.Enqueue(0);
        _answers.Enqueue(0);
        Assert.True(await Paste(document, "Root.AssetData.ParticleSystems"));
        Assert.Contains("another particle system is named FIRE, the pasted one becomes FIRE_2", _asked[^1]);
        Assert.Equal(["FIRE", "FIRE_2"], data.ParticleSystems.Select(system => system.Name));
    }

    // Values that follow a pasted one are worked out the way an edit works them out, unless they're pasted too
    [AvaloniaFact]
    public async Task APastedTypeFitsTheObjectToItButNotWhenItsListsComeAlong()
    {
        var character = AddObject("Character", 0x10, data =>
        {
            data.Type = ITwinObject.ObjectType.Character;
            data.IntProperties = [3];
        });
        var prop = AddObject("Prop", 0x11, data =>
        {
            data.FloatProperties = [2.0f];
            data.IntProperties = [0, 200];
            data.InstanceStateFlags = (Enums.InstanceState)0x7D36;
        });
        var other = AddObject("Other", 0x12, data => data.IntProperties = [7, 8]);

        await Copy(Open(character), "Root.AssetData.Type");
        Assert.True(await Paste(Open(prop), "Root.AssetData.Type"));
        Assert.Equal(ITwinObject.ObjectType.Character, Data<GameObjectData>(prop).Type);
        Assert.Equal([ObjectTypes.CharacterNone, 200, 2], Data<GameObjectData>(prop).IntProperties);
        Assert.Equal(56, Data<GameObjectData>(prop).FloatProperties.Count);

        await CopyAsset(character);
        Assert.True(await PasteAsset(Open(other)));
        Assert.Equal(ITwinObject.ObjectType.Character, Data<GameObjectData>(other).Type);
        Assert.Equal([3, 8], Data<GameObjectData>(other).IntProperties);
        Assert.Empty(Data<GameObjectData>(other).FloatProperties);
    }

    [AvaloniaFact]
    public async Task PastingIntoAChunksViewOfASharedAssetGivesTheChunkItsOwnValue()
    {
        var (chunk, crash) = ChunkWithCrash(_project.Project.GlobalPackagePS2);
        var source = AddObject("Source", 0x10, data => data.FloatProperties = [2.5f]);
        await Copy(Open(source), "Root.AssetData.FloatProperties");
        var document = Open(chunk);

        _answers.Enqueue(1);
        Assert.True(await Paste(document, $"{CrashObject}.FloatProperties"));
        document.Save();

        var @override = Assert.Single(chunk.Overrides);
        Assert.Equal(crash.URI, @override.Asset);
        Assert.Equal(2.5f, (float)@override.Values["AssetData.FloatProperties[0]"]);
        Assert.Equal([1.5f], Data<GameObjectData>(crash).FloatProperties);
    }

    [AvaloniaFact]
    public async Task ALinkToAPackageTheValuesPackageDoesntDependOnIsWarnedAboutUntilItDoes()
    {
        var ps2 = _project.Project.Ps2Package;
        var xbox = _project.Project.XboxPackage;
        // A model of the other version is used like any other package's (game objects, instances and behaviours aren't, AssetVersionsTests)
        var xboxModel = _project.Add(new OGI(), "Xbox Crash", 0x20, xbox);
        xboxModel.SetData(new OGIData(xboxModel));
        var crate = _project.Add(new GameObject(), "Crate", 0x21, ps2);
        crate.SetData(new GameObjectData(crate) { ModelSlots = [new ModelSlot { Ogi = xboxModel.URI }] });
        var link = Activated<UriLinkViewModel>(Open(crate), "Root.AssetData.ModelSlots[0].Ogi");
        Assert.True(link.IsMissingDependency);
        Assert.True(link.CanAddDependency);
        Assert.Equal("The referenced asset belongs to a package that the current one doesn't depend on!", link.MissingDependencyText);

        // Asked first, it can't be undone
        await link.AddDependencyCommand.Execute();
        Assert.Contains("can't be undone", _asked[^1]);
        Assert.DoesNotContain(xbox.URI, ps2.Dependencies);

        _answers.Enqueue(0);
        await link.AddDependencyCommand.Execute();
        Assert.Contains(xbox.URI, ps2.Dependencies);
        Assert.False(link.IsMissingDependency);
        Assert.Contains((string)xbox.URI, File.ReadAllText(Path.Combine(_project.AssetsPath, ps2.Name, $"{ps2.Name}.json")));
    }

    // A chunk's own value of an asset it shares is kept in the chunk's package
    [AvaloniaFact]
    public void AChunksOwnValueGoesByTheChunksPackage()
    {
        var ps2 = _project.Project.Ps2Package;
        var (chunk, crash) = ChunkWithCrash(ps2);
        var model = _project.Add(new OGI(), "Model", 0x30, ps2);
        model.SetData(new OGIData(model));
        Data<GameObjectData>(crash).ModelSlots = [new ModelSlot { Ogi = model.URI }];

        Assert.False(Activated<UriLinkViewModel>(Open(chunk), $"{CrashObject}.ModelSlots[0].Ogi").IsMissingDependency);
        // The object's own package is the global one, which depends on nothing
        Assert.True(Activated<UriLinkViewModel>(Open(crash), "Root.AssetData.ModelSlots[0].Ogi").IsMissingDependency);
    }

    [AvaloniaFact]
    public async Task CopyingTextForgetsTheCopiedValues()
    {
        var camera = AddCamera("Camera", data => data.BlendTime = 2.0f);
        var document = Open(camera);
        var editor = EditorDescRegistry.GetDesc(document, Node(document, "Root.AssetData.BlendTime")).Construct();

        await editor.CopyValuesAsync();
        Assert.Null(await editor.WhyValuesCantBePastedAsync());
        await ValuesClipboard.WriteTextAsync("Some text copied in a code editor");
        Assert.Equal("No values are copied", await editor.WhyValuesCantBePastedAsync());
    }

    [AvaloniaFact]
    public async Task ThePropertysMenuGraysPasteOutWithTheReason()
    {
        var camera = AddCamera("Camera", data => data.BlendTime = 2.0f);
        var document = Open(camera);
        var editor = Activated<DocumentNodeViewModel>(document, "Root.AssetData.BlendTime");
        await ValuesClipboard.WriteTextAsync(string.Empty);

        var items = PropertyMenu.Items(editor, await editor.WhyValuesCantBePastedAsync());
        Assert.Equal(2, items.Count);
        Assert.Equal("Copy values", ((MenuItem)items[0]).Header);
        var paste = (MenuItem)items[1];
        Assert.False(paste.IsEnabled);
        Assert.Contains("No values are copied", ((StackPanel)paste.Header!).Children.OfType<TextBlock>().Select(text => text.Text));

        await editor.CopyValuesAsync();
        paste = (MenuItem)PropertyMenu.Items(editor, await editor.WhyValuesCantBePastedAsync())[1];
        Assert.True(paste.IsEnabled);
        Assert.Equal("Paste values", paste.Header);

        // The field of view is grayed out without Sets Fov, it takes nothing
        await ValuesClipboard.WriteTextAsync(CopiedValues.Of(Node(document, "Root.AssetData.FovStart")));
        var fov = Activated<DocumentNodeViewModel>(document, "Root.AssetData.FovStart");
        Assert.True(fov.IsReadOnly);
        Assert.Equal("This value is grayed out", await fov.WhyValuesCantBePastedAsync());
    }

    // The Inspector panel's view of what was inspected takes the next inspected node over: the icons were left out of the next asset's
    // header, which was told it's an asset only after the view had it
    [AvaloniaFact]
    public void TheInspectedAssetsHeaderKeepsItsIconsWhenAnotherAssetIsInspected()
    {
        var (chunk, _) = ChunkWithCrash(_project.Project.GlobalPackagePS2);
        var document = Open(chunk);
        var host = new ContentControl { [!ContentControl.ContentProperty] = new Avalonia.Data.Binding(nameof(DocumentViewModel.Inspector)) { Source = document } };
        var window = new Window { Content = new TT_Lab.Controls.DocumentScrollViewer { Content = host }, Width = 900, Height = 700 };
        window.Show();
        List<Button> ShownIcons()
        {
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.UIThread.RunJobs();
            }

            return window.GetVisualDescendants().OfType<Button>()
                .Where(button => Equals(ToolTip.GetTip(button), DocumentNodeViewModel.CopyAssetValuesHint) && button.IsEffectivelyVisible).ToList();
        }

        var crash = Node(document, "Root.ChunkResources[3][data]");
        document.OpenInspector(crash);
        Assert.Single(ShownIcons());
        Assert.Equal(((IAsset)crash.Target).Alias, document.Inspector!.Caption);

        var particles = Node(document, "Root.ChunkResources[2][data]");
        document.OpenInspector(particles);
        Assert.Single(ShownIcons());
        Assert.Equal(((IAsset)particles.Target).Alias, document.Inspector!.Caption);

        // A part of an asset copies and pastes from its name's menu
        document.OpenInspector(Node(document, "Root.ChunkResources[3][data].AssetData"));
        Assert.Empty(ShownIcons());
        window.Close();
    }

    // The game's objects have long names: the caption took the header's width first and pushed its icons out of the inspector
    [AvaloniaFact]
    public void ALongAliasLeavesTheHeadersIconsInSight()
    {
        var gameObject = AddObject("_Bossarea_Earth_300404_1430_BossActorsOnly_Bossarea_e3earthhub_mechoonly_cutsceneonly_act_CORTEX_TRAINING_MINIBOSS_252", 0x30, _ => { });
        var document = Open(gameObject);
        var window = new Window { Content = new DocumentView { ViewModel = document }, Width = 500, Height = 400 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var header = window.GetVisualDescendants().OfType<DocumentModelView>().First(view => view.ViewModel!.IsAssetRoot).Header;
        var icons = ((StackPanel)Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => Equals(ToolTip.GetTip(button), DocumentNodeViewModel.CopyAssetValuesHint)).Parent!).Children;
        Assert.Equal(3, icons.Count);
        Assert.All(icons, icon =>
        {
            var right = Avalonia.VisualExtensions.TranslatePoint(icon, new Avalonia.Point(icon.Bounds.Width, 0), header)!.Value.X;
            Assert.True(icon.IsEffectivelyVisible && right <= header.Bounds.Width + 0.5, $"An icon ends at {right} in a {header.Bounds.Width} wide header");
        });
        var caption = header.Children.OfType<TextBlock>().First();
        Assert.True(caption.TextLayout.TextLines.Single().HasCollapsed, "The alias wasn't trimmed");
        window.Close();
    }

    // Only an asset's top has them, a document has a model view for every struct of a list
    [AvaloniaFact]
    public async Task AnAssetsHeaderCopiesAndPastesTheWholeAsset()
    {
        var camera = AddCamera("Camera", data => data.BlendTime = 2.0f);
        var document = Open(camera);
        var window = new Window { Content = new DocumentView { ViewModel = document }, Width = 900, Height = 700 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var copy = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => Equals(ToolTip.GetTip(button), DocumentNodeViewModel.CopyAssetValuesHint));
        var paste = (Button)((StackPanel)copy.Parent!).Children[1];
        await ValuesClipboard.WriteTextAsync(string.Empty);
        await document.Root.RefreshPasteStateAsync();
        Assert.False(paste.IsEnabled);
        Assert.Contains("No values are copied", ToolTip.GetTip(paste) as string);

        await document.Root.CopyValuesAsync();
        await document.Root.RefreshPasteStateAsync();
        Assert.True(paste.IsEnabled);
        window.Close();
    }
}
