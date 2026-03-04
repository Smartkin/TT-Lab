using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views;

namespace TT_Lab.ViewModels.Editors;

public partial class UriLinkViewModel : DocumentDataViewModel<LabURI>
{
    private readonly ObservableAsPropertyHelper<string> _linkText;
    private readonly ObservableAsPropertyHelper<bool> _hasDocument;
    private ObservableAsPropertyHelper<LabURI?> _uri;
    private LabURI? Uri => _uri?.Value;

    public enum Scope
    {
        Project,
        Document
    }
    
    public string LinkText => _linkText.Value;
    public bool HasDocument => _hasDocument.Value;
    
    public UriLinkViewModel(DocumentViewModel document, LabURI data) : base(document, data)
    {
        var assetManager = AssetManager.Get();
        
        _linkText = this.WhenAnyValue(x => x.Uri)
            .Select(uri => uri ?? InitialData)
            .Select(uri => uri == LabURI.Empty ? "Empty" : assetManager.GetAsset(uri).Alias)
            .ToProperty(this, x => x.LinkText, scheduler: ReactiveUI.Avalonia.AvaloniaScheduler.Instance);

        _hasDocument = this.WhenAnyValue(x => x.Uri)
            .Select(uri => uri ?? InitialData)
            .Select(uri => uri != LabURI.Empty)
            .ToProperty(this, x => x.HasDocument);
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);
        
        _uri = SelectUriFromLinkCommand.ToProperty(this, x => x.Uri, scheduler: ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
        
        _uri.DisposeWith(disposables);
        _linkText.DisposeWith(disposables);
        _hasDocument.DisposeWith(disposables);

        _browseType = GetEditorParameter(BrowseType, typeof(IAsset))!;
        _browseScope = GetEditorParameter(BrowseScope, Scope.Project);
        
        if (_browseType == typeof(IAsset) && Data != LabURI.Empty)
        {
            var asset = AssetManager.Get().GetAsset(Data);
            _browseType = asset.GetType();
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
        var resourcesToBrowse = new List<LabURI>();
        switch (_browseScope)
        {
            case Scope.Project:
                resourcesToBrowse.AddRange(AssetManager.Get().GetAllAssetUrisOf(_browseType));
                break;
            case Scope.Document:
            {
                var parent = Document.GetViewModel(Document.SaveLocation);
                if (parent.HasValue)
                {
                    resourcesToBrowse.AddRange((IEnumerable<LabURI>)parent.Value.GetFinalData()!);
                }
            }
                break;
        }
        var linkBrowser = new ResourceBrowserViewModel(_browseType, resourcesToBrowse, uri);
        
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
    public const string BrowseScope = "URI_LINK_FIELD_BROWSE_SCOPE";
    
    private Type _browseType = typeof(IAsset);
    private Scope _browseScope = Scope.Project;
    
}