using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views;

namespace TT_Lab.ViewModels.Editors;

public partial class UriLinkViewModel : DocumentDataViewModel<LabURI>
{
    private readonly ObservableAsPropertyHelper<string> _linkText;
    private ObservableAsPropertyHelper<LabURI?> _uri;
    private LabURI? Uri => _uri?.Value;
    
    public string LinkText => _linkText.Value;
    
    
    public UriLinkViewModel(DocumentViewModel document, LabURI data) : base(document, data)
    {
        var assetManager = AssetManager.Get();
        
        _linkText = this.WhenAnyValue(x => x.Uri)
            .Select(uri => uri ?? InitialData)
            .Select(uri => uri == LabURI.Empty ? "Empty" : assetManager.GetAsset(uri).Alias)
            .ToProperty(this, x => x.LinkText, scheduler: ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);
        
        _uri = SelectUriFromLinkCommand.ToProperty(this, x => x.Uri, scheduler: ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
        
        _uri.DisposeWith(disposables);
        _linkText.DisposeWith(disposables);

        if (EditorParameters.TryGetValue(BrowseType, out var browseType))
        {
            _browseType = (Type)browseType;
        }

        this.WhenAnyValue(x => x.Uri)
            .WhereNotNull()
            .Where(uri => uri != Data)
            .Subscribe(uri =>
            {
                Data = uri;
            }).DisposeWith(disposables);
    }

    [ReactiveCommand]
    private async Task<LabURI> SelectUriFromLink()
    {
        var uri = Uri ?? InitialData;
        var linkBrowser = new ResourceBrowserViewModel(_browseType, uri);
        
        var linkBrowserDialogue = new ResourceBrowserView
        {
            DataContext = linkBrowser
        };
        var result = await linkBrowserDialogue.ShowDialog<bool?>(MiscUtils.GetMainWindow());
        if (result.HasValue && result.Value)
        {
            return linkBrowser.SelectedLink;
        }

        return uri;
    }

    [ReactiveCommand]
    private void OpenDocument()
    {
        var uri = Uri ?? InitialData;
        if (uri == LabURI.Empty)
        {
            return;
        }
        
        var shell = Locator.Current.GetService<ILabManager>()!;
        shell.OpenEditor(AssetManager.Get().GetAsset(uri));
    }

    public const string BrowseType = "URI_LINK_FIELD_BROWSE_TYPE_NAME";
    
    private Type _browseType = typeof(IAsset);
}