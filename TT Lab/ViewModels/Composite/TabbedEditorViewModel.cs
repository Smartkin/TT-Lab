using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
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
            else
            {
                await using System.IO.FileStream fs = new($"{_asset.FullPath}{System.IO.Path.DirectorySeparatorChar}{_asset.Name}.json", System.IO.FileMode.Open, System.IO.FileAccess.Read);
                using System.IO.StreamReader reader = new(fs);
                var json = await reader.ReadToEndAsync();
                _asset.Deserialize(json);
                _asset.Dispose();
            }

            Cleanup();
            return true;
        }

        return false;
    }

    private void Cleanup()
    {
        if (_asset is LevelChunk chunk)
        {
            TwinIdGeneratorServiceProvider.DeregisterGeneratorServiceForChunk(chunk.AdditionalPath!);
        }
        
        Viewport?.Close();
        _creationTask.Dispose();
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