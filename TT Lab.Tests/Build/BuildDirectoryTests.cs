using System.Text;
using Splat;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Build;

// Saving a chunk while a build ran set the current directory to the project's assets, and the build, which went from folder to folder
// relative to it, failed writing Extras or wrote its files there. The build only goes by absolute paths now, and saving leaves the
// current directory alone
[Collection(ProjectCollection.Name)]
public sealed class BuildDirectoryTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly string _previous = Directory.GetCurrentDirectory();
    private readonly string _elsewhere = Directory.CreateTempSubdirectory().FullName;

    public BuildDirectoryTests()
    {
        var global = _project.Project.GlobalPackagePS2.Name;
        _project.BuildProjectTree(Path.Combine(global, "levels"), Path.Combine(global, "Extras", "Bonus"));
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_previous);
        Directory.Delete(_elsewhere, true);
        _project.Dispose();
    }

    // Writing it moves the current directory into the project's assets, the way saving a chunk at that moment did
    private sealed class SavedMeanwhile : TextFile
    {
        public override void ExportToFile(ITwinItemFactory factory, string directory)
        {
            Directory.SetCurrentDirectory(Path.Combine(Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath, "assets"));
            base.ExportToFile(factory, directory);
        }
    }

    private T AddText<T>(T asset, Folder folder, string name, string text) where T : TextFile
    {
        _project.Add(asset, name);
        asset.SetData(new TextFileData(asset, text));
        folder.AddChild(asset);
        return asset;
    }

    [Fact]
    public void GlobalFilesGoIntoTheBuildsFoldersWhereverTheCurrentDirectoryGoes()
    {
        var extras = _project.GetFolder(_project.Project.GlobalPackagePS2, "Extras");
        var bonus = _project.GetFolder(_project.Project.GlobalPackagePS2, "Extras", "Bonus");
        AddText(new SavedMeanwhile { GlobalPath = "Extras" }, extras, "credits", "Thanks");
        AddText(new TextFile { GlobalPath = "Extras/Bonus" }, bonus, "secret", "Hi");
        var output = Path.Combine(_project.Root, "build", "archives", "Extras");
        Directory.CreateDirectory(output);
        var assetsBefore = Directory.EnumerateFileSystemEntries(_project.AssetsPath).ToList();
        Directory.SetCurrentDirectory(_elsewhere);

        var cache = BuildCache.Load(_project.Project.ProjectPath, _project.AssetManager);
        UInt32 total = 0;
        UInt32 written = 0;
        _project.Project.ResolveGlobalAssets(new PS2ItemFactory(), cache, extras.Children, output, ref total, ref written);

        Assert.Equal("Thanks", File.ReadAllText(Path.Combine(output, "credits.txt"), Encoding.Latin1));
        // The folder after the file that moved the current directory still goes into the build's
        Assert.Equal("Hi", File.ReadAllText(Path.Combine(output, "Bonus", "secret.txt"), Encoding.Latin1));
        Assert.Equal(assetsBefore, Directory.EnumerateFileSystemEntries(_project.AssetsPath).ToList());
        Assert.Empty(Directory.EnumerateFileSystemEntries(_elsewhere));
    }

    [Fact]
    public void SavingAChunkOrAPackageOrMakingAFolderLeavesTheCurrentDirectory()
    {
        var package = _project.Project.Ps2Package;
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/beach" }, "beach", package: package);
        Directory.SetCurrentDirectory(_elsewhere);

        chunk.Serialize(SerializationFlags.SetDirectoryToAssets);
        package.Serialize(SerializationFlags.SetDirectoryToAssets);
        var levels = _project.GetFolder(_project.Project.GlobalPackagePS2, "levels");
        var folder = (Folder)AssetFactory.CreateAsset(typeof(Folder), levels, "hub", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<Folder>(),
            asset => AssetDataFactory.CreateFolderData(levels, asset))!;

        Assert.Equal(_elsewhere, Directory.GetCurrentDirectory());
        Assert.True(File.Exists(Path.Combine(chunk.FullPath, $"{chunk.Name}.json")));
        Assert.True(Directory.Exists(Path.Combine(_project.AssetsPath, _project.Project.GlobalPackagePS2.Name, "levels", folder.Alias)));
    }
}
