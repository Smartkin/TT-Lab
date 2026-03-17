using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Dock.Model.Core;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels.Composite;

public partial class TabbedEditorViewModel : ReactiveObject
{
    [Reactive]
    private string _title = "NO NAME TAB";

    [Reactive]
    private bool _isLoaded;
    
    [Reactive(SetModifier = AccessModifier.Private)]
    private DocumentViewModel? _document;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ViewportViewModel? _viewport;
    
    private readonly IFactory _factory;
    private readonly IAsset _asset;
    private readonly IDisposable _creationTask;

    public TabbedEditorViewModel(IFactory factory, IAsset asset)
    {
        _factory = factory;
        _asset = asset;
        Title = $"{_asset.Name} (Loading)";

        _creationTask = RxSchedulers.TaskpoolScheduler.Schedule(this, (_, state) =>
        {
            _asset.GetData<AbstractAssetData>(); // Load in the asset data
            Document = new DocumentViewModel(_asset);
        
            Document.Initialize();
            
            if (_asset.GetType().GetCustomAttribute<SupportsViewportAttribute>() != null)
            {
                Viewport = new ViewportViewModel();
                Viewport.Init(Document);
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

    public async Task<Boolean> CloseTab()
    {
        if (!IsLoaded || Document == null)
        {
            Viewport?.Close();
            _creationTask.Dispose();
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

            Viewport?.Close();
            _creationTask.Dispose();
            return true;
        }

        return false;
    }
    
    public string Id => _asset.URI;

    public bool CanPin => true;

    public LabURI EditableResource => _asset.URI;

    public Bitmap IconPath => new(ManifestResourceLoader.GetPathInExe($"Media/LabIcons/{_asset.IconPath}"));
}