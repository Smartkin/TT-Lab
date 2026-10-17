using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using Scenery = TT_Lab.Assets.Instance.Scenery;
using CollisionSurface = TT_Lab.Assets.Instance.CollisionSurface;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Blender;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Tests.Tools;

// A Blender linked to TT Lab over the loopback (LINK.md), stood in for by a socket of the test's: what it makes is the project's at once and
// written when it saves, what it changes is a step of the asset's document, what it didn't save goes when it drops, and what TT Lab changes
// goes to it
[Collection(ProjectCollection.Name)]
public sealed class BlenderLinkTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;
    private readonly string _state = Path.Combine(Path.GetTempPath(), "TT Lab Tests", "Link " + Guid.NewGuid().ToString("N"));
    private readonly Func<IEnumerable<TabbedEditorViewModel>> _tabs = LinkedDocuments.Tabs;
    private readonly string _blendFiles = BlendFiles.Folder;
    private BlenderLinkServer? _server;
    private TestBlender? _blender;

    public BlenderLinkTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "Graphics"), Path.Combine(_package.Name, "levels"));
        Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
        LinkedDocuments.Tabs = () => [];
        BlendFiles.Folder = Path.Combine(_state, "blend files");
    }

    public void Dispose()
    {
        _blender?.Dispose();
        _server?.Dispose();
        LinkedDocuments.CloseAll();
        LinkedDocuments.Tabs = _tabs;
        BlendFiles.Folder = _blendFiles;
        _project.Dispose();
        if (Directory.Exists(_state))
        {
            Directory.Delete(_state, true);
        }
    }

    private OGI CreateModel(string name)
    {
        return (OGI)AssetFactory.CreateAsset(typeof(OGI), _project.GetFolder(_package, "Graphics"), name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<OGI>(),
            AssetDataFactory.CreateOgiData)!;
    }

    private BlenderLinkServer Serve()
    {
        _server = new BlenderLinkServer(Locator.Current.GetService<ProjectManager>()!) { StateFolder = _state };
        _server.Start();
        return _server;
    }

    private async Task<TestBlender> Link()
    {
        var server = Serve();
        _blender = await TestBlender.Connect(server.Port);
        var welcome = await _blender.Ask(Hello(server.Token));
        Assert.Equal("welcome", welcome.Type);
        return _blender;
    }

    private static JsonObject Hello(string token, int protocol = LinkProtocol.Version) => TestBlender.Hello(token, protocol);

    private async Task<(OGI Model, Material Material, Texture Texture)> LinkModel(TestBlender blender)
    {
        var model = CreateModel("Crash");
        var linked = await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = model.FullDataPath });
        Assert.Equal("model", linked.Type);
        var material = _project.AssetManager.GetAsset<Material>(new LabURI(linked.Header["materials"]![0]!["uri"]!.GetValue<string>()));
        var texture = _project.AssetManager.GetAsset<Texture>(new LabURI(linked.Header["pictures"]![0]!["uri"]!.GetValue<string>()));
        return (model, material, texture);
    }

    private static T DataOf<T>(IAsset asset) where T : AbstractAssetData => asset.GetData<T>();

    private static JsonObject MaterialJson(Material material) => JsonNode.Parse(JsonConvert.SerializeObject(DataOf<MaterialData>(material)))!.AsObject();

    private static JsonObject UpdateBucket(Material material, UInt32 bucket)
    {
        var data = MaterialJson(material);
        data["DmaChainIndex"] = bucket;
        return new JsonObject { ["type"] = "update_material", ["uri"] = material.URI.ToString(), ["data"] = data, ["changed"] = new JsonArray(new JsonArray("DmaChainIndex")), ["images"] = new JsonArray() };
    }

    private static UInt32 SavedBucket(Material material) => JsonNode.Parse(File.ReadAllText(material.FullDataPath))!["DmaChainIndex"]!.GetValue<UInt32>();

    private static byte[] Png(int size, UInt32 argb) => TextureData.EncodePng(Enumerable.Repeat(argb, size * size).ToArray(), size, size);

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [AvaloniaFact]
    public void TheDiscoveryFileSaysHowToReachTTLab()
    {
        var server = Serve();
        var path = Path.Combine(_state, $"link-{Environment.ProcessId}.json");

        var discovery = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal((LinkProtocol.Version, server.Port, server.Token), (discovery["protocol"]!.GetValue<int>(), discovery["port"]!.GetValue<int>(), discovery["token"]!.GetValue<string>()));
        Assert.Equal(_project.Project.UUID.ToString(), discovery["project"]!["uuid"]!.GetValue<string>());
        Assert.Equal(_project.Project.ProjectPath, discovery["project"]!["path"]!.GetValue<string>());
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }

        server.Stop();
        Assert.False(File.Exists(path));
    }

    [AvaloniaFact]
    public async Task OnlyTheTokenOfTheFileLinks()
    {
        var server = Serve();
        using (var stranger = await TestBlender.Connect(server.Port))
        {
            Assert.Equal("error", (await stranger.Ask(Hello(new string('0', 64)))).Type);
            Assert.True(await stranger.IsClosed());
        }

        using (var old = await TestBlender.Connect(server.Port))
        {
            Assert.Equal("error", (await old.Ask(Hello(server.Token, LinkProtocol.Version + 1))).Type);
        }

        using var blender = await TestBlender.Connect(server.Port);
        var welcome = await blender.Ask(Hello(server.Token));
        Assert.Equal("welcome", welcome.Type);
        Assert.Equal(_project.Project.UUID.ToString(), welcome.Header["project"]!["uuid"]!.GetValue<string>());
    }

    [AvaloniaFact]
    public async Task ALinkedModelTellsWhatItDraws()
    {
        var blender = await Link();
        var model = CreateModel("Crash");

        var linked = await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = model.FullDataPath });

        Assert.Equal(model.URI.ToString(), linked.Header["uri"]!.GetValue<string>());
        Assert.Equal(Sha256(File.ReadAllBytes(model.FullDataPath)), linked.Header["revision"]!.GetValue<string>());
        Assert.False(linked.Header["changed"]!.GetValue<bool>());
        var material = _project.AssetManager.GetAsset<Material>(new LabURI(Assert.Single(linked.Header["materials"]!.AsArray())!["uri"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(MaterialJson(material), linked.Header["materials"]![0]!["data"]));
        var picture = Assert.Single(linked.Header["pictures"]!.AsArray())!;
        var texture = _project.AssetManager.GetAsset<Texture>(new LabURI(picture["uri"]!.GetValue<string>()));
        Assert.Equal(DataOf<MaterialData>(material).Shaders[0].TextureId, texture.URI);
        Assert.Equal(Sha256(DataOf<TextureData>(texture).GetPngBytes()), picture["hash"]!.GetValue<string>());

        Assert.Equal("error", (await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = Path.Combine(_state, "elsewhere.tlm") })).Type);
    }

    [AvaloniaFact]
    public async Task WhatBlenderChangesOfAMaterialIsAStepOfItsDocument()
    {
        var blender = await Link();
        var (_, material, _) = await LinkModel(blender);
        var bucket = DataOf<MaterialData>(material).DmaChainIndex;

        Assert.Equal("ok", (await blender.Ask(UpdateBucket(material, 6))).Type);

        Assert.Equal(6u, DataOf<MaterialData>(material).DmaChainIndex);
        Assert.Equal(bucket, SavedBucket(material));
        var document = LinkedDocuments.Find(material)!.Document;
        Assert.True(document.IsDirty);
        Assert.Equal($"From Blender: {material.Alias}", document.History.Current.Description);
        Assert.True(LinkedDocuments.IsBlenders(document.History.Current));
        document.Undo();
        Assert.Equal(bucket, DataOf<MaterialData>(material).DmaChainIndex);
        document.Redo();
        Assert.Equal(6u, DataOf<MaterialData>(material).DmaChainIndex);

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(material.URI.ToString()) })).Type);
        Assert.Equal(6u, SavedBucket(material));
        Assert.False(document.IsDirty);
    }

    [AvaloniaFact]
    public async Task WhatBlenderDidntSaveGoesWhenItDrops()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var bucket = DataOf<MaterialData>(checker).DmaChainIndex;
        var made = await blender.Ask(new JsonObject { ["type"] = "create_picture", ["owner"] = model.URI.ToString(), ["blender_id"] = "wood", ["name"] = "Wood.png" }, Png(16, 0xFF804020));
        var texture = _project.AssetManager.GetAsset<Texture>(new LabURI(made.Header["uri"]!.GetValue<string>()));
        var data = MaterialJson(checker);
        data["Shaders"]![0]!.AsObject().Remove("TextureId");
        data["Shaders"]![0]!["Image"] = 0;
        var created = await blender.Ask(new JsonObject
        {
            ["type"] = "create_material", ["owner"] = model.URI.ToString(), ["blender_id"] = "painted", ["name"] = "Painted", ["data"] = data,
            ["images"] = new JsonArray(new JsonObject { ["name"] = "Wood.png", ["blender_id"] = "wood", ["uri"] = texture.URI.ToString() })
        });
        var material = _project.AssetManager.GetAsset<Material>(new LabURI(created.Header["uri"]!.GetValue<string>()));
        Assert.Equal(texture.URI, DataOf<MaterialData>(material).Shaders[0].TextureId);
        Assert.Equal("Wood", texture.Alias);
        Assert.True(texture.IsUnsaved && material.IsUnsaved);
        Assert.False(File.Exists(texture.FullDataPath) || File.Exists(material.FullDataPath));
        Assert.Equal("ok", (await blender.Ask(UpdateBucket(checker, 6))).Type);

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "drop" })).Type);

        Assert.False(_project.AssetManager.DoesAssetExist(texture.URI) || _project.AssetManager.DoesAssetExist(material.URI));
        Assert.Null(LinkedDocuments.Find(checker));
        Assert.Equal(bucket, DataOf<MaterialData>(checker).DmaChainIndex);
    }

    [AvaloniaFact]
    public async Task WhatBlenderMadeIsWrittenWhenItSaves()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var png = Png(16, 0xFF804020);
        var made = await blender.Ask(new JsonObject { ["type"] = "create_picture", ["owner"] = model.URI.ToString(), ["blender_id"] = "wood", ["name"] = "Wood" }, png);
        var texture = _project.AssetManager.GetAsset<Texture>(new LabURI(made.Header["uri"]!.GetValue<string>()));

        await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(texture.URI.ToString()) });

        Assert.False(texture.IsUnsaved);
        Assert.True(File.Exists(texture.FullDataPath));
        Assert.Equal(model.URI.ToString(), texture.Parameters[BlenderAssets.OwnerParameter]?.ToString());
        // Blender's picture is what Blender knows it by while the texture is what it was made of
        Assert.Equal(Sha256(png), BlenderLinkAssets.PictureHash(texture));
    }

    // Asked again (the answer lost, the model's file brought it first), the model's own of the Blender ID is the one
    [AvaloniaFact]
    public async Task MakingWhatTheModelHasAgainGivesItsOwn()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var picture = new JsonObject { ["type"] = "create_picture", ["owner"] = model.URI.ToString(), ["blender_id"] = "wood", ["name"] = "Wood" };
        var first = await blender.Ask(picture.DeepClone().AsObject(), Png(16, 0xFF804020));
        var again = await blender.Ask(picture.DeepClone().AsObject(), Png(16, 0xFF0000FF));
        Assert.Equal(first.Header["uri"]!.GetValue<string>(), again.Header["uri"]!.GetValue<string>());
        var texture = _project.AssetManager.GetAsset<Texture>(new LabURI(again.Header["uri"]!.GetValue<string>()));
        Assert.All(DataOf<TextureData>(texture).GetPixels(), pixel => Assert.Equal(0xFF0000FFu, pixel));

        var material = new JsonObject { ["type"] = "create_material", ["owner"] = model.URI.ToString(), ["blender_id"] = "painted", ["name"] = "Painted", ["data"] = MaterialJson(checker), ["images"] = new JsonArray() };
        var made = await blender.Ask(material.DeepClone().AsObject());
        material["data"]!["DmaChainIndex"] = 6;
        var madeAgain = await blender.Ask(material.DeepClone().AsObject());
        Assert.Equal(made.Header["uri"]!.GetValue<string>(), madeAgain.Header["uri"]!.GetValue<string>());
        Assert.Equal(6u, DataOf<MaterialData>(_project.AssetManager.GetAsset<Material>(new LabURI(made.Header["uri"]!.GetValue<string>()))).DmaChainIndex);
        Assert.Single(_project.AssetManager.GetAllAssetsOf<Material>(), asset => asset.Alias.StartsWith("Painted"));
    }

    [AvaloniaFact]
    public async Task APicturePaintedInBlenderIsAStepThatUndoes()
    {
        var blender = await Link();
        var (_, _, texture) = await LinkModel(blender);
        var pixels = DataOf<TextureData>(texture).GetPixels();
        var format = texture.PixelFormat;

        var updated = await blender.Ask(new JsonObject { ["type"] = "update_picture", ["uri"] = texture.URI.ToString() }, Png(32, 0xFFFF0000));
        Assert.True(updated.Type == "ok", updated.Header.ToJsonString());

        Assert.All(DataOf<TextureData>(texture).GetPixels(), pixel => Assert.Equal(0xFFFF0000u, pixel));
        var document = LinkedDocuments.Find(texture)!.Document;
        Assert.Equal($"From Blender: {texture.Alias}", document.History.Current.Description);
        document.Undo();
        Assert.Equal(pixels, DataOf<TextureData>(texture).GetPixels());
        Assert.Equal(format, texture.PixelFormat);
        document.Redo();
        Assert.All(DataOf<TextureData>(texture).GetPixels(), pixel => Assert.Equal(0xFFFF0000u, pixel));
    }

    [AvaloniaFact]
    public async Task AnEditorOpenedTakesOverWhatBlenderChanged()
    {
        var blender = await Link();
        var (_, material, _) = await LinkModel(blender);
        var bucket = DataOf<MaterialData>(material).DmaChainIndex;
        await blender.Ask(UpdateBucket(material, 6));
        var resources = new ResourcesEditorsViewModel();
        LinkedDocuments.Tabs = () => resources.Tabs;

        resources.OpenEditor(material);
        var tab = resources.Tabs.Single();
        await WaitUntil(() => tab.IsLoaded);

        Assert.Same(tab.Document, LinkedDocuments.Find(material)!.Document);
        Assert.True(tab.Document!.IsDirty);
        Assert.True(LinkedDocuments.IsBlenders(tab.Document.History.Current));
        tab.Document.Undo();
        Assert.Equal(bucket, DataOf<MaterialData>(material).DmaChainIndex);
        Assert.Equal(bucket, tab.Document.PropertyGraph.Find("Root.AssetData.DmaChainIndex")!.GetValue());

        // Blender's next change is a step of the tab's
        await blender.Ask(UpdateBucket(material, 8));
        Assert.Equal(8u, tab.Document.PropertyGraph.Find("Root.AssetData.DmaChainIndex")!.GetValue());
        Assert.Equal($"From Blender: {material.Alias}", tab.Document.History.Current.Description);
        await tab.CloseTab(discardChanges: true);
    }

    [AvaloniaFact]
    public async Task WhatTTLabChangesGoesToBlender()
    {
        var blender = await Link();
        var (_, material, _) = await LinkModel(blender);
        await blender.Ask(UpdateBucket(material, 6));
        // Blender's own change isn't told back to it
        await Task.Delay(600);
        Assert.Empty(blender.Pushed("material_changed"));

        var document = LinkedDocuments.Find(material)!.Document;
        document.PropertyGraph.Find("Root.AssetData.DmaChainIndex")!.SetValue(10u);

        var told = await blender.WaitFor("material_changed");
        Assert.Equal(material.URI.ToString(), told.Header["uri"]!.GetValue<string>());
        Assert.Equal(10u, told.Header["data"]!["DmaChainIndex"]!.GetValue<UInt32>());

        material.Alias = "Fur";
        DocumentViewModel.TellChanged(material);
        var renamed = await blender.WaitFor("renamed");
        Assert.Equal("Fur", renamed.Header["name"]!.GetValue<string>());
    }

    [AvaloniaFact]
    public async Task ARenameInBlenderRenamesInTTLab()
    {
        var blender = await Link();
        var (model, _, texture) = await LinkModel(blender);
        var alias = texture.Alias;

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "rename", ["uri"] = texture.URI.ToString(), ["name"] = "Squares.png" })).Type);

        Assert.Equal("Squares", texture.Alias);
        var document = LinkedDocuments.Find(texture)!.Document;
        Assert.Equal($"From Blender: {alias} renamed to Squares", document.History.Current.Description);
        document.Undo();
        Assert.Equal(alias, texture.Alias);
        Assert.Equal("error", (await blender.Ask(new JsonObject { ["type"] = "rename", ["uri"] = texture.URI.ToString(), ["name"] = "Квадраты" })).Type);

        // The project tree marks what the Blender draws
        await WaitUntil(() => LinkMarks.IsLinked(model.URI) && LinkMarks.IsLinked(texture.URI));
        Assert.True(model.GetResourceTreeElement().IsLinkedToBlender);
        blender.Dispose();
        await WaitUntil(() => !LinkMarks.IsLinked(model.URI));
        Assert.False(LinkMarks.IsLinked(model.URI));
    }

    private static string Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TT Lab.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "TT Lab", "BlenderTools", "tests", "fixtures", name);
    }

    [AvaloniaFact]
    public async Task AModelMadeInBlenderBecomesAnAssetOfAFolderTTLabOffers()
    {
        var blender = await Link();
        var graphics = _project.GetFolder(_package, "Graphics");

        var targets = await blender.Ask(new JsonObject { ["type"] = "new_model_targets" });
        var offered = targets.Header["folders"]!.AsArray().Select(folder => folder!["uri"]!.GetValue<string>()).ToList();
        Assert.Contains(graphics.URI.ToString(), offered);
        Assert.All(_project.Project.GetLevelsFolders(TT_Lab.Project.Project.GamePlatform.PS2), levels => Assert.DoesNotContain(levels.URI.ToString(), offered));

        // The add-on's model template, its material and picture made in Blender
        var created = await blender.Ask(new JsonObject { ["type"] = "create_model", ["name"] = "Box", ["folder"] = graphics.URI.ToString() }, File.ReadAllBytes(Fixture("model_template.tlm")));

        Assert.True(created.Type == "created", created.Header.ToJsonString());
        var model = _project.AssetManager.GetAsset<Mesh>(new LabURI(created.Header["uri"]!.GetValue<string>()));
        Assert.Equal(model.FullDataPath, created.Header["path"]!.GetValue<string>());
        Assert.True(File.Exists(model.FullDataPath));
        var material = _project.AssetManager.GetAsset<Material>(DataOf<MeshData>(model).Materials[0]);
        Assert.Equal(model.URI.ToString(), material.Parameters[BlenderAssets.OwnerParameter]?.ToString());

        // The Blender material the file brought, made again through the link, is the one the file made
        var again = await blender.Ask(new JsonObject
        {
            ["type"] = "create_material", ["owner"] = model.URI.ToString(), ["blender_id"] = material.Parameters[TlmMaterials.BlenderMaterialParameter]!.ToString(),
            ["name"] = material.Alias, ["data"] = MaterialJson(material), ["images"] = new JsonArray()
        });
        Assert.Equal(material.URI.ToString(), again.Header["uri"]!.GetValue<string>());

        Assert.Equal("error", (await blender.Ask(new JsonObject { ["type"] = "create_model", ["name"] = "Box", ["folder"] = graphics.URI.ToString() }, "no model"u8.ToArray())).Type);
    }

    // TT Lab starts without a project: what it tells Blender once one is open still goes (the timer telling it ended at its first tick)
    [AvaloniaFact]
    public async Task WhatTTLabChangesGoesToBlenderAfterStartingWithoutAProject()
    {
        var projects = Locator.Current.GetService<ProjectManager>()!;
        var project = projects.OpenedProject;
        projects.OpenedProject = null;
        var server = Serve();
        await Task.Delay(400);
        projects.OpenedProject = project;
        // The server follows the project once a second, closing the links it had: linked before, the link would be closed
        await FollowedProject();
        _blender = await TestBlender.Connect(server.Port);
        Assert.Equal("welcome", (await _blender.Ask(Hello(server.Token))).Type);
        var (_, material, _) = await LinkModel(_blender);

        var document = (await LinkedDocumentsFor(material)).Document;
        document.PropertyGraph.Find("Root.AssetData.DmaChainIndex")!.SetValue(10u);

        var told = await _blender.WaitFor("material_changed");
        Assert.Equal(10u, told.Header["data"]!["DmaChainIndex"]!.GetValue<UInt32>());
    }

    // The discovery file names the project once the server's timer followed it
    private async Task FollowedProject()
    {
        var discovery = Path.Combine(_state, $"link-{Environment.ProcessId}.json");
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(discovery) && JsonNode.Parse(File.ReadAllText(discovery))?["project"] is JsonObject)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("The link didn't follow the project");
    }

    private static Task<LinkedDocuments.Place> LinkedDocumentsFor(IAsset asset) => LinkedDocuments.ForAsync(asset);

    [AvaloniaFact]
    public async Task WritingTheModelIsAStepKeepingTheOneBefore()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var revision = Sha256(File.ReadAllBytes(model.FullDataPath));
        var box = DataOf<OGIData>(model).BoundingBox[1].Y;
        var file = TlmFile.Load(model.FullDataPath);
        file.Root!.GetData()["BoundingBoxMax"] = new JsonArray(0.5f, 3f, 0.5f, 1f);
        var written = Path.Combine(_state, "written.tlm");
        Directory.CreateDirectory(_state);
        file.Save(written);

        var answer = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = revision }, File.ReadAllBytes(written));

        Assert.Equal("ok", answer.Type);
        Assert.Equal(Sha256(File.ReadAllBytes(model.FullDataPath)), answer.Header["revision"]!.GetValue<string>());
        Assert.Equal(3f, DataOf<OGIData>(model).BoundingBox[1].Y);
        Assert.Equal(3f, TlmFile.Load(model.FullDataPath).Root!.GetData().GetVector4("BoundingBoxMax", new()).Y);
        var document = LinkedDocuments.Find(model)!.Document;
        Assert.False(document.IsDirty);
        Assert.Equal($"From Blender: {model.Alias} written", document.History.Current.Description);
        document.Undo();
        Assert.Equal(box, DataOf<OGIData>(model).BoundingBox[1].Y);
        Assert.True(document.IsDirty);

        // TT Lab changed what Blender wrote since (undone): told what's there now
        var conflict = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = answer.Header["revision"]!.DeepClone() },
            File.ReadAllBytes(written));
        Assert.Equal("conflict", conflict.Type);
        Assert.True(conflict.Header["changed"]!.GetValue<bool>());
        // What TT Lab has now is the model Blender had before its write: no conflict for that
        Assert.Equal(revision, conflict.Header["revision"]!.GetValue<string>());

        var refused = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["force"] = true }, "not a model"u8.ToArray());
        Assert.Equal("refused", refused.Type);
        Assert.Equal(answer.Header["revision"]!.GetValue<string>(), Sha256(File.ReadAllBytes(model.FullDataPath)));
    }

    // Use an Existing Blend File: a model of a blend file put in the model's place draws what TT Lab's model doesn't, maybe of another project
    [AvaloniaFact]
    public async Task ALinkedModelOfABlendFileTellsWhatTheProjectLacksOfIt()
    {
        var blender = await Link();
        var model = CreateModel("Crash");
        var varnish = _project.Add(new Material(), "Varnish", 0x5000);
        varnish.SetData(new MaterialData(varnish));
        var wood = BlenderAssets.CreateTexture(model, "Wood", Png(16, 0xFF806040), null);

        var linked = await blender.Ask(new JsonObject
        {
            ["type"] = "link_model", ["path"] = model.FullDataPath,
            ["materials"] = new JsonArray(varnish.URI.ToString(), "res://Elsewhere/Material/Varnish", ""),
            ["pictures"] = new JsonArray(wood.URI.ToString(), "res://Elsewhere/Texture/Wood", varnish.URI.ToString())
        });

        Assert.Equal("model", linked.Type);
        // What TT Lab's model draws and what Blender's does
        Assert.Equal(2, linked.Header["materials"]!.AsArray().Count);
        Assert.Contains(linked.Header["materials"]!.AsArray(), entry => entry!["uri"]!.GetValue<string>() == varnish.URI.ToString());
        Assert.Contains(linked.Header["pictures"]!.AsArray(), entry => entry!["uri"]!.GetValue<string>() == wood.URI.ToString());
        // A material isn't a picture
        Assert.Equal(["res://Elsewhere/Material/Varnish", "res://Elsewhere/Texture/Wood", varnish.URI.ToString()],
            linked.Header["missing"]!.AsArray().Select(uri => uri!.GetValue<string>()).ToArray());
        Assert.Contains(model.URI, Assert.Single(_server!.Sessions).Assets.Models);

        // Given another model in Blender: not Blender's any more
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "unlink_model", ["uri"] = model.URI.ToString() })).Type);
        Assert.DoesNotContain(model.URI, Assert.Single(_server!.Sessions).Assets.Models);
    }

    [AvaloniaFact]
    public async Task AModelPutInTheModelsPlaceIsAStepSayingSo()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var file = TlmFile.Load(model.FullDataPath);
        file.Root!.GetData()["BoundingBoxMax"] = new JsonArray(0.5f, 3f, 0.5f, 1f);
        var written = Path.Combine(_state, "written.tlm");
        file.Save(written);

        var answer = await blender.Ask(new JsonObject
        {
            ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = "", ["force"] = true, ["replacing"] = true, ["name"] = "Reimu",
            ["blend"] = Path.Combine(_state, "reimu_spin.blend")
        }, File.ReadAllBytes(written));

        Assert.Equal("ok", answer.Type);
        Assert.Equal(3f, DataOf<OGIData>(model).BoundingBox[1].Y);
        var document = LinkedDocuments.Find(model)!.Document;
        Assert.Equal($"From Blender: {model.Alias} replaced by Reimu of reimu_spin.blend", document.History.Current.Description);
        Assert.False(document.IsDirty);
    }

    // Edit in Blender opens the blend file saved for its models, and no longer for the ones it no longer has a model of: this project's
    // models it released, and whatever of another project (which TT Lab can't read) it was remembered for
    [AvaloniaFact]
    public async Task ASavedBlendFileIsTheOneOfItsModelsOnly()
    {
        var blender = await Link();
        var model = CreateModel("Crash");
        var released = CreateModel("Cortex");
        var elsewhere = CreateModel("Nina");
        var blend = Path.Combine(_state, "Crash.blend");
        var other = Path.Combine(_state, "Nina.blend");
        File.WriteAllText(blend, string.Empty);
        File.WriteAllText(other, string.Empty);
        var uuid = _project.Project.UUID;
        var anotherProject = Guid.NewGuid();
        var stillHeld = Guid.NewGuid();
        var anotherModel = new LabURI("res://Another/OGI/Reimu");
        var anotherOwn = new LabURI("res://Another/OGI/Marisa");
        var heldModel = new LabURI("res://Held/OGI/Sanae");
        BlendFiles.Remember(uuid, released.URI, blend);
        BlendFiles.Remember(uuid, elsewhere.URI, other);
        BlendFiles.Remember(anotherProject, anotherModel, blend);
        BlendFiles.Remember(anotherProject, anotherOwn, other);
        BlendFiles.Remember(stillHeld, heldModel, blend);
        Assert.Equal("model", (await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = model.FullDataPath })).Type);

        var answer = await blender.Ask(new JsonObject
        {
            ["type"] = "blend_saved", ["path"] = blend,
            ["released"] = new JsonArray(released.FullDataPath, elsewhere.FullDataPath, Path.Combine(_state, "another project", "assets", "OGI.tlm")),
            ["released_projects"] = new JsonArray(anotherProject.ToString(), uuid.ToString())
        });

        Assert.Equal("ok", answer.Type);
        Assert.Equal(blend, BlendFiles.Of(uuid, model.URI));
        Assert.Null(BlendFiles.Of(uuid, released.URI));
        // Its blend file is another
        Assert.Equal(other, BlendFiles.Of(uuid, elsewhere.URI));
        // The blend file has no model of the other project any more, but of a third project still
        Assert.Null(BlendFiles.Of(anotherProject, anotherModel));
        Assert.Equal(other, BlendFiles.Of(anotherProject, anotherOwn));
        Assert.Equal(blend, BlendFiles.Of(stillHeld, heldModel));
        Assert.Equal(blend, Assert.Single(_server!.Sessions).BlendPath);
    }

    // Blender's own forced write into TT Lab's unsaved changes leaves them unsaved: its next write is no conflict, and the blend file it saved
    // with it opened again isn't told the model changed, also once TT Lab saved it (a Blender that has the file it read before is). TT Lab
    // saving the model as the Blender has it tells it nothing
    [AvaloniaFact]
    public async Task WhatABlenderWroteIsntAChangeForIt()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var original = "file:" + Sha256(File.ReadAllBytes(model.FullDataPath));
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f))).Type);
        var document = LinkedDocuments.Find(model)!.Document;
        document.Undo();
        var replaced = await blender.Ask(Replacing(model), WithBox(model, 5f));
        Assert.Equal("ok", replaced.Type);

        var again = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = replaced.Header["revision"]!.DeepClone() },
            WithBox(model, 6f));

        Assert.True(again.Type == "ok", again.Header.ToJsonString());
        Assert.True(document.IsDirty);
        Assert.Equal(6f, DataOf<OGIData>(model).BoundingBox[1].Y);
        var kept = again.Header["revision"]!.GetValue<string>();
        await LinksAs(kept, false);
        await LinksAs(original, true);

        // TT Lab saves it as the Blender has it: the same revision, nothing to tell (TT Lab's undo before was a change it's told of)
        await Task.Delay(500);
        var told = blender.Pushed("model_changed").Count();
        document.Save();
        Assert.Equal(kept, Sha256(File.ReadAllBytes(model.FullDataPath)));
        await LinksAs(kept, false);
        await Task.Delay(500);
        Assert.Equal(told, blender.Pushed("model_changed").Count());

        async Task LinksAs(string revision, bool changed)
        {
            using var other = await TestBlender.Connect(_server!.Port);
            Assert.Equal("welcome", (await other.Ask(TestBlender.Hello(_server.Token))).Type);
            var linked = await other.Ask(new JsonObject { ["type"] = "link_model", ["path"] = model.FullDataPath, ["revision"] = revision });
            Assert.Equal(changed, linked.Header["changed"]!.GetValue<bool>());
            Assert.Equal(kept, linked.Header["revision"]!.GetValue<string>());
        }
    }

    private byte[] Drawing(OGI model, Material material, bool exported = false)
    {
        var file = TlmFile.Load(model.FullDataPath);
        file.Materials[0]!["uri"] = material.URI.ToString();
        if (exported)
        {
            file.Json[TlmMaterials.ExportedByKey] = "twin_tech_tools test";
        }

        var written = Path.Combine(_state, "drawing.tlm");
        Directory.CreateDirectory(_state);
        file.Save(written);
        return File.ReadAllBytes(written);
    }

    private async Task<Material> MadeMaterial(TestBlender blender, OGI model, Material like, string id, JsonObject? data = null, JsonArray? images = null)
    {
        var created = await blender.Ask(new JsonObject
        {
            ["type"] = "create_material", ["owner"] = model.URI.ToString(), ["blender_id"] = id, ["name"] = id, ["data"] = data ?? MaterialJson(like), ["images"] = images ?? new JsonArray()
        });
        return _project.AssetManager.GetAsset<Material>(new LabURI(created.Header["uri"]!.GetValue<string>()));
    }

    // What Blender made that a model TT Lab took draws is saved with it: a drop doesn't take it from under the model's file
    [AvaloniaFact]
    public async Task WhatATakenModelDrawsStaysThroughADrop()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var painted = await MadeMaterial(blender, model, checker, "painted");
        Assert.True(painted.IsUnsaved);

        Assert.Equal("ok", (await blender.Ask(Replacing(model), Drawing(model, painted))).Type);

        Assert.False(painted.IsUnsaved);
        Assert.True(File.Exists(painted.FullDataPath));
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "drop" })).Type);
        Assert.True(_project.AssetManager.DoesAssetExist(painted.URI));
    }

    // Put in place over TT Lab's unsaved changes the model isn't saved: what it draws stays through a drop all the same, saved, a step can
    // bring it back
    [AvaloniaFact]
    public async Task WhatAnUnsavedTakenModelDrawsStaysThroughADrop()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f))).Type);
        LinkedDocuments.Find(model)!.Document.Undo();
        var painted = await MadeMaterial(blender, model, checker, "painted");
        Assert.Equal("ok", (await blender.Ask(Replacing(model), Drawing(model, painted))).Type);
        Assert.True(painted.IsUnsaved);

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "drop" })).Type);

        Assert.True(_project.AssetManager.DoesAssetExist(painted.URI));
        Assert.False(painted.IsUnsaved);
    }

    // The model's own saved file draws what it drew before its unsaved changes: discarded, it draws it again
    [AvaloniaFact]
    public async Task WhatTheModelsSavedFileDrawsIsUsed()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var painted = await MadeMaterial(blender, model, checker, "painted");
        File.WriteAllBytes(model.FullDataPath, Drawing(model, painted));
        LinkedDocuments.CloseAll();

        Assert.False(UndoHistory.KeepsLinkTo(painted.URI));
        Assert.True(BlenderAssets.IsUsedByOthers(painted, model));
        File.WriteAllBytes(model.FullDataPath, Drawing(model, checker));
        Assert.False(BlenderAssets.IsUsedByOthers(painted, model));
    }

    // The blend file is remembered for the models it has as TT Lab has them, which the add-on lists: not one TT Lab changed since
    [AvaloniaFact]
    public async Task ASavedBlendFileIsTheOneOfTheModelsItHasAsTTLabHasThem()
    {
        var blender = await Link();
        var model = CreateModel("Crash");
        var changed = CreateModel("Cortex");
        foreach (var linked in new[] { model, changed })
        {
            Assert.Equal("model", (await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = linked.FullDataPath })).Type);
        }

        var blend = Path.Combine(_state, "Crash.blend");
        File.WriteAllText(blend, string.Empty);

        Assert.Equal("ok", (await blender.Ask(new JsonObject
        {
            ["type"] = "blend_saved", ["path"] = blend, ["released"] = new JsonArray(), ["models"] = new JsonArray(model.URI.ToString())
        })).Type);

        Assert.Equal(blend, BlendFiles.Of(_project.Project.UUID, model.URI));
        Assert.Null(BlendFiles.Of(_project.Project.UUID, changed.URI));
    }

    // Blender's change waits for the step the user is making (a drag) and is a step of its own after it
    [AvaloniaFact]
    public async Task BlendersChangeWaitsForTheStepTheUserIsMaking()
    {
        var blender = await Link();
        var (_, checker, _) = await LinkModel(blender);
        Assert.Equal("ok", (await blender.Ask(UpdateBucket(checker, 6))).Type);
        var place = LinkedDocuments.Find(checker)!;
        var document = place.Document;
        var group = document.History.BeginGroup("Dragged");
        document.PropertyGraph.Find(place.Data.Path + ".DmaChainIndex")!.SetValue(8u);

        var answer = blender.Ask(UpdateBucket(checker, 10));
        await Task.Delay(300);

        Assert.False(answer.IsCompleted);
        group.Dispose();
        Assert.Equal("ok", (await answer).Type);
        Assert.Equal(10u, DataOf<MaterialData>(checker).DmaChainIndex);
        Assert.Equal($"From Blender: {checker.Alias}", document.History.Current.Description);
        Assert.Equal("Dragged", document.History.Current.Parent!.Description);
    }

    // A picture a material drew before Blender gave it another stays while a step of the material's document can put it back
    [AvaloniaFact]
    public async Task APictureAStepPutsBackIsntDeletedAsUnused()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        async Task<Texture> Picture(string id, UInt32 color)
        {
            var made = await blender.Ask(new JsonObject { ["type"] = "create_picture", ["owner"] = model.URI.ToString(), ["blender_id"] = id, ["name"] = id }, Png(16, color));
            return _project.AssetManager.GetAsset<Texture>(new LabURI(made.Header["uri"]!.GetValue<string>()));
        }

        JsonObject Drawn()
        {
            var data = MaterialJson(checker);
            data["Shaders"]![0]!.AsObject().Remove("TextureId");
            data["Shaders"]![0]!["Image"] = 0;
            return data;
        }

        JsonArray Image(Texture texture, string id) => new(new JsonObject { ["name"] = id, ["blender_id"] = id, ["uri"] = texture.URI.ToString() });

        var first = await Picture("first", 0xFF804020);
        var second = await Picture("second", 0xFF204080);
        var painted = await MadeMaterial(blender, model, checker, "painted", Drawn(), Image(first, "first"));
        Assert.Equal(first.URI, DataOf<MaterialData>(painted).Shaders[0].TextureId);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(first.URI.ToString(), second.URI.ToString(), painted.URI.ToString()) })).Type);
        var update = new JsonObject
        {
            ["type"] = "update_material", ["uri"] = painted.URI.ToString(), ["data"] = Drawn(), ["changed"] = new JsonArray(new JsonArray("Shaders", 0, "TextureId")),
            ["images"] = Image(second, "second")
        };
        Assert.Equal("ok", (await blender.Ask(update)).Type);
        Assert.Equal(second.URI, DataOf<MaterialData>(painted).Shaders[0].TextureId);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(painted.URI.ToString()) })).Type);
        var write = new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["force"] = true };

        Assert.Equal("ok", (await blender.Ask(write.DeepClone().AsObject(), Drawing(model, painted, true))).Type);
        await Settle();

        Assert.True(_project.AssetManager.DoesAssetExist(first.URI));
        // The material's document closed, nothing puts it back
        LinkedDocuments.CloseAll();
        Assert.Equal("ok", (await blender.Ask(write.DeepClone().AsObject(), Drawing(model, painted, true))).Type);
        await WaitUntil(() => !_project.AssetManager.DoesAssetExist(first.URI));
        Assert.False(_project.AssetManager.DoesAssetExist(first.URI));
        Assert.True(_project.AssetManager.DoesAssetExist(second.URI));
    }

    // A save writes every model: one Blender didn't change is no step and leaves the file alone
    [AvaloniaFact]
    public async Task AnUnchangedModelWrittenIsNoStep()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var file = File.ReadAllBytes(model.FullDataPath);
        var written = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(file) }, file);
        Assert.True(written.Type == "ok", written.Header.ToJsonString());
        var document = LinkedDocuments.Find(model)!.Document;

        Assert.Same(document.History.Root, document.History.Current);
        Assert.Equal(file, File.ReadAllBytes(model.FullDataPath));
        Assert.False(document.IsDirty);
    }

    // Take TT Lab's: the model as TT Lab has it, its unsaved changes too, which the Blender has from then on
    [AvaloniaFact]
    public async Task TakingTTLabsModelGivesItAsTTLabHasIt()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var box = DataOf<OGIData>(model).BoundingBox[1].Y;
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f))).Type);
        LinkedDocuments.Find(model)!.Document.Undo();

        var taken = await blender.Ask(new JsonObject { ["type"] = "get_model", ["uri"] = model.URI.ToString() });

        Assert.Equal("model", taken.Type);
        // Its unsaved changes: the revision is of what it gives
        Assert.True(LinkedDocuments.Find(model)!.Document.IsDirty);
        Assert.Equal(Sha256(taken.Payload!), taken.Header["revision"]!.GetValue<string>());
        Assert.Equal(box, TlmFile.Read(new MemoryStream(taken.Payload!)).Root!.GetData().GetVector4("BoundingBoxMax", new()).Y);
        // What it took isn't a change for it
        var written = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = taken.Header["revision"]!.DeepClone() },
            WithBox(model, 4f));
        Assert.True(written.Type == "ok", written.Header.ToJsonString());
    }

    // Blender says which blend file it has open: Use an Existing Blend File of it goes to that Blender
    [AvaloniaFact]
    public async Task ABlendersSessionKnowsTheBlendFileItHasOpen()
    {
        var server = Serve();
        _blender = await TestBlender.Connect(server.Port);
        var opened = Path.Combine(_state, "reimu_spin.blend");

        Assert.Equal("welcome", (await _blender.Ask(TestBlender.Hello(server.Token, path: opened))).Type);

        Assert.Equal(opened, Assert.Single(server.Sessions).BlendPath);
        // Saved as another
        var saved = Path.Combine(_state, "reimu_spin_2.blend");
        Assert.Equal("ok", (await _blender.Ask(new JsonObject { ["type"] = "blend_saved", ["path"] = saved, ["released"] = new JsonArray() })).Type);
        Assert.Equal(saved, Assert.Single(server.Sessions).BlendPath);
    }

    private JsonObject Replacing(OGI model, string name = "Reimu") => new()
    {
        ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = "", ["force"] = true, ["replacing"] = true, ["name"] = name,
        ["blend"] = Path.Combine(_state, "reimu_spin.blend")
    };

    private byte[] WithBox(OGI model, float height)
    {
        var file = TlmFile.Load(model.FullDataPath);
        file.Root!.GetData()["BoundingBoxMax"] = new JsonArray(0.5f, height, 0.5f, 1f);
        var written = Path.Combine(_state, "written.tlm");
        Directory.CreateDirectory(_state);
        file.Save(written);
        return File.ReadAllBytes(written);
    }

    // Put in the place of a model whose document has unsaved changes: one more unsaved step, its file stays what it was until TT Lab
    // saves it (undoing to where it was saved would have had Blender's file under data marked saved)
    [AvaloniaFact]
    public async Task AModelPutInPlaceOverUnsavedChangesStaysUnsaved()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var answer = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f));
        Assert.Equal("ok", answer.Type);
        var document = LinkedDocuments.Find(model)!.Document;
        document.Undo();
        Assert.True(document.IsDirty);
        var file = File.ReadAllBytes(model.FullDataPath);

        var replaced = await blender.Ask(Replacing(model), WithBox(model, 5f));

        Assert.Equal("ok", replaced.Type);
        Assert.Equal(5f, DataOf<OGIData>(model).BoundingBox[1].Y);
        Assert.Equal(file, File.ReadAllBytes(model.FullDataPath));
        Assert.True(document.IsDirty);
        Assert.Equal($"From Blender: {model.Alias} replaced by Reimu of reimu_spin.blend", document.History.Current.Description);
        document.Save();
        Assert.Equal(5f, TlmFile.Load(model.FullDataPath).Root!.GetData().GetVector4("BoundingBoxMax", new()).Y);
    }

    // Put in place by one Blender: another Blender's model of it stands for none from then on, its saves would take it back
    [AvaloniaFact]
    public async Task AModelPutInPlaceIsReleasedByTheOtherBlenders()
    {
        var first = await Link();
        var (model, _, _) = await LinkModel(first);
        using var second = await TestBlender.Connect(_server!.Port);
        Assert.Equal("welcome", (await second.Ask(Hello(_server.Token))).Type);
        Assert.Equal("model", (await second.Ask(new JsonObject { ["type"] = "link_model", ["path"] = model.FullDataPath })).Type);

        Assert.Equal("ok", (await second.Ask(Replacing(model), WithBox(model, 3f))).Type);

        var released = await first.WaitFor("released");
        Assert.Equal(model.URI.ToString(), released.Header["uri"]!.GetValue<string>());
        Assert.Equal(model.Alias, released.Header["name"]!.GetValue<string>());
        Assert.Equal("Reimu of reimu_spin.blend", released.Header["by"]!.GetValue<string>());
        Assert.Single(_server.Sessions, session => session.Assets.Models.Contains(model.URI));
        Assert.Empty(second.Pushed("released"));
    }

    // A material made in Blender the model doesn't draw any more is deleted once nothing uses it, not while a step of an open document can
    // bring back a model file drawing it: undone, the model drew a material that was gone
    [AvaloniaFact]
    public async Task WhatAStepCanBringBackIsntDeletedAsUnused()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var created = await blender.Ask(new JsonObject
        {
            ["type"] = "create_material", ["owner"] = model.URI.ToString(), ["blender_id"] = "painted", ["name"] = "Painted", ["data"] = MaterialJson(checker),
            ["images"] = new JsonArray()
        });
        var painted = new LabURI(created.Header["uri"]!.GetValue<string>());
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(painted.ToString()) })).Type);
        var file = TlmFile.Load(model.FullDataPath);
        file.Json[TlmMaterials.ExportedByKey] = "twin_tech_tools test";
        var drawn = file.Materials[0]!["uri"]!.GetValue<string>();
        var written = Path.Combine(_state, "written.tlm");
        Directory.CreateDirectory(_state);
        byte[] Drawing(string material)
        {
            file.Materials[0]!["uri"] = material;
            file.Save(written);
            return File.ReadAllBytes(written);
        }

        var write = new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["force"] = true };
        Assert.Equal("ok", (await blender.Ask(write.DeepClone().AsObject(), Drawing(painted.ToString()))).Type);
        Assert.Equal("ok", (await blender.Ask(write.DeepClone().AsObject(), Drawing(drawn))).Type);
        await Settle();

        Assert.True(_project.AssetManager.DoesAssetExist(painted));

        // Its document closed, the next write without it deletes it
        LinkedDocuments.CloseAll();
        Assert.Equal("ok", (await blender.Ask(write.DeepClone().AsObject(), Drawing(drawn))).Type);
        await WaitUntil(() => !_project.AssetManager.DoesAssetExist(painted));
        Assert.False(_project.AssetManager.DoesAssetExist(painted));
    }

    // What the dispatcher was handed meanwhile (the deletion of what a model doesn't draw any more) has run
    private static async Task Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(50);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int milliseconds = 5000)
    {
        for (var waited = 0; waited < milliseconds && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }
    }

    // A new chunk (Crash and a surface for its ground's collision), its scenery's model file with another fog
    private (LevelChunk Chunk, Scenery Scenery, byte[] Written) ChunkWithAnotherFog()
    {
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
        var folder = _project.GetFolder(_package, "levels");
        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, "beach", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(),
            asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        var scenery = chunk.ChunkResources.Select(_project.AssetManager.GetAsset).OfType<Scenery>().Single();
        var file = TlmFile.Load(scenery.FullDataPath);
        file.Root!.GetData()["FogColor"] = 3;
        var written = Path.Combine(_state, "scenery.tlm");
        Directory.CreateDirectory(_state);
        file.Save(written);
        return (chunk, scenery, File.ReadAllBytes(written));
    }

    private static async Task<TabbedEditorViewModel> OpenScene(ScenesEditorsViewModel scenes, LevelChunk chunk)
    {
        scenes.OpenEditor(chunk);
        var tab = scenes.Tabs.Single(tab => tab.EditableResource == chunk.URI);
        await WaitUntil(() => tab.IsLoaded);
        return tab;
    }

    // A chunk's scenery is edited in its scene: what Blender writes of it is a step of the scene's document while it's open
    [AvaloniaFact]
    public async Task ASceneryBlenderWritesIsAStepOfItsChunksScene()
    {
        var blender = await Link();
        var (chunk, scenery, written) = ChunkWithAnotherFog();
        var linked = await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = scenery.FullDataPath });
        Assert.Equal(scenery.URI.ToString(), linked.Header["uri"]!.GetValue<string>());
        var scenes = new ScenesEditorsViewModel();
        LinkedDocuments.Tabs = () => scenes.Tabs;
        var tab = await OpenScene(scenes, chunk);

        var answer = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = scenery.URI.ToString(), ["revision"] = linked.Header["revision"]!.DeepClone() }, written);

        Assert.True(answer.Type == "ok", answer.Header.ToJsonString());
        Assert.Equal(3u, DataOf<SceneryData>(scenery).FogColor);
        Assert.Same(tab.Document, LinkedDocuments.Find(scenery)!.Document);
        Assert.Equal($"From Blender: {scenery.Alias} written", tab.Document!.History.Current.Description);
        Assert.False(tab.Document.IsDirty);
        tab.Document.Undo();
        Assert.Equal(0u, DataOf<SceneryData>(scenery).FogColor);
        Assert.Equal(0u, tab.Document.PropertyGraph.Find(LinkedDocuments.Find(scenery)!.Data.Path + ".FogColor")!.GetValue());
        await tab.CloseTab(discardChanges: true);
    }

    // Written while the scene wasn't open: the scene opened later saves what Blender changed with the chunk
    [AvaloniaFact]
    public async Task ASceneOpenedTakesWhatBlenderChangedOfItsScenery()
    {
        var blender = await Link();
        var (chunk, scenery, written) = ChunkWithAnotherFog();
        var linked = await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = scenery.FullDataPath });
        var answer = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = scenery.URI.ToString(), ["revision"] = linked.Header["revision"]!.DeepClone() }, written);
        Assert.Equal("ok", answer.Type);
        // Undone where it was written: unsaved, the scene takes it
        LinkedDocuments.Find(scenery)!.Document.Undo();
        Assert.Equal(0u, DataOf<SceneryData>(scenery).FogColor);
        var scenes = new ScenesEditorsViewModel();
        LinkedDocuments.Tabs = () => scenes.Tabs;

        var tab = await OpenScene(scenes, chunk);

        Assert.Same(tab.Document, LinkedDocuments.Find(scenery)!.Document);
        Assert.True(tab.Document!.IsDirty);
        Assert.True(tab.Document.HasChangesIn(new HashSet<LabURI> { scenery.URI }));
        tab.SaveTab();
        Assert.Equal(0, TlmFile.Load(scenery.FullDataPath).Root!.GetData().GetInt("FogColor", -1));
        await tab.CloseTab(discardChanges: true);
    }

    private byte[] WithFog(Scenery scenery, int fog)
    {
        var file = TlmFile.Load(scenery.FullDataPath);
        file.Root!.GetData()["FogColor"] = fog;
        var written = Path.Combine(_state, "scenery.tlm");
        Directory.CreateDirectory(_state);
        file.Save(written);
        return File.ReadAllBytes(written);
    }

    private async Task<bool> LinksUnchanged(IAsset model, string revision)
    {
        using var other = await TestBlender.Connect(_server!.Port);
        Assert.Equal("welcome", (await other.Ask(TestBlender.Hello(_server.Token))).Type);
        var linked = await other.Ask(new JsonObject { ["type"] = "link_model", ["path"] = model.FullDataPath, ["revision"] = revision });
        return !linked.Header["changed"]!.GetValue<bool>();
    }

    // Steps of the scene that leave its scenery as it was (an instance's values) aren't changes of the scenery for Blender: its writes into the
    // scene with unsaved changes are no conflict, and saving the scene writes the scenery the Blender has (its revision the file's)
    [AvaloniaFact]
    public async Task OtherStepsOfTheSceneArentChangesOfItsScenery()
    {
        var blender = await Link();
        var (chunk, scenery, written) = ChunkWithAnotherFog();
        var linked = await blender.Ask(new JsonObject { ["type"] = "link_model", ["path"] = scenery.FullDataPath });
        var scenes = new ScenesEditorsViewModel();
        LinkedDocuments.Tabs = () => scenes.Tabs;
        var tab = await OpenScene(scenes, chunk);
        var document = tab.Document!;
        var instance = chunk.ChunkResources.Select(_project.AssetManager.GetAsset).OfType<TT_Lab.Assets.Instance.ObjectInstance>().First();
        void Renumber(short index) => document.PropertyGraph.Find(document.FindChunkResource(instance.URI)!.Path + ".AssetData.RefListIndex")!.SetValue(index);
        Renumber(5);
        Assert.True(document.IsDirty);

        var first = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = scenery.URI.ToString(), ["revision"] = linked.Header["revision"]!.DeepClone() }, written);
        Assert.True(first.Type == "ok", first.Header.ToJsonString());
        Renumber(6);
        var second = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = scenery.URI.ToString(), ["revision"] = first.Header["revision"]!.DeepClone() },
            WithFog(scenery, 4));

        Assert.True(second.Type == "ok", second.Header.ToJsonString());
        Assert.Equal(4u, DataOf<SceneryData>(scenery).FogColor);
        Assert.True(document.IsDirty);
        Assert.True(await LinksUnchanged(scenery, second.Header["revision"]!.GetValue<string>()));
        tab.SaveTab();
        Assert.Equal(second.Header["revision"]!.GetValue<string>(), Sha256(File.ReadAllBytes(scenery.FullDataPath)));
        Assert.True(await LinksUnchanged(scenery, second.Header["revision"]!.GetValue<string>()));
        Assert.False(await LinksUnchanged(scenery, linked.Header["revision"]!.GetValue<string>()));
        await tab.CloseTab(discardChanges: true);
    }

    // Deleted in TT Lab and made again in Blender (of the same name, so of the same URI): the new material's changes and save are its own, the
    // deleted one's document of no tab went with it
    [AvaloniaFact]
    public async Task AMaterialMadeAgainAfterItsDeletionIsSavedAsItself()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var painted = await MadeMaterial(blender, model, checker, "Painted");
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(painted.URI.ToString()) })).Type);
        Assert.Equal("ok", (await blender.Ask(UpdateBucket(painted, 6))).Type);
        Assert.Same(painted, LinkedDocuments.Find(painted)!.Document.DocumentModel);

        Assert.True(await AssetDeletion.DeleteAsync(painted));
        var again = await MadeMaterial(blender, model, checker, "Painted");

        Assert.Equal(painted.URI, again.URI);
        Assert.NotSame(painted, again);
        Assert.Equal("ok", (await blender.Ask(UpdateBucket(again, 8))).Type);
        Assert.Same(again, LinkedDocuments.Find(again)!.Document.DocumentModel);
        Assert.DoesNotContain(LinkedDocuments.Documents(), document => document.DocumentModel == painted);
        Assert.Equal(8u, DataOf<MaterialData>(again).DmaChainIndex);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(again.URI.ToString()) })).Type);
        Assert.False(again.IsUnsaved);
        Assert.True(File.Exists(Path.Combine(again.FullPath, $"{again.Name}.json")));
        Assert.Equal(8u, SavedBucket(again));
    }

    // A linked model deleted in TT Lab: the Blender is told and it isn't its model any more
    [AvaloniaFact]
    public async Task ADeletedModelIsToldToBlender()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);

        Assert.True(await AssetDeletion.DeleteAsync(model));

        var deleted = await blender.WaitFor("deleted");
        Assert.Equal(model.URI.ToString(), deleted.Header["uri"]!.GetValue<string>());
        Assert.DoesNotContain(model.URI, Assert.Single(_server!.Sessions).Assets.Models);
    }

    // Two Blenders draw the same material: the one closing its blend file unsaved takes back its own steps only, the other's stay to be saved
    [AvaloniaFact]
    public async Task ADropTakesBackOnlyItsOwnBlendersSteps()
    {
        var first = await Link();
        var (model, checker, _) = await LinkModel(first);
        using var second = await TestBlender.Connect(_server!.Port);
        Assert.Equal("welcome", (await second.Ask(Hello(_server.Token))).Type);
        Assert.Equal("model", (await second.Ask(new JsonObject { ["type"] = "link_model", ["path"] = model.FullDataPath })).Type);
        Assert.Equal("ok", (await second.Ask(UpdateBucket(checker, 8))).Type);
        Assert.Equal("ok", (await first.Ask(UpdateBucket(checker, 6))).Type);
        Assert.Equal(6u, DataOf<MaterialData>(checker).DmaChainIndex);

        Assert.Equal("ok", (await first.Ask(new JsonObject { ["type"] = "drop" })).Type);

        Assert.Equal(8u, DataOf<MaterialData>(checker).DmaChainIndex);
        var document = LinkedDocuments.Find(checker)!.Document;
        Assert.True(document.IsDirty);
        Assert.Equal("ok", (await second.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray(checker.URI.ToString()) })).Type);
        Assert.Equal(8u, SavedBucket(checker));
        Assert.False(document.IsDirty);
    }

    // The blend file closed unsaved: Blender's steps of a document of no tab are undone whatever they changed, a picture's layout of its size
    // and a name too, and the document goes
    [AvaloniaFact]
    public async Task ADropTakesBackAPicturesLayoutAndName()
    {
        var blender = await Link();
        var (_, _, texture) = await LinkModel(blender);
        var pixels = DataOf<TextureData>(texture).GetPixels();
        var (format, function, mips, alias) = (texture.PixelFormat, texture.TextureFunction, texture.GenerateMipmaps, texture.Alias);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "update_picture", ["uri"] = texture.URI.ToString() }, Png(256, 0xFFFF0000))).Type);
        // No palette layout of the size: every color, no mips
        Assert.Equal(ITwinTexture.TexturePixelFormat.PSMCT32, texture.PixelFormat);
        Assert.False(texture.GenerateMipmaps);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "rename", ["uri"] = texture.URI.ToString(), ["name"] = "Squares.png" })).Type);
        Assert.Equal("Squares", texture.Alias);

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "drop" })).Type);

        Assert.Equal(pixels, DataOf<TextureData>(texture).GetPixels());
        Assert.Equal((format, function, mips, alias), (texture.PixelFormat, texture.TextureFunction, texture.GenerateMipmaps, texture.Alias));
        Assert.Null(LinkedDocuments.Find(texture));
    }

    // Painted more times than a document of no tab keeps steps of data replaced whole: the drop reads the picture its file has
    [AvaloniaFact]
    public async Task ADropTakesBackMoreThanTheHistoryKeeps()
    {
        var blender = await Link();
        var (_, _, texture) = await LinkModel(blender);
        var pixels = DataOf<TextureData>(texture).GetPixels();
        for (var stroke = 0; stroke < 10; stroke++)
        {
            var painted = await blender.Ask(new JsonObject { ["type"] = "update_picture", ["uri"] = texture.URI.ToString() }, Png(64, 0xFF000000u | (UInt32)(stroke * 20)));
            Assert.Equal("ok", painted.Type);
        }

        // The step it was saved at is let go of
        Assert.Null(LinkedDocuments.Find(texture)!.Document.History.Saved);

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "drop" })).Type);

        Assert.Equal(pixels, DataOf<TextureData>(texture).GetPixels());
        Assert.Null(LinkedDocuments.Find(texture));
        Assert.Equal(Sha256(DataOf<TextureData>(texture).GetPngBytes()), BlenderLinkAssets.PictureHash(texture));
    }

    // A picture of another size is a step with its layout, undone and redone together
    [AvaloniaFact]
    public async Task APictureOfAnotherLayoutIsUndoneWithIt()
    {
        var blender = await Link();
        var (_, _, texture) = await LinkModel(blender);
        var format = texture.PixelFormat;
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "update_picture", ["uri"] = texture.URI.ToString() }, Png(256, 0xFFFF0000))).Type);
        var document = LinkedDocuments.Find(texture)!.Document;
        Assert.Equal(ITwinTexture.TexturePixelFormat.PSMCT32, texture.PixelFormat);

        document.Undo();
        Assert.Equal(format, texture.PixelFormat);
        Assert.Equal(64, DataOf<TextureData>(texture).Bitmap!.PixelSize.Width);
        document.Redo();
        Assert.Equal(ITwinTexture.TexturePixelFormat.PSMCT32, texture.PixelFormat);
        Assert.Equal(256, DataOf<TextureData>(texture).Bitmap!.PixelSize.Width);
    }

    // The PNG with a text chunk after its header: the same pixels, other bytes
    private static byte[] WithComment(byte[] png)
    {
        var text = "Comment\0Painted in Blender"u8.ToArray();
        var chunk = new byte[12 + text.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (UInt32)text.Length);
        "tEXt"u8.CopyTo(chunk.AsSpan(4));
        text.CopyTo(chunk, 8);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + text.Length), Crc32(chunk.AsSpan(4, 4 + text.Length)));
        // The signature and the header chunk (13 bytes of data) come first
        return [..png[..33], ..chunk, ..png[33..]];
    }

    private static UInt32 Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? crc >> 1 ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    // Blender's PNG of the picture the texture has is the one it knows the texture by, saved with what it saves: after TT Lab starts again
    // its picture is still Blender's, not the one TT Lab made of it
    [AvaloniaFact]
    public async Task APngOfTheSamePictureIsBlendersOnceSaved()
    {
        var blender = await Link();
        var (_, _, texture) = await LinkModel(blender);
        var png = WithComment(DataOf<TextureData>(texture).GetPngBytes());
        Assert.NotEqual(Sha256(png), BlenderLinkAssets.PictureHash(texture));

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "update_picture", ["uri"] = texture.URI.ToString() }, png)).Type);

        Assert.Equal(Sha256(png), BlenderLinkAssets.PictureHash(texture));
        var document = LinkedDocuments.Find(texture)!.Document;
        Assert.Same(document.History.Root, document.History.Current);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "save", ["uris"] = new JsonArray() })).Type);
        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(texture.FullPath, $"{texture.Name}.json")))!;
        Assert.Equal(Sha256(png), saved["Parameters"]![BlenderLinkAssets.BlenderPngParameter]!.GetValue<string>());
    }

    // What Blender made that only what it made draws goes when it drops: a material it made and changed (in a document of its own, which
    // links the picture) keeps no picture it made
    [AvaloniaFact]
    public async Task WhatOnlyWhatBlenderMadeDrawsGoesWhenItDrops()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var made = await blender.Ask(new JsonObject { ["type"] = "create_picture", ["owner"] = model.URI.ToString(), ["blender_id"] = "wood", ["name"] = "Wood" }, Png(16, 0xFF804020));
        var wood = _project.AssetManager.GetAsset<Texture>(new LabURI(made.Header["uri"]!.GetValue<string>()));
        var data = MaterialJson(checker);
        data["Shaders"]![0]!.AsObject().Remove("TextureId");
        data["Shaders"]![0]!["Image"] = 0;
        var painted = await MadeMaterial(blender, model, checker, "painted", data, new JsonArray(new JsonObject { ["name"] = "Wood", ["blender_id"] = "wood", ["uri"] = wood.URI.ToString() }));
        Assert.Equal(wood.URI, DataOf<MaterialData>(painted).Shaders[0].TextureId);
        Assert.Equal("ok", (await blender.Ask(UpdateBucket(painted, 6))).Type);
        Assert.NotNull(LinkedDocuments.Find(painted));

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "drop" })).Type);

        Assert.False(_project.AssetManager.DoesAssetExist(painted.URI));
        Assert.False(_project.AssetManager.DoesAssetExist(wood.URI));
        Assert.False(File.Exists(wood.FullDataPath));
        Assert.Null(LinkedDocuments.Find(painted));
    }

    // What TT Lab changes that leaves the model as the Blender has it (its name, saving or closing its tab unchanged) tells it nothing
    [AvaloniaFact]
    public async Task ARenamedOrSavedModelIsntAChangeForBlender()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var resources = new ResourcesEditorsViewModel();
        LinkedDocuments.Tabs = () => resources.Tabs;

        model.Alias = "Bandicoot";
        DocumentViewModel.TellChanged(model);
        resources.OpenEditor(model);
        var tab = resources.Tabs.Single();
        await WaitUntil(() => tab.IsLoaded);
        tab.SaveTab();
        await tab.CloseTab(discardChanges: true);
        // What's told after them: they were told before, if at all
        (await LinkedDocumentsFor(checker)).Document.PropertyGraph.Find("Root.AssetData.DmaChainIndex")!.SetValue(10u);
        await blender.WaitFor("material_changed");

        Assert.Empty(blender.Pushed("model_changed"));
    }

    // Take TT Lab's answered too late (the add-on read the model's file instead): what the Blender has is the file, TT Lab's unsaved changes
    // are a change of the model for it
    [AvaloniaFact]
    public async Task AModelReadOfItsFileIsntTTLabsUnsavedOne()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f))).Type);
        // Undone: TT Lab's unsaved change, the file has Blender's write
        LinkedDocuments.Find(model)!.Document.Undo();
        var file = "file:" + Sha256(File.ReadAllBytes(model.FullDataPath));
        var taken = await blender.Ask(new JsonObject { ["type"] = "get_model", ["uri"] = model.URI.ToString() });

        Assert.False(await LinksUnchanged(model, file));
        Assert.True(await LinksUnchanged(model, taken.Header["revision"]!.GetValue<string>()));
        var conflict = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = file }, WithBox(model, 5f));
        Assert.Equal("conflict", conflict.Type);
        Assert.Equal(taken.Header["revision"]!.GetValue<string>(), conflict.Header["revision"]!.GetValue<string>());
    }

    // Blender's write waits for the step the user is making: what that step changed of the model is TT Lab's change, a conflict
    [AvaloniaFact]
    public async Task AWriteWaitingForTheUsersStepSeesWhatItChanged()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var revision = Sha256(File.ReadAllBytes(model.FullDataPath));
        var document = (await LinkedDocumentsFor(model)).Document;
        var group = document.History.BeginGroup("Dragged");

        var answer = blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = revision }, WithBox(model, 3f));
        await Task.Delay(300);

        Assert.False(answer.IsCompleted);
        // The step changes the model while the write waits
        document.ReplaceData(document.PropertyGraph.Find("Root.AssetData")!, AssetDataBytes.Read(model, typeof(OGIData), WithBox(model, 4f)), "Dragged");
        group.Dispose();
        Assert.Equal("conflict", (await answer).Type);
        Assert.Equal(4f, DataOf<OGIData>(model).BoundingBox[1].Y);
    }

    private GameObject ObjectDrawing(OGI model, string name = "Bandicoot")
    {
        var gameObject = _project.Add(new GameObject(), name, 0x10);
        var data = new GameObjectData(gameObject) { Name = name };
        data.ModelSlots.Add(new ModelSlot { Ogi = model.URI });
        gameObject.SetData(data);
        // Saved: its changes discarded, it's read back
        gameObject.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData | SerializationFlags.FixReferences);
        return gameObject;
    }

    // A model Blender writes, undone and redone in its own document: another document showing it under a link (a game object's model slot)
    // shows the data the model has, its nodes made again for it
    [AvaloniaFact]
    public async Task AnotherDocumentShowsTheModelBlenderWroteAndItsUndo()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var gameObject = ObjectDrawing(model);
        var resources = new ResourcesEditorsViewModel();
        LinkedDocuments.Tabs = () => resources.Tabs;
        resources.OpenEditor(gameObject);
        var tab = resources.Tabs.Single();
        await WaitUntil(() => tab.IsLoaded);
        const string Animations = "Root.AssetData.ModelSlots[0].Ogi[data].AssetData.Animations";
        var before = DataOf<OGIData>(model);
        Assert.Same(before, tab.Document!.PropertyGraph.Find(Animations)!.Target);

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f))).Type);

        var written = DataOf<OGIData>(model);
        Assert.NotSame(before, written);
        Assert.Same(written, tab.Document.PropertyGraph.Find(Animations)!.Target);
        var own = LinkedDocuments.Find(model)!.Document;
        own.Undo();
        Assert.NotSame(written, DataOf<OGIData>(model));
        Assert.Same(DataOf<OGIData>(model), tab.Document.PropertyGraph.Find(Animations)!.Target);
        own.Redo();
        Assert.Same(DataOf<OGIData>(model), tab.Document.PropertyGraph.Find(Animations)!.Target);
        Assert.Equal(3f, DataOf<OGIData>(model).BoundingBox[1].Y);
        await tab.CloseTab(discardChanges: true);
    }

    // A chunk's scene shows a model it shares (an instance's object's) through its view of it: the view has the data Blender wrote
    [AvaloniaFact]
    public async Task AChunksViewOfAModelHasWhatBlenderWrote()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var (chunk, _, _) = ChunkWithAnotherFog();
        var crash = _project.AssetManager.GetAllAssetsOf<GameObject>().Single(gameObject => gameObject.Alias == "Crash");
        DataOf<GameObjectData>(crash).ModelSlots.Add(new ModelSlot { Ogi = model.URI });
        var scenes = new ScenesEditorsViewModel();
        LinkedDocuments.Tabs = () => scenes.Tabs;
        var tab = await OpenScene(scenes, chunk);
        var instance = chunk.ChunkResources.Select(_project.AssetManager.GetAsset).OfType<TT_Lab.Assets.Instance.ObjectInstance>().First();
        var animations = tab.Document!.FindChunkResource(instance.URI)!.Path + ".AssetData.ObjectId[data].AssetData.ModelSlots[0].Ogi[data].AssetData.Animations";
        var view = tab.Document.PropertyGraph.Overrides!.ViewOf(model);
        Assert.NotNull(view);
        Assert.Same(DataOf<OGIData>(model), tab.Document.PropertyGraph.Find(animations)!.Target);

        Assert.Equal("ok", (await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f))).Type);

        Assert.Same(DataOf<OGIData>(model), view!.GetData());
        Assert.Same(DataOf<OGIData>(model), tab.Document.PropertyGraph.Find(animations)!.Target);
        LinkedDocuments.Find(model)!.Document.Undo();
        Assert.Same(DataOf<OGIData>(model), view.GetData());
        Assert.Same(DataOf<OGIData>(model), tab.Document.PropertyGraph.Find(animations)!.Target);
        await tab.CloseTab(discardChanges: true);
    }

    // TT Lab changed the model through another document (a material picked for its slot in a game object's tab): Blender's write over it is
    // a conflict
    [AvaloniaFact]
    public async Task ChangesOfTheModelThroughAnotherDocumentAreAConflict()
    {
        var blender = await Link();
        var (model, checker, _) = await LinkModel(blender);
        var varnish = _project.Add(new Material(), "Varnish", 0x5000);
        varnish.SetData(new MaterialData(varnish));
        var gameObject = ObjectDrawing(model);
        var resources = new ResourcesEditorsViewModel();
        LinkedDocuments.Tabs = () => resources.Tabs;
        resources.OpenEditor(gameObject);
        var tab = resources.Tabs.Single();
        await WaitUntil(() => tab.IsLoaded);
        var revision = Sha256(File.ReadAllBytes(model.FullDataPath));

        tab.Document!.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Ogi[data].AssetData.MaterialSlots[0].Material")!.SetValue(varnish.URI);
        Assert.Contains(varnish, DataOf<OGIData>(model).MaterialSlots.Select(slot => _project.AssetManager.GetAsset(slot.Material)));

        var answer = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = revision }, WithBox(model, 3f));
        Assert.Equal("conflict", answer.Type);
        Assert.DoesNotContain(checker, DataOf<OGIData>(model).MaterialSlots.Select(slot => _project.AssetManager.GetAsset(slot.Material)));
        await tab.CloseTab(discardChanges: true);
    }

    // Moved in TT Lab: the Blender is told the model's new place, keeps it by its new URI and writes it there, and the blend file remembered
    // for it is the one of its new place
    [AvaloniaFact]
    public async Task AMovedModelIsTheBlendersAtItsNewPlace()
    {
        var blender = await Link();
        var (model, _, _) = await LinkModel(blender);
        var blend = Path.Combine(_state, "Crash.blend");
        File.WriteAllText(blend, string.Empty);
        var uuid = _project.Project.UUID;
        BlendFiles.Remember(uuid, model.URI, blend);
        var from = model.URI;
        var graphics = _project.GetFolder(_package, "Graphics");
        var characters = (Folder)AssetFactory.CreateAsset(typeof(Folder), graphics, "Characters", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<Folder>(),
            asset => AssetDataFactory.CreateFolderData(graphics, asset))!;
        var ask = AssetRelocation.Ask;
        AssetRelocation.Ask = (_, _, answers) => Task.FromResult<int?>(answers.Count == 0 ? null : 0);
        try
        {
            Assert.True(await AssetRelocation.MoveAsync(model, characters));
        }
        finally
        {
            AssetRelocation.Ask = ask;
        }

        Assert.NotEqual(from, model.URI);
        _server!.TellMoved(new Dictionary<LabURI, LabURI> { [from] = model.URI });

        var moved = await blender.WaitFor("moved");
        Assert.Equal((from.ToString(), model.URI.ToString(), model.FullDataPath),
            (moved.Header["uri"]!.GetValue<string>(), moved.Header["new_uri"]!.GetValue<string>(), moved.Header["path"]!.GetValue<string>()));
        Assert.Contains(model.URI, Assert.Single(_server.Sessions).Assets.Models);
        Assert.Equal(blend, BlendFiles.Of(uuid, model.URI));
        Assert.Null(BlendFiles.Of(uuid, from));
        var written = await blender.Ask(new JsonObject { ["type"] = "write_model", ["uri"] = model.URI.ToString(), ["revision"] = Sha256(File.ReadAllBytes(model.FullDataPath)) },
            WithBox(model, 3f));
        Assert.True(written.Type == "ok", written.Header.ToJsonString());
        Assert.Equal(3f, TlmFile.Load(model.FullDataPath).Root!.GetData().GetVector4("BoundingBoxMax", new()).Y);
    }

    // A model Blender exported without TT Lab (exported_by, its revision what it wrote) that TT Lab took as it read it, writing it again: what
    // the Blender has is TT Lab's model while the file is what TT Lab wrote of it
    [AvaloniaFact]
    public async Task AnExportTTLabTookIsWhatTheBlenderHas()
    {
        Serve();
        var model = CreateModel("Crash");
        var file = TlmFile.Load(model.FullDataPath);
        file.Json[TlmMaterials.ExportedByKey] = "twin_tech_tools test";
        // A material made in Blender, which TT Lab makes the project's and names in the file it writes again
        var checker = _project.AssetManager.GetAsset<Material>(new LabURI(file.Materials[0]!["uri"]!.GetValue<string>()));
        file.Materials[0] = new JsonObject { ["name"] = "Hat", ["blender_id"] = "hat", ["data"] = MaterialJson(checker), ["images"] = new JsonArray() };
        var exported = Path.Combine(_state, "exported.tlm");
        file.Save(exported);
        var bytes = File.ReadAllBytes(exported);
        File.WriteAllBytes(model.FullDataPath, bytes);
        model.UnloadData();

        DataOf<OGIData>(model);

        Assert.NotEqual(Sha256(bytes), Sha256(File.ReadAllBytes(model.FullDataPath)));
        Assert.True(await LinksUnchanged(model, "exported:" + Sha256(bytes)));
        // Written since: what the Blender exported isn't it
        File.WriteAllBytes(model.FullDataPath, WithBox(model, 3f));
        Assert.False(await LinksUnchanged(model, "exported:" + Sha256(bytes)));
    }

    // Before its hello is welcomed a frame is a hello: anyone can connect to the port, a frame claiming more is refused before its buffers are
    // made and the connection closed
    [AvaloniaFact]
    public async Task AFrameBiggerThanAHelloIsRefusedBeforeTheHello()
    {
        var server = Serve();
        async Task<bool> ClosesAfter(UInt32 header, UInt32 payload)
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", server.Port);
            var stream = client.GetStream();
            var lengths = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(lengths.AsSpan(0, 4), header);
            BinaryPrimitives.WriteUInt32LittleEndian(lengths.AsSpan(4, 4), payload);
            await stream.WriteAsync(lengths);
            // TT Lab says it closes (a frame of a few bytes) and closes
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var read = 0;
            try
            {
                for (var count = -1; count != 0 && read < 1024; read += count)
                {
                    count = await stream.ReadAsync(new byte[256], timeout.Token);
                }
            }
            catch (IOException)
            {
            }

            return read < 1024;
        }

        Assert.True(await ClosesAfter(64, LinkProtocol.MostPayload));
        Assert.True(await ClosesAfter(LinkProtocol.MostHelloHeader + 1, 0));
        await WaitUntil(() => server.Sessions.Count == 0);
        Assert.Empty(server.Sessions);
        // A Blender still links
        using var blender = await TestBlender.Connect(server.Port);
        Assert.Equal("welcome", (await blender.Ask(Hello(server.Token))).Type);
    }
}
