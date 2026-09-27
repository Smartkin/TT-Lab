namespace TT_Lab.Tests.Projects;

// Creating a project leaves the folders of the kinds of assets that ended up internal empty, models and skins are kept in the files of
// what they're parts of
public sealed class EmptyFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TT Lab Tests", Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_root, true);

    private string Make(params string[] path)
    {
        var directory = Path.Combine([_root, .. path]);
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public void EmptyFoldersGoAtEveryDepth()
    {
        var package = Make("PS2");
        File.WriteAllText(Path.Combine(package, "PS2.json"), "{}");
        Make("PS2", "Skin");
        Make("PS2", "Model");
        var chunk = Make("PS2", "levels", "earth", "hub", "beach");
        File.WriteAllText(Path.Combine(chunk, "beach.json"), "{}");
        Make("PS2", "levels", "earth", "hub", "beach", "Mesh");
        // Only empty folders in it, it's empty once they're gone
        Make("PS2", "levels", "earth", "hub", "huba", "Model", "Layout_0");

        TT_Lab.Project.Project.DeleteEmptyFolders(new DirectoryInfo(package));

        Assert.True(File.Exists(Path.Combine(package, "PS2.json")));
        Assert.True(File.Exists(Path.Combine(chunk, "beach.json")));
        Assert.Equal([chunk], Directory.GetDirectories(package, "*", SearchOption.AllDirectories).Where(directory => !Directory.EnumerateFileSystemEntries(directory).Any(Directory.Exists)));
    }
}
