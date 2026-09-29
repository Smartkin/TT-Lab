using Splat;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.Tools;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Tests.Editor;

// The disc's music archives and videos are rows of the project tree the twinstudio tools open, never assets of the project
[Collection(ProjectCollection.Name)]
public sealed class DiscFileTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private ProjectManager Manager => Locator.Current.GetService<ProjectManager>()!;

    private string DiscPath => Path.Combine(_project.Project.ProjectPath, "disc", "ps2");

    private void WriteDiscFiles()
    {
        Directory.CreateDirectory(Path.Combine(DiscPath, "Crash6"));
        Directory.CreateDirectory(Path.Combine(DiscPath, "FMV", "Bonus"));
        File.WriteAllBytes(Path.Combine(DiscPath, "Crash6", "Music.mh"), [1]);
        File.WriteAllBytes(Path.Combine(DiscPath, "Crash6", "Music.mb"), [2]);
        File.WriteAllBytes(Path.Combine(DiscPath, "Crash6", "English.mh"), [3]);
        File.WriteAllBytes(Path.Combine(DiscPath, "Crash6", "Crash.BD"), [4]);
        File.WriteAllBytes(Path.Combine(DiscPath, "FMV", "Attract.pss"), [5]);
        File.WriteAllBytes(Path.Combine(DiscPath, "FMV", "Bonus", "Bonus1.pss"), [6]);
    }

    private ResourceTreeElementViewModel Folder(params string[] path)
    {
        var element = Assert.Single(Manager.FullProjectTree, item => item.Alias == path[0]);
        foreach (var name in path.Skip(1))
        {
            element = Assert.Single(element.GetInternalChildren()!, item => item.Alias == name);
        }

        return element;
    }

    [Fact]
    public void MusicArchivesAreOneRowAndVideosTheirOwn()
    {
        WriteDiscFiles();
        _project.BuildProjectTree();

        var crash6 = Folder("disc", "ps2", "Crash6");
        Assert.Equal(["English.mh", "Music.mh/mb"], crash6.GetInternalChildren()!.Select(row => row.Alias));
        var music = Assert.IsType<DiscFileElementViewModel>(crash6.GetInternalChildren()![1]);
        var file = Assert.IsType<DiscFile>(music.Asset);
        Assert.Equal(DiscFileTool.Music, file.Tool);
        Assert.Equal(Path.Combine(DiscPath, "Crash6", "Music.mh"), file.MainPath);
        Assert.Equal([Path.Combine(DiscPath, "Crash6", "Music.mh"), Path.Combine(DiscPath, "Crash6", "Music.mb")], file.Paths);
        Assert.Equal("res://__DISC__/disc/ps2/Crash6/Music", file.URI.ToString());
        var videos = Folder("disc", "ps2", "FMV");
        Assert.Equal(["Bonus", "Attract.pss"], videos.GetInternalChildren()!.Select(row => row.Alias));
        Assert.Equal(DiscFileTool.Video, ((DiscFile)videos.GetInternalChildren()![1].Asset).Tool);
        Assert.Equal(["Bonus1.pss"], Folder("disc", "ps2", "FMV", "Bonus").GetInternalChildren()!.Select(row => row.Alias));

        // Saving the project writes no asset file for them and the tree follows the disc folder
        _project.Project.Serialize();
        Assert.Empty(Directory.GetFiles(Path.Combine(DiscPath, "Crash6"), "*.json"));
        File.Delete(Path.Combine(DiscPath, "Crash6", "English.mh"));
        File.WriteAllBytes(Path.Combine(DiscPath, "FMV", "Intro.pss"), [7]);
        Manager.SyncProjectTree();
        Assert.Equal(["Music.mh/mb"], crash6.GetInternalChildren()!.Select(row => row.Alias));
        Assert.Equal(["Bonus", "Attract.pss", "Intro.pss"], videos.GetInternalChildren()!.Select(row => row.Alias));
        Assert.False(_project.AssetManager.DoesAssetExist(new LabURI("res://__DISC__/disc/ps2/Crash6/English")));
    }

    [Fact]
    public void ToolsAreFoundInTheirOwnFoldersNextToTheApp()
    {
        var previous = ExternalTools.ToolsFolder;
        ExternalTools.ToolsFolder = Path.Combine(_project.Project.ProjectPath, "tools");
        try
        {
            Assert.Null(ExternalTools.FindExecutable(DiscFileTool.Music));
            Assert.Contains("twinmusic", ExternalTools.MissingMessage(DiscFileTool.Music));
            Assert.Contains("Install the tools", ExternalTools.MissingMessage(DiscFileTool.Music));

            var executable = Path.Combine(ExternalTools.ToolsFolder, "twinpss", OperatingSystem.IsWindows() ? "twinpss.exe" : "twinpss");
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
            File.WriteAllBytes(executable, [0]);

            Assert.Equal(executable, ExternalTools.FindExecutable(DiscFileTool.Video));
            Assert.Null(ExternalTools.FindExecutable(DiscFileTool.Music));
        }
        finally
        {
            ExternalTools.ToolsFolder = previous;
        }
    }

    // A release zip is a bare executable with a resources folder, installed into the tool's own folder in place of what was there
    [Fact]
    public void ReleaseZipsUnpackIntoTheToolsFolderRunnable()
    {
        var previous = ExternalTools.ToolsFolder;
        ExternalTools.ToolsFolder = Path.Combine(_project.Project.ProjectPath, "tools");
        try
        {
            var folder = ExternalTools.ToolFolder(DiscFileTool.Video);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "stale.txt"), "old");
            using var zip = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                var name = OperatingSystem.IsWindows() ? "twinpss.exe" : "twinpss";
                using (var entry = archive.CreateEntry(name).Open())
                {
                    entry.Write([0x7F, (byte)'E', (byte)'L', (byte)'F']);
                }

                using var fonts = archive.CreateEntry("resources/fonts/LICENSE.txt").Open();
                fonts.Write("license"u8);
            }

            zip.Position = 0;
            ExternalTools.InstallFromZip(DiscFileTool.Video, zip);

            var executable = ExternalTools.FindExecutable(DiscFileTool.Video);
            Assert.NotNull(executable);
            Assert.True(File.Exists(Path.Combine(folder, "resources", "fonts", "LICENSE.txt")));
            Assert.False(File.Exists(Path.Combine(folder, "stale.txt")));
            if (!OperatingSystem.IsWindows())
            {
                Assert.True(File.GetUnixFileMode(executable!).HasFlag(UnixFileMode.UserExecute));
            }

            Assert.Equal(OperatingSystem.IsWindows() ? "twinmusic-windows-latest.zip" : OperatingSystem.IsLinux() ? "twinmusic-ubuntu-latest.zip" : null, ExternalTools.ReleaseAssetName(DiscFileTool.Music));
        }
        finally
        {
            ExternalTools.ToolsFolder = previous;
        }
    }
}
