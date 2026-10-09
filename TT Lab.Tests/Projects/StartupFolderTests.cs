using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Splat;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// The global package's URIs name its startup folder in two cases: Startup for the startup folder's files (the disc's case) and startup
// for the default chunk and its assets (the chunk's path). Windows had one folder for both, named after whichever got written first, and
// on Linux the files of the other weren't found
[Collection(ProjectCollection.Name)]
public sealed class StartupFolderTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly string _elsewhere = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        Directory.Delete(_elsewhere, true);
        _project.Dispose();
    }

    private string GlobalPackagePath => Path.Combine(_project.AssetsPath, _project.Project.GlobalPackagePS2.Name);

    private List<string> StartupFolders => !Directory.Exists(GlobalPackagePath) ? [] : Directory.GetDirectories(GlobalPackagePath).Select(Path.GetFileName)
        .Where(name => name!.Equals("startup", StringComparison.OrdinalIgnoreCase)).Select(name => name!).ToList();

    // A startup file, the default chunk and one of its collision surfaces, the way creating a project names them
    private (TextFile Readme, LevelChunk Chunk, CollisionSurface Surface) AddStartupAssets()
    {
        var readme = _project.Add(new TextFile { GlobalPath = "Startup" }, "Readme");
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "startup/default" }, "default");
        var surface = new CollisionSurface { Chunk = "startup/default", LayoutID = 7 };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Stone", 0x1);
        GiveData(readme, surface);
        return (readme, chunk, surface);
    }

    // Saving the project lets go of the data written
    private static void GiveData(TextFile readme, CollisionSurface surface)
    {
        readme.SetData(new TextFileData(readme, "Startup's own"));
        surface.SetData(new CollisionSurfaceData(surface) { ImpactParticleSystemId = 7 });
    }

    // What Windows makes of both names: one folder, named in the case of whichever got written first
    private void PutIntoOneFolder(string name)
    {
        var gathered = Path.Combine(GlobalPackagePath, "gathered");
        foreach (var folder in StartupFolders)
        {
            MoveInto(Path.Combine(GlobalPackagePath, folder), gathered);
        }

        Directory.Move(gathered, Path.Combine(GlobalPackagePath, name));
    }

    private static void MoveInto(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Move(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            MoveInto(directory, Path.Combine(to, Path.GetFileName(directory)));
        }

        Directory.Delete(from);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var waited = 0; waited < 20000 && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    [Fact]
    public void AFolderThereInAnotherCaseIsTheOneFound()
    {
        Directory.CreateDirectory(Path.Combine(_elsewhere, "Startup", "default"));

        var resolved = DirectoryCase.Resolve(_elsewhere, Path.Combine("startup", "default", "Mesh"));
        Directory.CreateDirectory(Path.Combine(_elsewhere, resolved));

        // What isn't there yet is made in the folder that is
        Assert.Equal(["Startup"], Directory.GetDirectories(_elsewhere).Select(Path.GetFileName));
        Assert.True(Directory.Exists(Path.Combine(_elsewhere, "Startup", "default", "Mesh")));
    }

    // Projects made on Linux before have a folder of each name
    [Fact]
    public void TheExactNameComesFirst()
    {
        Directory.CreateDirectory(Path.Combine(_elsewhere, "Startup"));
        Directory.CreateDirectory(Path.Combine(_elsewhere, "startup", "default"));

        Assert.Equal(Path.Combine("startup", "default"), DirectoryCase.Resolve(_elsewhere, Path.Combine("startup", "default")));
        Assert.Equal("Startup", DirectoryCase.Resolve(_elsewhere, "Startup"));
    }

    // Written in parallel the way creating a project writes them, whichever comes first
    [Fact]
    public void ANewProjectHasOneStartupFolderNamedLikeTheDiscs()
    {
        _project.Project.MakeStartupFolder(["Levels\\Earth\\Hub\\Beach.rm2", "Startup\\Default.rm2", "Startup\\Crash.ico"], _project.Project.GlobalPackagePS2,
            ".rm2");
        var (readme, chunk, surface) = AddStartupAssets();

        _project.Project.Serialize();

        Assert.Equal(["Startup"], StartupFolders);
        Assert.True(File.Exists(Path.Combine(GlobalPackagePath, "Startup", "Readme.json")));
        Assert.True(File.Exists(Path.Combine(GlobalPackagePath, "Startup", "default", "default.json")));
        Assert.True(File.Exists(Path.Combine(GlobalPackagePath, "Startup", "default", "CollisionSurface", "Layout_7", "Stone.data")));
        // Where they are is where they're read from
        Assert.True(File.Exists(Path.Combine(readme.FullPath, "Readme.json")));
        Assert.True(File.Exists(Path.Combine(chunk.FullPath, "default.json")));
        Assert.True(File.Exists(surface.FullDataPath));
    }

    // Without the folder made first whichever name gets made first is the folder of both, like on Windows: written in parallel there was a
    // folder of each name, and an asset's data could go into the one and its metadata into the other
    [Fact]
    public void AssetsWrittenAtOnceUnderBothNamesShareOneFolder()
    {
        var (readme, _, surface) = AddStartupAssets();
        for (var i = 0; i < 10; i++)
        {
            foreach (var folder in StartupFolders)
            {
                Directory.Delete(Path.Combine(GlobalPackagePath, folder), true);
            }

            DirectoryCase.Forget();
            GiveData(readme, surface);
            _project.Project.Serialize();

            var startup = Path.Combine(GlobalPackagePath, Assert.Single(StartupFolders));
            Assert.True(File.Exists(Path.Combine(startup, "Readme.json")));
            Assert.True(File.Exists(Path.Combine(startup, "default", "default.json")));
            Assert.True(File.Exists(Path.Combine(startup, "default", "CollisionSurface", "Layout_7", "Stone.json")));
            Assert.True(File.Exists(Path.Combine(startup, "default", "CollisionSurface", "Layout_7", "Stone.data")));
        }
    }

    [AvaloniaTheory]
    [InlineData("startup")]
    [InlineData("Startup")]
    public async Task AProjectMadeOnWindowsOpensOnLinux(string folderName)
    {
        var (readme, chunk, surface) = AddStartupAssets();
        _project.Project.Serialize();
        PutIntoOneFolder(folderName);
        var projectManager = Locator.Current.GetService<ProjectManager>()!;
        // Opening clears the log panel
        Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), projectManager));
        try
        {
            projectManager.OpenProject(_project.Project.ProjectPath);
            await WaitUntil(() => projectManager.WorkableProject);

            var assets = projectManager.OpenedProject!.AssetManager;
            var openedReadme = assets.GetAsset<TextFile>(readme.URI);
            var openedChunk = assets.GetAsset<LevelChunk>(chunk.URI);
            var openedSurface = assets.GetAsset<CollisionSurface>(surface.URI);
            Assert.Equal("Startup's own", ((IAsset)openedReadme).GetData<TextFileData>().Text);
            Assert.Equal(7, ((IAsset)openedSurface).GetData<CollisionSurfaceData>().ImpactParticleSystemId);

            // The chunk's folder in the tree is the one there, new instances of the default chunk go into it
            var chunkFolder = openedChunk.GetChunkFolder();
            Assert.Equal("default", chunkFolder.Alias);
            Assert.Equal(folderName, assets.GetAsset<Folder>(chunkFolder.Parent).Alias);

            // Saved into the folder they were read from. A chunk's path has the separators of the system the project was made on, a
            // Windows path was the name of one folder on Linux
            var folders = Directory.GetDirectories(GlobalPackagePath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
            openedChunk.AdditionalPath = "startup\\default";
            openedReadme.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
            openedChunk.Serialize();
            openedSurface.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
            Assert.Equal(folders, Directory.GetDirectories(GlobalPackagePath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            Assert.Equal([folderName], StartupFolders);
            Assert.Equal(["CollisionSurface", "default.json"], Directory.GetFileSystemEntries(Path.Combine(GlobalPackagePath, folderName, "default"))
                .Select(Path.GetFileName).Order(StringComparer.Ordinal));
            Assert.Equal("Startup's own", File.ReadAllText(Path.Combine(GlobalPackagePath, folderName, "Readme.txt")).Trim('"'));
        }
        finally
        {
            // Opening posted the panel's clearing, which reads the panel when it runs
            Dispatcher.UIThread.RunJobs();
            Log.SetViewModel(null);
        }
    }
}
