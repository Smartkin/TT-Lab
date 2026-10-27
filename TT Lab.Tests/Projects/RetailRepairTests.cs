using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Splat;
using TT_Lab.Controls;
using TT_Lab.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.Project.Repair;
using TT_Lab.Tests.Support;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using static TT_Lab.Tests.Support.TestGeometry;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// TT Lab 1.1.0's unpack wrote into a project's game packages what only the disc can say is wrong: objects linking another chunk's graph of
// a reused ID or none and graphs without the game's starter ID (A1), chunks drawing another chunk's picture of a reused texture ID (A5) and
// chunks' own values linking variants the merge deleted (C7). The repair unpacks the disc again beside the project and takes its values
// where the project still has 1.1.0's, what the user changed stays and is noted. The projects here are the fixed unpack's files edited back
// to 1.1.0's values, the discs made up
[Collection(ProjectCollection.Name)]
public sealed class RetailRepairTests : IDisposable
{
    private const UInt32 Reused = 0x31;
    private const UInt32 MissileStarter = 0x30;
    private const UInt32 FurnitureStarter = 0x3E;
    private const UInt32 BearOrPlatform = 0x51;
    private const UInt32 PlatformStarter = 0x50;
    private const UInt32 BearDefault = 0x53;
    private const UInt32 PlatformDefault = 0x55;

    private const UInt16 HudId = 0x10;
    private const UInt16 OgiId = 0x20;
    private const UInt32 RigidModelId = 0x30;
    private const UInt32 ModelId = 0x40;
    private const UInt32 MaterialId = 0x50;
    private const UInt32 TextureId = 0x60;

    private const UInt16 LinkingId = 0x70;
    private const UInt16 LinkedId = 0x71;

    private readonly TestProject _project = new();
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _project.Dispose();
    }

    private string ProjectFile => Path.Combine(_project.Project.ProjectPath, "Test.tson");

    private static PS2ItemFactory FactoryOf(GamePlatform platform) => platform == GamePlatform.Xbox ? new XboxItemFactory() : new PS2ItemFactory();

    // The graph of one state and, when it has one, its starter, compiled for the version the way the game's files have them
    private static List<ITwinItem> Graph(GamePlatform platform, string name, UInt32 id, UInt32? starterId, UInt32? child = null)
    {
        var script = $"behaviour {name} {{\n" + (starterId != null ? "   starter {\n      assigner = {\n         AssignType = ME;\n      }\n   }\n" : "") +
                     "   state S() {\n   }\n}\n";
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write((Int32)id);
            writer.Write(Encoding.UTF8.GetBytes(script));
        }

        stream.Position = 0;
        var result = FactoryOf(platform).GenerateBehaviourGraph(stream);
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var graph = result.Get<ITwinBehaviourGraph>();
        graph.SetID(id);
        if (child != null)
        {
            graph.ScriptStates[0].BehaviourIndexOrSlot = (Int16)child.Value;
        }

        if (starterId == null)
        {
            return [graph];
        }

        var starter = result.Get<TwinBehaviourStarter>();
        starter.SetID(starterId.Value);
        starter.Compile(id);
        return [starter, graph];
    }

    private static ITwinObject Object(GamePlatform platform, string name, UInt16 id, UInt16[] slots, UInt16[] references, UInt16? message = null, Single mass = 1)
    {
        PS2AnyObject gameObject = platform == GamePlatform.Xbox ? new XboxAnyObject() : new PS2AnyObject();
        gameObject.Name = name;
        gameObject.BehaviourSlots = [..slots];
        gameObject.RefBehaviours = [..references];
        gameObject.FloatProperties = [mass];
        gameObject.IntProperties = [0, 255];
        if (message != null)
        {
            gameObject.TriggerBehaviours = [new TwinObjectTriggerBehaviour { MessageID = 5, TriggerBehaviour = message.Value }];
        }

        gameObject.BehaviourPack = platform == GamePlatform.Xbox ? new XboxBehaviourCommandPack() : new PS2BehaviourCommandPack();
        gameObject.SetID(id);
        return gameObject;
    }

    private static ITwinSection ScriptChunk(GamePlatform platform, IEnumerable<ITwinItem> behaviours, params ITwinObject[] objects)
    {
        var chunk = FactoryOf(platform).GenerateRM();
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var behaviourSection = code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        foreach (var behaviour in behaviours)
        {
            behaviourSection.AddItem(behaviour);
        }

        var objectSection = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        foreach (var gameObject in objects)
        {
            objectSection.AddItem(gameObject);
        }

        return FakeDisc.WithCollisionAndParticles(chunk, platform == GamePlatform.Xbox);
    }

    // A hub's object drawn by an OGI of one rigid model, whose material draws a picture of one color: the same items in every hub but the picture
    private static ITwinSection HubChunk(Byte red, Byte blue, UInt32 toolSlot = 0, ITwinTexture.TexturePixelFormat format = ITwinTexture.TexturePixelFormat.PSMT8)
    {
        var chunk = new PS2ItemFactory().GenerateRM();
        var graphics = chunk.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var texture = new PS2AnyTexture();
        texture.FromBitmap(Enumerable.Range(0, 16 * 16).Select(_ => new Color(red, 0, blue, 255)).ToList(), 16, ITwinTexture.TextureFunction.MODULATE, format);
        texture.Leftovers = new TwinTextureLeftovers { ToolSlot = toolSlot };
        texture.SetID(TextureId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION).AddItem(texture);
        var material = new PS2AnyMaterial { Name = "lambert50", Shaders = [new TwinShader { ShaderType = TwinShader.Type.StandardUnlit, TextureId = TextureId }] };
        material.SetID(MaterialId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION).AddItem(material);
        var model = new PS2AnyModel { SubModels = [RigidSubModel(new Random(31), TwinVifPadding.QuadWord, [12], false)] };
        model.Compile();
        model.SetID(ModelId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION).AddItem(model);
        var rigidModel = new PS2AnyRigidModel { Model = ModelId, Materials = [MaterialId] };
        rigidModel.SetID(RigidModelId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_RIGID_MODELS_SECTION).AddItem(rigidModel);

        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var ogi = new PS2AnyOGI
        {
            Joints =
            [
                new TwinJoint { Index = 0, ParentIndex = 255, Id = 255, LocalTranslation = new Vector4(0, 0, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1),
                    WorldTranslation = new Vector4(0, 0, 0, 1), UnusedRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) }
            ],
            RigidModelIds = [RigidModelId],
            JointIndices = [0],
            SkinInverseBindMatrices = [new Matrix4()]
        };
        ogi.SetID(OgiId);
        code.GetItem<ITwinSection>(Constants.CODE_OGIS_SECTION).AddItem(ogi);
        var gameObject = new PS2AnyObject { Name = "HUD", RefOGIs = [OgiId], FloatProperties = [1], IntProperties = [0, 255], BehaviourPack = new PS2BehaviourCommandPack() };
        gameObject.SetID(HudId);
        code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION).AddItem(gameObject);
        return FakeDisc.WithCollisionAndParticles(chunk, false);
    }

    private static PS2AnyObject SlotObject(string name, UInt16 id, Single mass, params UInt16[] slots)
    {
        var gameObject = new PS2AnyObject { Name = name, FloatProperties = [mass], IntProperties = [0, 255], ObjectSlots = [..slots], BehaviourPack = new PS2BehaviourCommandPack() };
        gameObject.SetID(id);
        return gameObject;
    }

    // In the PS2 archive's order High Seas' missile comes first and is the graph of the reused ID, AltEarth's furniture its variant; in the
    // order 1.1.0 read the Xbox disc in the furniture comes first. AltEarth's starter of the furniture has another ID than the one before it.
    // The Xbox version's default chunk has the missile too: read first (A8), it's the global package's graph of the ID, read last the Xbox
    // package's variant
    private FakeDisc Disc(GamePlatform platform)
    {
        var extension = platform == GamePlatform.Xbox ? "rmx" : "rm2";
        var disc = new FakeDisc(platform);
        _disposables.Add(disc);
        disc.AddDefaultChunk($"Startup\\Default.{extension}", platform == GamePlatform.Xbox ? Graph(platform, "COM_PIRATE_MISSILE_INIT", Reused, MissileStarter) : [])
            .Add($"Levels\\Ice\\HighSeas\\gpa04.{extension}", ScriptChunk(platform, Graph(platform, "COM_PIRATE_MISSILE_INIT", Reused, MissileStarter),
                Object(platform, "MISSILE", 0x60, [(UInt16)MissileStarter], [(UInt16)MissileStarter, (UInt16)Reused]),
                Object(platform, "SHARED", 0x66, [(UInt16)Reused], [(UInt16)Reused], mass: 1)))
            .Add($"Levels\\AltEarth\\Core\\corea.{extension}", ScriptChunk(platform, Graph(platform, "COM_FURNITURE", Reused, FurnitureStarter),
                Object(platform, "SPHERE", 0x61, [], [(UInt16)FurnitureStarter, (UInt16)Reused], (UInt16)FurnitureStarter),
                Object(platform, "SPAWNER", 0x62, [(UInt16)FurnitureStarter], [(UInt16)FurnitureStarter, (UInt16)Reused]),
                Object(platform, "SPAWNER_TWO", 0x65, [(UInt16)FurnitureStarter], [(UInt16)FurnitureStarter, (UInt16)Reused]),
                Object(platform, "SHARED", 0x66, [(UInt16)Reused], [(UInt16)Reused], mass: 2)))
            .Add($"Levels\\AltEarth\\Core\\coreb.{extension}", ScriptChunk(platform, Graph(platform, "COM_FURNITURE", Reused, FurnitureStarter),
                Object(platform, "SHARED", 0x66, [(UInt16)Reused], [(UInt16)Reused], mass: 3)))
            .Add($"Levels\\Earth\\Totem\\l03stock.{extension}", ScriptChunk(platform, Graph(platform, "COM_FLIP_FLOP_PLATFORM", BearOrPlatform, PlatformStarter)
                    .Concat(Graph(platform, "COM_PLATFORM_DEFAULT", PlatformDefault, null, BearOrPlatform)),
                Object(platform, "PLATFORM", 0x64, [(UInt16)PlatformStarter], [(UInt16)PlatformStarter, (UInt16)BearOrPlatform])))
            .Add($"Levels\\Earth\\Zoo\\bearpit.{extension}", ScriptChunk(platform, Graph(platform, "COM_BEAR_MOVE", BearOrPlatform, null)
                    .Concat(Graph(platform, "COM_BEAR_DEFAULT", BearDefault, null, BearOrPlatform)),
                Object(platform, "BEAR", 0x63, [], [(UInt16)BearOrPlatform])));
        if (platform == GamePlatform.PS2)
        {
            disc.Add("Levels\\Earth\\Hub\\huba.rm2", HubChunk(255, 0))
                .Add("Levels\\Earth\\Hub\\hubb.rm2", HubChunk(0, 255))
                .Add("Levels\\Earth\\Hub\\hubc.rm2", HubChunk(0, 255))
                .Add("Levels\\Earth\\Hub\\hubd.rm2", HubChunk(255, 0))
                .Add("Levels\\Earth\\Hub\\hube.rm2", HubChunk(0, 255, 7))
                .Add("Levels\\Earth\\Hub\\hubf.rm2", HubChunk(0, 255, format: ITwinTexture.TexturePixelFormat.PSMCT32))
                .AddChunk("Levels\\Earth\\Cove\\cove1.rm2", SlotObject("LINKING", LinkingId, 1, LinkedId), SlotObject("LINKED", LinkedId, 1))
                .AddChunk("Levels\\Earth\\Cove\\cove2.rm2", SlotObject("LINKING", LinkingId, 2, LinkedId), SlotObject("LINKED", LinkedId, 2));
        }

        return disc;
    }

    // The project made of the disc the way creating it does, with the fixed unpack: the Xbox version read in 1.1.0's order unless asked
    private void MakeProject(FakeDisc disc, bool newXboxOrder = false)
    {
        var project = _project.Project;
        using var gate = new MemoryGate(1L << 30);
        var writer = new CreationWriter(project.AssetManager, gate);
        if (disc.Platform == GamePlatform.PS2)
        {
            project.DiscContentPathPS2 = disc.Folder;
            project.UnpackAssetsPS2(gate);
        }
        else
        {
            project.DiscContentPathXbox = disc.Folder;
            project.UnpackAssetsXbox(gate, !newXboxOrder);
        }

        writer.ImportAndWrite();
        writer.RemoveInternalAssets();
        new VariantMerger(project.AssetManager, _project.AssetsPath).Merge();
        RetailIds.Record(project.AssetManager.GetAssets().ToList(), project.AssetManager.GetAllAssetsOf<LevelChunk>());
        project.Serialize(writer.IsWritten);
    }

    private string Assets => _project.AssetsPath;

    private string PackageFolder(GamePlatform platform) => Path.Combine(Assets, platform == GamePlatform.Xbox ? "XBOX_Test" : "PS2_Test");

    private static JObject Read(string file) => JObject.Parse(File.ReadAllText(file));

    private static void Write(string file, JObject json) => File.WriteAllText(file, json.ToString(Formatting.Indented));

    private static JObject Link(string uri) => new() { ["_uri"] = uri };

    private static JArray Links(params string[] uris) => new(uris.Select(Link));

    private static List<string> UrisOf(JToken? links) => links is JArray array ? array.Select(link => (string)link["_uri"]!).ToList() : [];

    private string GraphFile(string name)
    {
        return Directory.EnumerateFiles(Assets, "*.json", SearchOption.AllDirectories)
            .Single(file => Path.GetFileName(Path.GetDirectoryName(file)) == "BehaviourGraph" && (string?)Read(file)[nameof(IAsset.InvariantName)] == name);
    }

    private string GraphUri(string name) => (string)Read(GraphFile(name))[nameof(IAsset.URI)]!["_uri"]!;

    private string ObjectFile(GamePlatform platform, string name, UInt16 id, string extension) => Path.Combine(PackageFolder(platform), "GameObject", $"{name}_{id:X}{extension}");

    private JObject ObjectData(GamePlatform platform, string name, UInt16 id) => Read(ObjectFile(platform, name, id, ".data"));

    private List<string> ReferencesOf(GamePlatform platform, string name, UInt16 id) => UrisOf(Read(ObjectFile(platform, name, id, ".json"))[nameof(IAsset.References)]);

    // An object's data and references as 1.1.0's unpack wrote them
    private void Edit(GamePlatform platform, string name, UInt16 id, Action<JObject> edit, params string[] references)
    {
        var data = ObjectData(platform, name, id);
        edit(data);
        Write(ObjectFile(platform, name, id, ".data"), data);
        var metadata = Read(ObjectFile(platform, name, id, ".json"));
        metadata[nameof(IAsset.References)] = Links(references);
        Write(ObjectFile(platform, name, id, ".json"), metadata);
    }

    private string ChunkFile(string chunk) => Directory.EnumerateFiles(Assets, $"{chunk}.json", SearchOption.AllDirectories).Single();

    // The chunk's own values of each asset by the asset's URI, as written
    private Dictionary<string, JObject> OverridesOf(string chunk)
    {
        return ((JArray)Read(ChunkFile(chunk))[nameof(LevelChunk.Overrides)]!).Cast<JObject>()
            .ToDictionary(entry => (string)entry[nameof(AssetOverride.Asset)]!["_uri"]!, entry => (JObject)entry[nameof(AssetOverride.Values)]!);
    }

    private void EditOverrides(string chunk, Action<JArray> edit)
    {
        var file = ChunkFile(chunk);
        var metadata = Read(file);
        edit((JArray)metadata[nameof(LevelChunk.Overrides)]!);
        Write(file, metadata);
    }

    private static JObject? EntryOf(JArray overrides, string asset) => overrides.Cast<JObject>().FirstOrDefault(entry => (string)entry[nameof(AssetOverride.Asset)]!["_uri"]! == asset);

    // Every file of the project's assets with its bytes
    private SortedDictionary<string, string> Files()
    {
        return new SortedDictionary<string, string>(Directory.EnumerateFiles(Assets, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(Assets, file), file => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))), StringComparer.Ordinal);
    }

    // 1.1.0 gave every graph of an ID the first chunk's starter: the bear's move got the totem's platform's, which no chunk starts it with
    private void GiveTheBearThePlatformsStarter()
    {
        var bear = Path.ChangeExtension(GraphFile("COM_BEAR_MOVE"), ".lab");
        var platformScript = File.ReadAllLines(Path.ChangeExtension(GraphFile("COM_FLIP_FLOP_PLATFORM"), ".lab"));
        var start = Array.IndexOf(platformScript, "   starter {");
        var end = Array.IndexOf(platformScript, "   }", start);
        var lines = File.ReadAllLines(bear).ToList();
        lines.InsertRange(lines.FindIndex(line => line.StartsWith("behaviour ")) + 1, new[] { string.Empty }.Concat(platformScript[start..(end + 1)]));
        File.WriteAllText(bear, string.Join('\n', lines) + "\n");
    }

    private void TakeOutStarterId(string graph)
    {
        var file = GraphFile(graph);
        var metadata = Read(file);
        Assert.NotNull(metadata.Property(nameof(TT_Lab.Assets.Code.BehaviourGraph.StarterId)));
        metadata.Remove(nameof(TT_Lab.Assets.Code.BehaviourGraph.StarterId));
        Write(file, metadata);
    }

    private const string TextureVariant = "res://PS2_Test/Texture/Texture 60_levels_earth_hub_";
    private const string Material = "res://PS2_Test/Material/lambert50_50";
    private const string ShaderTexture = "AssetData.Shaders[0].TextureId";

    // The PS2 project as 1.1.0's unpack wrote it. AltEarth's objects link the missile (the version's graph of the ID without a variation), a
    // slot naming the furniture's starter is empty and every graph is listed without its starter; the bear's move has the platform's starter.
    // coreb's shared object links the missile like the object, corea's found its own graph by its variation. Only hubc read its picture
    // (its variant named after it), the other hubs draw the first one, and cove2's linking object keeps its link to the merged-away variant.
    // The user changed the spawner's slot
    private Dictionary<string, string> MakeLikeOnes110MadePs2()
    {
        var missile = GraphUri("COM_PIRATE_MISSILE_INIT");
        var platformGraph = GraphUri("COM_FLIP_FLOP_PLATFORM");
        var made = new Dictionary<string, string> { ["bear"] = File.ReadAllText(Path.ChangeExtension(GraphFile("COM_BEAR_MOVE"), ".lab")) };
        TakeOutStarterId("COM_FURNITURE");
        GiveTheBearThePlatformsStarter();
        Edit(GamePlatform.PS2, "SPHERE", 0x61, data =>
        {
            data["TriggerBehaviours"]![0]!["TriggerBehaviour"] = Link(missile);
            data["GraphsWithoutStarter"] = Links(missile);
        }, missile);
        Edit(GamePlatform.PS2, "SPAWNER", 0x62, data =>
        {
            data["BehaviourSlots"]![0] = Link(platformGraph);
            data["GraphsWithoutStarter"] = Links(missile);
        }, platformGraph, missile);
        Edit(GamePlatform.PS2, "SPAWNER_TWO", 0x65, data =>
        {
            data["BehaviourSlots"]![0] = Link(RepairFiles.EmptyUri);
            data["GraphsWithoutStarter"] = Links(missile);
        }, missile);
        Edit(GamePlatform.PS2, "BEAR", 0x63, data => data["GraphsWithoutStarter"] = Links(platformGraph), platformGraph);
        EditOverrides("coreb", overrides =>
        {
            var values = (JObject)EntryOf(overrides, "res://PS2_Test/GameObject/SHARED_66")![nameof(AssetOverride.Values)]!;
            values.Remove("AssetData.BehaviourSlots[0]");
            values.Remove("AssetData.GraphsWithoutStarter[0]");
        });

        var textures = Path.Combine(PackageFolder(GamePlatform.PS2), "Texture");
        var hubb = Path.Combine(textures, "Texture 60_levels_earth_hub_hubb");
        var hubc = Path.Combine(textures, "Texture 60_levels_earth_hub_hubc");
        File.Copy(hubb + ".png", hubc + ".png");
        var variant = Read(hubb + ".json");
        variant[nameof(IAsset.Alias)] = "Texture 60_levels_earth_hub_hubc";
        variant[nameof(IAsset.Variation)] = "levels_earth_hub_hubc";
        variant[nameof(IAsset.URI)] = Link(TextureVariant + "hubc");
        Write(hubc + ".json", variant);
        foreach (var file in new[] { hubb, Path.Combine(textures, "Texture 60_levels_earth_hub_hubf") }.SelectMany(name => new[] { name + ".json", name + ".png" }))
        {
            File.Delete(file);
        }

        foreach (var hub in new[] { "hubb", "hube", "hubf" })
        {
            EditOverrides(hub, overrides => overrides.Clear());
        }

        EditOverrides("hubc", overrides => EntryOf(overrides, Material)![nameof(AssetOverride.Values)]![ShaderTexture] = Link(TextureVariant + "hubc"));
        var materialFile = Path.Combine(PackageFolder(GamePlatform.PS2), "Material", "lambert50_50.json");
        var material = Read(materialFile);
        material[nameof(IAsset.RetailFingerprints)] = new JArray(material[nameof(IAsset.RetailFingerprints)]![0]!);
        Write(materialFile, material);

        EditOverrides("cove2", overrides =>
            EntryOf(overrides, "res://PS2_Test/GameObject/LINKING_70")![nameof(AssetOverride.Values)]!["AssetData.ObjectSlots[0]"] = Link("res://PS2_Test/GameObject/LINKED_71_levels_earth_cove_cove2"));
        return made;
    }

    // What a chunk's view of the material with the values is made of, the way creation records it
    private static UInt32 ViewFingerprint(string materialFile, string texture)
    {
        var json = File.ReadAllText(materialFile);
        var asset = new TT_Lab.Assets.Graphics.Material();
        asset.Deserialize(json);
        using (new AssetDataScope())
        {
            return AssetOverrides.CreateView(asset, new Dictionary<string, JToken> { [ShaderTexture] = Link(texture) }).Fingerprint();
        }
    }

    private RetailRepair.Result Repair(GamePlatform platform, FakeDisc disc, bool confirmed = true)
    {
        return RetailRepair.Repair(ProjectFile, [platform], new Dictionary<GamePlatform, RetailRepair.Disc> { [platform] = new(disc.Folder, confirmed) });
    }

    // A project 1.0.0 made and 1.1.0 migrated recorded nothing of what its graphics are made of: the shadow's are the disc's
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void APs2ProjectGetsTheDiscsValuesWhereItHasOnes110Made(bool madeBy100)
    {
        var disc = Disc(GamePlatform.PS2);
        MakeProject(disc);
        var made = MakeLikeOnes110MadePs2();
        var materialFile = Path.Combine(PackageFolder(GamePlatform.PS2), "Material", "lambert50_50.json");
        var materialsOwn = (UInt32)Read(materialFile)[nameof(IAsset.RetailFingerprints)]![0]!;
        if (madeBy100)
        {
            foreach (var file in Directory.EnumerateFiles(Assets, "*.json", SearchOption.AllDirectories))
            {
                var metadata = Read(file);
                if (metadata.Remove(nameof(IAsset.RetailFingerprints)))
                {
                    Write(file, metadata);
                }
            }
        }
        var furniture = GraphUri("COM_FURNITURE");
        var platformGraph = GraphUri("COM_FLIP_FLOP_PLATFORM");
        var bear = GraphUri("COM_BEAR_MOVE");
        // What a TT Lab that was ended in the middle of a repair left
        Directory.CreateDirectory(Path.Combine(_project.Project.ProjectPath, "repair_scratch_2026-01-01_00-00-00", "assets"));

        var result = Repair(GamePlatform.PS2, disc);

        Assert.Equal([GamePlatform.PS2], result.Repaired);
        Assert.Empty(result.Skipped);
        // The graph's starter of another ID, its objects linking it, the bear's move without the platform's starter
        Assert.Equal(FurnitureStarter, (UInt32)Read(GraphFile("COM_FURNITURE"))[nameof(TT_Lab.Assets.Code.BehaviourGraph.StarterId)]!);
        Assert.Equal(made["bear"], File.ReadAllText(Path.ChangeExtension(GraphFile("COM_BEAR_MOVE"), ".lab")));
        var sphere = ObjectData(GamePlatform.PS2, "SPHERE", 0x61);
        Assert.Equal(furniture, (string)sphere["TriggerBehaviours"]![0]!["TriggerBehaviour"]!["_uri"]!);
        Assert.Empty(UrisOf(sphere["GraphsWithoutStarter"]));
        Assert.Equal([furniture], ReferencesOf(GamePlatform.PS2, "SPHERE", 0x61));
        var spawnerTwo = ObjectData(GamePlatform.PS2, "SPAWNER_TWO", 0x65);
        Assert.Equal([furniture], UrisOf(spawnerTwo["BehaviourSlots"]));
        Assert.Empty(UrisOf(spawnerTwo["GraphsWithoutStarter"]));
        Assert.Equal([bear], UrisOf(ObjectData(GamePlatform.PS2, "BEAR", 0x63)["GraphsWithoutStarter"]));
        Assert.Equal([bear], ReferencesOf(GamePlatform.PS2, "BEAR", 0x63));
        // The user's slot stays and is noted, what isn't edited in TT Lab is the disc's
        var spawner = ObjectData(GamePlatform.PS2, "SPAWNER", 0x62);
        Assert.Equal([platformGraph], UrisOf(spawner["BehaviourSlots"]));
        Assert.Empty(UrisOf(spawner["GraphsWithoutStarter"]));
        Assert.Equal([furniture, platformGraph], ReferencesOf(GamePlatform.PS2, "SPAWNER", 0x62));
        Assert.Contains(result.Notes, note => note.StartsWith("SPAWNER_62: behaviour slot 0 was changed after creation") && note.Contains("COM_FURNITURE_levels_altearth_core_corea"));
        // coreb's own values of the shared object link its graph again
        var coreb = OverridesOf("coreb")["res://PS2_Test/GameObject/SHARED_66"];
        Assert.Equal(["AssetData.BehaviourSlots[0]", "AssetData.FloatProperties[0]", "AssetData.GraphsWithoutStarter[0]"], coreb.Properties().Select(property => property.Name));
        Assert.Equal(furniture, (string)coreb["AssetData.BehaviourSlots[0]"]!["_uri"]!);

        // The blue hubs draw hubc's picture, the project's of the same picture and settings, and hubf the disc's of its own settings
        foreach (var hub in new[] { "hubb", "hubc", "hube" })
        {
            Assert.Equal(TextureVariant + "hubc", (string)OverridesOf(hub)[Material][ShaderTexture]!["_uri"]!);
        }

        Assert.Equal(TextureVariant + "hubf", (string)OverridesOf("hubf")[Material][ShaderTexture]!["_uri"]!);
        var textures = Path.Combine(PackageFolder(GamePlatform.PS2), "Texture");
        Assert.Equal(["Texture 60.json", "Texture 60.png", "Texture 60_levels_earth_hub_hubc.json", "Texture 60_levels_earth_hub_hubc.png",
            "Texture 60_levels_earth_hub_hubf.json", "Texture 60_levels_earth_hub_hubf.png"], Directory.GetFiles(textures).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.NotNull(Read(Path.Combine(textures, "Texture 60_levels_earth_hub_hubf.json"))[nameof(IAsset.RetailFingerprints)]);
        Assert.Empty(OverridesOf("huba"));
        Assert.Empty(OverridesOf("hubd"));
        // The chunks' views are made of the disc's pictures, built with the material's ID
        var fingerprints = ((JArray)Read(materialFile)[nameof(IAsset.RetailFingerprints)]!).Select(value => (UInt32)value).ToList();
        Assert.Equal([materialsOwn, ViewFingerprint(materialFile, TextureVariant + "hubc"), ViewFingerprint(materialFile, TextureVariant + "hubf")], fingerprints);

        // cove2's linking object links the shared linked object again, the chunk's own mass stays
        Assert.Equal(["AssetData.FloatProperties[0]"], OverridesOf("cove2")["res://PS2_Test/GameObject/LINKING_70"].Properties().Select(property => property.Name));

        // What changed is in the backup, the shadow's gone, the marker says it's repaired
        Assert.NotNull(result.Backup);
        Assert.True(File.Exists(result.Backup));
        Assert.Empty(Directory.GetDirectories(_project.Project.ProjectPath, "repair_scratch_*"));
        Assert.Equal(RepairState.Repaired, RepairMarker.Read(_project.Project.ProjectPath).PS2!.Repair);
        Assert.Empty(RetailRepair.VersionsToOffer(ProjectFile));

        // Repaired again, nothing changes and the user's slot is noted again
        var files = Files();
        var again = Repair(GamePlatform.PS2, disc);
        Assert.Equal(0, again.Changed);
        Assert.Null(again.Backup);
        Assert.Equal(files, Files());
        Assert.Contains(again.Notes, note => note.StartsWith("SPAWNER_62: behaviour slot 0"));
    }

    // A picture or a material the user changed stays what the chunks draw
    [AvaloniaFact]
    public void AChunkWhosePictureWasChangedKeepsIt()
    {
        var disc = Disc(GamePlatform.PS2);
        MakeProject(disc);
        MakeLikeOnes110MadePs2();
        var picture = Path.Combine(PackageFolder(GamePlatform.PS2), "Texture", "Texture 60.png");
        File.WriteAllBytes(picture, TT_Lab.AssetData.Graphics.TextureData.EncodePng(Enumerable.Repeat(0xFF00FF00u, 16 * 16).ToArray(), 16, 16));

        var result = Repair(GamePlatform.PS2, disc);

        foreach (var hub in new[] { "hubb", "hube", "hubf" })
        {
            Assert.Empty(OverridesOf(hub));
            Assert.Contains(result.Notes, note => note.StartsWith($"{hub}: Texture 60 was changed after creation"));
        }

        Assert.False(File.Exists(Path.Combine(PackageFolder(GamePlatform.PS2), "Texture", "Texture 60_levels_earth_hub_hubf.json")));
        // The rest is repaired
        Assert.Equal(GraphUri("COM_FURNITURE"), (string)ObjectData(GamePlatform.PS2, "SPHERE", 0x61)["TriggerBehaviours"]![0]!["TriggerBehaviour"]!["_uri"]!);
    }

    // The Xbox version is read in the order its packages were made in: 1.1.0's (the levels before the startup) without the marker saying
    // otherwise, or the default chunk's missile has another URI than the project's
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnXboxProjectIsRepairedFromTheDiscReadTheWayItWasMade(bool newOrder)
    {
        var disc = Disc(GamePlatform.Xbox);
        MakeProject(disc, newOrder);
        if (newOrder)
        {
            // Made by this TT Lab, the repair from the menu changes nothing
            var marker = new RepairMarker();
            marker.For(GamePlatform.Xbox).Repair = RepairState.NotNeeded;
            marker.For(GamePlatform.Xbox).NewFileOrder = true;
            marker.Write(_project.Project.ProjectPath);
            var files = Files();

            var unchanged = Repair(GamePlatform.Xbox, disc, false);

            Assert.Equal([GamePlatform.Xbox], unchanged.Repaired);
            Assert.Equal(0, unchanged.Changed);
            Assert.Empty(unchanged.Notes);
            Assert.Equal(files, Files());
            Assert.StartsWith("res://Global XBOX_Test/", GraphUri("COM_PIRATE_MISSILE_INIT"));
            return;
        }

        var missile = GraphUri("COM_PIRATE_MISSILE_INIT");
        var furniture = GraphUri("COM_FURNITURE");
        var platformGraph = GraphUri("COM_FLIP_FLOP_PLATFORM");
        var bear = GraphUri("COM_BEAR_MOVE");
        Assert.Equal("res://XBOX_Test/BehaviourGraph/COM_PIRATE_MISSILE_INIT_levels_ice_highseas_gpa04", missile);
        Assert.Equal("res://XBOX_Test/BehaviourGraph/COM_FURNITURE", furniture);
        var bearScript = File.ReadAllText(Path.ChangeExtension(GraphFile("COM_BEAR_MOVE"), ".lab"));
        // 1.1.0: every graph of the ID got AltEarth's starter, the missile's slot naming its own starter found none
        TakeOutStarterId("COM_FURNITURE");
        GiveTheBearThePlatformsStarter();
        Edit(GamePlatform.Xbox, "MISSILE", 0x60, data => data["BehaviourSlots"]![0] = Link(RepairFiles.EmptyUri), furniture);
        Edit(GamePlatform.Xbox, "SPHERE", 0x61, data => data["GraphsWithoutStarter"] = Links(furniture), furniture);
        Edit(GamePlatform.Xbox, "SPAWNER", 0x62, data => data["GraphsWithoutStarter"] = Links(furniture), furniture);
        Edit(GamePlatform.Xbox, "SPAWNER_TWO", 0x65, data =>
        {
            data["BehaviourSlots"]![0] = Link(platformGraph);
            data["GraphsWithoutStarter"] = Links(furniture);
        }, platformGraph, furniture);
        Edit(GamePlatform.Xbox, "BEAR", 0x63, data => data["GraphsWithoutStarter"] = Links(platformGraph), platformGraph);

        // An Xbox disc is taken without asking
        var result = Repair(GamePlatform.Xbox, disc, false);

        Assert.Equal([GamePlatform.Xbox], result.Repaired);
        Assert.Equal(FurnitureStarter, (UInt32)Read(GraphFile("COM_FURNITURE"))[nameof(TT_Lab.Assets.Code.BehaviourGraph.StarterId)]!);
        Assert.Equal(bearScript, File.ReadAllText(Path.ChangeExtension(GraphFile("COM_BEAR_MOVE"), ".lab")));
        Assert.Equal([missile], UrisOf(ObjectData(GamePlatform.Xbox, "MISSILE", 0x60)["BehaviourSlots"]));
        Assert.Equal([missile], ReferencesOf(GamePlatform.Xbox, "MISSILE", 0x60));
        Assert.Empty(UrisOf(ObjectData(GamePlatform.Xbox, "SPHERE", 0x61)["GraphsWithoutStarter"]));
        Assert.Empty(UrisOf(ObjectData(GamePlatform.Xbox, "SPAWNER", 0x62)["GraphsWithoutStarter"]));
        Assert.Equal([platformGraph], UrisOf(ObjectData(GamePlatform.Xbox, "SPAWNER_TWO", 0x65)["BehaviourSlots"]));
        Assert.Equal([bear], UrisOf(ObjectData(GamePlatform.Xbox, "BEAR", 0x63)["GraphsWithoutStarter"]));
        Assert.Equal(["SPAWNER_TWO_65: behaviour slot 0 was changed after creation, it should link COM_FURNITURE"], result.Notes);
        Assert.False(RepairMarker.Read(_project.Project.ProjectPath).HasNewXboxFileOrder);
    }

    // The project's disc folder can have a build's archive: a PS2 archive of other hashes than a release's is taken once the user says it's
    // the original disc. Without a disc a chunk's own values linking what the project doesn't have are noted for the user to put back
    [AvaloniaFact]
    public void AnArchiveOfOtherHashesIsOnlyTakenOnceConfirmed()
    {
        var disc = Disc(GamePlatform.PS2);
        MakeProject(disc);
        MakeLikeOnes110MadePs2();
        var files = Files();

        var refused = Repair(GamePlatform.PS2, disc, false);

        Assert.Empty(refused.Repaired);
        Assert.Equal([GamePlatform.PS2], refused.Skipped);
        Assert.Contains(refused.Notes, note => note.Contains("isn't one TT Lab knows") && note.Contains("offered again"));
        Assert.Contains(refused.Notes, note => note.StartsWith("cove2: its own value AssetData.ObjectSlots[0] of LINKING_70 links res://PS2_Test/GameObject/LINKED_71_levels_earth_cove_cove2"));
        Assert.Equal(files, Files());
        Assert.Null(RepairMarker.Read(_project.Project.ProjectPath).PS2);
        Assert.Equal([GamePlatform.PS2], RetailRepair.VersionsToOffer(ProjectFile));
        // Declined, it's offered no more: only the menu repairs it
        RetailRepair.Decline(ProjectFile, [GamePlatform.PS2]);
        var declined = RetailRepair.Repair(ProjectFile, [GamePlatform.PS2], new Dictionary<GamePlatform, RetailRepair.Disc>());
        Assert.Contains(declined.Notes, note => note.StartsWith("The PS2 version wasn't repaired, no disc of it was given. Project > Repair from the Game's Discs"));
        Assert.DoesNotContain(declined.Notes, note => note.Contains("offered again"));

        // A release's archive is known by its hashes, the Xbox disc by its files
        Assert.Equal(RepairDiscs.Verdict.Unknown, RepairDiscs.Of(GamePlatform.PS2, disc.Folder).Verdict);
        var (header, archive) = disc.ArchiveSha256;
        Assert.Equal(RepairDiscs.Verdict.Known, RepairDiscs.Of(GamePlatform.PS2, disc.Folder, [new RepairDiscs.Release("Test", header, archive)]).Verdict);
        Assert.Equal(RepairDiscs.Verdict.Unknown, RepairDiscs.Of(GamePlatform.PS2, disc.Folder, [new RepairDiscs.Release("Test", header, new string('0', 64))]).Verdict);
        Assert.Equal(RepairDiscs.Verdict.Missing, RepairDiscs.Of(GamePlatform.Xbox, disc.Folder).Verdict);
        var xbox = new FakeDisc(GamePlatform.Xbox);
        _disposables.Add(xbox);
        Assert.Equal(RepairDiscs.Verdict.Missing, RepairDiscs.Of(GamePlatform.Xbox, xbox.Folder).Verdict);
        xbox.AddText("STARTUP/DEFAULT.RMX", "");
        Assert.Equal(RepairDiscs.Verdict.Known, RepairDiscs.Of(GamePlatform.Xbox, xbox.Folder).Verdict);

        Assert.Equal([GamePlatform.PS2], Repair(GamePlatform.PS2, disc).Repaired);
    }

    private ProjectManager Manager => Locator.Current.GetService<ProjectManager>()!;

    private async Task Open(bool repair = false)
    {
        Manager.OpenProject(_project.Project.ProjectPath, repair);
        await WaitUntil(() => !Manager.IsOpeningProject);
        Assert.True(Manager.WorkableProject);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var waited = 0; waited < 30000 && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    // Opening a project 1.1.0 unpacked asks before it's read: Not Now opens it as it is and asks again next time, Don't Ask Again never asks
    // again, and the Project menu repairs it whatever the marker says, asking for the disc when the project's disc folder doesn't have a
    // release's archive and to confirm one TT Lab doesn't know
    [AvaloniaFact]
    public async Task OpeningAProjectOffersTheRepair()
    {
        var disc = Disc(GamePlatform.PS2);
        MakeProject(disc);
        MakeLikeOnes110MadePs2();
        var files = Files();
        var markerFile = Path.Combine(_project.Project.ProjectPath, RepairMarker.FileName);
        var manager = Manager;
        Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), manager));
        var asked = new List<string>();
        var answer = RepairAnswer.NotNow;
        var picked = new List<RepairDiscDialogue.Request>();
        var confirmed = new List<string>();
        var told = new List<string>();
        manager.AskToRepair = question =>
        {
            asked.Add(question);
            return Task.FromResult(answer);
        };
        manager.PreferredDiscFolder = _ => null;
        manager.AskForRepairDisc = request =>
        {
            picked.Add(request);
            return Task.FromResult<string?>(disc.Folder);
        };
        manager.ConfirmOriginalDisc = question =>
        {
            confirmed.Add(question);
            return Task.FromResult(true);
        };
        manager.TellRepaired = told.Add;
        try
        {
            await Open();
            Assert.Contains("PS2 game packages", Assert.Single(asked));
            Assert.Equal(files, Files());
            Assert.False(File.Exists(markerFile));
            await Open();
            Assert.Equal(2, asked.Count);

            answer = RepairAnswer.DontAskAgain;
            await Open();
            Assert.Equal(3, asked.Count);
            Assert.Equal(files, Files());
            Assert.Equal(RepairState.Declined, RepairMarker.Read(_project.Project.ProjectPath).PS2!.Repair);
            await Open();
            Assert.Equal(3, asked.Count);
            Assert.Empty(told);

            await Open(true);
            Assert.Equal(3, asked.Count);
            // The project's disc folder has the fake archive: picked, then confirmed
            var request = Assert.Single(picked);
            Assert.Equal((GamePlatform.PS2, disc.Folder), (request.Platform, request.Suggestion));
            Assert.Contains(disc.Folder, Assert.Single(confirmed));
            Assert.StartsWith("Test's PS2 game packages are repaired", Assert.Single(told));
            Assert.Equal(RepairState.Repaired, RepairMarker.Read(_project.Project.ProjectPath).PS2!.Repair);
            Assert.Equal(FurnitureStarter, (UInt32)Read(GraphFile("COM_FURNITURE"))[nameof(TT_Lab.Assets.Code.BehaviourGraph.StarterId)]!);
            var opened = manager.OpenedProject!.AssetManager.GetAsset<TT_Lab.Assets.Code.BehaviourGraph>(new LabURI(GraphUri("COM_FURNITURE")));
            Assert.Equal(FurnitureStarter, opened.StarterId);
        }
        finally
        {
            // Opening posted the panel's clearing, which reads the panel when it runs
            Dispatcher.UIThread.RunJobs();
            Log.SetViewModel(null);
        }
    }

    // A project made by this TT Lab has none of 1.1.0's unpack's faults
    [AvaloniaFact]
    public async Task AProjectThisTtLabMakesIsNotOfferedTheRepair()
    {
        var disc = new FakeDisc(GamePlatform.PS2).AddChunk("Levels\\Earth\\Hub\\beach.rm2", SlotObject("RAT", 0x54, 1));
        _disposables.Add(disc);
        var manager = Manager;
        Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), manager));
        var root = Path.Combine(_project.Root, "Created");
        try
        {
            manager.CreateProject("Made", root, disc.Folder, null, false);
            await WaitUntil(() => !manager.IsCreatingProject);

            Assert.True(manager.WorkableProject);
            var marker = RepairMarker.Read(Path.Combine(root, "Made"));
            Assert.Equal(RepairState.NotNeeded, marker.PS2!.Repair);
            Assert.Equal(RepairState.NotNeeded, marker.Xbox!.Repair);
            Assert.True(marker.HasNewXboxFileOrder);
            Assert.Equal([GamePlatform.PS2], RetailRepair.UnpackedVersions(Path.Combine(root, "Made", "Made.tson")));
            Assert.Empty(RetailRepair.VersionsToOffer(Path.Combine(root, "Made", "Made.tson")));
        }
        finally
        {
            manager.StopTreeWatcher();
            Dispatcher.UIThread.RunJobs();
            Log.SetViewModel(null);
        }
    }

    // A project shared without the game's packages gets its Xbox packages unpacked in the order they were made in, which its own packages link
    // the URIs of: 1.1.0's unless the marker says otherwise. Unpacked again they need no repair
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task XboxPackagesUnpackedAgainAreReadTheWayTheyWereMade(bool newOrder)
    {
        var disc = Disc(GamePlatform.Xbox);
        MakeProject(disc, newOrder);
        if (newOrder)
        {
            RepairMarker.WriteForNewProject(_project.Project.ProjectPath);
        }

        var made = GraphUri("COM_PIRATE_MISSILE_INIT");
        Assert.StartsWith(newOrder ? "res://Global XBOX_Test/" : "res://XBOX_Test/", made);
        foreach (var package in new[] { "Global XBOX_Test", "XBOX_Test" })
        {
            Directory.Delete(Path.Combine(Assets, package), true);
        }

        var manager = Manager;
        Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), manager));
        var asked = 0;
        manager.PreferredDiscFolder = platform => platform == GamePlatform.Xbox ? disc.Folder : null;
        manager.AskForRetailDiscs = _ => Task.FromResult<IReadOnlyDictionary<GamePlatform, string>?>(null);
        manager.AskToRepair = _ =>
        {
            asked++;
            return Task.FromResult(RepairAnswer.NotNow);
        };
        try
        {
            await Open();

            Assert.Equal(made, GraphUri("COM_PIRATE_MISSILE_INIT"));
            var marker = RepairMarker.Read(_project.Project.ProjectPath);
            Assert.Equal(RepairState.NotNeeded, marker.Xbox!.Repair);
            Assert.Equal(newOrder, marker.HasNewXboxFileOrder);
            Assert.Equal(0, asked);
        }
        finally
        {
            Dispatcher.UIThread.RunJobs();
            Log.SetViewModel(null);
        }
    }

    // The user put the shared picture back in the chunk 1.1.0 named its variant after (its own link taken out): the repair leaves it out
    [AvaloniaFact]
    public void AChunksOwnPictureTheUserTookOutStaysOut()
    {
        var disc = Disc(GamePlatform.PS2);
        MakeProject(disc);
        MakeLikeOnes110MadePs2();
        EditOverrides("hubc", overrides => EntryOf(overrides, Material)!.Remove());

        var result = Repair(GamePlatform.PS2, disc);

        Assert.False(OverridesOf("hubc").ContainsKey(Material));
        Assert.Contains("hubc's own picture Texture 60_levels_earth_hub_hubc of lambert50_50 was taken out after creation, it stays out", result.Notes);
        Assert.Equal(TextureVariant + "hubc", (string)OverridesOf("hubb")[Material][ShaderTexture]!["_uri"]!);
    }

    // Objects' slots link command sequences' packs by URIs no file has, a chunk's own value linking one leads somewhere
    [AvaloniaFact]
    public void AnOwnValueLinkingACommandPackIsntMissing()
    {
        var disc = Disc(GamePlatform.PS2);
        MakeProject(disc);
        const string pack = "res://PS2_Test/BehaviourGraph/604/67";
        const string none = "res://PS2_Test/BehaviourGraph/605/67";
        var sequence = _project.Add(new TT_Lab.Assets.Code.BehaviourCommandsSequence(), "Library", 0x43, _project.Project.Ps2Package);
        sequence.BehaviourGraphLinks[604] = new LabURI(pack);
        sequence.Serialize();
        EditOverrides("cove2", overrides =>
        {
            var values = EntryOf(overrides, "res://PS2_Test/GameObject/LINKING_70")![nameof(AssetOverride.Values)]!;
            values["AssetData.BehaviourSlots"] = new JArray(Link(pack));
            values["AssetData.SoundSlots"] = new JArray(Link(none));
        });

        var result = RetailRepair.Repair(ProjectFile, [GamePlatform.PS2], new Dictionary<GamePlatform, RetailRepair.Disc>());

        Assert.DoesNotContain(result.Notes, note => note.Contains(pack));
        Assert.Contains(result.Notes, note => note.Contains($"links {none}, which the project doesn't have"));
    }

    private string WritesFolder()
    {
        var folder = Path.Combine(_project.Root, "Writes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "assets"));
        return folder;
    }

    // Every file the repair changes is in the backup before any of them changes: a backup that fails changes nothing, and nothing it never
    // wrote counts as changed
    [Fact]
    public void ABackupThatFailsChangesNothing()
    {
        var folder = WritesFolder();
        var file = Path.Combine(folder, "assets", "chunk.json");
        var made = Path.Combine(folder, "assets", "made.json");
        File.WriteAllText(file, "{\"original\": true}");
        // An archive that can't be made
        var backup = Path.Combine(folder, "repair_backup.zip");
        Directory.CreateDirectory(backup);
        var context = new TT_Lab.Project.Migration.MigrationContext(folder, backup);

        var failure = Assert.Throws<RetailRepair.RepairFailedException>(() =>
            RetailRepair.WriteChanges(context, folder, backup, [(file, Encoding.UTF8.GetBytes("{}")), (made, Encoding.UTF8.GetBytes("{}"))]));

        Assert.Contains("nothing of the project was changed", failure.Message);
        Assert.Equal("{\"original\": true}", File.ReadAllText(file));
        Assert.False(File.Exists(made));
        Assert.DoesNotContain(Path.GetFullPath(file), context.Changed);
    }

    // A write that fails puts back what the closed backup has and deletes what the repair made, the files are as they were
    [Fact]
    public void AWriteThatFailsPutsTheFilesBack()
    {
        var folder = WritesFolder();
        var file = Path.Combine(folder, "assets", "chunk.json");
        var made = Path.Combine(folder, "assets", "Texture", "made.png");
        var blocked = Path.Combine(folder, "assets", "blocked.json");
        File.WriteAllText(file, "{\"original\": true}");
        // A file that can't be written: a folder of its name
        Directory.CreateDirectory(blocked);
        var backup = Path.Combine(folder, "repair_backup.zip");
        var context = new TT_Lab.Project.Migration.MigrationContext(folder, backup);

        var failure = Assert.Throws<RetailRepair.RepairFailedException>(() => RetailRepair.WriteChanges(context, folder, backup,
            [(file, Encoding.UTF8.GetBytes("{}")), (made, [1, 2, 3]), (blocked, Encoding.UTF8.GetBytes("{}"))]));

        Assert.Contains("they're as they were", failure.Message);
        Assert.Equal("{\"original\": true}", File.ReadAllText(file));
        Assert.False(File.Exists(made));
        Assert.True(Directory.Exists(blocked));
    }

    // A known archive of another release than the project's disc folder's (preferences of PAL 1.01 for an NTSC project) or files of a
    // release that can't be told are only taken once the user says they're the original disc of the project's game
    [AvaloniaFact]
    public void AnotherReleasesDiscIsOnlyTakenOnceConfirmed()
    {
        var ntsc = Disc(GamePlatform.PS2).OfRelease("SLUS_209.09", "1.00").AddText("Startup\\Ntsc.txt", "NTSC");
        var pal = Disc(GamePlatform.PS2).OfRelease("SLES_525.68", "1.01");
        var (header, archive) = pal.ArchiveSha256;
        RepairDiscs.Release[] known = [new("PAL 1.01", header, archive)];
        MakeProject(ntsc);
        MakeLikeOnes110MadePs2();
        var files = Files();

        Assert.Equal(true, RepairDiscs.ProjectsRelease(GamePlatform.PS2, ntsc.Folder, ntsc.Folder).Same);
        Assert.Equal(false, RepairDiscs.ProjectsRelease(GamePlatform.PS2, pal.Folder, ntsc.Folder).Same);
        var copy = Disc(GamePlatform.PS2).OfRelease("SLUS_209.09", "1.00");
        Assert.Equal(true, RepairDiscs.ProjectsRelease(GamePlatform.PS2, copy.Folder, ntsc.Folder).Same);
        Assert.Null(RepairDiscs.ProjectsRelease(GamePlatform.PS2, pal.Folder, Path.Combine(_project.Root, "gone")).Same);
        Assert.Null(RepairDiscs.ProjectsRelease(GamePlatform.PS2, pal.Folder, null).Same);
        // The Xbox releases by their executables' certificates
        var xboxPal = new FakeDisc(GamePlatform.Xbox).OfXboxRelease(0x56550036, 4, 0x13080402);
        var xboxCopy = new FakeDisc(GamePlatform.Xbox).OfXboxRelease(0x56550036, 4, 0x13080402);
        var xboxNtsc = new FakeDisc(GamePlatform.Xbox).OfXboxRelease(0x56550036, 7, 1);
        var xboxUnread = new FakeDisc(GamePlatform.Xbox);
        _disposables.AddRange([xboxPal, xboxCopy, xboxNtsc, xboxUnread]);
        Assert.Equal(true, RepairDiscs.ProjectsRelease(GamePlatform.Xbox, xboxCopy.Folder, xboxPal.Folder).Same);
        Assert.Equal(false, RepairDiscs.ProjectsRelease(GamePlatform.Xbox, xboxNtsc.Folder, xboxPal.Folder).Same);
        Assert.Null(RepairDiscs.ProjectsRelease(GamePlatform.Xbox, xboxUnread.Folder, xboxPal.Folder).Same);

        var refused = RetailRepair.Repair(ProjectFile, [GamePlatform.PS2], new Dictionary<GamePlatform, RetailRepair.Disc> { [GamePlatform.PS2] = new(pal.Folder) }, known);

        Assert.Empty(refused.Repaired);
        Assert.Contains(refused.Notes, note => note.Contains("PAL 1.01's (SLES_525.68 1.01)") && note.Contains("NTSC 1.00's (SLUS_209.09 1.00)"));
        Assert.Equal(files, Files());

        var confirmed = RetailRepair.Repair(ProjectFile, [GamePlatform.PS2], new Dictionary<GamePlatform, RetailRepair.Disc> { [GamePlatform.PS2] = new(pal.Folder, true) }, known);

        Assert.Equal([GamePlatform.PS2], confirmed.Repaired);
    }

    // The preferences' folder of a known archive of another release isn't taken without asking
    [AvaloniaFact]
    public async Task ThePreferencesDiscOfAnotherReleaseIsAskedAbout()
    {
        var ntsc = Disc(GamePlatform.PS2).OfRelease("SLUS_209.09", "1.00").AddText("Startup\\Ntsc.txt", "NTSC");
        var pal = Disc(GamePlatform.PS2).OfRelease("SLES_525.68", "1.01");
        var (header, archive) = pal.ArchiveSha256;
        MakeProject(ntsc);
        MakeLikeOnes110MadePs2();
        var files = Files();
        var manager = Manager;
        Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), manager));
        var picked = new List<RepairDiscDialogue.Request>();
        var confirmed = new List<string>();
        var told = new List<string>();
        manager.KnownPs2Archives = [new("PAL 1.01", header, archive)];
        manager.PreferredDiscFolder = _ => pal.Folder;
        manager.AskForRepairDisc = request =>
        {
            picked.Add(request);
            return Task.FromResult<string?>(picked.Count == 1 ? pal.Folder : null);
        };
        manager.ConfirmOriginalDisc = question =>
        {
            confirmed.Add(question);
            return Task.FromResult(false);
        };
        manager.TellRepaired = told.Add;
        try
        {
            await Open(true);

            Assert.Equal(2, picked.Count);
            var question = Assert.Single(confirmed);
            Assert.Contains("PAL 1.01's (SLES_525.68 1.01)", question);
            Assert.Contains("NTSC 1.00's (SLUS_209.09 1.00)", question);
            Assert.StartsWith("Test's game packages weren't repaired.", Assert.Single(told));
            Assert.Equal(files, Files());
        }
        finally
        {
            Dispatcher.UIThread.RunJobs();
            Log.SetViewModel(null);
        }
    }

    // A project of another version isn't repaired: the migration left it alone and it isn't opened
    [AvaloniaFact]
    public void AProjectOfAnotherVersionIsntRepaired()
    {
        var disc = Disc(GamePlatform.PS2);
        MakeProject(disc);
        MakeLikeOnes110MadePs2();
        var project = Read(ProjectFile);
        project[nameof(IProject.Version)] = "1.2.0";
        Write(ProjectFile, project);
        var files = Files();
        var asked = 0;
        Manager.AskToRepair = _ =>
        {
            asked++;
            return Task.FromResult(RepairAnswer.Repair);
        };

        Manager.RepairIfAsked(ProjectFile, false);
        Manager.RepairIfAsked(ProjectFile, true);

        Assert.Equal(0, asked);
        Assert.Throws<ProjectException>(() => Repair(GamePlatform.PS2, disc));
        Assert.Equal(files, Files());
    }
}
