using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Caliburn.Micro;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Project.Prefabs;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using Action = System.Action;

namespace TT_Lab.ViewModels;

/// <summary>
/// A prefab of the project as the panel lists it
/// </summary>
public sealed class PrefabEntry : ReactiveObject
{
    /// <param name="location">The folder it's in, for a prefab the panel found searching</param>
    public PrefabEntry(Prefab prefab, string details, string? cantPlace, Action place, Action delete, string? location = null)
    {
        Prefab = prefab;
        Details = details;
        Location = location;
        CanPlace = cantPlace == null;
        PlaceTip = cantPlace ?? "Drag it into the scene, or place it at the cursor from the menu";
        PlaceCommand = ReactiveCommand.Create(place);
        DeleteCommand = ReactiveCommand.Create(delete);
    }

    private Bitmap? _preview;
    private bool _previewLoaded;

    public Prefab Prefab { get; }
    public string Name => Prefab.Name;
    public string Details { get; }
    public string? Location { get; }
    public bool HasLocation => !string.IsNullOrEmpty(Location);
    public bool CanPlace { get; }
    public string PlaceTip { get; }
    public ReactiveCommand<Unit, Unit> PlaceCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public bool HasPreview => Prefab.PreviewPath != null;

    /// <summary>
    /// The picture taken of it since the tile showed (the ones made of the instances get theirs in the background)
    /// </summary>
    internal void ShowPicture(string? path)
    {
        Prefab.PreviewPath = path;
        _preview = null;
        _previewLoaded = false;
        this.RaisePropertyChanged(nameof(HasPreview));
        this.RaisePropertyChanged(nameof(Preview));
    }

    /// <summary>
    /// The picture taken when it got saved, read when the tile first shows
    /// </summary>
    public Bitmap? Preview
    {
        get
        {
            if (_previewLoaded)
            {
                return _preview;
            }

            _previewLoaded = true;
            if (Prefab.PreviewPath == null)
            {
                return null;
            }

            try
            {
                _preview = new Bitmap(Prefab.PreviewPath);
            }
            catch (Exception e)
            {
                Log.WriteLine($"Prefab {Name}'s picture couldn't be read: {e.Message}", Log.LogType.Debug);
            }

            return _preview;
        }
    }
}

/// <summary>
/// A folder of the prefabs as the panel shows it, a tile among the prefabs opened with a double click, renamed in place and deleted after
/// asking
/// </summary>
public sealed partial class PrefabFolderEntry : ReactiveObject
{
    private readonly Action<PrefabFolderEntry, string?> _rename;

    /// <param name="rename">Told the new name once it's typed, none when the renaming was given up on or it started (renaming is
    /// what's typed)</param>
    public PrefabFolderEntry(string path, int prefabs, Action<PrefabFolderEntry> open, Action<PrefabFolderEntry, string?> rename, Action<PrefabFolderEntry> delete, bool isRenaming = false,
        string? location = null)
    {
        Path = path;
        Location = location;
        Name = PrefabLibrary.ParentOf(path).Length == 0 ? path : path[(PrefabLibrary.ParentOf(path).Length + 1)..];
        _rename = rename;
        _editName = Name;
        _isRenaming = isRenaming;
        DeleteQuestion = prefabs == 0 ? "Delete the folder?" : $"Delete it with what's in it ({prefabs} prefabs)?";
        Tip = $"{(prefabs == 1 ? "1 prefab" : $"{prefabs} prefabs")} in it. Double click to open it, drop a prefab onto it to move it there, right click it to rename or delete it";
        OpenCommand = ReactiveCommand.Create(() => open(this));
        StartRenameCommand = ReactiveCommand.Create(() =>
        {
            EditName = Name;
            IsRenaming = true;
            _rename(this, null);
        });
        CommitRenameCommand = ReactiveCommand.Create(CommitRename);
        CancelRenameCommand = ReactiveCommand.Create(() =>
        {
            IsRenaming = false;
            _rename(this, null);
        });
        AskDeleteCommand = ReactiveCommand.Create(() => { IsAskingDelete = true; });
        DeleteCommand = ReactiveCommand.Create(() => delete(this));
        KeepCommand = ReactiveCommand.Create(() => { IsAskingDelete = false; });
    }

    [Reactive]
    private bool _isRenaming;

    [Reactive]
    private string _editName;

    [Reactive]
    private bool _isAskingDelete;

    public string Path { get; }
    public string Name { get; }
    public string? Location { get; }
    public bool HasLocation => !string.IsNullOrEmpty(Location);
    public string DeleteQuestion { get; }
    public string Tip { get; }

    // The project tree's
    public Bitmap Icon => MiscUtils.GetLabIcon("Folder");
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> StartRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> AskDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> KeepCommand { get; }

    public void CommitRename()
    {
        if (!IsRenaming)
        {
            return;
        }

        IsRenaming = false;
        _rename(this, string.IsNullOrWhiteSpace(EditName) || EditName.Trim() == Name ? null : EditName.Trim());
    }
}

/// <summary>
/// A step of the way to the folder the panel shows
/// </summary>
public sealed record PrefabCrumb(string Name, string Path, ReactiveCommand<Unit, Unit> OpenCommand)
{
    public bool IsFirst => Path.Length == 0;
}

/// <summary>
/// The project's prefabs, a panel: saves the selection of the scene being edited and places prefabs into it, dragged into the
/// viewport or at its cursor. It shows one folder of them at a time, its folders as tiles before its prefabs
/// </summary>
public partial class PrefabsViewModel : Document, IHandle<ProjectManagerMessage>
{

    private const string NoSceneHint = "Open a chunk's scene to save its selection as a prefab or to place one";

    [Reactive(SetModifier = AccessModifier.Private)]
    private DocumentViewModel? _document;

    [Reactive]
    private string _prefabName = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canSave;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _hint = NoSceneHint;

    // The folder shown, folders from the library's separated by '/', empty for the library's own
    [Reactive(SetModifier = AccessModifier.Private)]
    private string _currentFolder = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isMakingInstancePrefabs;

    // How far taking the prefabs' pictures in the background got, empty while none are taken
    [Reactive(SetModifier = AccessModifier.Private)]
    private string _picturesStatus = string.Empty;

    // What the panel looks for in the folder shown and every folder in it, the folder itself shows while it's empty
    [Reactive]
    private string _searchText = string.Empty;

    // How many the search found, or that the folder is empty
    [Reactive(SetModifier = AccessModifier.Private)]
    private string _listStatus = string.Empty;

    // Thousands of tiles take long to make, the search shows the first ones
    private const int SearchLimit = 200;

    private readonly PrefabPictures _pictures;
    private ViewportViewModel? _viewport;
    private IDisposable? _viewportFollow;
    // The folder being renamed, still renamed when the panel reads its folder again (another scene picked, a project opened)
    private string? _renaming;
    private int _progressPending;

    /// <summary>
    /// The folder's folders and then its prefabs, the tiles the panel shows
    /// </summary>
    public ObservableCollection<object> Items { get; } = [];
    public IReadOnlyList<PrefabEntry> Prefabs => Items.OfType<PrefabEntry>().ToList();
    public IReadOnlyList<PrefabFolderEntry> Folders => Items.OfType<PrefabFolderEntry>().ToList();
    public ObservableCollection<PrefabCrumb> Crumbs { get; } = [];
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> MakeInstancePrefabsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearSearchCommand { get; }

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    public PrefabsViewModel(ScenesEditorsViewModel scenes, IEventAggregator eventAggregator, PrefabPictures pictures)
    {
        Id = "Prefabs";
        Title = "Prefabs";
        _pictures = pictures;
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.CanSave));
        RefreshCommand = ReactiveCommand.Create(Refresh);
        NewFolderCommand = ReactiveCommand.Create(NewFolder);
        MakeInstancePrefabsCommand = ReactiveCommand.CreateFromTask(MakeInstancePrefabsAsync, this.WhenAnyValue(x => x.IsMakingInstancePrefabs, making => !making));
        ChunkResourcesViewModel.FollowActiveEditor(scenes).Subscribe(Follow);
        // The list is read again whenever it could be stale: a project opened or closed, the tab picked (OnSelected) or the panel
        // shown (PrefabsView), so a panel opened after the project shows what the project has
        eventAggregator.SubscribeOnUIThread(this);
        pictures.Taken += prefab => Dispatcher.UIThread.Post(() => ShowPicture(prefab), DispatcherPriority.Background);
        // Moving assets gives the prefabs' files their new links, the prefabs shown are read again
        AssetRelocation.Relocated += () => Dispatcher.UIThread.Post(Refresh);
        pictures.ProgressChanged += ShowPicturesProgress;
        ClearSearchCommand = ReactiveCommand.Create(() =>
        {
            SearchText = string.Empty;
            Refresh();
        });
        // Looked for again once the typing stops
        this.WhenAnyValue(x => x.SearchText).Skip(1).Throttle(TimeSpan.FromMilliseconds(250)).ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => Refresh());
    }

    public override void OnSelected()
    {
        base.OnSelected();
        Refresh();
    }

    public Task HandleAsync(ProjectManagerMessage message, CancellationToken cancellationToken)
    {
        if (message.PropertyName == nameof(ProjectManager.ProjectOpened))
        {
            Refresh();
        }

        return Task.CompletedTask;
    }

    // The panel works on the scene last worked on, following what its viewport's selection can be saved as
    private void Follow(DocumentViewModel? document)
    {
        Document = document;
        _viewportFollow?.Dispose();
        _viewportFollow = null;
        _viewport = document?.Viewport;
        if (_viewport == null)
        {
            CanSave = false;
            Hint = NoSceneHint;
        }
        else
        {
            _viewportFollow = _viewport.WhenAnyValue(x => x.CanSavePrefab, x => x.PrefabHint, x => x.PrefabDefaultName)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    CanSave = _viewport.CanSavePrefab;
                    Hint = _viewport.PrefabHint;
                    PrefabName = _viewport.PrefabDefaultName;
                });
        }

        Refresh();
    }

    /// <summary>
    /// Reads the folder's prefabs and folders again, other scenes save them as well
    /// </summary>
    public void Refresh()
    {
        Items.Clear();
        Crumbs.Clear();
        ListStatus = string.Empty;
        var library = PrefabLibrary.ForOpenedProject();
        if (library == null)
        {
            return;
        }

        // A folder taken away elsewhere leaves the one it was in
        while (CurrentFolder.Length > 0 && !library.FolderExists(CurrentFolder))
        {
            CurrentFolder = PrefabLibrary.ParentOf(CurrentFolder);
        }

        Crumbs.Add(new PrefabCrumb("Prefabs", string.Empty, ReactiveCommand.Create(() => OpenFolder(string.Empty))));
        var path = string.Empty;
        foreach (var name in CurrentFolder.Split(PrefabLibrary.FolderSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            path = PrefabLibrary.Join(path, name);
            var crumbPath = path;
            Crumbs.Add(new PrefabCrumb(name, crumbPath, ReactiveCommand.Create(() => OpenFolder(crumbPath))));
        }

        if (IsSearching)
        {
            ShowSearch(library);
            return;
        }

        foreach (var name in library.Folders(CurrentFolder))
        {
            Items.Add(FolderEntry(library, PrefabLibrary.Join(CurrentFolder, name), null));
        }

        foreach (var prefab in library.Load(CurrentFolder))
        {
            Items.Add(Entry(library, prefab, null));
        }

        ListStatus = Items.Count == 0 ? "Nothing in this folder" : string.Empty;
    }

    // The folders and prefabs of the folder shown and the folders in it the search finds, each with where it is
    private void ShowSearch(PrefabLibrary library)
    {
        var terms = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (folders, prefabs, matched) = library.Search(CurrentFolder, terms, SearchLimit);
        foreach (var folder in folders)
        {
            Items.Add(FolderEntry(library, folder, Where(PrefabLibrary.ParentOf(folder))));
        }

        foreach (var prefab in prefabs)
        {
            Items.Add(Entry(library, prefab, Where(prefab.Folder)));
        }

        var found = folders.Count == 0 ? Count(matched, "prefab") : $"{Count(matched, "prefab")} and {Count(folders.Count, "folder")}";
        ListStatus = matched == 0 && folders.Count == 0 ? "Nothing here has that"
            : matched > prefabs.Count ? $"The first {prefabs.Count} of {found}, type more to narrow them down" : found;
    }

    private static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

    // The folder within the one the search looks in
    private string Where(string folder)
    {
        var within = CurrentFolder.Length == 0 ? folder : folder.Length > CurrentFolder.Length ? folder[(CurrentFolder.Length + 1)..] : string.Empty;
        return within.Length == 0 ? "Here" : within;
    }

    private PrefabFolderEntry FolderEntry(PrefabLibrary library, string folder, string? location)
    {
        return new PrefabFolderEntry(folder, library.CountPrefabs(folder), entry => OpenFolder(entry.Path), RenameFolder, entry =>
        {
            library.DeleteFolder(entry.Path);
            Refresh();
        }, folder == _renaming, location);
    }

    private PrefabEntry Entry(PrefabLibrary library, Prefab prefab, string? location)
    {
        var chunk = Document?.DocumentModel as LevelChunk;
        string? cantPlace;
        try
        {
            cantPlace = chunk == null ? "Open a chunk to place it" : library.CanPlace(prefab, chunk, Document, out var reason) ? null : reason;
        }
        catch (Exception e)
        {
            cantPlace = e.Message;
        }

        return new PrefabEntry(prefab, Describe(prefab), cantPlace, () => Place(prefab), () =>
        {
            library.Delete(prefab);
            Refresh();
        }, location);
    }

    public void Place(Prefab prefab)
    {
        _viewport?.PlacePrefab(prefab);
    }

    /// <summary>
    /// Shows the folder's prefabs and folders
    /// </summary>
    public void OpenFolder(string folder)
    {
        // A folder opened from the search's results shows what's in it
        SearchText = string.Empty;
        CurrentFolder = folder;
        Refresh();
    }

    /// <summary>
    /// Shows the folder the one shown is in
    /// </summary>
    public void OpenParentFolder()
    {
        if (CurrentFolder.Length > 0)
        {
            OpenFolder(PrefabLibrary.ParentOf(CurrentFolder));
        }
    }

    private void ShowPicture(Prefab prefab)
    {
        foreach (var entry in Items.OfType<PrefabEntry>().Where(entry => entry.Prefab.FilePath == prefab.FilePath))
        {
            entry.ShowPicture(prefab.PreviewPath);
        }
    }

    // Raised for every picture, the panel only shows the latest
    private void ShowPicturesProgress()
    {
        if (Interlocked.Exchange(ref _progressPending, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _progressPending, 0);
            PicturesStatus = _pictures.IsTaking ? $"Taking the prefabs' pictures, {_pictures.Done} of {_pictures.Total}" : string.Empty;
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// A new folder in the one shown, named as it's typed
    /// </summary>
    public void NewFolder()
    {
        if (PrefabLibrary.ForOpenedProject() is not { } library)
        {
            return;
        }

        _renaming = library.CreateFolder(CurrentFolder, "New folder");
        Refresh();
    }

    // Told when the renaming started (the name null) and how it ended
    private void RenameFolder(PrefabFolderEntry entry, string? name)
    {
        _renaming = entry.IsRenaming ? entry.Path : null;
        if (name == null || PrefabLibrary.ForOpenedProject() is not { } library)
        {
            return;
        }

        try
        {
            library.RenameFolder(entry.Path, name);
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Log.WriteLine($"Folder {entry.Name} couldn't be renamed: {e.Message}", Log.LogType.Warning);
        }

        Refresh();
    }

    /// <summary>
    /// Moves the prefab into the folder
    /// </summary>
    public void Move(Prefab prefab, string folder)
    {
        if (PrefabLibrary.ForOpenedProject() is not { } library)
        {
            return;
        }

        try
        {
            library.Move(prefab, folder);
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Log.WriteLine($"Prefab {prefab.Name} couldn't be moved: {e.Message}", Log.LogType.Warning);
        }

        Refresh();
    }

    private async Task SaveAsync()
    {
        if (_viewport != null && await _viewport.SavePrefabAsync(PrefabName, CurrentFolder))
        {
            Refresh();
        }
    }

    // Prefabs of the chunks' different object instances, scenery meshes and LODs, the ones a project gets when it's made, for projects
    // made before
    private async Task MakeInstancePrefabsAsync()
    {
        var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject as Project.Project;
        if (project == null)
        {
            return;
        }

        IsMakingInstancePrefabs = true;
        try
        {
            var start = DateTime.Now;
            var (made, kept) = await Task.Run(() => InstancePrefabs.Make(project, new PrefabLibrary(project)));
            Log.WriteLine($"Made {made} prefabs of the chunks' different object instances in {DateTime.Now - start}{AlreadyThere(kept)}", Log.LogType.Info);
            start = DateTime.Now;
            (made, kept) = await Task.Run(() => SceneryPrefabs.Make(project, new PrefabLibrary(project)));
            Log.WriteLine($"Made {made} prefabs of the sceneries' different meshes and LODs in {DateTime.Now - start}{AlreadyThere(kept)}", Log.LogType.Info);
        }
        catch (Exception e)
        {
            Log.WriteLine($"Prefabs of the object instances couldn't be made: {e.Message}", Log.LogType.Error);
        }
        finally
        {
            IsMakingInstancePrefabs = false;
        }

        Refresh();
        _pictures.TakeMissing(project);
    }

    private static string AlreadyThere(int kept) => kept == 0 ? string.Empty : $", the other {kept} the prefabs have already";

    private static string Describe(Prefab prefab)
    {
        try
        {
            return PrefabLibrary.Describe(prefab);
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
