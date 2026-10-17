using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Splat;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Project;
using TT_Lab.Util;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// Edit in Blender (the project tree, a model's editor, a chunk's scene): the model's blend file opens in Blender, which links it to TT Lab.
/// A model without one gets a new one, the model imported into it and saved where the user puts it (the project's source folder first), or
/// an existing one whose model of the kind replaces it (Use an Existing Blend File, which a model with a blend file has too). Either is
/// remembered on this computer once Blender saved it (<see cref="BlendFiles"/>). The add-on TT Lab comes with gets installed into the
/// Blender first when TT Lab didn't install it, after asking
/// </summary>
public static class BlenderEditing
{
    /// <summary>
    /// What a model without a blend file gets: a new one of the model, or an existing one whose model replaces it
    /// </summary>
    internal enum BlendChoice
    {
        None,
        New,
        Existing
    }

    public const string UseExistingHeader = "Use an Existing Blend File...";
    public const string UseExistingHint = "Use an existing blend file: Blender opens it and its model of this kind replaces this one in TT Lab once Blender " +
                                          "linked it (when TT Lab can take it), the blend file is the model's from then on";

    // What tests answer: a new or an existing blend file, where a new blend file goes (none: it isn't made), which existing one is used
    // (none: nothing starts), whether to install the add-on, how Blender starts
    internal static Func<IAsset, Task<BlendChoice>> AskNewOrExisting { get; set; } = AskChoice;
    internal static Func<string, Task<string?>> AskWhereToSave { get; set; } = AskWhere;
    internal static Func<IAsset, string, Task<string?>> AskForBlendFile { get; set; } = AskExisting;
    internal static Func<string, Task<bool>> AskToInstall { get; set; } = AskInstall;
    internal static Func<ProcessStartInfo, bool> Start { get; set; } = StartBlender;

    /// <summary>
    /// What Blender edits: an asset kept in a model file of its own (TLM.md)
    /// </summary>
    public static bool CanEdit(IAsset asset) => !asset.IsInternal && asset is not Folder && AssetLinks.IsModelFile(asset.Data);

    public static async Task EditAsync(IAsset asset)
    {
        if (OpenedProject(asset) is not { } project)
        {
            return;
        }

        if (LinkedSession(asset) is { } linked)
        {
            linked.Push(new JsonObject { ["type"] = "edit_in_blender", ["uri"] = asset.URI.ToString() });
            Log.WriteLine($"{asset.Alias} is open in Blender already");
            return;
        }

        if (await ReadyBlender() is not { } blender)
        {
            return;
        }

        var blend = BlendFiles.Of(project.UUID, asset.URI);
        if (blend != null)
        {
            StartBlenderWith(InFolderOf(blender.StartInfo(blend), blend), $"Blender opens {asset.Alias} ({blend})");
            return;
        }

        switch (await AskNewOrExisting(asset))
        {
            case BlendChoice.New:
                blend = await AskWhereToSave(BlendFiles.DefaultFor(project.ProjectPath, asset));
                if (string.IsNullOrEmpty(blend))
                {
                    return;
                }

                BlendFiles.Remember(project.UUID, asset.URI, blend);
                StartBlenderWith(InFolderOf(blender.StartInfo("--python-expr", ImportExpression(), "--", asset.FullDataPath, blend), blend), $"Blender opens {asset.Alias} ({blend})");
                break;
            case BlendChoice.Existing:
                await UseExisting(project, blender, asset);
                break;
        }
    }

    /// <summary>
    /// Use an Existing Blend File: Blender opens the blend file picked and makes its model of the kind TT Lab's model, which it writes
    /// through TT Lab in its place once it's linked (when the add-on's checks and TT Lab's take it) and saves the blend file, which is the
    /// model's from then on. A model TT Lab can't take is said why in Blender, saving the blend file once it's fixed replaces it
    /// </summary>
    public static async Task UseBlendFileAsync(IAsset asset)
    {
        if (OpenedProject(asset) is not { } project || await ReadyBlender() is not { } blender)
        {
            return;
        }

        await UseExisting(project, blender, asset);
    }

    private static async Task UseExisting(Project.Project project, BlenderInstall blender, IAsset asset)
    {
        var remembered = BlendFiles.Of(project.UUID, asset.URI);
        var folder = Path.GetDirectoryName(remembered ?? BlendFiles.DefaultFor(project.ProjectPath, asset))!;
        var blend = await AskForBlendFile(asset, folder);
        if (string.IsNullOrEmpty(blend))
        {
            return;
        }

        if (!File.Exists(blend))
        {
            Log.WriteLine($"There's no blend file {blend}", Log.LogType.Warning);
            return;
        }

        var type = ModelTypeOf(asset);
        if (type.Length == 0)
        {
            Log.WriteLine($"TT Lab can't tell what kind of model {asset.Alias} is, Blender doesn't replace it", Log.LogType.Warning);
            return;
        }

        // A linked Blender has it open: the model is picked there, a second Blender would save the file over the first's
        if (Locator.Current.GetService<BlenderLinkServer>()?.Sessions.FirstOrDefault(session => session.IsWelcomed && session.BlendPath.Length > 0
                && BlendFiles.IsSameFile(session.BlendPath, blend)) is { } open)
        {
            open.Push(new JsonObject { ["type"] = "use_for", ["model"] = asset.FullDataPath, ["asset_type"] = type, ["name"] = asset.Alias });
            Log.WriteLine($"{blend} is open in Blender: its model replaces {asset.Alias} there, when TT Lab can take it");
            return;
        }

        StartBlenderWith(InFolderOf(blender.StartInfo(blend, "--python-expr", UseExpression(), "--", asset.FullDataPath, type, asset.Alias), blend),
            $"Blender opens {blend}: its model replaces {asset.Alias} once Blender linked it, when TT Lab can take it");
    }

    // Blender's working directory is the blend file's folder: TT Lab's is the project's, where paths of another system (a Windows path on
    // Linux) made relative led
    private static ProcessStartInfo InFolderOf(ProcessStartInfo info, string blend)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(blend));
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            info.WorkingDirectory = folder;
        }

        return info;
    }

    /// <summary>
    /// What Blender runs to make a model's blend file (the model's data file and the blend file after "--"): a new file of nothing, the model
    /// imported into it and saved where it goes, once Blender's window is up
    /// </summary>
    internal static string ImportExpression()
    {
        // The paths are arguments after "--": written into the expression as strings, characters past the BMP broke them. Timers run
        // without a window, the operators need one (the import enters Edit mode), and reading the home file makes the windows again
        return "import bpy, sys\n" +
               "_model, _blend = sys.argv[sys.argv.index('--') + 1:][:2]\n" +
               "def _tt_lab_edit():\n" +
               "    with bpy.context.temp_override(window=bpy.context.window_manager.windows[0]):\n" +
               "        bpy.ops.wm.read_homefile(use_empty=True)\n" +
               "    with bpy.context.temp_override(window=bpy.context.window_manager.windows[0]):\n" +
               "        bpy.ops.ttt.edit_from_tt_lab(model=_model, blend=_blend)\n" +
               "bpy.app.timers.register(_tt_lab_edit, first_interval=0.5)\n";
    }

    /// <summary>
    /// What Blender runs on the blend file it opened to make its model of the type TT Lab's model (the model's data file, its model file's
    /// type, "Ogi", and its name after "--"): the request is kept at once, so the scene's models of the kind aren't linked meanwhile, and once
    /// Blender's window is up it's invoked, asking which when the file has several, a step of Blender's undo
    /// </summary>
    internal static string UseExpression()
    {
        return "import bpy, sys\n" +
               "_model, _type, _name = sys.argv[sys.argv.index('--') + 1:][:3]\n" +
               "_missing = 'Twin Tech: the blend file\\'s model doesn\\'t replace TT Lab\\'s, install the add-on TT Lab comes with (%s)'\n" +
               "try:\n" +
               "    bpy.ops.ttt.tt_lab_request(model=_model, asset_type=_type, name=_name)\n" +
               "except (AttributeError, RuntimeError) as error:\n" +
               "    print(_missing % error)\n" +
               "def _tt_lab_use():\n" +
               "    with bpy.context.temp_override(window=bpy.context.window_manager.windows[0]):\n" +
               "        try:\n" +
               "            bpy.ops.ttt.use_for_tt_lab_model('INVOKE_DEFAULT', True, model=_model, asset_type=_type, name=_name)\n" +
               "        except (AttributeError, RuntimeError) as error:\n" +
               "            print(_missing % error)\n" +
               "bpy.app.timers.register(_tt_lab_use, first_interval=0.5)\n";
    }

    /// <summary>
    /// The type of the model's file (TLM.md's "asset", "Ogi" or "Scenery"), what a model of a blend file has to be to replace it: its file
    /// says, else its data's kind
    /// </summary>
    internal static string ModelTypeOf(IAsset asset)
    {
        try
        {
            if (File.Exists(asset.FullDataPath) && TlmFile.ReadJson(asset.FullDataPath)["asset"] is JsonObject header && header.GetString("type") is { Length: > 0 } type)
            {
                return type;
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
        {
            Log.WriteLine($"Couldn't read the kind of {asset.Alias}'s model file: {exception.Message}", Log.LogType.Warning);
        }

        return BlenderLinkAssets.DataTypeOf(asset).GetField("TlmAssetType")?.GetRawConstantValue() as string ?? string.Empty;
    }

    private static Project.Project? OpenedProject(IAsset asset)
    {
        if (!CanEdit(asset) || Locator.Current.GetService<ProjectManager>()?.OpenedProject is not Project.Project project)
        {
            Log.WriteLine($"{asset.Alias} isn't kept in a model file, Blender doesn't edit it", Log.LogType.Warning);
            return null;
        }

        return project;
    }

    private static BlenderLinkSession? LinkedSession(IAsset asset)
    {
        return Locator.Current.GetService<BlenderLinkServer>()?.Sessions.FirstOrDefault(session => session.IsWelcomed && session.Assets.Models.Contains(asset.URI));
    }

    // Found, with the add-on TT Lab comes with
    private static async Task<BlenderInstall?> ReadyBlender()
    {
        var blender = BlenderInstall.Find(Preferences.GetPreference<string>(Preferences.BlenderPath));
        if (blender == null)
        {
            Log.WriteLine("No Blender found: pick its executable in the Preferences' Blender section", Log.LogType.Warning);
            return null;
        }

        return await HasAddOn(blender) ? blender : null;
    }

    private static void StartBlenderWith(ProcessStartInfo info, string started)
    {
        if (Start(info))
        {
            Log.WriteLine(started);
        }
    }

    // Installed by TT Lab of the version it comes with, else installed after asking. A TT Lab without the add-on next to it trusts the
    // user's own
    private static async Task<bool> HasAddOn(BlenderInstall blender)
    {
        var zip = BlenderAddonInstaller.BundledZip();
        if (zip == null || BlenderAddonInstaller.InstalledVersion(blender) == BlenderAddonInstaller.VersionOf(zip))
        {
            return true;
        }

        if (!await AskToInstall(blender.ToString()))
        {
            return false;
        }

        var problem = await BlenderAddonInstaller.InstallAsync(blender, zip);
        if (problem != null)
        {
            Log.WriteLine(problem, Log.LogType.Warning);
            return false;
        }

        return true;
    }

    private static async Task<BlendChoice> AskChoice(IAsset asset)
    {
        return await ChoiceDialogue.Ask("Edit in Blender",
            $"{asset.Alias} has no blend file on this computer yet. A new blend file gets the model as TT Lab has it. An existing blend file's model of " +
            $"this kind replaces {asset.Alias}'s once Blender opened it, when TT Lab can take it.", ["New Blend File", "Existing Blend File..."]) switch
        {
            0 => BlendChoice.New,
            1 => BlendChoice.Existing,
            _ => BlendChoice.None
        };
    }

    private static async Task<string?> AskWhere(string suggested)
    {
        var folder = Path.GetDirectoryName(suggested)!;
        Directory.CreateDirectory(folder);
        var picked = await MiscUtils.GetSaveFileFromDialogueAsync("Where the model's blend file goes", "Blend files", ["*.blend"], Path.GetFileName(suggested), "blend", folder);
        return string.IsNullOrEmpty(picked) ? null : picked;
    }

    private static async Task<string?> AskExisting(IAsset asset, string folder)
    {
        var picked = await MiscUtils.GetFileFromDialogueAsync($"The blend file whose model replaces {asset.Alias}", "Blend files", ["*.blend"], Directory.Exists(folder) ? folder : string.Empty);
        return string.IsNullOrEmpty(picked) ? null : picked;
    }

    private static async Task<bool> AskInstall(string blender)
    {
        return await ChoiceDialogue.Ask("Blender add-on", $"Editing in Blender needs the add-on TT Lab comes with in {blender}. Install it? TT Lab 1.0.0's add-on gets turned off.",
            ["Install"]) == 0;
    }

    private static bool StartBlender(ProcessStartInfo info)
    {
        try
        {
            using var process = Process.Start(info);
            return process != null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            Log.WriteLine($"Blender didn't start: {exception.Message}", Log.LogType.Warning);
            return false;
        }
    }
}
