using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Project;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

public record ClosedEditor(LabURI Uri, string Name);

public class EditorsViewModel
{
    private const int MaxRecentlyClosed = 10;
    private readonly List<ClosedEditor> _recentlyClosed = [];

    public event Action? RecentlyClosedChanged;

    private readonly ProjectManager _projectManager;

    public EditorsViewModel(ScenesEditorsViewModel scenesEditorsViewModel,
        ResourcesEditorsViewModel resourcesEditorsViewModel, ProjectManager projectManager)
    {
        _projectManager = projectManager;
        ScenesEditorsViewModel = scenesEditorsViewModel;
        ResourcesEditorsViewModel = resourcesEditorsViewModel;

        ScenesEditorsViewModel.EditorClosed += OnEditorClosed;
        ResourcesEditorsViewModel.EditorClosed += OnEditorClosed;
        projectManager.AssetFilesChanged += paths => _ = ReloadChangedFiles(paths);
    }

    public ScenesEditorsViewModel ScenesEditorsViewModel { get; }
    public ResourcesEditorsViewModel ResourcesEditorsViewModel { get; }
    public IReadOnlyList<ClosedEditor> RecentlyClosed => _recentlyClosed;

    public EditorsViewerViewModel GetViewerFor(IAsset asset)
    {
        return asset.Type == typeof(LevelChunk) ? ScenesEditorsViewModel : ResourcesEditorsViewModel;
    }

    public void OpenEditor(IAsset asset)
    {
        if (_recentlyClosed.RemoveAll(closed => closed.Uri == asset.URI) > 0)
        {
            RecentlyClosedChanged?.Invoke();
        }

        GetViewerFor(asset).OpenEditor(asset);
    }

    public void ForgetRecentlyClosed(ClosedEditor closedEditor)
    {
        if (_recentlyClosed.Remove(closedEditor))
        {
            RecentlyClosedChanged?.Invoke();
        }
    }

    public void ClearRecentlyClosed()
    {
        _recentlyClosed.Clear();
        RecentlyClosedChanged?.Invoke();
    }

    // What's asked when another program changed what editors have unsaved changes to, tests answer it themselves
    internal Func<string, Task<bool>> AskToReloadChangedFiles { get; set; } = ExternalChangeDialogue.Ask;

    private List<TabbedEditorViewModel> AllTabs => ScenesEditorsViewModel.Tabs.Concat(ResourcesEditorsViewModel.Tabs).ToList();

    private static readonly TimeSpan WaitForEditors = TimeSpan.FromMilliseconds(250);

    // Files written while the question about earlier ones is open wait for its answer, with whether they're new (asked about). Files editors
    // kept their changes to get looked at again once an editor is saved or closed, without asking again
    private bool _isReloading;
    private readonly Dictionary<string, bool> _pending = new(AssetFileStamps.PathComparer);
    private readonly HashSet<string> _deferred = new(AssetFileStamps.PathComparer);

    /// <summary>
    /// Makes the editors showing assets another program changed (a text editor saving a script, Blender exporting a model over its file)
    /// again, what TT Lab wrote itself aside (<see cref="AssetFileStamps"/>): the changed assets are read again, everything else stays as it
    /// is in memory, unsaved changes and the history included. Editors whose unsaved changes are in a changed asset ask first, once for all
    /// of them: kept, they and what they show stay as they are until they're saved or closed. Changed data no editor shows is let go of,
    /// the next read takes the file
    /// </summary>
    public Task ReloadChangedFiles(IReadOnlyCollection<string> paths) => Reload(paths, true);

    private async Task Reload(IEnumerable<string> paths, bool ask)
    {
        foreach (var path in paths)
        {
            _pending[path] = ask || _pending.GetValueOrDefault(path);
        }

        if (_isReloading)
        {
            return;
        }

        _isReloading = true;
        try
        {
            while (_pending.Count > 0)
            {
                var batch = _pending.ToList();
                _pending.Clear();
                var asked = batch.Where(path => path.Value).Select(path => AssetFileStamps.Normalize(path.Key)).ToHashSet(AssetFileStamps.PathComparer);
                await ReloadChanged(batch.Select(path => path.Key).ToList(), asked);
            }
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Couldn't reload what changed outside TT Lab: {ex.Message}", Log.LogType.Warning);
        }
        finally
        {
            _isReloading = false;
        }
    }

    // Like releasing data when an editor closes: a tab still loading may hold data it doesn't track yet, and scenes being built add internal
    // assets from their own thread. Builds read the editors' data
    private async Task<bool> WaitForEditorsToSettle()
    {
        while (AllTabs.Any(tab => !tab.IsLoaded || tab.Viewport is { IsBuildingScene: true }) || !_projectManager.WorkableProject)
        {
            await Task.Delay(WaitForEditors);
            if (_projectManager.OpenedProject == null)
            {
                return false;
            }
        }

        return true;
    }

    private async Task ReloadChanged(List<string> paths, IReadOnlySet<string> askedAbout)
    {
        // What the editors loaded meanwhile counts as read, a file they were reloading for isn't changed any more
        if (_projectManager.OpenedProject == null || !await WaitForEditorsToSettle())
        {
            return;
        }

        var written = paths.Where(path => !AssetFileStamps.IsAsRecorded(path)).Select(AssetFileStamps.Normalize).ToHashSet(AssetFileStamps.PathComparer);
        if (written.Count == 0)
        {
            return;
        }

        // Data that isn't loaded gets read from the file anyway, unless a scene drew it before letting go of it
        var assetManager = AssetManager.Get();
        var changed = assetManager.GetAssets()
            .Where(asset => !asset.IsInternal && written.Contains(AssetFileStamps.Normalize(asset.FullDataPath)))
            .Where(asset => asset.IsLoaded || AllTabs.Any(tab => tab.Shows(new HashSet<LabURI> { asset.URI })))
            .ToList();
        // A file that doesn't read (its program failed writing it, a model exported over another kind of asset) leaves what TT Lab has
        var readable = new List<IAsset>();
        foreach (var asset in changed)
        {
            if (await CanRead(asset))
            {
                readable.Add(asset);
            }
        }

        changed = readable;
        if (changed.Count == 0)
        {
            return;
        }

        var changedUris = changed.Select(asset => asset.URI).ToHashSet();
        var fresh = changed.Where(asset => askedAbout.Contains(AssetFileStamps.Normalize(asset.FullDataPath))).Select(asset => asset.URI).ToHashSet();
        var asked = AllTabs.Where(tab => tab.Document?.HasChangesIn(fresh) == true).ToList();
        var discard = false;
        if (asked.Count > 0)
        {
            var conflicting = changed.Where(asset => fresh.Contains(asset.URI) && asked.Any(tab => tab.Document!.HasChangesIn(new HashSet<LabURI> { asset.URI }))).ToList();
            discard = await AskToReloadChangedFiles(Question(conflicting, asked));
            if (!await WaitForEditorsToSettle())
            {
                return;
            }
        }

        // The editors as they are after the question, some may have closed or opened. Unsaved changes to a changed asset not asked about
        // (kept before) stay
        var tabs = AllTabs.Where(tab => tab.Shows(changedUris)).ToList();
        var kept = tabs.Where(tab => tab.Document?.HasChangesIn(changedUris) == true && !(discard && asked.Contains(tab))).ToList();
        var used = AllTabs.ToDictionary(tab => tab, tab => tab.GetReferencedAssets().ToHashSet());

        // What an editor keeping its changes shows stays as it is, the rest is read again by the editors showing it
        var keptAssets = kept.SelectMany(tab => used[tab]).ToHashSet();
        var reread = changed.Where(asset => !keptAssets.Contains(asset.URI)).ToList();
        var rereadUris = reread.Select(asset => asset.URI).ToHashSet();
        var reloading = tabs.Except(kept).Where(tab => tab.Shows(rereadUris)).ToList();
        var discarding = reloading.Where(tab => discard && asked.Contains(tab)).ToList();
        var keptStates = new Dictionary<TabbedEditorViewModel, TabbedEditorViewModel.KeptState?>();
        foreach (var tab in reloading)
        {
            keptStates[tab] = await tab.Unload(discarding.Contains(tab), rereadUris, NameAssets(reread));
        }

        // The changes discarded with an editor are in the data of what it showed as well, which goes unless another editor shows it
        var stillShown = AllTabs.Except(reloading).SelectMany(tab => used[tab]).ToHashSet();
        var discarded = discarding.SelectMany(tab => used[tab]).Where(uri => !stillShown.Contains(uri));
        foreach (var asset in reread.Concat(discarded.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset)).Distinct())
        {
            asset.UnloadData();
        }

        // The internal assets the old data made go with it, the new data makes its own
        assetManager.RemoveOrphanedInternalAssets(stillShown);
        foreach (var tab in reloading)
        {
            tab.LoadAgain(keptStates[tab]);
        }

        if (kept.Count > 0)
        {
            var keptChanged = changed.Where(asset => keptAssets.Contains(asset.URI)).ToList();
            Defer(keptChanged.Select(asset => asset.FullDataPath));
            Log.WriteLine($"{NameAssets(keptChanged)} changed outside TT Lab, {NameEditors(kept)} kept {(kept.Count == 1 ? "its" : "their")} unsaved changes");
        }

        if (reloading.Count > 0)
        {
            Log.WriteLine($"{NameAssets(reread)} changed outside TT Lab, reloaded {NameEditors(reloading)}");
        }
    }

    // Read the way an editor reads it, in a scope of its own the read data and what it makes go with
    private static Task<bool> CanRead(IAsset asset) => Task.Run(() =>
    {
        using var scope = new AssetDataScope();
        scope.ReadAgain(asset);
        try
        {
            asset.GetData<AbstractAssetData>();
            return true;
        }
        catch (Exception ex)
        {
            Log.WriteLine($"{asset.Name} changed outside TT Lab but couldn't be read, TT Lab keeps what it has: {ex.Message}", Log.LogType.Warning);
            return false;
        }
    });

    private static string Question(List<IAsset> conflicting, List<TabbedEditorViewModel> editors)
    {
        var one = editors.Count == 1;
        var names = NameEditors(editors);
        var files = conflicting.Count == 1 ? "file" : "files";
        return $"{NameAssets(conflicting)} changed outside TT Lab, and {names} {(one ? "has" : "have")} unsaved changes to {(conflicting.Count == 1 ? "it" : "them")}.\n\n" +
               $"Reload: {names} {(one ? "loses its" : "lose their")} unsaved changes and {(one ? "shows" : "show")} the {files}.\n" +
               $"Keep my changes: {names} {(one ? "stays as it is" : "stay as they are")}, saving writes over the {files}.";
    }

    private void Defer(IEnumerable<string> paths)
    {
        if (_deferred.Count == 0)
        {
            DocumentViewModel.Saved += OnDocumentSaved;
        }

        _deferred.UnionWith(paths);
    }

    private void OnDocumentSaved(DocumentViewModel document) => Dispatcher.UIThread.Post(RecheckDeferred, DispatcherPriority.Background);

    // An editor that kept its changes saved them over the files, or closed and let them go, so the others can show the files now
    private void RecheckDeferred()
    {
        if (_deferred.Count == 0)
        {
            return;
        }

        DocumentViewModel.Saved -= OnDocumentSaved;
        var paths = _deferred.ToList();
        _deferred.Clear();
        _ = Reload(paths, false);
    }

    private static string NameAssets(IEnumerable<IAsset> assets) => Names(assets.Select(asset => asset.Name).ToList());

    // What's asked about every editor's unsaved changes at once, tests answer it themselves
    internal Func<string, Task<UnsavedChangesDialogue.AnswerResult>> AskAboutUnsavedChanges { get; set; } = UnsavedChangesDialogue.Ask;

    public List<TabbedEditorViewModel> GetUnsavedEditors()
    {
        return ScenesEditorsViewModel.Tabs.Concat(ResourcesEditorsViewModel.Tabs).Where(tab => tab.IsLoaded && tab.Document is { IsDirty: true }).ToList();
    }

    /// <summary>
    /// Asks once about every editor's unsaved changes (closing TT Lab, the project or every editor), the way closing an editor asks about
    /// its own, and saves them when told to. Whether to go on: false when cancelled or a save failed, what's unsaved stays open then
    /// </summary>
    public async Task<bool> SaveOrDiscardUnsavedChanges()
    {
        var unsaved = GetUnsavedEditors();
        if (unsaved.Count == 0)
        {
            return true;
        }

        var answer = await AskAboutUnsavedChanges(NameEditors(unsaved));
        if (answer == UnsavedChangesDialogue.AnswerResult.CANCEL)
        {
            return false;
        }

        if (answer != UnsavedChangesDialogue.AnswerResult.YES)
        {
            return true;
        }

        foreach (var editor in unsaved)
        {
            try
            {
                editor.SaveTab();
            }
            catch (Exception exception)
            {
                Log.WriteLine($"Couldn't save {editor.AssetName}, nothing closes: {exception.Message}", Log.LogType.Error);
                return false;
            }
        }

        return true;
    }

    // One question about every editor's unsaved changes, each asked about its own one after another before
    public async Task<bool> CloseAllEditors()
    {
        if (!await SaveOrDiscardUnsavedChanges())
        {
            return false;
        }

        return await ScenesEditorsViewModel.CloseAllTabs(discardChanges: true) && await ResourcesEditorsViewModel.CloseAllTabs(discardChanges: true);
    }

    private static string NameEditors(List<TabbedEditorViewModel> editors) => Names(editors.Select(editor => editor.AssetName).ToList());

    // The first few of many
    private static string Names(List<string> names)
    {
        const int named = 3;
        if (names.Count > named)
        {
            return $"{string.Join(", ", names.Take(named))} and {names.Count - named} more";
        }

        return names.Count == 1 ? names[0] : $"{string.Join(", ", names.SkipLast(1))} and {names[^1]}";
    }

    public async Task<bool> CloseEditorsReferencing(IReadOnlySet<LabURI> assets)
    {
        return await ScenesEditorsViewModel.CloseTabsReferencing(assets) && await ResourcesEditorsViewModel.CloseTabsReferencing(assets);
    }

    public void ForgetDeletedAssets(IReadOnlySet<LabURI> assets)
    {
        if (_recentlyClosed.RemoveAll(closed => assets.Contains(closed.Uri)) > 0)
        {
            RecentlyClosedChanged?.Invoke();
        }
    }

    public void Save()
    {
        ScenesEditorsViewModel.Save();
        ResourcesEditorsViewModel.Save();
    }

    private void OnEditorClosed(TabbedEditorViewModel editor)
    {
        _recentlyClosed.RemoveAll(closed => closed.Uri == editor.EditableResource);
        _recentlyClosed.Insert(0, new ClosedEditor(editor.EditableResource, editor.AssetName));
        if (_recentlyClosed.Count > MaxRecentlyClosed)
        {
            _recentlyClosed.RemoveAt(_recentlyClosed.Count - 1);
        }

        RecentlyClosedChanged?.Invoke();
        ReleaseUnusedAssetData();
        RecheckDeferred();
    }

    private void ReleaseUnusedAssetData()
    {
        // Builds and saves work with the assets' data in the background
        if (_projectManager.OpenedProject == null || !_projectManager.WorkableProject)
        {
            return;
        }

        var openTabs = ScenesEditorsViewModel.Tabs.Concat(ResourcesEditorsViewModel.Tabs).ToList();
        // A tab that's still loading may hold data it doesn't track yet, releasing it would detach the data it edits from its asset.
        // Scenes being built also add internal assets from another thread while the asset manager isn't thread safe
        if (openTabs.Any(tab => !tab.IsLoaded || tab.Viewport is { IsBuildingScene: true }))
        {
            return;
        }

        var referencedAssets = openTabs.SelectMany(tab => tab.GetReferencedAssets()).ToHashSet();
        var assetManager = AssetManager.Get();
        var releasedAssets = 0;
        foreach (var asset in assetManager.GetAssets())
        {
            if (!asset.IsLoaded || asset.IsInternal || referencedAssets.Contains(asset.URI))
            {
                continue;
            }

            asset.UnloadData();
            releasedAssets++;
        }

        var removedInternalAssets = assetManager.RemoveOrphanedInternalAssets(referencedAssets);
        Log.WriteLine($"Released data of {releasedAssets} assets and removed {removedInternalAssets} internal assets no longer used by any editor", Log.LogType.Debug);
    }
}
