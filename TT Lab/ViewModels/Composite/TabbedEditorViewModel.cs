using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels.Composite;

public abstract partial class TabbedEditorViewModel : Document
{
    [Reactive]
    private bool _isLoaded;
    
    [Reactive(SetModifier = AccessModifier.Private)]
    private DocumentViewModel? _document;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ViewportViewModel? _viewport;

    [Reactive] private GridLength _viewportWidth = new(0);

    public bool HasViewport { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    
    private readonly IAsset _asset;
    private IDisposable _creationTask;

    protected TabbedEditorViewModel(IAsset asset)
    {
        _asset = asset;
        Id = asset.URI;
        Title = $"{_asset.Name} (Loading)";

        if (asset is LevelChunk chunk)
        {
            TwinIdGeneratorServiceProvider.RegisterGeneratorServiceForChunk(chunk);
        }

        SaveCommand = ReactiveCommand.Create(SaveTab);
        CloseCommand = ReactiveCommand.Create(RequestClose);
        UndoCommand = ReactiveCommand.Create(() => Document?.Undo());
        RedoCommand = ReactiveCommand.Create(() => Document?.Redo());

        HasViewport = asset.SupportsViewport;
        if (HasViewport)
        {
            ViewportWidth = new GridLength(5, GridUnitType.Star);
        }

        // Whichever document it has, being made again gives it another
        this.WhenAnyValue(x => x.Document!.IsDirty).Subscribe(_ => ChangeDisplayName());
        _creationTask = StartLoading(null);
    }

    /// <summary>
    /// What a tab made again keeps: where a chunk's camera was, the path of what the inspector showed and what the document carries over
    /// </summary>
    internal sealed record KeptState(GlmSharp.mat4? View, string? Inspected, DocumentViewModel.Carried? Carried);

    // Reads the asset's data and makes the document and the viewport in the background, made again it's the way it was kept
    private IDisposable StartLoading(KeptState? kept)
    {
        return RxSchedulers.TaskpoolScheduler.Schedule(this, (_, state) =>
        {
            try
            {
                if (HasViewport)
                {
                    var viewport = new ViewportViewModel();
                    viewport.BeginLoading(_asset);
                    if (kept?.View is { } view)
                    {
                        viewport.KeepView(view);
                    }

                    Viewport = viewport;
                }

                _asset.GetData<AbstractAssetData>(); // Load in the asset data
                var document = new DocumentViewModel(_asset, Viewport, kept?.Carried?.Overrides);
                if (kept?.Carried is { } carried)
                {
                    document.TakeOver(carried);
                }

                Document = document;
                Document.Initialize();

                if (HasViewport)
                {
                    Viewport!.Init(Document);
                }

                ChangeDisplayName();
                IsLoaded = true;
                if (kept?.Inspected is { } inspected)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (Document == document && document.PropertyGraph.Find(inspected) is { } node)
                        {
                            document.OpenInspector(node);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Nothing observes the task, the tab stayed loading for good
                Log.WriteLine($"{_asset.Name} couldn't be opened: {ex.Message}", Log.LogType.Error);
                Document = null;
                Dispatcher.UIThread.Post(RequestClose);
            }

            return Disposable.Empty;
        });
    }

    /// <summary>
    /// The first half of making the editor again, another program changed files it shows: the tab lets go of its document and viewport and
    /// stays where it is, loading. Its unsaved changes are discarded when told to (they were in what got changed, the user said to), like
    /// closing it without saving does, else the new document takes them over: they're in the data, which stays loaded but for what's read
    /// again. Gives what <see cref="LoadAgain"/> keeps
    /// </summary>
    internal async Task<KeptState?> Unload(bool discardChanges, IReadOnlySet<LabURI> reread, string rereadNames)
    {
        if (!IsLoaded || Document == null)
        {
            return null;
        }

        var view = Viewport?.CurrentView;
        KeptState kept;
        if (discardChanges && Document.IsDirty)
        {
            // What's inspected can be somewhere else among what the discarded changes put in or took out
            kept = new KeptState(view, null, null);
            try
            {
                await RevertToSaved();
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            {
                // Its data still goes, what's read again is the other program's
                Log.WriteLine($"{_asset.Name}'s saved file couldn't be read back: {ex.Message}", Log.LogType.Warning);
            }

            if (_asset is LevelChunk chunk)
            {
                ForgetUnplacedInstances(chunk);
            }
        }
        else
        {
            kept = new KeptState(view, Document.Inspector?.Property.Path, Document.Carry(reread, rereadNames));
        }

        Viewport?.Close();
        _creationTask.Dispose();
        IsLoaded = false;
        Document = null;
        Viewport = null;
        Title = $"{_asset.Name} (Loading)";
        return kept;
    }

    internal void LoadAgain(KeptState? kept)
    {
        _creationTask = StartLoading(kept);
    }

    private void ChangeDisplayName()
    {
        if (Document == null)
        {
            return;
        }
        
        Title = Document.IsDirty ? $"{_asset.Name}*" : _asset.Name;
    }

    public void SaveTab()
    {
        Document?.Save();
    }

    // Goes through the tab's factory the same way the tab's close button does, so unsaved changes are asked about first
    public void RequestClose()
    {
        Factory?.CloseDockable(this);
    }

    // Discarding the changes without asking when every editor's were asked about at once (closing them all)
    public async Task<Boolean> CloseTab(bool discardChanges = false)
    {
        if (!IsLoaded || Document == null)
        {
            Cleanup();
            return true;
        }
        
        var canClose = discardChanges ? DocumentViewModel.DocumentClosing.CloseAndNotSave : await Document.CanCloseDocument();
        if (canClose is DocumentViewModel.DocumentClosing.CloseAndNotSave or DocumentViewModel.DocumentClosing.CloseAndSave)
        {
            if (canClose == DocumentViewModel.DocumentClosing.CloseAndSave)
            {
                SaveTab();
            }
            // Nothing changed stays as it is: other editors may be using the data, and it's let go of once none does
            else if (Document.IsDirty)
            {
                await RevertToSaved();
            }

            Cleanup();
            return true;
        }

        return false;
    }

    // The asset as it was saved: its file read again and its data let go of
    private async Task RevertToSaved()
    {
        await using System.IO.FileStream fs = new($"{_asset.FullPath}{System.IO.Path.DirectorySeparatorChar}{_asset.Name}.json", System.IO.FileMode.Open, System.IO.FileAccess.Read);
        using System.IO.StreamReader reader = new(fs);
        var json = await reader.ReadToEndAsync();
        _asset.Deserialize(json);
        DiscardData();
    }

    // The changes go by loading the data again. Data another open editor uses stays with it: a chunk's view of a behaviour, a model, a
    // picture or a sound shares its asset's data, and disposing it emptied what the chunk showed (a behaviour's script). A chunk's own
    // resources go back with it, chunks linking it reference it as well
    private void DiscardData()
    {
        var editors = Locator.Current.GetService<EditorsViewModel>();
        var usedElsewhere = _asset is not LevelChunk && editors != null && editors.ScenesEditorsViewModel.Tabs.Concat(editors.ResourcesEditorsViewModel.Tabs)
            .Any(tab => tab != this && tab.IsLoaded && tab.GetReferencedAssets().Contains(_asset.URI));
        if (usedElsewhere)
        {
            _asset.UnloadData();
            return;
        }

        _asset.Dispose();
    }

    private void Cleanup()
    {
        if (_asset is LevelChunk chunk)
        {
            TwinIdGeneratorServiceProvider.DeregisterGeneratorServiceForChunk(chunk.AdditionalPath!);
            ForgetUnplacedInstances(chunk);
        }
        
        Viewport?.Close();
        _creationTask.Dispose();
    }

    // Instances made for the chunk (placed, copied, from prefabs) that it doesn't list once it's closed, their placing undone or the
    // chunk closed without saving, never got files: they'd stay in the project tree without anything to open
    private static void ForgetUnplacedInstances(LevelChunk chunk)
    {
        var listed = chunk.ChunkResources.ToHashSet();
        var unplaced = AssetManager.Get().GetAssets()
            .OfType<SerializableInstance>()
            .Where(asset => asset.IsUnsaved && asset.Package == chunk.Package && asset.Chunk == chunk.AdditionalPath && !listed.Contains(asset.URI))
            .ToList();
        foreach (var asset in unplaced)
        {
            AssetDeletion.ForgetUnsaved(asset);
        }
    }
    
    public IEnumerable<LabURI> GetReferencedAssets()
    {
        yield return _asset.URI;
        if (Document == null)
        {
            yield break;
        }

        foreach (var uri in Document.PropertyGraph.Tracker.ExploredUris)
        {
            yield return uri;
        }
    }

    // What its document reads or its scene draws, the textures and materials a scene draws are no part of the document
    internal bool Shows(IReadOnlySet<LabURI> assets) => GetReferencedAssets().Any(assets.Contains) || (Viewport != null && assets.Any(Viewport.HasRead));

    public LabURI EditableResource => _asset.URI;

    public string AssetName => _asset.Name;

    public Bitmap IconPath => MiscUtils.GetLabIcon(System.IO.Path.GetFileNameWithoutExtension(_asset.IconPath));
}