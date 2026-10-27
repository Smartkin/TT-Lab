using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
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

    // A chunk has its animations by the IDs the build gave them, it's built again once one of them gets another
    [Fact]
    public void ChunksWhoseAnimationsGetOtherIdsAreRebuilt()
    {
        var ids = LoadAnimationIds();
        var dance = ids.KeyOf([1, 2, 3]);
        var dependency = AnimationIds.DependencyOf(dance, 0x8000);
        var cache = Load();
        cache.AnimationIds = ids;
        cache.Record(Key, [_dependency], [_output], animations: new Dictionary<string, UInt32> { [dependency] = ids.IdOf(dance, 0x8000, "Dance") });
        cache.Save();
        ids.Save();

        var reloaded = Load();
        reloaded.AnimationIds = LoadAnimationIds();
        Assert.True(reloaded.IsUpToDate(Key, [_output]));
        Assert.Equal([dependency], reloaded.RecordedAnimations());

        reloaded.AnimationIds.KeepOnly([]);
        Assert.False(reloaded.IsUpToDate(Key, [_output]));
    }

    private AnimationIds LoadAnimationIds() => AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, []);

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

    // Loaded data no editor shows can have changes that aren't saved yet
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

    // Loaded data the open editors show has what its files have while it has no edit they don't (a chunk's scene open for Play loads every
    // resource of the chunk): what was built of it is kept, and so is what was built while they showed it. An edit counts as changed until
    // it's saved, so does an asset never saved
    [Fact]
    public void LoadedDataEditorsShowWithoutAnEditCountsAsItsFiles()
    {
        using var editors = ShowingEditors.Show(_dependency.URI);
        RecordAndSave(_dependency);
        _dependency.GetData();
        Assert.True(Load().IsUpToDate(Key, [_output]));
        RecordAndSave(_dependency);
        Assert.True(Load().IsUpToDate(Key, [_output]));

        _dependency.NoteEdited();
        Assert.False(Load().IsUpToDate(Key, [_output]));
        _dependency.Save();
        Assert.True(Load().IsUpToDate(Key, [_output]));

        // Shown by mistake, an asset of no files is never what its files have
        _dependency.IsUnsaved = true;
        try
        {
            Assert.False(Load().IsUpToDate(Key, [_output]));
        }
        finally
        {
            _dependency.IsUnsaved = false;
        }
    }

    // Data edited while the build runs (an instance dragged in the scene Play's build started from) can be what the build read: what's
    // recorded after the edit counts as changed, so the chunk isn't kept by its files once the edit is gone (undone, the scene closed without
    // saving). What was recorded before the edit was built of the files
    [Fact]
    public void LoadedDataEditedWhileTheBuildRunsIsRecordedAsChanged()
    {
        const string before = "chunk:res://Test/levels/before";
        using var editors = ShowingEditors.Show(_dependency.URI);
        _dependency.GetData();
        var cache = Load();
        cache.Record(before, [_dependency], [_output]);
        _dependency.NoteEdited();
        cache.Record(Key, [_dependency], [_output]);
        cache.Save();

        _dependency.UnloadData();
        var reloaded = Load();
        Assert.True(reloaded.IsUpToDate(before, [_output]));
        Assert.False(reloaded.IsUpToDate(Key, [_output]));
    }

    // Two editors share the data (the particles' own and the default chunk's scene): an edit in one that the other saved, then undone, leaves
    // both where they were saved and the data without what its file has. It counts as changed until it's saved, and what was built of it
    // isn't kept by the file once the data is let go of
    [Fact]
    public void LoadedDataEditedSinceItWasSavedCountsAsChanged()
    {
        using var editors = ShowingEditors.Show(_dependency.URI);
        _dependency.GetData();
        _dependency.NoteEdited();
        _dependency.Save();
        _dependency.NoteEdited();
        RecordAndSave(_dependency);
        Assert.False(Load().IsUpToDate(Key, [_output]));

        _dependency.UnloadData();
        Assert.False(Load().IsUpToDate(Key, [_output]));
        RecordAndSave(_dependency);
        Assert.True(Load().IsUpToDate(Key, [_output]));
    }

    // Data whose file another program wrote since TT Lab read it (Blender exported over the model, which isn't read again yet) isn't what the
    // file has: it counts as changed, and what was built of it isn't kept once the file is read
    [Fact]
    public void LoadedDataOfAFileAnotherProgramWroteCountsAsChanged()
    {
        using var editors = ShowingEditors.Show(_dependency.URI);
        _dependency.GetData();
        File.WriteAllText(_dependency.FullDataPath, "Hello from another program");
        RecordAndSave(_dependency);
        Assert.False(Load().IsUpToDate(Key, [_output]));

        _dependency.UnloadData();
        _dependency.GetData();
        Assert.False(Load().IsUpToDate(Key, [_output]));
        RecordAndSave(_dependency);
        Assert.True(Load().IsUpToDate(Key, [_output]));
    }

    // Editors showing an asset, what builds ask (BuildCache.EditorsShowing) until it's disposed. They're asked about every loaded asset
    private sealed class ShowingEditors : IDisposable
    {
        private readonly Func<IReadOnlySet<LabURI>, IReadOnlySet<LabURI>>? _before = BuildCache.EditorsShowing;

        public static ShowingEditors Show(LabURI shown)
        {
            var editors = new ShowingEditors();
            BuildCache.EditorsShowing = loaded => loaded.Where(uri => uri == shown).ToHashSet();
            return editors;
        }

        public void Dispose() => BuildCache.EditorsShowing = _before;
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
