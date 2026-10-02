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
    private readonly IDisposable _creationTask;

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

        _creationTask = RxSchedulers.TaskpoolScheduler.Schedule(this, (_, state) =>
        {
            try
            {
                if (HasViewport)
                {
                    Viewport = new ViewportViewModel();
                }

                _asset.GetData<AbstractAssetData>(); // Load in the asset data
                Document = new DocumentViewModel(_asset, Viewport);

                Document.Initialize();

                if (HasViewport)
                {
                    Viewport!.Init(Document);
                }

                Title = _asset.Name;
                this.WhenAnyValue(x => x.Document!.IsDirty).Subscribe(x => ChangeDisplayName());

                IsLoaded = true;
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

    public async Task<Boolean> CloseTab()
    {
        if (!IsLoaded || Document == null)
        {
            Cleanup();
            return true;
        }
        
        var canClose = await Document.CanCloseDocument();
        if (canClose is DocumentViewModel.DocumentClosing.CloseAndNotSave or DocumentViewModel.DocumentClosing.CloseAndSave)
        {
            if (canClose == DocumentViewModel.DocumentClosing.CloseAndSave)
            {
                SaveTab();
            }
            // Nothing changed stays as it is: other editors may be using the data, and it's let go of once none does
            else if (Document.IsDirty)
            {
                await using System.IO.FileStream fs = new($"{_asset.FullPath}{System.IO.Path.DirectorySeparatorChar}{_asset.Name}.json", System.IO.FileMode.Open, System.IO.FileAccess.Read);
                using System.IO.StreamReader reader = new(fs);
                var json = await reader.ReadToEndAsync();
                _asset.Deserialize(json);
                DiscardData();
            }

            Cleanup();
            return true;
        }

        return false;
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

    public LabURI EditableResource => _asset.URI;

    public string AssetName => _asset.Name;

    public Bitmap IconPath => MiscUtils.GetLabIcon(System.IO.Path.GetFileNameWithoutExtension(_asset.IconPath));
}