using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors;

public enum ChunkResourceRowKind
{
    TypeFolder,
    LayoutFolder,
    Resource,
    // Listed by the chunk but not in the project
    Missing,
}

public sealed partial class ChunkResourceRow : ReactiveObject
{
    [Reactive]
    private bool _isExpanded;

    [Reactive]
    private string _caption;

    [Reactive]
    private string _count = string.Empty;

    [Reactive]
    private string _newName = string.Empty;

    private bool _isRenaming;

    public ChunkResourceRow(ChunkResourceRowKind kind, Type? assetType, int? layout, string caption, Bitmap? icon, ChunkResourceRow? parent)
    {
        Kind = kind;
        AssetType = assetType;
        Layout = layout;
        _caption = caption;
        Icon = icon;
        Parent = parent;
    }

    public ChunkResourceRowKind Kind { get; }
    public Type? AssetType { get; }
    public int? Layout { get; }
    public Bitmap? Icon { get; }
    public ChunkResourceRow? Parent { get; }
    public ObservableCollection<ChunkResourceRow> Children { get; } = [];
    public string? Hint { get; init; }

    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRenaming, value);
            this.RaisePropertyChanged(nameof(IsNotRenaming));
        }
    }

    public bool IsNotRenaming => !IsRenaming;

    // The chunk's element linking the resource, made again whenever the list changes
    public PropertyNode? Element { get; set; }

    public LabURI? Uri { get; init; }
}

/// <summary>
/// The chunk's resources laid out like the project tree: a folder for each kind of asset, the layouts of the kinds that have them, and
/// the assets. Picking one inspects it (and selects it in the scene), a right click on a folder makes a new asset of its kind
/// </summary>
public sealed partial class ChunkResourcesTreeViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies)
    : DocumentNodeViewModel(document, node, dependencies)
{
    private static readonly StringComparer NameOrder = StringComparer.OrdinalIgnoreCase;

    // Kinds every level chunk shows a folder of, empty or not, so a first one can be made
    private static readonly Type[] LevelKinds = [typeof(ObjectInstance), typeof(Position), typeof(Trigger), typeof(Camera), typeof(AiPosition), typeof(AiPath), typeof(Path)];

    [Reactive]
    private ChunkResourceRow? _selectedRow;

    private readonly Dictionary<(Type?, int?), ChunkResourceRow> _folders = [];
    private readonly Dictionary<LabURI, ChunkResourceRow> _resources = [];
    private bool _isFollowing;

    public ObservableCollection<ChunkResourceRow> Folders { get; } = [];

    /// <summary>
    /// Asks the view to bring the row into sight, its folders are expanded already
    /// </summary>
    public event Action<ChunkResourceRow>? RevealRequested;

    public static string KindName(Type? type)
    {
        if (type == null)
        {
            return "Missing Assets";
        }

        var words = string.Join(' ', System.Text.RegularExpressions.Regex.Split(type.Name, "(?<!^)(?=[A-Z])"));
        return words.StartsWith("Ai ", StringComparison.Ordinal) ? $"AI {words[3..]}" : words;
    }

    private static string LayoutName(int layout)
    {
        var found = ChunkLayouts.Find(layout)!;
        return $"Layout {layout} ({found.Name})";
    }

    private LevelChunk? Chunk => Document.DocumentModel as LevelChunk;

    private ViewportViewModel? Viewport => Document.Viewport;

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        Sync();
        Document.PropertyGraph.Changed += GraphOnChanged;
        Disposable.Create(() => Document.PropertyGraph.Changed -= GraphOnChanged).DisposeWith(disposables);
        Document.WhenAnyValue(x => x.Inspector)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => FollowInspector())
            .DisposeWith(disposables);
        this.WhenAnyValue(x => x.SelectedRow)
            .Skip(1)
            .Where(_ => !_isFollowing)
            .WhereNotNull()
            .Where(row => row.Kind == ChunkResourceRowKind.Resource)
            .Subscribe(Inspect)
            .DisposeWith(disposables);
    }

    private void GraphOnChanged(PropertyChange change)
    {
        var changed = change.Node;
        if (changed == Property || changed.Parent == Property)
        {
            Sync();
            return;
        }

        // A resource renamed, in the inspector or here
        if (changed.Name == nameof(IAsset.Alias) && changed.Parent?.Parent?.Parent == Property && changed.Parent.Target is IAsset asset
            && _resources.TryGetValue(asset.URI, out var row))
        {
            row.Caption = asset.Alias;
        }
    }

    /// <summary>
    /// Lays the rows out for what the chunk lists now, keeping the ones that stay with how they're expanded
    /// </summary>
    public void Sync()
    {
        var assetManager = AssetManager.Get();
        var listed = new List<(PropertyNode Element, LabURI Uri, IAsset? Asset)>();
        foreach (var element in Property.Children)
        {
            if (element.GetValue() is not LabURI uri || uri == LabURI.Empty)
            {
                continue;
            }

            listed.Add((element, uri, assetManager.DoesAssetExist(uri) ? assetManager.GetAsset(uri) : null));
        }

        var kinds = listed.Select(entry => entry.Asset?.GetType()).Distinct().ToList();
        if (Chunk is { } chunk && !IsGlobalPackage(chunk.Package))
        {
            kinds.AddRange(LevelKinds.Where(kind => !kinds.Contains(kind)));
        }

        var folders = new List<ChunkResourceRow>();
        var kept = new HashSet<LabURI>();
        foreach (var kind in kinds.OrderBy(kind => kind == null ? 1 : 0).ThenBy(KindName, NameOrder))
        {
            var folder = GetFolder(kind, null, null);
            var ofKind = listed.Where(entry => entry.Asset?.GetType() == kind).ToList();
            var children = new List<ChunkResourceRow>();
            foreach (var layout in ofKind.Where(entry => entry.Asset?.LayoutID != null).Select(entry => entry.Asset!.LayoutID!.Value).Distinct().Order())
            {
                var layoutFolder = GetFolder(kind, layout, folder);
                var rows = ofKind.Where(entry => entry.Asset?.LayoutID == layout).Select(entry => GetResource(entry, layoutFolder)).ToList();
                Update(layoutFolder.Children, rows);
                layoutFolder.Count = rows.Count.ToString();
                children.Add(layoutFolder);
                kept.UnionWith(rows.Select(row => row.Uri!));
            }

            var unlaid = ofKind.Where(entry => entry.Asset?.LayoutID == null).Select(entry => GetResource(entry, folder)).ToList();
            kept.UnionWith(unlaid.Select(row => row.Uri!));
            children.AddRange(unlaid);
            Update(folder.Children, children);
            folder.Count = ofKind.Count.ToString();
            folders.Add(folder);
        }

        Update(Folders, folders);
        foreach (var gone in _resources.Keys.Where(uri => !kept.Contains(uri)).ToList())
        {
            _resources.Remove(gone);
        }
    }

    private static bool IsGlobalPackage(LabURI package)
    {
        var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject as Project.Project;
        return project != null && (package == project.GlobalPackagePS2.URI || package == project.GlobalPackageXbox.URI);
    }

    private ChunkResourceRow GetFolder(Type? kind, int? layout, ChunkResourceRow? parent)
    {
        if (_folders.TryGetValue((kind, layout), out var folder))
        {
            return folder;
        }

        folder = layout == null
            ? new ChunkResourceRow(ChunkResourceRowKind.TypeFolder, kind, null, KindName(kind), MiscUtils.GetLabIcon("Folder"), null)
            : new ChunkResourceRow(ChunkResourceRowKind.LayoutFolder, kind, layout, LayoutName(layout.Value), MiscUtils.GetLabIcon("Folder"), parent)
            {
                Hint = ChunkLayouts.Find(layout)!.Description,
            };
        _folders[(kind, layout)] = folder;
        return folder;
    }

    private ChunkResourceRow GetResource((PropertyNode Element, LabURI Uri, IAsset? Asset) entry, ChunkResourceRow folder)
    {
        // A resource moved to another layout gets a row in its new folder
        if (!_resources.TryGetValue(entry.Uri, out var row) || row.Parent != folder)
        {
            row = entry.Asset == null
                ? new ChunkResourceRow(ChunkResourceRowKind.Missing, null, null, entry.Uri.ToString(), MiscUtils.GetLabIcon("Common_Node"), folder)
                {
                    Uri = entry.Uri,
                    Hint = "The chunk lists this asset, but it isn't in the project",
                }
                : new ChunkResourceRow(ChunkResourceRowKind.Resource, entry.Asset.GetType(), entry.Asset.LayoutID, entry.Asset.Alias,
                    MiscUtils.GetLabIcon(System.IO.Path.GetFileNameWithoutExtension(entry.Asset.IconPath)), folder)
                {
                    Uri = entry.Uri,
                };
            _resources[entry.Uri] = row;
        }

        row.Element = entry.Element;
        if (entry.Asset != null)
        {
            row.Caption = entry.Asset.Alias;
        }

        return row;
    }

    // Puts the rows in the order given with as few changes as it takes, expanded rows keep their items
    private static void Update(ObservableCollection<ChunkResourceRow> target, IReadOnlyList<ChunkResourceRow> rows)
    {
        var wanted = rows.ToHashSet();
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (i < target.Count && target[i] == rows[i])
            {
                continue;
            }

            var at = target.IndexOf(rows[i]);
            if (at >= 0)
            {
                target.Move(at, i);
            }
            else
            {
                target.Insert(i, rows[i]);
            }
        }
    }

    // Something inspected elsewhere, like an instance clicked in the scene, gets its row picked
    private void FollowInspector()
    {
        var inspected = Document.Inspector?.Property;
        while (inspected != null && inspected.Parent != Property)
        {
            inspected = inspected.Parent;
        }

        ChunkResourceRow? row = null;
        if (inspected?.GetValue() is LabURI uri)
        {
            _resources.TryGetValue(uri, out row);
        }

        // Something that isn't one of the resources, or nothing, leaves no row picked: picking the row again inspects it again
        if (row == SelectedRow || (row == null && SelectedRow?.Kind != ChunkResourceRowKind.Resource))
        {
            return;
        }

        _isFollowing = true;
        try
        {
            for (var folder = row?.Parent; folder != null; folder = folder.Parent)
            {
                folder.IsExpanded = true;
            }

            SelectedRow = row;
        }
        finally
        {
            _isFollowing = false;
        }

        if (row != null)
        {
            RevealRequested?.Invoke(row);
        }
    }

    public void Inspect(ChunkResourceRow row)
    {
        if (row.Element?.Find("[data]") is { } data)
        {
            Document.OpenInspector(data);
        }
    }

    /// <summary>
    /// Inspects the resource and shows it in the scene
    /// </summary>
    public void Show(ChunkResourceRow row)
    {
        if (row.Kind != ChunkResourceRowKind.Resource || row.Element == null)
        {
            return;
        }

        Inspect(row);
        Viewport?.ShowResource(row.Element);
    }

    public void Delete(ChunkResourceRow row)
    {
        if (!CanDelete(row) || row.Element is not { } element)
        {
            return;
        }

        if (Viewport != null)
        {
            Viewport.DeleteResources([element]);
        }
        else
        {
            Property.RemoveElement(element);
        }
    }

    // Instances of layouts, and what the chunk lists without having it
    public static bool CanDelete(ChunkResourceRow row) => row.Kind == ChunkResourceRowKind.Missing || (row.Kind == ChunkResourceRowKind.Resource && row.Layout != null);

    public void Duplicate(ChunkResourceRow row)
    {
        if (row.Kind == ChunkResourceRowKind.Resource && row.Layout != null && row.Element != null && Viewport?.DuplicateResource(row.Element) is { } copy)
        {
            InspectNew(copy.URI);
        }
    }

    public void BeginRename(ChunkResourceRow row)
    {
        if (row.Kind != ChunkResourceRowKind.Resource)
        {
            return;
        }

        row.NewName = row.Caption;
        row.IsRenaming = true;
    }

    public void EndRename(ChunkResourceRow row, bool keep)
    {
        if (!row.IsRenaming)
        {
            return;
        }

        row.IsRenaming = false;
        var name = row.NewName.Trim();
        if (!keep || name.Length == 0 || name == row.Caption)
        {
            return;
        }

        if (!NameRules.IsAscii(name))
        {
            Log.WriteLine($"{row.Caption} isn't renamed to {name}: a name {NameRules.AsciiOnly}", Log.LogType.Warning);
            return;
        }

        // Through the document, so it's a step to undo and gets saved with the chunk
        row.Element?.Find($"[data].{nameof(IAsset.Alias)}")?.SetValue(name);
    }

    /// <summary>
    /// A new asset of the folder's kind, in its layout or the one the chunk keeps most of the kind in, at the scene's cursor
    /// </summary>
    public void Create(ChunkResourceRow folder)
    {
        if (Viewport == null || Chunk is not { } chunk || folder.AssetType is not { } kind)
        {
            return;
        }

        if (kind == typeof(ChunkLinks))
        {
            Viewport.CreateChunkLink();
            return;
        }

        if (kind == typeof(Particles))
        {
            Viewport.CreateParticleEmitter();
            return;
        }

        var created = Viewport.CreateResource(kind, folder.Layout ?? ViewportViewModel.DefaultLayoutFor(kind, chunk));
        if (created != null)
        {
            InspectNew(created.URI);
        }
    }

    // What got made is picked here and inspected right away, the scene selects it once its objects are made
    private void InspectNew(LabURI uri)
    {
        var element = Property.Children.FirstOrDefault(element => Equals(element.GetValue(), uri));
        if (element?.Find("[data]") is { } data)
        {
            Document.OpenInspector(data);
        }
    }

    /// <summary>
    /// What a right click on the row offers: making an asset of a folder's kind, what can be done with a resource
    /// </summary>
    public IReadOnlyList<ViewportMenuEntry> GetMenu(ChunkResourceRow row)
    {
        var entries = new List<ViewportMenuEntry>();
        switch (row.Kind)
        {
            case ChunkResourceRowKind.TypeFolder:
            case ChunkResourceRowKind.LayoutFolder:
                AddCreateEntries(entries, row);
                break;
            case ChunkResourceRowKind.Resource:
                entries.Add(new ViewportMenuEntry("Inspect", () => Inspect(row)));
                entries.Add(new ViewportMenuEntry("Show in the scene", () => Show(row), Viewport != null));
                if (row.AssetType == typeof(ChunkLinks) || row.AssetType == typeof(Particles))
                {
                    AddCreateEntries(entries, row);
                }

                if (row.Layout != null)
                {
                    entries.Add(ViewportMenuEntry.Separator);
                    entries.Add(new ViewportMenuEntry("Rename (F2)", () => BeginRename(row)));
                    entries.Add(new ViewportMenuEntry("Duplicate", () => Duplicate(row), Viewport != null));
                    entries.Add(new ViewportMenuEntry("Delete (Del)", () => Delete(row)));
                }
                else
                {
                    entries.Add(new ViewportMenuEntry("Open in its own editor", () => OpenEditor(row)));
                }

                break;
            case ChunkResourceRowKind.Missing:
                entries.Add(new ViewportMenuEntry("Take out of the chunk", () => Delete(row)));
                break;
        }

        return entries;
    }

    private void AddCreateEntries(List<ViewportMenuEntry> entries, ChunkResourceRow row)
    {
        var kind = row.AssetType;
        if (kind == null || Chunk is not { } chunk)
        {
            return;
        }

        var canCreate = Viewport != null;
        var where = row.Kind == ChunkResourceRowKind.LayoutFolder ? $" in layout {row.Layout}" : string.Empty;
        if (kind == typeof(ChunkLinks))
        {
            entries.Add(new ViewportMenuEntry("Add a chunk link", () => Create(row), canCreate));
        }
        else if (kind == typeof(Particles))
        {
            entries.Add(new ViewportMenuEntry("Add a particle emitter", () => Create(row), canCreate));
        }
        else if (kind == typeof(AiPath))
        {
            var positions = Viewport?.SelectedAiPositions() ?? [];
            entries.Add(positions.Count == 2
                ? new ViewportMenuEntry($"Create AI Path from {positions[0].Alias} to {positions[1].Alias}", () => Create(row), canCreate)
                : new ViewportMenuEntry("Create AI Path (select two AI positions in the scene first)", IsEnabled: false));
        }
        else if (kind == typeof(ObjectInstance))
        {
            var gameObject = ViewportViewModel.DefaultObjectFor(chunk);
            entries.Add(gameObject == null
                ? new ViewportMenuEntry("Create Object Instance (the chunk has no objects)", IsEnabled: false)
                : new ViewportMenuEntry($"Create Object Instance of {gameObject.Alias}{where}", () => Create(row), canCreate));
        }
        else if (ViewportViewModel.CreatableTypes.Contains(kind))
        {
            entries.Add(new ViewportMenuEntry($"Create {KindName(kind)}{where}", () => Create(row), canCreate));
        }

        if (entries.Count > 0 && !canCreate)
        {
            entries.Add(new ViewportMenuEntry("Open the chunk's scene to make new ones", IsEnabled: false));
        }
    }

    private static void OpenEditor(ChunkResourceRow row)
    {
        if (row.Uri is { } uri && AssetManager.Get().DoesAssetExist(uri))
        {
            Locator.Current.GetService<ILabManager>()?.OpenEditor(AssetManager.Get().GetAsset(uri));
        }
    }
}
