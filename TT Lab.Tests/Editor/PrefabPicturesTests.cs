using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Caliburn.Micro;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Project.Prefabs;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// The prefabs saved without a picture (the ones made of the instances) get the picture of their object's model in the background, one
// taken for every prefab drawn with the same model. The pictures are taken by a fake here, the tests have no GL
[Collection(ProjectCollection.Name)]
public sealed class PrefabPicturesTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly PrefabLibrary _library;
    private readonly FakeTaker _taker = new();
    private readonly PrefabPictures _pictures = new();

    public PrefabPicturesTests()
    {
        _library = new PrefabLibrary(_project.Project);
        _pictures.StartTaker = () => Task.FromResult<IPrefabPictureTaker?>(_taker);
    }

    public void Dispose() => _project.Dispose();

    private sealed class FakeTaker : IPrefabPictureTaker
    {
        public List<LabURI> Models { get; } = [];
        public List<int> SceneryBatches { get; } = [];
        public TaskCompletionSource? Gate { get; set; }
        public System.Action? Started { get; set; }

        public async Task<byte[]?> TakeAsync(LabURI model)
        {
            Models.Add(model);
            Started?.Invoke();
            if (Gate != null)
            {
                await Gate.Task;
            }

            return Png();
        }

        public Task<byte[]?[]> TakeSceneryAsync(IReadOnlyList<(LabURI Package, byte[] Model)> models)
        {
            SceneryBatches.Add(models.Count);
            return Task.FromResult(models.Select(_ => (byte[]?)Png()).ToArray());
        }

        private static byte[] Png() => TextureData.EncodePng([0xFF336699, 0xFF336699, 0xFF336699, 0xFF336699], 2, 2);

        public void Dispose()
        {
        }
    }

    private GameObject AddObject(string name, params LabURI[] models)
    {
        var gameObject = _project.Add(new GameObject(), name);
        gameObject.SetData(new GameObjectData(gameObject) { Name = name, ModelSlots = models.Select(model => new ModelSlot { Ogi = model }).ToList() });
        return gameObject;
    }

    private Prefab Save(string name, LabURI objectUri, PrefabKind kind = PrefabKind.Instance)
    {
        var prefab = new Prefab
        {
            Name = name, Kind = kind, Platform = "PS2", Package = _project.Project.GlobalPackagePS2.URI, LayoutID = 0,
            AssetType = typeof(ObjectInstance).FullName!, DataType = typeof(TT_Lab.AssetData.Instance.ObjectInstanceData).FullName,
            Data = new JObject { [nameof(TT_Lab.AssetData.Instance.ObjectInstanceData.ObjectId)] = JObject.FromObject(objectUri) }
        };
        _library.Save(prefab);
        return prefab;
    }

    private static string PictureOf(Prefab prefab) => Path.ChangeExtension(prefab.FilePath!, ".png");

    [Fact]
    public async Task EveryPrefabOfAModelGetsItsPicture()
    {
        var wumpaModel = _project.Add(new OGI(), "WumpaModel");
        var crabModel = _project.Add(new OGI(), "CrabModel");
        var wumpa = AddObject("WUMPA", LabURI.Empty, wumpaModel.URI);
        var crab = AddObject("CRAB", crabModel.URI);
        var sound = AddObject("IMPACT_SOUND");
        var wumpaA = Save("Wumpa A", wumpa.URI);
        var wumpaB = Save("Wumpa B", wumpa.URI);
        var crabPrefab = Save("Crab", crab.URI);
        var soundPrefab = Save("Sound", sound.URI);
        var gone = Save("Gone", new LabURI("res://Global PS2_Test/GameObject/Gone"));
        var group = Save("Group", crab.URI, PrefabKind.Group);
        var pictured = Save("Pictured", crab.URI);
        File.WriteAllBytes(PictureOf(pictured), [1, 2, 3]);
        var taken = new List<string>();
        _pictures.Taken += prefab => taken.Add(prefab.Name);

        _pictures.TakeMissing(_project.Project);
        await _pictures.WhenDone();

        // One picture per model, the object without one is drawn as a box
        Assert.Equal(3, _taker.Models.Count);
        Assert.Equal(new HashSet<LabURI> { wumpaModel.URI, crabModel.URI, LabURI.Empty }, _taker.Models.ToHashSet());
        Assert.Equal(["Crab", "Sound", "Wumpa A", "Wumpa B"], taken.Order());
        Assert.All(new[] { wumpaA, wumpaB, crabPrefab, soundPrefab }, prefab => Assert.True(File.Exists(PictureOf(prefab))));
        Assert.False(File.Exists(PictureOf(gone)));
        Assert.False(File.Exists(PictureOf(group)));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(PictureOf(pictured)));
        Assert.False(_pictures.IsTaking);
        Assert.Equal((4, 4), (_pictures.Done, _pictures.Total));

        // Taken again there's nothing left to take
        _pictures.TakeMissing(_project.Project);
        await _pictures.WhenDone();
        Assert.Equal(3, _taker.Models.Count);
    }

    // Scenery prefabs get theirs a folder at a time, drawn from their model files
    [Fact]
    public async Task SceneryPrefabsGetTheirPicturesAFolderAtATime()
    {
        Prefab SaveScenery(string name, string folder)
        {
            var prefab = new Prefab
            {
                Name = name, Kind = PrefabKind.Scenery, Platform = "PS2", Package = _project.Project.GlobalPackagePS2.URI, AssetType = typeof(Scenery).FullName!,
                Data = new JObject { ["Count"] = 1 }, Model = [1, 2, 3], Folder = folder
            };
            _library.Save(prefab);
            return prefab;
        }

        var meshes = new[] { SaveScenery("Mesh 1", "levels/beach/Meshes"), SaveScenery("Mesh 2", "levels/beach/Meshes"), SaveScenery("LOD 1", "levels/beach/LODs") };
        var lost = SaveScenery("Mesh 3", "levels/beach/Meshes");
        File.Delete(Path.ChangeExtension(lost.FilePath!, ".tlm"));

        _pictures.TakeMissing(_project.Project);
        await _pictures.WhenDone();

        Assert.Equal([1, 2], _taker.SceneryBatches);
        Assert.All(meshes, prefab => Assert.True(File.Exists(PictureOf(prefab))));
        // Without its model file there's nothing to draw
        Assert.False(File.Exists(PictureOf(lost)));
    }

    // The project closing stops the pictures, the one being taken isn't saved
    [Fact]
    public async Task StoppingTakesNoMorePictures()
    {
        var model = _project.Add(new OGI(), "Model");
        var other = _project.Add(new OGI(), "Other");
        var first = Save("First", AddObject("FIRST", model.URI).URI);
        var second = Save("Second", AddObject("SECOND", other.URI).URI);
        var started = new TaskCompletionSource();
        _taker.Gate = new TaskCompletionSource();
        _taker.Started = () => started.TrySetResult();

        _pictures.TakeMissing(_project.Project);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _pictures.Stop();
        _taker.Gate.SetResult();
        await _pictures.WhenDone().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(_taker.Models);
        Assert.False(File.Exists(PictureOf(first)));
        Assert.False(File.Exists(PictureOf(second)));
        Assert.False(_pictures.IsTaking);
    }

    // Asked again while taking pictures, it takes the ones of the prefabs made meanwhile once it's done
    [Fact]
    public async Task AskedAgainItTakesWhatWasMadeMeanwhile()
    {
        var model = _project.Add(new OGI(), "Model");
        var other = _project.Add(new OGI(), "Other");
        var first = Save("First", AddObject("FIRST", model.URI).URI);
        var started = new TaskCompletionSource();
        _taker.Gate = new TaskCompletionSource();
        _taker.Started = () => started.TrySetResult();

        _pictures.TakeMissing(_project.Project);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = Save("Second", AddObject("SECOND", other.URI).URI);
        _pictures.TakeMissing(_project.Project);
        _taker.Gate.SetResult();
        await _pictures.WhenDone().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(File.Exists(PictureOf(first)));
        Assert.True(File.Exists(PictureOf(second)));
    }

    [Fact]
    public async Task WithoutAViewportThereAreNoPictures()
    {
        var model = _project.Add(new OGI(), "Model");
        var prefab = Save("Prefab", AddObject("OBJECT", model.URI).URI);
        _pictures.StartTaker = () => Task.FromResult<IPrefabPictureTaker?>(null);

        _pictures.TakeMissing(_project.Project);
        await _pictures.WhenDone();

        Assert.False(File.Exists(PictureOf(prefab)));
        Assert.False(_pictures.IsTaking);
    }

    // The panel's tiles show the pictures as they're taken, and how far taking them got
    [AvaloniaFact]
    public async Task ThePanelShowsThePicturesAsTheyreTaken()
    {
        var model = _project.Add(new OGI(), "Model");
        Save("Crab", AddObject("CRAB", model.URI).URI);
        var panel = new PrefabsViewModel(new ScenesEditorsViewModel(), new EventAggregator(), _pictures);
        panel.Refresh();
        var entry = Assert.Single(panel.Prefabs);
        Assert.False(entry.HasPreview);
        var changed = new List<string?>();
        entry.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _pictures.TakeMissing(_project.Project);
        await _pictures.WhenDone();
        Dispatcher.UIThread.RunJobs();

        Assert.True(entry.HasPreview);
        Assert.NotNull(entry.Preview);
        Assert.Contains(nameof(PrefabEntry.Preview), changed);
        Assert.Equal(string.Empty, panel.PicturesStatus);
    }
}
