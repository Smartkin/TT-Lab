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
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views;

namespace TT_Lab.ViewModels.Editors;

public partial class UriLinkViewModel : DocumentDataViewModel<LabURI>
{
    [ObservableAsProperty]
    private string _linkText = "Empty";

    public enum Scope
    {
        Project,
        Document
    }
    
    public UriLinkViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies) : base(document, data, dependencies)
    {
        var assetManager = AssetManager.Get();
        
        _linkTextHelper = this.WhenAnyValue(x => x.CurrentValue)
            .WhereNotNull()
            .Select(uri => uri == LabURI.Empty ? "Empty" : assetManager.GetAsset(uri).Alias)
            .ToProperty(this, x => x.LinkText, scheduler: ReactiveUI.Avalonia.AvaloniaScheduler.Instance)
            .DisposeWith(FullDeactivationDisposables);
    }

    protected override void ApplyEditorAttributes()
    {
        base.ApplyEditorAttributes();
        
        _browseType = GetEditorParameter(BrowseType, typeof(IAsset))!;
        if (_browseType == typeof(IAsset) && CurrentValue != LabURI.Empty)
        {
            var asset = AssetManager.Get().GetAsset(CurrentValue!);
            _browseType = asset.GetType();
        }
        
        _browseScope = GetEditorParameter(BrowseScope, Scope.Project);
        _openInInspector = GetEditorParameter(OpenInInspector, false);
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        
        this.WhenAnyValue(x => x.CurrentValue)
            .WhereNotNull()
            .Where(uri => uri != CurrentValue)
            .Subscribe(uri =>
            {
                Document.RemoveResource(CurrentValue!);
                SetValueCommand.Execute(uri);
                Document.AddResource(CurrentValue!);
            }).DisposeWith(disposables);
    }

    [ReactiveCommand]
    private async Task<LabURI> SelectUriFromLink()
    {
        var uri = CurrentValue;
        var resourcesToBrowse = new List<LabURI>();
        switch (_browseScope)
        {
            case Scope.Project:
                resourcesToBrowse.AddRange(AssetManager.Get().GetAllAssetUrisOf(_browseType));
                break;
            case Scope.Document:
                resourcesToBrowse.AddRange(Document.Uris);
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

        return uri!;
    }

    [ReactiveCommand]
    private void OpenDocument()
    {
        var uri = CurrentValue;
        if (uri == LabURI.Empty)
        {
            return;
        }

        if (_openInInspector)
        {
            Document.OpenInspector(Property.Find("[data]"));
        }
        else
        {
            var shell = Locator.Current.GetService<ILabManager>()!;
            shell.OpenEditor(AssetManager.Get().GetAsset(uri!));
        }
    }

    public const string BrowseType = "URI_LINK_FIELD_BROWSE_TYPE_NAME";
    public const string BrowseScope = "URI_LINK_FIELD_BROWSE_SCOPE";
    public const string OpenInInspector = "URI_LINK_FIELD_OPEN_IN_INSPECTOR";
    
    private Type _browseType = typeof(IAsset);
    private Scope _browseScope = Scope.Project;
    private bool _openInInspector = false;
}