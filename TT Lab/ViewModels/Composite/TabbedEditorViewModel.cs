using Caliburn.Micro;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Composite;

public class TabbedEditorViewModel : Conductor<IEditorViewModel>
{
    private IAsset _asset;

    public TabbedEditorViewModel(IAsset asset)
    {
        _asset = asset;
        Document = new DocumentViewModel(_asset);
        _ = new DocumentViewModel(_asset.GetData<AbstractAssetData>())
        {
            Parent = Document,
        };
        
        this.WhenAnyValue(x => x.Document.IsDirty).Subscribe(x => ChangeDisplayName());
    }

    private void ChangeDisplayName()
    {
        DisplayName = Document.IsDirty ? $"{_asset.Name}*" : _asset.Name;
        NotifyOfPropertyChange(nameof(DisplayName));
    }

    public void SaveTab()
    {
        Document.Save(string.Empty);
    }

    public async Task CloseTab()
    {
        var canClose = await Document.CanClose();
        if (canClose is DocumentViewModel.DocumentClosing.CloseAndNotSave or DocumentViewModel.DocumentClosing.CloseAndSave)
        {
            if (canClose == DocumentViewModel.DocumentClosing.CloseAndSave)
            {
                SaveTab();
            }
            
            await this.DeactivateAsync(true);
        }
    }

    public LabURI EditableResource => _asset.URI;

    public Bitmap IconPath => new(ManifestResourceLoader.GetPathInExe($"Media/LabIcons/{_asset.IconPath}"));
    
    public DocumentViewModel Document { get; }
}