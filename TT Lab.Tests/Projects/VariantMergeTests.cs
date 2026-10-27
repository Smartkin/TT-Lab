using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// Unpacking turns the variants a disc's later chunks made into the chunks' overrides of the first chunk's assets. A variant's values are worked
// out once every variant's asset is known, with every link to a merged variant pointing at its asset, so the order they're merged in doesn't
// matter: merged one after the other, a level's extra life crate came before the level's own health it links to, and its override kept a link
// to it once it was merged away (the level didn't build). Nothing the merge writes links to an asset the project doesn't have
[Collection(ProjectCollection.Name)]
public sealed class VariantMergeTests : IDisposable
{
    private const UInt16 LinkingId = 0x10;
    private const UInt16 LinkedId = 0x20;
    private const string HubVariation = "levels_earth_hub_hubd";

    private TestProject? _project;

    private TestProject Project => _project ??= new TestProject();

    public void Dispose() => _project?.Dispose();

    private static PS2AnyObject Object(string name, UInt16 id, Single mass, params UInt16[] slots)
    {
        var gameObject = new PS2AnyObject
        {
            Name = name, FloatProperties = [mass], IntProperties = [0, 255], ObjectSlots = [..slots], BehaviourPack = new PS2BehaviourCommandPack()
        };
        gameObject.SetID(id);
        return gameObject;
    }

    // A level's resource file of the objects, its collision and particles empty (the unpack reads them of every chunk)
    private static ITwinSection Chunk(params ITwinObject[] objects)
    {
        var chunk = new PS2ItemFactory().GenerateRM();
        var objectSection = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        foreach (var gameObject in objects)
        {
            objectSection.AddItem(gameObject);
        }

        var collision = new PS2AnyCollisionData { Version = PS2AnyCollisionData.GameVersion };
        collision.SetID(Constants.LEVEL_COLLISION_ITEM);
        chunk.AddItem(collision);
        var particles = new PS2AnyParticleData();
        particles.SetID(Constants.LEVEL_PARTICLES_ITEM);
        chunk.AddItem(particles);
        return chunk;
    }

    // The beach's objects are the assets, the hub's of another mass their variants, and the hub's object linking to the other links to the
    // hub's version of it. Unpacked, written and merged the way creating a project does it, the project's assets folder
    private string Unpack(string linking, string linked, bool linksBack)
    {
        UInt16[] back = linksBack ? [LinkingId] : [];
        var chunks = new (string Path, ITwinSection Chunk)[]
        {
            ("Levels\\Earth\\Hub\\Beach.rm2", Chunk(Object(linking, LinkingId, 1, LinkedId), Object(linked, LinkedId, 1, back))),
            ("Levels\\Earth\\Hub\\Hubd.rm2", Chunk(Object(linking, LinkingId, 2, LinkedId), Object(linked, LinkedId, 2, back)))
        };
        var files = chunks.Select(chunk =>
        {
            var bytes = TestGeometry.Serialize(chunk.Chunk);
            return new TT_Lab.Project.Project.DiscFile(chunk.Path, () => bytes);
        }).ToList();
        var project = Project.Project;
        project.GlobalPackagePS2.Enabled = true;
        project.Ps2Package.Enabled = true;
        using var gate = new MemoryGate(1L << 30);
        var writer = new CreationWriter(project.AssetManager, gate);

        project.UnpackAssets(GamePlatform.PS2, files, project.GlobalPackagePS2, project.Ps2Package, gate);
        writer.ImportAndWrite();
        writer.RemoveInternalAssets();
        new VariantMerger(project.AssetManager, Project.AssetsPath).Merge();
        project.Serialize(writer.IsWritten);

        return Project.AssetsPath;
    }

    private static JObject ReadJson(string file) => JObject.Parse(File.ReadAllText(file));

    // Every file under the folder by its path in it
    private static SortedDictionary<string, byte[]> Files(string folder)
    {
        return new SortedDictionary<string, byte[]>(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes), StringComparer.Ordinal);
    }

    // The chunk's own values of each asset by the asset's URI, as written
    private static Dictionary<string, JObject> OverridesOf(string assets, string chunk)
    {
        var file = Directory.EnumerateFiles(assets, $"{chunk}.json", SearchOption.AllDirectories).Single();
        return ((JArray)ReadJson(file)[nameof(LevelChunk.Overrides)]!).Cast<JObject>()
            .ToDictionary(entry => (string)entry[nameof(AssetOverride.Asset)]!["_uri"]!, entry => (JObject)entry[nameof(AssetOverride.Values)]!);
    }

    // An object's URI by its name, read from its metadata
    private static string UriOf(string assets, string name, UInt16 id)
    {
        var file = Directory.EnumerateFiles(assets, $"{name}_{id:X}.json", SearchOption.AllDirectories).Single();
        return (string)ReadJson(file)[nameof(IAsset.URI)]!["_uri"]!;
    }

    [Theory]
    // The linking object's variant has the URI that sorts first
    [InlineData("CRATE", "HEALTH", false)]
    [InlineData("WUMPA", "HEALTH", false)]
    // Each links to the other, one of them is merged first in any order
    [InlineData("CRATE", "HEALTH", true)]
    public void VariantsLinkingToOtherVariantsHaveNoValuesOfTheLinks(string linking, string linked, bool linksBack)
    {
        var assets = Unpack(linking, linked, linksBack);

        AssertEveryLinkLeadsToAnAsset(assets);
        // The hub's versions differ in their mass alone once their links lead to the merged variants' assets
        var linkedUri = UriOf(assets, linked, LinkedId);
        var linkingUri = UriOf(assets, linking, LinkingId);
        var hub = OverridesOf(assets, "hubd");
        Assert.Equal(new[] { linkedUri, linkingUri }.Order(StringComparer.Ordinal), hub.Keys.Order(StringComparer.Ordinal));
        foreach (var (asset, values) in hub)
        {
            Assert.True(values.Properties().Select(property => property.Name).SequenceEqual(["AssetData.FloatProperties[0]"]), $"The hub's values of {asset}: {values}");
        }

        Assert.Empty(OverridesOf(assets, "beach"));
        var linkingData = ReadJson(Directory.EnumerateFiles(assets, $"{linking}_{LinkingId:X}.data", SearchOption.AllDirectories).Single());
        Assert.Equal(linkedUri, (string)linkingData[nameof(GameObjectData.ObjectSlots)]![0]!["_uri"]!);
        Assert.DoesNotContain(Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories), file => file.Contains(HubVariation));
    }

    // Every link of the asset files, chunks' overrides included, leads to an asset of the files or is no asset (the empty link, TT Lab's
    // built-in ones). The assets are the metadata files' URIs; their references and data are the links, model files' material URIs too.
    // What package an asset is in is no link the merge writes (a project's packages are in a package of the base package's name)
    private static void AssertEveryLinkLeadsToAnAsset(string assets)
    {
        var uris = new HashSet<string>(StringComparer.Ordinal);
        var links = new List<(string File, string Path, string Uri)>();
        foreach (var file in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories))
        {
            switch (Path.GetExtension(file))
            {
                case ".json":
                {
                    var metadata = ReadJson(file);
                    if (metadata[nameof(IAsset.URI)]?["_uri"] is JValue { Type: JTokenType.String } uri)
                    {
                        uris.Add((string)uri!);
                        metadata.Remove(nameof(IAsset.URI));
                    }

                    metadata.Remove(nameof(IAsset.Package));
                    links.AddRange(LinksIn(metadata).Select(link => (file, link.Path, link.Uri)));
                    break;
                }
                case ".data":
                    links.AddRange(LinksIn(JToken.Parse(File.ReadAllText(file))).Select(link => (file, link.Path, link.Uri)));
                    break;
                case ".tlm":
                {
                    var header = TT_Lab.AssetData.Graphics.TlModel.TlmFile.ReadJson(file);
                    links.AddRange(JObject.Parse(header.ToJsonString()).Descendants().OfType<JValue>()
                        .Where(value => value.Type == JTokenType.String && ((string)value!).StartsWith("res://", StringComparison.Ordinal))
                        .Select(value => (file, value.Path, (string)value!)));
                    break;
                }
            }
        }

        Assert.NotEmpty(links);
        var dangling = links.Where(link => !uris.Contains(link.Uri) && link.Uri != LabURI.Empty.ToString() && !link.Uri.StartsWith("res://__GLOBAL__/", StringComparison.Ordinal))
            .Select(link => $"{Path.GetRelativePath(assets, link.File)} {link.Path}: {link.Uri}").ToList();
        Assert.True(dangling.Count == 0, $"Links to assets the project doesn't have:\n{string.Join("\n", dangling)}");
    }

    private static IEnumerable<(string Path, string Uri)> LinksIn(JToken token)
    {
        var tokens = token is JContainer container ? container.DescendantsAndSelf() : [token];
        return tokens.OfType<JObject>().Where(link => link.Count == 1 && link["_uri"] is JValue { Type: JTokenType.String })
            .Select(link => (link.Path, (string)link["_uri"]!));
    }

    // The project's assets kept them in the order they were added (parallel writes left them in the order of the slots they freed): the
    // hub's object linking to the other merged first, its override kept a link to the other one's variant, merged first it had none
    [Fact]
    public void VariantsMergedInEitherOrderGiveTheSameFiles()
    {
        var linkingFirst = MergedInOrder(linkingFirst: true);
        var linkedFirst = MergedInOrder(linkingFirst: false);

        Assert.Equal(linkingFirst.Keys, linkedFirst.Keys);
        foreach (var (path, bytes) in linkingFirst)
        {
            Assert.True(bytes.AsSpan().SequenceEqual(linkedFirst[path]), $"{path} differs");
        }

        Assert.Contains(linkingFirst.Keys, path => path.EndsWith("/hubd.json", StringComparison.Ordinal));
        Assert.DoesNotContain(linkingFirst.Keys, path => path.Contains(HubVariation));
    }

    // The beach's objects and the hub's variants of them of another mass, the linking ones' slot naming the other object of their chunk, the
    // hub listing its versions. Added to a project of their own in the order given and merged: its assets folder's files
    private static SortedDictionary<string, byte[]> MergedInOrder(bool linkingFirst)
    {
        using var project = new TestProject();
        var package = project.Project.Ps2Package.URI;
        var hub = new LevelChunk { AdditionalPath = "levels/earth/hub/hubd" };
        project.Add(hub, "hubd", 0x100, project.Project.Ps2Package);
        var linked = Prepare(package, "HEALTH", LinkedId, string.Empty);
        var linking = Prepare(package, "CRATE", LinkingId, string.Empty);
        var linkedVariant = Prepare(package, "HEALTH", LinkedId, HubVariation);
        var linkingVariant = Prepare(package, "CRATE", LinkingId, HubVariation);
        hub.ItemVersions.AddRange([linkingVariant.URI, linkedVariant.URI]);
        IEnumerable<GameObject> order = linkingFirst ? [linking, linked, linkingVariant, linkedVariant] : [linked, linking, linkedVariant, linkingVariant];
        foreach (var gameObject in order)
        {
            project.AssetManager.AddAsset(gameObject);
        }

        Write(linked, 1, LabURI.Empty);
        Write(linking, 1, linked.URI);
        Write(linkedVariant, 2, LabURI.Empty);
        Write(linkingVariant, 2, linkedVariant.URI);
        hub.Serialize();

        Assert.Equal(2, new VariantMerger(project.AssetManager, project.AssetsPath).Merge());
        return Files(project.AssetsPath);
    }

    private static GameObject Prepare(LabURI package, string name, UInt32 id, string variation)
    {
        var gameObject = new GameObject { Package = package, InvariantName = name, Alias = name, Variation = variation, ID = id };
        gameObject.RegenerateUri();
        return gameObject;
    }

    private static void Write(GameObject gameObject, Single mass, LabURI slot)
    {
        var data = new GameObjectData(gameObject) { Name = gameObject.InvariantName, BehaviourPack = "SetObject(0x60050000, 0x000000C0);\n" };
        data.FloatProperties.Add(mass);
        data.IntProperties.AddRange([0, 255]);
        data.ObjectSlots.Add(slot);
        gameObject.SetData(data);
        gameObject.Serialize(SerializationFlags.SaveData);
    }

    // Values chunks already have of an asset that link to a variant the merge takes away link to the variant's asset, and a value that then
    // is the asset's own is no value of the chunk's: a chunk without values of an asset has no override of it
    [Fact]
    public void ChunksValuesLinkingToMergedVariantsLinkToTheirAssets()
    {
        var hub = new LevelChunk(Project.Project.Ps2Package.URI, "hubb") { AdditionalPath = "levels/earth/hub/hubb" };
        hub.RegenerateUri();
        Project.AssetManager.AddAsset(hub);
        var bird = AddObject("Bird", 0x10, string.Empty);
        var variant = AddObject("Bird", 0x10, "levels_earth_hub_hubb", data => data.TaggedProperties[0] = new(7));
        hub.ItemVersions.Add(variant.URI);
        var spawner = AddObject("Spawner", 0x12, string.Empty, data => data.ObjectSlots.AddRange([bird.URI, LabURI.Empty]));
        var nest = AddObject("Nest", 0x13, string.Empty, data => data.ObjectSlots.Add(bird.URI));
        hub.Overrides.Add(new AssetOverride
        {
            Asset = spawner.URI, Values = { ["AssetData.ObjectSlots[0]"] = Link(variant.URI), ["AssetData.ObjectSlots[1]"] = Link(variant.URI) }
        });
        hub.Overrides.Add(new AssetOverride { Asset = nest.URI, Values = { ["AssetData.ObjectSlots[0]"] = Link(variant.URI) } });

        Assert.Equal(1, new VariantMerger(Project.AssetManager, Project.AssetsPath).Merge());

        Assert.False(Project.AssetManager.DoesAssetExist(variant.URI));
        Assert.Equal([bird.URI, spawner.URI], hub.Overrides.Select(entry => entry.Asset).Order());
        Assert.Equal(["AssetData.TaggedProperties[0]"], hub.GetOverride(bird.URI)!.Values.Keys);
        // The spawner's empty slot is the bird in the hub, its first slot is the asset's
        var (path, value) = Assert.Single(hub.GetOverride(spawner.URI)!.Values);
        Assert.Equal("AssetData.ObjectSlots[1]", path);
        Assert.Equal(bird.URI.ToString(), (string)value["_uri"]!);
        // As written
        var written = ReadJson(Path.Combine(hub.FullPath, $"{hub.Name}.json")).ToString();
        Assert.DoesNotContain(variant.URI.ToString(), written);
        Assert.Contains(spawner.URI.ToString(), written);
        Assert.DoesNotContain(nest.URI.ToString(), written);
    }

    private static JObject Link(LabURI uri) => new() { ["_uri"] = uri.ToString() };

    private GameObject AddObject(string name, UInt32 id, string variation, Action<GameObjectData>? edit = null)
    {
        var gameObject = new GameObject { Variation = variation };
        Project.Add(gameObject, name, id, Project.Project.Ps2Package);
        var data = new GameObjectData(gameObject) { Name = "|Hub|act_BIRD", BehaviourPack = "SetObject(0x60050000, 0x000000C0);\n" };
        data.TaggedProperties.AddRange([new(1), new(2), new(3)]);
        edit?.Invoke(data);
        gameObject.SetData(data);
        gameObject.Serialize(SerializationFlags.SaveData);
        return gameObject;
    }
}
