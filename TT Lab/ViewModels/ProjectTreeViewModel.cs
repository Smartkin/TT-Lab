using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.ViewModels;

public class ProjectTreeViewModel : Document, IHandle<ProjectManagerMessage>
{
    private const string LocateHint = "Locate the asset of the editor last worked in (Alt+Shift+L): its folders open and its row gets picked, or the row "
                                      + "of what holds it when the tree doesn't list it (the chunk's for what's in a chunk's folder)";
    private const string NothingToLocateHint = "No editor is open: locating shows the asset of the editor last worked in";

    private readonly ProjectManager _projectManager;
    private readonly ScenesEditorsViewModel _scenes;
    private readonly ResourcesEditorsViewModel _resources;
    private ResourceTreeElementViewModel? _selectedRow;
    // The folders open before a search started, put back once it's cleared
    private HashSet<ResourceTreeElementViewModel>? _openBeforeSearch;
    // The tree's view lets go of its pick with its items, which is no pick of the user's
    private bool _isShowingNewTree;
    private bool _canLocate;

    public ProjectTreeViewModel(ProjectManager projectManager, IEventAggregator eventAggregator, ScenesEditorsViewModel scenes, ResourcesEditorsViewModel resources)
    {
        _projectManager = projectManager;
        _scenes = scenes;
        _resources = resources;
        Id = "ProjectTree";
        Title = "Project Tree";
        eventAggregator.SubscribeOnUIThread(this);

        var hasEditor = scenes.WhenAnyValue(x => x.ActiveEditor)
            .CombineLatest(resources.WhenAnyValue(x => x.ActiveEditor), (scene, resource) => scene != null || resource != null);
        hasEditor.Subscribe(has => CanLocate = has);
        LocateCommand = ReactiveCommand.Create(Locate, hasEditor);
    }

    /// <summary>
    /// Raised with the row located once the folders down to it are open, for the view to scroll to it and focus it
    /// </summary>
    public event Action<ResourceTreeElementViewModel>? RevealRequested;

    public BindableCollection<ResourceTreeElementViewModel> ProjectTree => _projectManager.ProjectTree;

    public string SearchAsset
    {
        get => _projectManager.SearchAsset;
        set
        {
            var wasSearching = IsSearching;
            var searches = !string.IsNullOrEmpty(value);
            if (!wasSearching && searches)
            {
                // A search shows what it finds in closed folders, the ones open before it come back once it's cleared
                _openBeforeSearch = Rows(_projectManager.FullProjectTree).Where(row => row.IsExpanded).ToHashSet();
                foreach (var row in _openBeforeSearch)
                {
                    row.RestoreExpanded(false);
                }
            }

            var picked = _selectedRow;
            var tree = ProjectTree;
            _projectManager.SearchAsset = value;
            if (wasSearching && !searches)
            {
                RestoreOpenFolders(picked);
            }

            // Searching gives the tree new rows to show right away, the manager's message comes later
            if (!ReferenceEquals(tree, ProjectTree))
            {
                ShowTree();
            }
        }
    }

    private bool IsSearching => !string.IsNullOrEmpty(_projectManager.SearchAsset);

    /// <summary>
    /// The row picked in the tree, the one located
    /// </summary>
    public ResourceTreeElementViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (_isShowingNewTree)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedRow, value);
        }
    }

    public ReactiveCommand<Unit, Unit> LocateCommand { get; }

    public bool CanLocate
    {
        get => _canLocate;
        private set
        {
            this.RaiseAndSetIfChanged(ref _canLocate, value);
            this.RaisePropertyChanged(nameof(LocateTip));
        }
    }

    public string LocateTip => CanLocate ? LocateHint : NothingToLocateHint;

    /// <summary>
    /// The row located last until the view has shown it: reopening the closed panel to locate makes its view afterwards
    /// </summary>
    internal ResourceTreeElementViewModel? PendingReveal { get; private set; }

    internal void Revealed(ResourceTreeElementViewModel row)
    {
        if (PendingReveal == row)
        {
            PendingReveal = null;
        }
    }

    /// <summary>
    /// Locates the asset of the editor last worked in, the History panel's: the active one of the viewer used last, the other viewer's when
    /// that one has none open
    /// </summary>
    public void Locate()
    {
        var editor = EditorsViewerViewModel.CurrentEditor(_scenes, _resources);
        if (editor == null)
        {
            return;
        }

        var assets = AssetManager.Get();
        if (!assets.DoesAssetExist(editor.EditableResource))
        {
            Log.WriteLine($"{editor.AssetName} can't be located, it isn't in the project anymore", Log.LogType.Warning);
            return;
        }

        Locate(assets.GetAsset(editor.EditableResource));
    }

    internal void Locate(IAsset asset)
    {
        var path = RowPathFor(asset, _projectManager.FullProjectTree);
        if (path == null)
        {
            Log.WriteLine($"{asset.Alias} can't be located: neither it nor anything holding it has a row in the Project Tree", Log.LogType.Warning);
            return;
        }

        // A search that doesn't show the row is cleared, one that shows it stays
        if (IsSearching && !IsShown(path))
        {
            SearchAsset = string.Empty;
            this.RaisePropertyChanged(nameof(SearchAsset));
        }

        foreach (var folder in path.Take(path.Count - 1))
        {
            folder.IsExpanded = true;
        }

        var row = path[^1];
        _selectedRow = row;
        this.RaisePropertyChanged(nameof(SelectedRow));
        PendingReveal = row;
        RevealRequested?.Invoke(row);
    }

    /// <summary>
    /// The rows from the top of the tree down to the asset's, or down to the row of what holds it when the tree doesn't list it: the
    /// chunk's for what's in its folder (its instances, the default chunk's collision surfaces, a scenery's materials and pictures), the
    /// owner's for a part of another asset, the package's folder for a package. Null when nothing holding it has a row either
    /// </summary>
    internal static List<ResourceTreeElementViewModel>? RowPathFor(IAsset asset, IEnumerable<ResourceTreeElementViewModel> tree)
    {
        var roots = tree.ToList();
        var looked = new HashSet<LabURI>();
        for (IAsset? holder = asset; holder != null && looked.Add(holder.URI); holder = HolderOf(holder))
        {
            var shown = ShownAs(holder);
            if (PathTo(roots, row => row.GetInternalChildren(), row => row.Asset.URI == shown.URI) is { } path)
            {
                return path;
            }
        }

        return null;
    }

    // A chunk's row and a package's are their folders'
    private static IAsset ShownAs(IAsset asset)
    {
        try
        {
            return asset switch
            {
                LevelChunk chunk => chunk.GetChunkFolder(),
                Package package => package.GetPackageFolder(),
                _ => asset
            };
        }
        catch (KeyNotFoundException)
        {
            return asset;
        }
    }

    // What holds an asset the tree doesn't list: the owner of a part of another asset, the chunk whose folder its files are in
    private static IAsset? HolderOf(IAsset asset)
    {
        if (asset.IsInternal)
        {
            return asset.InternalOwner;
        }

        if (asset is LevelChunk or Package)
        {
            return null;
        }

        return LevelChunk.Owning(asset) ?? ChunkWithFilesOf(asset);
    }

    // The chunk of the asset's package whose folder has the asset's files, the deepest one. The URIs of a chunk and of what's in its folder
    // come from the chunk's path, but the global packages' startup folder goes by two names (DirectoryCase)
    private static LevelChunk? ChunkWithFilesOf(IAsset asset)
    {
        var directory = AssetRelocation.DirectoryOf(asset);
        return AssetManager.Get().GetAllAssetsOf<LevelChunk>()
            .Select(chunk => (Chunk: chunk, Directory: AssetRelocation.DirectoryOf(chunk)))
            .Where(entry => entry.Chunk.Package == asset.Package && entry.Directory.Length > 0
                                                                  && (directory.Equals(entry.Directory, StringComparison.OrdinalIgnoreCase)
                                                                      || directory.StartsWith(entry.Directory + "/", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(entry => entry.Directory.Length)
            .Select(entry => entry.Chunk)
            .FirstOrDefault();
    }

    // The rows from a top row down to the first one found, through the children each row lists
    private static List<ResourceTreeElementViewModel>? PathTo(IEnumerable<ResourceTreeElementViewModel> roots,
        Func<ResourceTreeElementViewModel, IEnumerable<ResourceTreeElementViewModel>?> children, Func<ResourceTreeElementViewModel, bool> isFound)
    {
        var path = new List<ResourceTreeElementViewModel>();
        foreach (var root in roots)
        {
            if (Visit(root))
            {
                return path;
            }
        }

        return null;

        bool Visit(ResourceTreeElementViewModel row)
        {
            path.Add(row);
            if (isFound(row))
            {
                return true;
            }

            foreach (var child in children(row) ?? [])
            {
                if (Visit(child))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    // Whether the tree as it's searched shows the rows of the path, each listed by the one before it
    private bool IsShown(IReadOnlyList<ResourceTreeElementViewModel> path)
    {
        if (!ProjectTree.Contains(path[0]))
        {
            return false;
        }

        for (var i = 1; i < path.Count; i++)
        {
            if (path[i - 1].Children?.Contains(path[i]) != true)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<ResourceTreeElementViewModel> Rows(IEnumerable<ResourceTreeElementViewModel> roots)
    {
        var rows = new Stack<ResourceTreeElementViewModel>(roots.ToList());
        while (rows.Count > 0)
        {
            var row = rows.Pop();
            yield return row;
            foreach (var child in row.GetInternalChildren() ?? [])
            {
                rows.Push(child);
            }
        }
    }

    // Clearing a search puts back the folders open before it, and opens the ones down to what got picked among its results
    private void RestoreOpenFolders(ResourceTreeElementViewModel? picked)
    {
        var open = _openBeforeSearch ?? [];
        _openBeforeSearch = null;
        foreach (var row in Rows(_projectManager.FullProjectTree))
        {
            row.RestoreExpanded(open.Contains(row));
        }

        if (picked == null || PathTo(_projectManager.FullProjectTree.ToList(), row => row.GetInternalChildren(), row => row == picked) is not { } path)
        {
            return;
        }

        foreach (var folder in path.Take(path.Count - 1))
        {
            folder.IsExpanded = true;
        }
    }

    // The view takes the manager's new list of rows: a search's, or the same rows again after the file system changed. The pick stays
    // while the tree still shows it
    private void ShowTree()
    {
        var picked = _selectedRow;
        _isShowingNewTree = true;
        try
        {
            this.RaisePropertyChanged(nameof(ProjectTree));
        }
        finally
        {
            _isShowingNewTree = false;
        }

        _selectedRow = picked != null && PathTo(ProjectTree.ToList(), row => row.Children, row => row == picked) != null ? picked : null;
        this.RaisePropertyChanged(nameof(SelectedRow));
    }

    public Task HandleAsync(ProjectManagerMessage message, CancellationToken cancellationToken)
    {
        switch (message.PropertyName)
        {
            case nameof(ProjectManager.ProjectTree):
                ShowTree();
                break;
            case nameof(ProjectManager.SearchAsset):
                this.RaisePropertyChanged(nameof(SearchAsset));
                break;
        }

        return Task.CompletedTask;
    }
}
