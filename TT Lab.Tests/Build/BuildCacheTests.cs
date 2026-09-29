using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.Project;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Build;

[Collection(ProjectCollection.Name)]
public sealed class BuildCacheTests : IDisposable
{
    private const string Key = "chunk:res://Test/levels/hub";

    private readonly TestProject _project = new();
    private readonly string _output;
    private readonly TextFile _dependency;

    public BuildCacheTests()
    {
        _output = Path.Combine(_project.Project.ProjectPath, "build", "archives", "hub.rm2");
        Directory.CreateDirectory(Path.GetDirectoryName(_output)!);
        File.WriteAllText(_output, "built chunk");
        _dependency = CreateTextFile("Strings", "Hello");
    }

    public void Dispose() => _project.Dispose();

    private TextFile CreateTextFile(string name, string text)
    {
        var textFile = new TextFile(_project.Project.GlobalPackagePS2.URI, false, string.Empty, name, text) { GlobalPath = "Text" };
        textFile.RegenerateUri();
        _project.AssetManager.AddAsset(textFile);
        textFile.Serialize(SerializationFlags.SaveData);
        return textFile;
    }

    private BuildCache Load() => BuildCache.Load(_project.Project.ProjectPath, _project.AssetManager);

    private void RecordAndSave(params IAsset[] dependencies)
    {
        var cache = Load();
        cache.Record(Key, dependencies, [_output]);
        cache.Save();
    }

    [Fact]
    public void NothingIsUpToDateBeforeItWasBuilt()
    {
        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void UnchangedOutputIsReusedByTheNextBuild()
    {
        RecordAndSave(_dependency);

        Assert.True(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void ChangedDependencyMakesTheOutputStale()
    {
        RecordAndSave(_dependency);

        File.WriteAllText(_dependency.FullDataPath, "Hello, world");

        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void ChangedDependencyMetadataMakesTheOutputStale()
    {
        RecordAndSave(_dependency);

        _dependency.Alias = "Renamed";

        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void DeletedDependencyMakesTheOutputStale()
    {
        RecordAndSave(_dependency);

        _project.AssetManager.RemoveAsset(_dependency);

        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    // Loaded data can have changes that aren't saved yet
    [Fact]
    public void DependencyLoadedWhenTheBuildStartsIsAlwaysStale()
    {
        RecordAndSave(_dependency);

        _dependency.GetData();

        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void OutputBuiltFromLoadedDataStaysStaleWhileTheDataIsLoaded()
    {
        _dependency.GetData();
        RecordAndSave(_dependency);

        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void ChangedOrMissingOutputIsRebuilt()
    {
        RecordAndSave(_dependency);

        File.WriteAllText(_output, "tampered chunk");
        Assert.False(Load().IsUpToDate(Key, [_output]));

        RecordAndSave(_dependency);
        File.Delete(_output);
        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void DifferentOutputsAreRebuilt()
    {
        RecordAndSave(_dependency);

        var otherOutput = Path.Combine(Path.GetDirectoryName(_output)!, "hub.sm2");
        File.WriteAllText(otherOutput, "scenery");

        Assert.False(Load().IsUpToDate(Key, [_output, otherOutput]));
    }

    // Internal assets are recreated from their owner's data so changing the owner changes them
    [Fact]
    public void InternalDependenciesFollowTheirOwner()
    {
        var owner = CreateTextFile("Owner", "Model");
        var internalAsset = new OGI { IsInternal = true, InternalOwner = owner };
        _project.Add(internalAsset, "Internal");
        RecordAndSave(internalAsset);

        Assert.True(Load().IsUpToDate(Key, [_output]));

        File.WriteAllText(owner.FullDataPath, "Changed model");
        Assert.False(Load().IsUpToDate(Key, [_output]));
    }

    [Fact]
    public void CorruptedManifestStartsFromScratch()
    {
        RecordAndSave(_dependency);
        var manifest = Path.Combine(_project.Project.ProjectPath, "build", "cache", "manifest.ttcache");
        File.WriteAllText(manifest, "{ not json");

        var cache = Load();

        Assert.False(cache.IsUpToDate(Key, [_output]));
        cache.Record(Key, [_dependency], [_output]);
        cache.Save();
        Assert.True(Load().IsUpToDate(Key, [_output]));
    }
}
