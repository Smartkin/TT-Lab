using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views;

namespace TT_Lab.ViewModels.Editors;

public partial class UriLinkViewModel : DocumentDataViewModel<LabURI>
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private string _linkText = "Empty";

    /// <summary>
    /// What a link field says when the asset it links is in a package the one its value is kept in doesn't depend on
    /// </summary>
    public const string MissingDependencyWarning = "The referenced asset belongs to a package that the current one doesn't depend on!";

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isMissingDependency;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _addDependencyHint = string.Empty;

    /// <summary>
    /// What the warning sign says: the missing dependency, or why the other version's asset can't be used at all
    /// </summary>
    [Reactive(SetModifier = AccessModifier.Private)]
    private string _missingDependencyText = MissingDependencyWarning;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canAddDependency;

    private (Package Current, Package Referenced)? _missingDependency;

    public enum Scope
    {
        Project,
        Document,
        /// <summary>
        /// The assets of the chunk the edited asset belongs to (an instance's other instances, positions and paths)
        /// </summary>
        Chunk
    }
    
    public UriLinkViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies) : base(document, data, dependencies)
    {
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
        _browseExcludeWhen = GetEditorParameter<string>(BrowseExcludeWhen);
        _browseExcludeOwnerChunk = GetEditorParameter(BrowseExcludeOwnerChunk, false);
        _openInInspector = GetEditorParameter(OpenInInspector, false);
        _isIncludeEmpty = GetEditorParameter(IncludeEmpty, false);
    }

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        
        UpdateLinkText();
        UpdateIsOverridden();
        UpdateMissingDependency();
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        SelectUriFromLinkCommand.InvokeCommand(SetValueCommand).DisposeWith(disposables);
        PackageDependencies.Changed += UpdateMissingDependency;
        Disposable.Create(() => PackageDependencies.Changed -= UpdateMissingDependency).DisposeWith(disposables);

        UpdateLinkText();
        UpdateMissingDependency();
    }

    private void UpdateMissingDependency()
    {
        // Game objects, their instances and behaviours of the other version can't be used whatever the dependencies
        if (WhyNotOfThisVersion() is { } why)
        {
            _missingDependency = null;
            IsMissingDependency = true;
            CanAddDependency = false;
            MissingDependencyText = why;
            AddDependencyHint = string.Empty;
            return;
        }

        _missingDependency = FindMissingDependency();
        IsMissingDependency = _missingDependency != null;
        CanAddDependency = _missingDependency != null;
        MissingDependencyText = MissingDependencyWarning;
        AddDependencyHint = _missingDependency is { } missing
            ? $"Makes {missing.Current.Alias} depend on {missing.Referenced.Alias}, which {LinkText} belongs to. {missing.Current.Alias} gets saved right away and it can't be undone"
            : string.Empty;
    }

    private string? WhyNotOfThisVersion()
    {
        var uri = CurrentValue;
        var assets = AssetManager.Get();
        if (uri == null || uri == LabURI.Empty || !assets.DoesAssetExist(uri))
        {
            return null;
        }

        return AssetVersions.WhyNotUsableBy(CurrentPackage()?.URI, assets.GetAsset(uri));
    }

    private (Package Current, Package Referenced)? FindMissingDependency()
    {
        var uri = CurrentValue;
        var assets = AssetManager.Get();
        if (uri == null || uri == LabURI.Empty || !assets.DoesAssetExist(uri))
        {
            return null;
        }

        var referenced = assets.GetAsset(uri);
        var referencedPackage = referenced.Package;
        if (referenced is Package || referencedPackage == null || CurrentPackage() is not { } current || assets.IsOwnOrDependency(current.URI, referencedPackage)
            || !assets.DoesAssetExist(referencedPackage) || assets.GetAsset(referencedPackage) is not Package package)
        {
            return null;
        }

        return (current, package);
    }

    private Package? CurrentPackage() => PackageKeeping(Property);

    /// <summary>
    /// The package a link's value is kept in: its asset's, or for a chunk's view of an asset it shares the chunk's, which keeps the value as
    /// its own
    /// </summary>
    internal static Package? PackageKeeping(PropertyNode link)
    {
        var assets = AssetManager.Get();
        for (var node = link.Parent; node != null; node = node.Parent)
        {
            if (node.Target is not IAsset asset)
            {
                continue;
            }

            var package = asset is SerializableAsset { OverriddenAsset: not null } && link.Graph?.Overrides is { } overrides ? overrides.Chunk.Package : asset.Package;
            return package != null && assets.DoesAssetExist(package) && assets.GetAsset(package) is Package found ? found : null;
        }

        return null;
    }

    [ReactiveCommand]
    private async Task AddDependency()
    {
        if (_missingDependency is not { } missing)
        {
            return;
        }

        var answer = await ValuesPaste.Ask("Add a package dependency",
            $"{missing.Current.Alias} gets to depend on {missing.Referenced.Alias} and is saved right away. This can't be undone.", ["Add the dependency"]);
        if (answer == 0)
        {
            PackageDependencies.Add(missing.Current, missing.Referenced);
        }
    }

    private void UpdateLinkText()
    {
        var assetManager = AssetManager.Get();
        if (CurrentValue == null || CurrentValue == LabURI.Empty)
        {
            LinkText = "Empty";
        }
        else
        {
            LinkText = assetManager.GetAsset(CurrentValue).Alias;
        }
    }

    /// <summary>
    /// What the link browser offers: the assets of the link's type in its scope, without the excluded ones
    /// </summary>
    internal List<LabURI> GetBrowseCandidates()
    {
        var assetManager = AssetManager.Get();
        var resourcesToBrowse = new List<LabURI>();
        switch (_browseScope)
        {
            case Scope.Project:
                // A link of no particular type offers everything, the browser tells the types apart
                resourcesToBrowse.AddRange(_browseType == typeof(IAsset) ? assetManager.GetAllAssetUris() : assetManager.GetAllAssetUrisOf(_browseType));
                break;
            case Scope.Document:
                resourcesToBrowse.AddRange(Document.Uris);
                break;
            case Scope.Chunk:
            {
                var (package, chunk) = GetOwnerChunkPath();
                resourcesToBrowse.AddRange(assetManager.GetAllAssetUrisOf(_browseType)
                    .Where(uri => assetManager.GetAsset(uri) is SerializableInstance instance && instance.Package == package && instance.Chunk == chunk));
                break;
            }
        }

        if (_browseExcludeWhen != null)
        {
            resourcesToBrowse.RemoveAll(IsExcludedFromBrowsing);
        }

        if (_browseExcludeOwnerChunk)
        {
            var ownerChunk = GetOwnerChunk();
            resourcesToBrowse.RemoveAll(link => link != LabURI.Empty && link == ownerChunk);
        }

        // The other version's assets are offered but for the game objects, instances and behaviours, which only their version uses
        if (CurrentPackage() is { } current)
        {
            resourcesToBrowse.RemoveAll(link => link != LabURI.Empty && assetManager.DoesAssetExist(link)
                                                && AssetVersions.WhyNotUsableBy(current.URI, assetManager.GetAsset(link)) != null);
        }

        if (_isIncludeEmpty)
        {
            resourcesToBrowse.Add(LabURI.Empty);
        }

        return resourcesToBrowse;
    }

    [ReactiveCommand]
    private async Task<LabURI> SelectUriFromLink()
    {
        var uri = CurrentValue;
        var resourcesToBrowse = GetBrowseCandidates();
        var linkBrowser = new ResourceBrowserViewModel(_browseType, resourcesToBrowse, uri);
        var linkBrowserDialogue = new ResourceBrowserView
        {
            DataContext = linkBrowser
        };
        var result = await linkBrowserDialogue.ShowDialog<bool?>(MiscUtils.GetMainWindow());
        if (result.HasValue && result.Value)
        {
            Document.RemoveResource(uri);
            Document.AddResource(linkBrowser.SelectedLink);
            return linkBrowser.SelectedLink;
        }

        return uri!;
    }

    private bool IsExcludedFromBrowsing(LabURI uri) => IsExcludedFromBrowsing(uri, _browseExcludeWhen!);

    /// <summary>
    /// Whether a link field that leaves out the assets the condition (a boolean property of theirs) is true of leaves the asset out
    /// </summary>
    internal static bool IsExcludedFromBrowsing(LabURI uri, string condition)
    {
        if (uri == LabURI.Empty)
        {
            return false;
        }

        var asset = AssetManager.Get().GetAsset(uri);
        var property = asset.GetType().GetProperty(condition, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(asset) is true;
    }

    // The link can be edited either as a part of the chunk's document or from within one of the chunk's resources
    // The chunk the edited asset belongs to: the chunk itself or the instance's chunk, as the package and chunk path
    private (LabURI? Package, string? Chunk) GetOwnerChunkPath()
    {
        for (var node = Property.Parent; node != null; node = node.Parent)
        {
            switch (node.Target)
            {
                case LevelChunk chunk:
                    return (chunk.Package, chunk.AdditionalPath);
                case SerializableInstance { Chunk: not null } instance:
                    return (instance.Package, instance.Chunk);
            }
        }

        return (null, null);
    }

    private LabURI GetOwnerChunk() => OwnerChunkOf(Property);

    /// <summary>
    /// The chunk the asset a link is in belongs to: the chunk itself or the instance's chunk
    /// </summary>
    internal static LabURI OwnerChunkOf(PropertyNode link)
    {
        for (var node = link.Parent; node != null; node = node.Parent)
        {
            switch (node.Target)
            {
                case LevelChunk chunk:
                    return chunk.URI;
                case SerializableInstance { Chunk: not null } instance:
                    var ownerChunk = AssetManager.Get().GetAllAssetsOf<LevelChunk>()
                        .FirstOrDefault(c => c.Package == instance.Package && c.AdditionalPath == instance.Chunk);
                    return ownerChunk?.URI ?? LabURI.Empty;
            }
        }

        return LabURI.Empty;
    }

    [ReactiveCommand]
    private void OpenDocument()
    {
        var uri = CurrentValue;
        if (uri == LabURI.Empty)
        {
            return;
        }

        // A chunk's document edits the assets it shares with other chunks through its views of them and its own resources where its list
        // has them, both in its inspector, a step along the trail it goes back on. The assets' own editors edit them for every chunk
        var data = Property.Find("[data]");
        if (data != null && (_openInInspector || data.Target is SerializableAsset { OverriddenAsset: not null }))
        {
            Document.FollowInInspector(data);
            return;
        }

        if (Document.FindChunkResource(uri!) is { } resource)
        {
            Document.FollowInInspector(resource);
            return;
        }

        var shell = Locator.Current.GetService<ILabManager>()!;
        shell.OpenEditor(AssetManager.Get().GetAsset(uri!));
    }

    public const string BrowseType = "URI_LINK_FIELD_BROWSE_TYPE_NAME";
    public const string BrowseScope = "URI_LINK_FIELD_BROWSE_SCOPE";
    public const string OpenInInspector = "URI_LINK_FIELD_OPEN_IN_INSPECTOR";
    // Name of a boolean property on the browsed assets, assets where it's true can't be linked
    public const string BrowseExcludeWhen = "URI_LINK_FIELD_BROWSE_EXCLUDE_WHEN";
    public const string BrowseExcludeOwnerChunk = "URI_LINK_FIELD_BROWSE_EXCLUDE_OWNER_CHUNK";
    public const string IncludeEmpty = "URI_LINK_FIELD_BROWSE_INCLUDE_EMPTY";
    
    private Type _browseType = typeof(IAsset);
    private Scope _browseScope = Scope.Project;
    private bool _openInInspector = false;
    private string? _browseExcludeWhen;
    private bool _isIncludeEmpty = false;
    private bool _browseExcludeOwnerChunk;

    // The kind of asset the link takes, its rules read without the field being shown (replacing many links at once), what it takes is
    // GetBrowseCandidates
    internal Type ReadReplacementKind()
    {
        ApplyEditorAttributes();
        return _browseType;
    }
}