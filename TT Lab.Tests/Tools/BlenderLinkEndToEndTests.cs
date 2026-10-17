using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Blender;
using TT_Lab.ViewModels.Composite;
using Xunit.Abstractions;

namespace TT_Lab.Tests.Tools;

// Blender itself linked to TT Lab, editing a model of the project the way a user does (BlenderTools/tests/blender_tt_lab_link.py): its
// material changed, a material and picture made and the material renamed, and the blend file saved. Only with a Blender: TTL_BLENDER names its executable, the
// test does nothing without it (the CI has none)
[Collection(ProjectCollection.Name)]
public sealed class BlenderLinkEndToEndTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;
    private readonly string _state = Path.Combine(Path.GetTempPath(), "TT Lab Tests", "Blender " + Guid.NewGuid().ToString("N"));
    private readonly Func<IEnumerable<TabbedEditorViewModel>> _tabs = LinkedDocuments.Tabs;
    private readonly string _blendFiles = BlendFiles.Folder;
    private readonly ITestOutputHelper _output;
    private BlenderLinkServer? _server;

    public BlenderLinkEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "Graphics"));
        Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
        LinkedDocuments.Tabs = () => [];
        // Blender saving its blend file makes TT Lab remember it, not into this computer's
        BlendFiles.Folder = Path.Combine(_state, "blend files");
    }

    public void Dispose()
    {
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

    private static string AddOnTest(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TT Lab.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "TT Lab", "BlenderTools", "tests", name);
    }

    private static T DataOf<T>(IAsset asset) where T : AbstractAssetData => asset.GetData<T>();

    private static string? Blender()
    {
        var blender = Environment.GetEnvironmentVariable("TTL_BLENDER");
        return string.IsNullOrEmpty(blender) || !File.Exists(blender) ? null : blender;
    }

    private void Serve()
    {
        // The add-on finds the project by its file. Its empty folders stay, models get made in them
        _project.Project.Serialize(null, true, new HashSet<string>());
        Directory.CreateDirectory(_state);
        // Where the add-on looks with the state folders RunBlender gives it
        var folder = OperatingSystem.IsWindows() ? Path.Combine(_state, "TT Lab", "Blender") : Path.Combine(_state, "tt-lab", "blender");
        _server = new BlenderLinkServer(Locator.Current.GetService<ProjectManager>()!) { StateFolder = folder };
        _server.Start();
    }

    // Blender runs the script of the add-on's tests with the arguments, what it saw comes back
    private async Task<JsonNode> RunBlender(string blender, string script, params string[] arguments)
    {
        var result = Path.Combine(_state, "result.json");
        var info = new ProcessStartInfo(blender) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "--background", "--factory-startup", "--python-exit-code", "1", "--python", AddOnTest(script), "--" }.Concat(arguments).Append(result))
        {
            info.ArgumentList.Add(argument);
        }

        // It sees none of this computer's TT Labs (their discovery files are in the home's state folder too), settings or add-ons
        var home = Path.Combine(_state, "home");
        Directory.CreateDirectory(home);
        info.Environment["XDG_STATE_HOME"] = _state;
        info.Environment["LOCALAPPDATA"] = _state;
        info.Environment["HOME"] = home;
        info.Environment["BLENDER_USER_RESOURCES"] = Path.Combine(_state, "blender");
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
        {
            await process.WaitForExitAsync(timeout.Token);
        }

        _output.WriteLine(await output);
        _output.WriteLine(await errors);
        var seen = JsonNode.Parse(await File.ReadAllTextAsync(result))!;
        _output.WriteLine(seen.ToJsonString());
        Assert.Null(seen["error"]);
        return seen;
    }

    [AvaloniaFact]
    public async Task BlenderEditsAModelThroughTheLink()
    {
        if (Blender() is not { } blender)
        {
            _output.WriteLine("TTL_BLENDER names no Blender, nothing to link");
            return;
        }

        var model = (OGI)AssetFactory.CreateAsset(typeof(OGI), _project.GetFolder(_package, "Graphics"), "Crash", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<OGI>(),
            AssetDataFactory.CreateOgiData)!;
        Serve();

        var seen = await RunBlender(blender, "blender_tt_lab_link.py", model.FullDataPath, Path.Combine(_state, "scene.blend"));
        Assert.Equal((true, true, true, true, true), (seen["linked"]!.GetValue<bool>(), seen["sent"]!.GetValue<bool>(), seen["made"]!.GetValue<bool>(),
            seen["renamed"]!.GetValue<bool>(), seen["written"]!.GetValue<bool>()));
        Assert.Equal(model.URI.ToString(), seen["uri"]!.GetValue<string>());

        // Saving the blend file saved what Blender changed and made
        var changed = _project.AssetManager.GetAsset<Material>(new LabURI(seen["changed"]!.GetValue<string>()));
        Assert.Equal(6u, JsonNode.Parse(await File.ReadAllTextAsync(changed.FullDataPath))!["DmaChainIndex"]!.GetValue<UInt32>());
        var painted = _project.AssetManager.GetAsset<Material>(new LabURI(seen["material"]!.GetValue<string>()));
        var picture = _project.AssetManager.GetAsset<Texture>(new LabURI(seen["picture"]!.GetValue<string>()));
        Assert.Equal("Varnished", painted.Alias);
        Assert.False(painted.IsUnsaved || picture.IsUnsaved);
        Assert.True(File.Exists(painted.FullDataPath) && File.Exists(picture.FullDataPath));
        Assert.Equal((model.URI.ToString(), model.URI.ToString()), (painted.Parameters[BlenderAssets.OwnerParameter]?.ToString(), picture.Parameters[BlenderAssets.OwnerParameter]?.ToString()));
        Assert.Equal(picture.URI, DataOf<MaterialData>(painted).Shaders[0].TextureId);
        Assert.Equal((64, 64), (DataOf<TextureData>(picture).Bitmap!.PixelSize.Width, DataOf<TextureData>(picture).Bitmap!.PixelSize.Height));

        // The model Blender wrote draws it, a step of its document keeps the one before
        var body = _project.AssetManager.GetAssetData<RigidModelData>(Assert.Single(DataOf<OGIData>(model).RigidModelIds));
        Assert.Equal([painted.URI], body.Materials);
        Assert.Contains(painted.URI.ToString(), AssetLinks.UrisInModelFile(model.FullDataPath));
        var document = LinkedDocuments.Find(model)!.Document;
        Assert.Equal($"From Blender: {model.Alias} written", document.History.Current.Description);
        Assert.False(document.IsDirty);
        Assert.Equal(seen["revision"]!.GetValue<string>(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(model.FullDataPath))).ToLowerInvariant());
    }

    // Use an Existing Blend File: Blender opens a blend file made of another model and puts its model in the model's place once it's linked,
    // what it draws that the project doesn't have made the model's own, and saves the blend file, the model's from then on
    [AvaloniaFact]
    public async Task BlenderPutsTheModelOfAnExistingBlendFileInAModelsPlace()
    {
        if (Blender() is not { } blender)
        {
            _output.WriteLine("TTL_BLENDER names no Blender, nothing to put in place");
            return;
        }

        var model = (OGI)AssetFactory.CreateAsset(typeof(OGI), _project.GetFolder(_package, "Graphics"), "Crash", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<OGI>(),
            AssetDataFactory.CreateOgiData)!;
        var target = (OGI)AssetFactory.CreateAsset(typeof(OGI), _project.GetFolder(_package, "Graphics"), "Target", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<OGI>(),
            AssetDataFactory.CreateOgiData)!;
        var modelFile = await File.ReadAllBytesAsync(model.FullDataPath);
        Serve();
        var blend = Path.Combine(_state, "existing.blend");
        // Edit in Blender made it for the model
        BlendFiles.Remember(_project.Project.UUID, model.URI, blend);

        var seen = await RunBlender(blender, "blender_tt_lab_use_blend.py", model.FullDataPath, target.FullDataPath, blend);

        Assert.Equal((true, true), (seen["replacing"]!.GetValue<bool>(), seen["replaced"]!.GetValue<bool>()));
        Assert.Equal((target.URI.ToString(), target.FullDataPath), (seen["uri"]!.GetValue<string>(), seen["path"]!.GetValue<string>()));
        Assert.Equal([model.FullDataPath], seen["released"]!.AsArray().Select(path => path!.GetValue<string>()));
        // The blend file was saved with the model as the target's, of the revision TT Lab wrote
        Assert.Equal(target.FullDataPath, seen["saved_path"]!.GetValue<string>());
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(target.FullDataPath))).ToLowerInvariant(),
            seen["saved_revision"]!.GetValue<string>());

        // The bigger model is the target's, a step of its document keeps the one before, the model it was made of stays as it was
        Assert.True(DataOf<OGIData>(target).BoundingBox[1].Y > 1.5f);
        Assert.Equal(modelFile, await File.ReadAllBytesAsync(model.FullDataPath));
        var document = LinkedDocuments.Find(target)!.Document;
        Assert.Equal("From Blender: Target replaced by Custom of existing.blend", document.History.Current.Description);
        Assert.False(document.IsDirty);

        // What Blender made and the material of the other project are the target's own, saved
        Assert.NotEqual("res://Elsewhere/Material/Foreign", seen["foreign"]!.GetValue<string>());
        var models = AssetLinks.UrisInModelFile(target.FullDataPath).ToList();
        foreach (var made in new[] { seen["painted"]!.GetValue<string>(), seen["foreign"]!.GetValue<string>() })
        {
            var material = _project.AssetManager.GetAsset<Material>(new LabURI(made));
            Assert.False(material.IsUnsaved);
            Assert.True(File.Exists(material.FullDataPath));
            Assert.Equal(target.URI.ToString(), material.Parameters[BlenderAssets.OwnerParameter]?.ToString());
            Assert.Contains(made, models);
        }

        var picture = _project.AssetManager.GetAsset<Texture>(new LabURI(seen["picture"]!.GetValue<string>()));
        Assert.False(picture.IsUnsaved);
        Assert.Equal(picture.URI, DataOf<MaterialData>(_project.AssetManager.GetAsset<Material>(new LabURI(seen["painted"]!.GetValue<string>()))).Shaders[0].TextureId);

        // Edit in Blender opens it for the target, the model it was made of gets a blend file of its own again
        Assert.Equal(blend, BlendFiles.Of(_project.Project.UUID, target.URI));
        Assert.Null(BlendFiles.Of(_project.Project.UUID, model.URI));
    }

    [AvaloniaFact]
    public async Task BlenderAddsAModelItMadeToTheProject()
    {
        if (Blender() is not { } blender)
        {
            _output.WriteLine("TTL_BLENDER names no Blender, nothing to add");
            return;
        }

        Serve();
        var graphics = _project.GetFolder(_package, "Graphics");
        var label = BlenderLinkAssets.NewModelFolders().Single(target => target.Folder == graphics).Label;

        var seen = await RunBlender(blender, "blender_tt_lab_new_model.py", label, Path.Combine(_state, "crate.blend"));

        Assert.Equal((true, true, true), (seen["linked"]!.GetValue<bool>(), seen["material_linked"]!.GetValue<bool>(), seen["written"]!.GetValue<bool>()));
        var model = _project.AssetManager.GetAsset<Mesh>(new LabURI(seen["uri"]!.GetValue<string>()));
        Assert.Equal("Crate", model.Alias);
        Assert.Equal(model.FullDataPath, seen["path"]!.GetValue<string>());
        // The material the file brought is the one the link knows, made once
        var material = _project.AssetManager.GetAsset<Material>(DataOf<MeshData>(model).Materials[0]);
        Assert.Equal(material.URI.ToString(), seen["material"]!.GetValue<string>());
        Assert.Single(_project.AssetManager.GetAllAssetsOf<Material>(), asset => asset.Alias.StartsWith("Crate"));
        Assert.False(material.IsUnsaved);
    }
}
