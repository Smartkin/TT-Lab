using System.Diagnostics;
using TT_Lab.Tools.Blender;
using Xunit.Abstractions;

namespace TT_Lab.Tests.Tools;

// The add-on TT Lab comes with installed into Blender as an extension, TT Lab 1.0.0's legacy add-on turned off (its operators have the same
// names). Installing needs a Blender: TTL_BLENDER names its executable, it installs into a Blender folder of the test's own
public sealed class BlenderAddonInstallerTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TT Lab Tests", "Add-on " + Guid.NewGuid().ToString("N"));
    private readonly string? _resources = Environment.GetEnvironmentVariable("BLENDER_USER_RESOURCES");
    private readonly string _record = BlenderAddonInstaller.Folder;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BLENDER_USER_RESOURCES", _resources);
        BlenderAddonInstaller.Folder = _record;
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void TheNewestAddOnNextToTTLabIsInstalled()
    {
        Directory.CreateDirectory(_root);
        Assert.Null(BlenderAddonInstaller.BundledZip(_root));
        foreach (var version in new[] { "2.0.0", "2.10.0", "2.9.1" })
        {
            File.WriteAllText(Path.Combine(_root, $"twin_tech_tools-{version}.zip"), string.Empty);
        }

        Assert.Equal(Path.Combine(_root, "twin_tech_tools-2.10.0.zip"), BlenderAddonInstaller.BundledZip(_root));
    }

    private static string AddOnFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TT Lab.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "TT Lab", "BlenderTools");
    }

    private static async Task<string> Run(BlenderInstall blender, params string[] arguments)
    {
        var info = blender.StartInfo(arguments);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info)!;
        var text = await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return text;
    }

    [Fact]
    public async Task TheExtensionIsInstalledAndTheLegacyAddOnTurnedOff()
    {
        var executable = Environment.GetEnvironmentVariable("TTL_BLENDER");
        var zip = BlenderAddonInstaller.BundledZip(Path.Combine(AddOnFolder(), "dist"));
        if (string.IsNullOrEmpty(executable) || !File.Exists(executable) || zip == null)
        {
            output.WriteLine("TTL_BLENDER names no Blender or the add-on isn't packaged (scripts/package_addon.py), nothing to install");
            return;
        }

        Environment.SetEnvironmentVariable("BLENDER_USER_RESOURCES", _root);
        BlenderAddonInstaller.Folder = Path.Combine(_root, "state");
        var blender = BlenderInstall.Find(executable)!;
        var legacy = Path.Combine(_root, "scripts", "addons", "twin_tech_tools");
        Directory.CreateDirectory(legacy);
        await File.WriteAllTextAsync(Path.Combine(legacy, "__init__.py"), """
            import bpy
            bl_info = {"name": "Twin Tech Tools", "blender": (4, 2, 0), "category": "Import-Export"}
            class TTT_OT_Legacy(bpy.types.Operator):
                bl_idname = "ttt.legacy"
                bl_label = "Legacy"
                def execute(self, context):
                    return {"FINISHED"}
            def register():
                bpy.utils.register_class(TTT_OT_Legacy)
            def unregister():
                bpy.utils.unregister_class(TTT_OT_Legacy)
            """.Replace("            ", string.Empty));
        await Run(blender, "--background", "--python-expr", "import addon_utils, bpy; addon_utils.enable('twin_tech_tools', default_set=True); bpy.ops.wm.save_userpref()");

        var problem = await BlenderAddonInstaller.InstallAsync(blender, zip);

        Assert.Null(problem);
        var addons = await Run(blender, "--background", "--python-expr", "import bpy; print('ADDONS', sorted(bpy.context.preferences.addons.keys()))");
        output.WriteLine(addons);
        var line = addons.Split('\n').Single(text => text.StartsWith("ADDONS"));
        Assert.Contains("'bl_ext.user_default.twin_tech_tools'", line);
        Assert.DoesNotContain("'twin_tech_tools'", line);
        Assert.True(File.Exists(Path.Combine(legacy, "__init__.py")));
        // Edit in Blender doesn't ask to install it again
        Assert.Equal(BlenderAddonInstaller.VersionOf(zip), BlenderAddonInstaller.InstalledVersion(blender));
    }
}
