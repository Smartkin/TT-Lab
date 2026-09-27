using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.ViewModels.Composite;

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

    public async Task<bool> CloseAllEditors()
    {
        return await ScenesEditorsViewModel.CloseAllTabs() && await ResourcesEditorsViewModel.CloseAllTabs();
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
