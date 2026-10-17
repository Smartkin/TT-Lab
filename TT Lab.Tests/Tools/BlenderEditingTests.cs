using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Blender;
using TT_Lab.ViewModels.Editors;
using TT_Lab.Views.Editors;

namespace TT_Lab.Tests.Tools;

// Edit in Blender: a model without a blend file is imported into a new one, put where the user says (the project's source folder first)
// and remembered on this computer, or gets an existing one whose model replaces it, a model with one opens it
[Collection(ProjectCollection.Name)]
public sealed class BlenderEditingTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly string _state = Path.Combine(Path.GetTempPath(), "TT Lab Tests", "Editing " + Guid.NewGuid().ToString("N"));
    private readonly string _blenderPreference = Preferences.GetPreference<string>(Preferences.BlenderPath);
    private readonly string _blendFiles = BlendFiles.Folder;
    private readonly string _installed = BlenderAddonInstaller.Folder;
    private readonly Func<ProcessStartInfo, bool> _start = BlenderEditing.Start;
    private readonly Func<string, Task<bool>> _askToInstall = BlenderEditing.AskToInstall;
    private readonly Func<string, Task<string?>> _askWhereToSave = BlenderEditing.AskWhereToSave;
    private readonly Func<IAsset, Task<BlenderEditing.BlendChoice>> _askNewOrExisting = BlenderEditing.AskNewOrExisting;
    private readonly Func<IAsset, string, Task<string?>> _askForBlendFile = BlenderEditing.AskForBlendFile;
    private readonly List<ProcessStartInfo> _started = [];
    private readonly List<string> _asked = [];

    public BlenderEditingTests()
    {
        var package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(package.Name, "Graphics"));
        Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
        Directory.CreateDirectory(_state);
        BlendFiles.Folder = _state;
        BlenderAddonInstaller.Folder = _state;
        var blender = Path.Combine(_state, "blender");
        File.WriteAllText(blender, string.Empty);
        Preferences.SetPreference(Preferences.BlenderPath, blender);
        // The add-on next to TT Lab counts as installed, tests run no Blender
        if (BlenderAddonInstaller.BundledZip() is { } zip)
        {
            File.WriteAllText(Path.Combine(_state, "installed-addons.json"), $$"""{ {{System.Text.Json.JsonSerializer.Serialize(blender)}}: "{{BlenderAddonInstaller.VersionOf(zip)}}" }""");
        }

        BlenderEditing.Start = info =>
        {
            _started.Add(info);
            return true;
        };
        BlenderEditing.AskToInstall = _ => Task.FromResult(false);
    }

    public void Dispose()
    {
        BlenderEditing.Start = _start;
        BlenderEditing.AskToInstall = _askToInstall;
        BlenderEditing.AskWhereToSave = _askWhereToSave;
        BlenderEditing.AskNewOrExisting = _askNewOrExisting;
        BlenderEditing.AskForBlendFile = _askForBlendFile;
        Preferences.SetPreference(Preferences.BlenderPath, _blenderPreference);
        BlendFiles.Folder = _blendFiles;
        BlenderAddonInstaller.Folder = _installed;
        _project.Dispose();
        Directory.Delete(_state, true);
    }

    private OGI CreateModel(string name)
    {
        return (OGI)AssetFactory.CreateAsset(typeof(OGI), _project.GetFolder(_project.Project.GlobalPackagePS2, "Graphics"), name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<OGI>(), AssetDataFactory.CreateOgiData)!;
    }

    [AvaloniaFact]
    public async Task AModelWithoutABlendFileIsImportedIntoANewOne()
    {
        var model = CreateModel("Crash");
        var blend = Path.Combine(_state, "Crash.blend");
        BlenderEditing.AskNewOrExisting = _ => Task.FromResult(BlenderEditing.BlendChoice.New);
        BlenderEditing.AskWhereToSave = suggested =>
        {
            _asked.Add(suggested);
            return Task.FromResult<string?>(blend);
        };

        Assert.True(BlenderEditing.CanEdit(model));
        await BlenderEditing.EditAsync(model);

        Assert.Equal([Path.Combine(_project.Project.ProjectPath, "source", "Crash.blend")], _asked);
        var started = Assert.Single(_started);
        // The paths are arguments after "--", Blender runs in the blend file's folder
        Assert.Equal(["--python-expr", BlenderEditing.ImportExpression(), "--", model.FullDataPath, blend], started.ArgumentList);
        Assert.Contains("bpy.ops.ttt.edit_from_tt_lab(model=_model, blend=_blend)", started.ArgumentList[1]);
        Assert.Equal(_state, started.WorkingDirectory);

        // Blender saved it there: the next time it's opened
        File.WriteAllText(blend, string.Empty);
        await BlenderEditing.EditAsync(model);
        Assert.Equal([blend], _started[1].ArgumentList);
        Assert.Single(_asked);
    }

    // Its model replaces the model once Blender opened the blend file and linked it, which remembers it once it's saved
    [AvaloniaFact]
    public async Task AModelWithoutABlendFileCanUseAnExistingOne()
    {
        var model = CreateModel("Crash");
        var existing = Path.Combine(_state, "reimu_spin.blend");
        File.WriteAllText(existing, string.Empty);
        var folders = new List<string>();
        BlenderEditing.AskNewOrExisting = _ => Task.FromResult(BlenderEditing.BlendChoice.Existing);
        BlenderEditing.AskForBlendFile = (_, folder) =>
        {
            folders.Add(folder);
            return Task.FromResult<string?>(existing);
        };

        await BlenderEditing.EditAsync(model);

        Assert.Equal([Path.Combine(_project.Project.ProjectPath, "source")], folders);
        var started = Assert.Single(_started);
        Assert.Equal([existing, "--python-expr", BlenderEditing.UseExpression(), "--", model.FullDataPath, "Ogi", "Crash"], started.ArgumentList);
        var expression = started.ArgumentList[2];
        // Kept at once, so the file's models of the kind aren't linked meanwhile, then picked once Blender's window is up
        Assert.Contains("bpy.ops.ttt.tt_lab_request(model=_model, asset_type=_type, name=_name)", expression);
        Assert.Contains("bpy.ops.ttt.use_for_tt_lab_model('INVOKE_DEFAULT', True, model=_model, asset_type=_type, name=_name)", expression);
        Assert.True(expression.IndexOf("tt_lab_request", StringComparison.Ordinal) < expression.IndexOf("bpy.app.timers.register", StringComparison.Ordinal));
        Assert.Equal(_state, started.WorkingDirectory);
        Assert.Null(BlendFiles.Of(_project.Project.UUID, model.URI));
    }

    // A model with a blend file is given another one from its menus, starting where its own is
    [AvaloniaFact]
    public async Task AModelWithABlendFileCanUseAnotherOne()
    {
        var model = CreateModel("Crash");
        var own = Path.Combine(_state, "own", "Crash.blend");
        var existing = Path.Combine(_state, "Custom.blend");
        Directory.CreateDirectory(Path.GetDirectoryName(own)!);
        File.WriteAllText(own, string.Empty);
        File.WriteAllText(existing, string.Empty);
        BlendFiles.Remember(_project.Project.UUID, model.URI, own);
        string? folder = null;
        BlenderEditing.AskNewOrExisting = _ => throw new InvalidOperationException("A model with a blend file isn't asked");
        BlenderEditing.AskForBlendFile = (_, from) =>
        {
            folder = from;
            return Task.FromResult<string?>(null);
        };

        await BlenderEditing.UseBlendFileAsync(model);
        Assert.Equal(Path.GetDirectoryName(own), folder);
        Assert.Empty(_started);

        BlenderEditing.AskForBlendFile = (_, _) => Task.FromResult<string?>(existing);
        await BlenderEditing.UseBlendFileAsync(model);
        Assert.Equal(existing, Assert.Single(_started).ArgumentList[0]);
        // Edit in Blender still opens its own until Blender saved the other for it
        await BlenderEditing.EditAsync(model);
        Assert.Equal([own], _started[1].ArgumentList);

        // A blend file that isn't there starts nothing
        BlenderEditing.AskForBlendFile = (_, _) => Task.FromResult<string?>(Path.Combine(_state, "gone.blend"));
        await BlenderEditing.UseBlendFileAsync(model);
        Assert.Equal(2, _started.Count);
    }

    // A blend file a linked Blender has open: its model is picked there, a second Blender would save the file over the first's
    [AvaloniaFact]
    public async Task ABlendFileOpenInALinkedBlenderIsUsedThere()
    {
        var model = CreateModel("Crash");
        var existing = Path.Combine(_state, "reimu_spin.blend");
        var other = Path.Combine(_state, "reimu_dead.blend");
        File.WriteAllText(existing, string.Empty);
        File.WriteAllText(other, string.Empty);
        using var server = new BlenderLinkServer(Locator.Current.GetService<ProjectManager>()!) { StateFolder = _state };
        server.Start();
        Locator.CurrentMutable.RegisterConstant(server);
        try
        {
            using var blender = await TestBlender.Connect(server.Port);
            Assert.Equal("welcome", (await blender.Ask(TestBlender.Hello(server.Token, path: existing))).Type);
            BlenderEditing.AskForBlendFile = (_, _) => Task.FromResult<string?>(existing);

            await BlenderEditing.UseBlendFileAsync(model);

            Assert.Empty(_started);
            var pushed = await blender.WaitFor("use_for");
            Assert.Equal((model.FullDataPath, "Ogi", "Crash"),
                (pushed.Header["model"]!.GetValue<string>(), pushed.Header["asset_type"]!.GetValue<string>(), pushed.Header["name"]!.GetValue<string>()));

            // Another blend file gets a Blender of its own
            BlenderEditing.AskForBlendFile = (_, _) => Task.FromResult<string?>(other);
            await BlenderEditing.UseBlendFileAsync(model);
            Assert.Equal(other, Assert.Single(_started).ArgumentList[0]);
        }
        finally
        {
            Locator.CurrentMutable.UnregisterAll<BlenderLinkServer>();
        }
    }

    // A model whose file can't be read is of its data's kind
    [AvaloniaFact]
    public void AModelsKindIsItsFilesElseItsDatas()
    {
        var model = CreateModel("Crash");
        Assert.Equal("Ogi", BlenderEditing.ModelTypeOf(model));
        File.WriteAllText(model.FullDataPath, "not a model");
        Assert.Equal(OGIData.TlmAssetType, BlenderEditing.ModelTypeOf(model));
    }

    // The project tree's menu has it next to Edit in Blender
    [AvaloniaFact]
    public async Task TheProjectTreeUsesAnExistingBlendFile()
    {
        var model = CreateModel("Crash");
        var asked = new List<IAsset>();
        BlenderEditing.AskForBlendFile = (asset, _) =>
        {
            asked.Add(asset);
            return Task.FromResult<string?>(null);
        };
        var row = model.GetResourceTreeElement();
        await row.CreateContextMenuAction();
        var headers = row.MenuOptions.Select(item => item.Header as string).ToList();
        Assert.Equal(headers.IndexOf("Edit in Blender") + 1, headers.IndexOf(BlenderEditing.UseExistingHeader));

        row.MenuOptions.Single(item => item.Header as string == BlenderEditing.UseExistingHeader).Command!.Execute(null);

        await WaitUntil(() => asked.Count > 0);
        Assert.Same(model, Assert.Single(asked));
    }

    private static async Task WaitUntil(Func<bool> condition, int milliseconds = 5000)
    {
        for (var waited = 0; waited < milliseconds && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }
    }

    [AvaloniaFact]
    public async Task NothingStartsWithoutTheAddOnOrABlendFileToGoTo()
    {
        var model = CreateModel("Crash");
        BlenderEditing.AskNewOrExisting = _ => Task.FromResult(BlenderEditing.BlendChoice.None);
        await BlenderEditing.EditAsync(model);
        Assert.Empty(_started);

        BlenderEditing.AskNewOrExisting = _ => Task.FromResult(BlenderEditing.BlendChoice.New);
        BlenderEditing.AskWhereToSave = _ => Task.FromResult<string?>(null);
        await BlenderEditing.EditAsync(model);
        Assert.Empty(_started);

        File.Delete(Path.Combine(_state, "installed-addons.json"));
        BlenderEditing.AskWhereToSave = _ => Task.FromResult<string?>(Path.Combine(_state, "Crash.blend"));
        await BlenderEditing.EditAsync(model);
        Assert.Equal(BlenderAddonInstaller.BundledZip() == null, _started.Count == 1);
    }

    // A model's editor has Edit in Blender in its header, next to copying and pasting its values
    [AvaloniaFact]
    public async Task AModelsHeaderEditsItInBlender()
    {
        var model = CreateModel("Crash");
        var asked = new List<IAsset>();
        BlenderEditing.AskForBlendFile = (asset, _) =>
        {
            asked.Add(asset);
            return Task.FromResult<string?>(null);
        };
        ((IAsset)model).GetData<TT_Lab.AssetData.AbstractAssetData>();
        var document = new DocumentViewModel(model);
        document.Initialize();
        var window = new Window { Content = new DocumentView { ViewModel = document }, Width = 900, Height = 700 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var button = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => Equals(ToolTip.GetTip(button), DocumentModelView.BlenderHint));
        Assert.True(button.IsEffectivelyVisible);
        // Its dropdown and right click have Use an Existing Blend File as well
        var dropdown = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => Equals(ToolTip.GetTip(button), DocumentModelView.BlenderMenuHint));
        Assert.True(dropdown.IsEffectivelyVisible);
        foreach (var menu in new[] { dropdown.Flyout, button.ContextFlyout })
        {
            Assert.Equal(["Edit in Blender", BlenderEditing.UseExistingHeader], Assert.IsType<MenuFlyout>(menu).Items.OfType<MenuItem>().Select(item => item.Header as string));
        }

        var useExisting = ((MenuFlyout)dropdown.Flyout!).Items.OfType<MenuItem>().Single(item => item.Header as string == BlenderEditing.UseExistingHeader);
        useExisting.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        await WaitUntil(() => asked.Count > 0);
        Assert.Same(model, Assert.Single(asked));

        window.Close();
        document.Close();
    }
}
