using TT_Lab.Tools.Blender;

namespace TT_Lab.Tests.Tools;

// The Blender TT Lab opens models in: the preferences', else on the path, the Flatpak, Steam's in any of its libraries or Program Files'
public sealed class BlenderInstallTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TT Lab Tests", "Blender " + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string Executable(string folder, string name = "blender")
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private BlenderInstall.Places Linux(string pathVariable = "") => new(Path.Combine(_root, "home"), pathVariable, [], false, true);

    [Fact]
    public void ThePreferencesBlenderComesFirst()
    {
        var mine = Executable(Path.Combine(_root, "mine"));
        Executable(Path.Combine(_root, "bin"));

        Assert.Equal(mine, BlenderInstall.Find(mine, Linux(Path.Combine(_root, "bin")))?.Executable);
        Assert.True(BlenderInstall.Find(BlenderInstall.FlatpakId, Linux())!.IsFlatpak);
        Assert.Null(BlenderInstall.Find(Path.Combine(_root, "gone"), Linux()));
    }

    [Fact]
    public void BlenderOnThePathComesBeforeSteams()
    {
        var onPath = Executable(Path.Combine(_root, "bin"));
        var home = Path.Combine(_root, "home");
        Executable(Path.Combine(home, ".local/share/Steam/steamapps/common/Blender"));

        Assert.Equal(onPath, BlenderInstall.Find(string.Empty, Linux(Path.Combine(_root, "bin")))?.Executable);
    }

    [Fact]
    public void SteamsBlenderIsFoundInAnyOfItsLibraries()
    {
        var home = Path.Combine(_root, "home");
        var library = Path.Combine(_root, "games", "SteamLibrary");
        var steam = Path.Combine(home, ".local/share/Steam/steamapps");
        Directory.CreateDirectory(steam);
        File.WriteAllText(Path.Combine(steam, "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{home}/.local/share/Steam\"\n\t}}\n\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{library}\"\n\t}}\n}}\n");
        var blender = Executable(Path.Combine(library, "steamapps", "common", "Blender"));

        Assert.Equal(blender, BlenderInstall.Find(string.Empty, Linux())?.Executable);
    }

    [Fact]
    public void TheNewestOfProgramFilesIsFoundOnWindows()
    {
        var programs = Path.Combine(_root, "Program Files");
        Executable(Path.Combine(programs, "Blender Foundation", "Blender 4.5"), "blender.exe");
        var newest = Executable(Path.Combine(programs, "Blender Foundation", "Blender 5.2"), "blender.exe");

        Assert.Equal(newest, BlenderInstall.Find(string.Empty, new BlenderInstall.Places(Path.Combine(_root, "home"), string.Empty, [programs], true, false))?.Executable);
    }

    [Fact]
    public void ItsVersionIsWhatItSays()
    {
        Assert.Equal(new Version(5, 2, 2), BlenderInstall.VersionOf("Blender 5.2.2 LTS\n\tbuild date: 2026-09-15"));
        Assert.Equal(new Version(4, 5, 0), BlenderInstall.VersionOf("Blender 4.5"));
        Assert.Null(BlenderInstall.VersionOf("not Blender"));
    }
}
