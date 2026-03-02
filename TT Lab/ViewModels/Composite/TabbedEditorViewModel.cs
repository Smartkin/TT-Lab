using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Dock.Model.Core;
using ReactiveUI;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels.Composite;

public class TabbedEditorViewModel : ReactiveObject
{
    private readonly IFactory _factory;
    private IAsset _asset;

    public TabbedEditorViewModel(IFactory factory, IAsset asset)
    {
        _factory = factory;
        _asset = asset;
        
        Document = new DocumentViewModel(null, _asset)
        {
            Caption = _asset.Alias,
            IsExpanded = true
        };
        var assetData = _asset.GetData<AbstractAssetData>();
        if (assetData is not DummyData)
        {
            var dataDocument = new DocumentViewModel(Document, assetData)
            {
                Caption = "Data",
                IsExpanded = true
            };
            Document.AddDocument(dataDocument);
        }

        if (_asset.GetType().GetCustomAttribute<SupportsViewportAttribute>() != null)
        {
            Viewport = Locator.Current.GetService<ViewportViewModel>();
        }

        this.WhenAnyValue(x => x.Document.IsDirty).Subscribe(x => ChangeDisplayName());
    }

    private void ChangeDisplayName()
    {
        Title = Document.IsDirty ? $"{_asset.Name}*" : _asset.Name;
        this.RaisePropertyChanged(nameof(Title));
    }

    public void SaveTab()
    {
        Document.Save();
    }

    public async Task<Boolean> CloseTab()
    {
        var canClose = await Document.CanCloseDocument();
        if (canClose is DocumentViewModel.DocumentClosing.CloseAndNotSave or DocumentViewModel.DocumentClosing.CloseAndSave)
        {
            if (canClose == DocumentViewModel.DocumentClosing.CloseAndSave)
            {
                SaveTab();
            }

            return true;
        }

        return false;
    }
    
    public string Id => _asset.URI;

    public bool CanPin => true;

    public string Title { get; set; } = "NO NAME TAB";

    public LabURI EditableResource => _asset.URI;

    public Bitmap IconPath => new(ManifestResourceLoader.GetPathInExe($"Media/LabIcons/{_asset.IconPath}"));
    
    public DocumentViewModel Document { get; }
    
    public ViewportViewModel? Viewport { get; private set; }
}