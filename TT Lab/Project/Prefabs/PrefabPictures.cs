using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Rendering;
using EditableObject = TT_Lab.Rendering.Objects.EditableObject;
using ObjectInstance = TT_Lab.Assets.Instance.ObjectInstance;
using Scenery = TT_Lab.Assets.Instance.Scenery;
using TT_Lab.Util;

namespace TT_Lab.Project.Prefabs;

/// <summary>
/// What takes the pictures of models, a hidden viewport of its own
/// </summary>
internal interface IPrefabPictureTaker : IDisposable
{
    /// <summary>
    /// The PNG of an object instance drawn with the model (a box for none), none when it couldn't be drawn
    /// </summary>
    Task<byte[]?> TakeAsync(LabURI model);

    /// <summary>
    /// The PNGs of scenery prefabs' model files of the package, their meshes and LODs where the files place them, none for one that couldn't
    /// be drawn
    /// </summary>
    Task<byte[]?[]> TakeSceneryAsync(IReadOnlyList<(LabURI Package, byte[] Model)> models);
}

/// <summary>
/// Pictures of the object instance and scenery prefabs saved without one, the ones made of a project's chunks: the object's model or the
/// meshes on their own, the way saving a prefab from a scene takes its picture, taken in the background, one for every prefab drawn with
/// the same model and a folder's scenery prefabs at once
/// </summary>
public sealed class PrefabPictures
{
    // Frames of three times the picture's size scaled down, like a viewport's frame
    private const int FrameSize = PreviewImage.Size * 3;
    // A stage of its own now and then lets go of the models' buffers and textures it made
    private const int PicturesPerStage = 200;
    // Scenery prefabs taken at once share their data, a folder's materials and textures are read once
    private const int SceneryBatch = 100;

    private readonly object _lock = new();
    private Task _running = Task.CompletedTask;
    private CancellationTokenSource? _cancellation;
    private Project? _project;
    private bool _again;

    /// <summary>
    /// A prefab got its picture, raised off the UI thread
    /// </summary>
    public event Action<Prefab>? Taken;

    /// <summary>
    /// The pictures taken so far or how many there are to take changed, raised off the UI thread
    /// </summary>
    public event Action? ProgressChanged;

    public bool IsTaking { get; private set; }
    public int Done { get; private set; }
    public int Total { get; private set; }

    internal Func<Task<IPrefabPictureTaker?>> StartTaker { get; set; } = StageTaker.StartAsync;

    /// <summary>
    /// Takes the pictures the project's prefabs lack in the background, the ones lacking one once the pictures being taken are done too
    /// </summary>
    public void TakeMissing(Project project)
    {
        lock (_lock)
        {
            if (!_running.IsCompleted && _project == project && _cancellation is { IsCancellationRequested: false })
            {
                _again = true;
                return;
            }

            _project = project;
            _again = false;
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            _running = _running.ContinueWith(_ => RunAsync(project, token), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        }
    }

    /// <summary>
    /// Stops taking pictures, the project is closing
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _cancellation?.Cancel();
            _again = false;
        }
    }

    /// <summary>
    /// Done once the pictures asked for are taken or stopped
    /// </summary>
    public Task WhenDone()
    {
        lock (_lock)
        {
            return _running;
        }
    }

    private bool TakeAgain()
    {
        lock (_lock)
        {
            var again = _again;
            _again = false;
            return again;
        }
    }

    private async Task RunAsync(Project project, CancellationToken token)
    {
        var start = DateTime.Now;
        var taken = 0;
        try
        {
            do
            {
                taken += await TakeAsync(project, token);
            }
            while (TakeAgain() && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
        }
        // The project closing takes its assets away from under the pictures
        catch (Exception) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Log.WriteLine($"The prefabs' pictures couldn't be taken: {e.Message}", Log.LogType.Warning);
            Log.WriteLine(e.ToString(), Log.LogType.Debug);
        }
        finally
        {
            IsTaking = false;
            ProgressChanged?.Invoke();
        }

        if (taken > 0)
        {
            Log.WriteLine($"Took the pictures of {taken} prefabs in {DateTime.Now - start}", Log.LogType.Info);
        }
    }

    // The pictures of the prefabs without one, how many got one comes back
    private async Task<int> TakeAsync(Project project, CancellationToken token)
    {
        var library = new PrefabLibrary(project);
        var (looks, sceneries) = await Task.Run(() => Missing(library), token);
        var total = looks.Sum(look => look.Prefabs.Count) + sceneries.Sum(batch => batch.Count);
        if (total == 0)
        {
            return 0;
        }

        Done = 0;
        Total = total;
        IsTaking = true;
        ProgressChanged?.Invoke();
        Log.WriteLine($"Taking the pictures of {Total} prefabs in the background...", Log.LogType.Info);
        IPrefabPictureTaker? taker = null;
        var onTaker = 0;
        var taken = 0;
        try
        {
            foreach (var (model, prefabs) in looks)
            {
                token.ThrowIfCancellationRequested();
                if (await TakerFor(1) is not { } current)
                {
                    return taken;
                }

                byte[]? png = null;
                try
                {
                    png = await current.TakeAsync(model);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Log.WriteLine($"The picture of {prefabs[0].Name} couldn't be taken: {e.Message}", Log.LogType.Debug);
                }

                token.ThrowIfCancellationRequested();
                taken += Save(library, prefabs, png);
                ProgressChanged?.Invoke();
            }

            foreach (var batch in sceneries)
            {
                token.ThrowIfCancellationRequested();
                if (await TakerFor(batch.Count) is not { } current)
                {
                    return taken;
                }

                // Read when their batch's turn comes, there can be thousands
                var models = batch.Select(prefab => (Prefab: prefab, Model: PrefabLibrary.ModelOf(prefab))).ToList();
                var pictures = new byte[]?[models.Count];
                try
                {
                    var drawn = models.Where(model => model.Model != null).ToList();
                    var drawnPictures = await current.TakeSceneryAsync(drawn.Select(model => (new LabURI(model.Prefab.Package), model.Model!)).ToList());
                    for (var i = 0; i < drawn.Count; i++)
                    {
                        pictures[models.IndexOf(drawn[i])] = drawnPictures[i];
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Log.WriteLine($"The pictures of the prefabs of {batch[0].Folder} couldn't be taken: {e.Message}", Log.LogType.Debug);
                }

                token.ThrowIfCancellationRequested();
                for (var i = 0; i < batch.Count; i++)
                {
                    taken += Save(library, [batch[i]], pictures[i]);
                }

                ProgressChanged?.Invoke();
            }
        }
        finally
        {
            taker?.Dispose();
        }

        return taken;

        // The stage gets made again once it took its share of pictures
        async Task<IPrefabPictureTaker?> TakerFor(int pictures)
        {
            if (taker == null || onTaker > 0 && onTaker + pictures > PicturesPerStage)
            {
                taker?.Dispose();
                taker = await StartTaker();
                onTaker = 0;
                if (taker == null)
                {
                    Log.WriteLine("The prefabs' pictures can't be taken, no viewport could be made for them", Log.LogType.Warning);
                    return null;
                }
            }

            onTaker += pictures;
            return taker;
        }
    }

    // The picture next to every prefab still there to have it, how many got it comes back
    private int Save(PrefabLibrary library, IEnumerable<Prefab> prefabs, byte[]? png)
    {
        var saved = 0;
        foreach (var prefab in prefabs)
        {
            if (png != null && library.SavePicture(prefab, png))
            {
                saved++;
                Taken?.Invoke(prefab);
            }

            Done++;
        }

        return saved;
    }

    /// <summary>
    /// The prefabs without a picture that can get one, found by their files: object instances by the model their object is drawn with
    /// (<see cref="Looks"/>), scenery prefabs in batches of a folder's
    /// </summary>
    internal static (List<(LabURI Model, List<Prefab> Prefabs)> Looks, List<List<Prefab>> Sceneries) Missing(PrefabLibrary library)
    {
        var prefabs = library.Load(library.FilesWithoutPicture());
        var sceneries = prefabs.Where(prefab => prefab.Kind == PrefabKind.Scenery && prefab.FilePath != null && (prefab.Model != null || prefab.ModelPath != null))
            .GroupBy(prefab => prefab.Folder, StringComparer.Ordinal)
            .SelectMany(folder => folder.Chunk(SceneryBatch))
            .Select(batch => batch.ToList())
            .ToList();
        return (Looks(prefabs), sceneries);
    }

    /// <summary>
    /// The object instance prefabs without a picture by the model their object's instances are drawn with, none for a box. The prefabs
    /// of objects the project doesn't have are left out
    /// </summary>
    internal static List<(LabURI Model, List<Prefab> Prefabs)> Looks(IEnumerable<Prefab> prefabs)
    {
        var assetManager = AssetManager.Get();
        using var scope = new AssetDataScope();
        var models = new Dictionary<LabURI, LabURI?>();
        var looks = new Dictionary<LabURI, List<Prefab>>();
        foreach (var prefab in prefabs.Where(NeedsPicture))
        {
            var objectUri = prefab.Data[nameof(ObjectInstanceData.ObjectId)]?.ToObject<LabURI>();
            if (objectUri == null)
            {
                continue;
            }

            if (!models.TryGetValue(objectUri, out var model))
            {
                model = assetManager.DoesAssetExist(objectUri) && assetManager.GetAsset(objectUri) is GameObject gameObject
                    ? ObjectInstanceData.ModelOf(((IAsset)gameObject).GetData<GameObjectData>())
                    : null;
                if (model != null && model != LabURI.Empty && !assetManager.DoesAssetExist(model))
                {
                    model = null;
                }

                models[objectUri] = model;
            }

            if (model == null)
            {
                continue;
            }

            if (!looks.TryGetValue(model, out var list))
            {
                looks[model] = list = [];
            }

            list.Add(prefab);
        }

        return [.. looks.Select(look => (look.Key, look.Value))];
    }

    private static bool NeedsPicture(Prefab prefab) => prefab.PreviewPath == null && prefab.FilePath != null && prefab.Kind == PrefabKind.Instance
                                                       && prefab.AssetType == typeof(ObjectInstance).FullName;

    // Pictures taken on a hidden viewport, drawn the way a chunk's viewport draws an instance of the object
    private sealed class StageTaker(PreviewStage stage) : IPrefabPictureTaker
    {
        public static async Task<IPrefabPictureTaker?> StartAsync()
        {
            var stage = await PreviewStage.StartAsync(FrameSize);
            return stage == null ? null : new StageTaker(stage);
        }

        public Task<byte[]?[]> TakeSceneryAsync(IReadOnlyList<(LabURI Package, byte[] Model)> models)
        {
            return stage.TakeManyAsync(models.Select(model => (Func<RenderContext, IReadOnlyList<EditableObject>>)(context => SceneryVisuals(context, model.Package, model.Model))).ToList(),
                PreviewImage.Size);
        }

        // The prefab's meshes and LODs drawn the way a scenery draws them, read for a scenery of their package nobody has
        private static IReadOnlyList<EditableObject> SceneryVisuals(RenderContext context, LabURI package, byte[] model)
        {
            var owner = new Scenery { Package = package, InvariantName = "Prefab picture", Alias = "Prefab picture", Chunk = string.Empty };
            using var stream = new MemoryStream(model);
            var placements = new SceneryData(owner).ReadPlacements(TlmFile.Read(stream), Vector3.Zero);
            return placements.Select((placement, index) => SceneryData.CreatePlacementVisual(context, placement, $"PREFAB_PICTURE_{index}")).OfType<EditableObject>().ToList();
        }

        public Task<byte[]?> TakeAsync(LabURI model)
        {
            return stage.TakeAsync(context => ObjectInstanceData.CreateVisual(context, model, "PREFAB_PICTURE"), PreviewImage.Size);
        }

        public void Dispose() => stage.Dispose();
    }
}
