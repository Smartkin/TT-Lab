using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Dock.Model.Controls;
using ReactiveUI;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels;

public record RecentlyClosedEditorEntry(string Name, System.Windows.Input.ICommand Reopen);

public record RecentProjectEntry(string Name, string Path, System.Windows.Input.ICommand Open)
{
    public string Location => System.IO.Path.GetDirectoryName(Path) ?? Path;
}

public class ShellViewModel : Screen, ILabManager
{
    private const string LayoutFileFilterName = "TT Lab Layout Files";
    private static readonly string[] LayoutFileFilters = ["*.json"];
    private static readonly string AutoSavedLayoutPath = ManifestResourceLoader.GetPathInExe("layout.json");

    private readonly IWindowManager _windowManager;
    private readonly IEventAggregator _eventAggregator;
    private readonly ProjectManager _projectManager;
    private readonly ProjectTreeViewModel _projectTree;
    private readonly ChunkResourcesViewModel _chunkResources;
    private readonly ChunkInspectorViewModel _chunkInspector;
    private readonly HistoryViewModel _history;
    private readonly PrefabsViewModel _prefabs;
    private readonly Dictionary<String, List<String>> _managerPropsToShellProps = new();

    public ShellViewModel(IWindowManager windowManager, LogViewModel logViewModel, EditorsViewModel editors,
        ProjectTreeViewModel projectTree, ChunkResourcesViewModel chunkResources, ChunkInspectorViewModel chunkInspector, HistoryViewModel history, PrefabsViewModel prefabs, DockFactory dockFactory,
        IEventAggregator eventAggregator, ProjectManager projectManager)
    {
        _chunkResources = chunkResources;
        _chunkInspector = chunkInspector;
        _history = history;
        _prefabs = prefabs;
        Logger = logViewModel;
        DockFactory = dockFactory;
        _windowManager = windowManager;
        EditorsViewModel = editors;
        _projectTree = projectTree;
        _projectManager = projectManager;
        _eventAggregator = eventAggregator;
        _eventAggregator.SubscribeOnUIThread(this);

        // The log panel can now be closed so logging can't depend on its view having been shown
        Log.SetViewModel(logViewModel);
        if (Log.SessionLogPath != null)
        {
            Log.WriteLine($"Session log is saved to {Log.SessionLogPath}");
        }

        _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectTitle), [nameof(WindowTitle)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectOpened), [nameof(ProjectOpened), nameof(TreeOptionsVisibility)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.IsCreatingProject), [nameof(IsCreatingProject)]);

        EditorsViewModel.RecentlyClosedChanged += UpdateRecentlyClosedEditors;

        Preferences.Load();
        _projectManager.RecentProjectsChanged += UpdateRecentProjects;
        UpdateRecentProjects();
        Layout = LoadAutoSavedLayout() ?? DockFactory.CreateLayout();
    }

    public void SaveProject()
    {
        if (!_projectManager.ProjectOpened) return;

        _projectManager.WorkableProject = false;
        try
        {
            Log.WriteLine($"Saving {_projectManager.OpenedProject!.Name}...");
            var now = DateTime.Now;
            EditorsViewModel.Save();
            Log.WriteLine($"Saved project in {DateTime.Now - now}");
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Error saving project: {ex.Message}");
        }
        finally
        {
            _projectManager.WorkableProject = true;
        }
    }

    public void OpenEditor(IAsset asset)
    {
        try
        {
            var isFolder = asset.Type == typeof(Folder);
            var openedAsset = asset;

            if (isFolder)
            {
                if (((Folder)asset).Mark.HasFlag(FolderMark.IsChunk))
                {
                    openedAsset = AssetManager.Get().GetAsset(((Folder)asset).Children[0]);
                }
                else
                {
                    return;
                }
            }

            // A level chunk's resources are edited in its scene and never get a tab of their own, the startup chunk's are the game's
            if (LevelChunk.Owning(openedAsset) is { IsGlobalDefaultChunk: false } chunk)
            {
                openedAsset = chunk;
            }

            DockFactory.ShowPanel(EditorsViewModel.GetViewerFor(openedAsset));
            EditorsViewModel.OpenEditor(openedAsset);
            // Scene tabs only have the viewport, the chunk's resources are in their own panel
            if (openedAsset is LevelChunk && !DockFactory.IsInLayout(_chunkResources))
            {
                DockFactory.ShowPanel(_chunkResources);
            }
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Failed to create editor: {ex.Message}");
        }
    }

    public void AssetBlockMouseMove(TreeView projectTree, PointerEventArgs e)
    {
        // if (e.LeftButton != MouseButtonState.Pressed)
        // {
        //     return;
        // }
        //
        // var asset = (ResourceTreeElementViewModel)projectTree.SelectedItem;
        // var data = new DraggedData
        // {
        //     Data = asset
        // };
        // DragDrop.DoDragDrop(projectTree, data, DragDropEffects.Copy);
    }

    public void LaunchTwinMusic() => Tools.ExternalTools.Launch(DiscFileTool.Music);

    public void LaunchTwinPss() => Tools.ExternalTools.Launch(DiscFileTool.Video);

    public void OpenToolsFolder()
    {
        Directory.CreateDirectory(Tools.ExternalTools.ToolsFolder);
        Tools.ExternalTools.ShowInFileManager(Tools.ExternalTools.ToolsFolder);
    }

    // Downloads the tools' latest release for this system into their folders, the log tells how it went
    public async Task InstallTools()
    {
        Log.WriteLine("Installing the twinstudio tools...");
        var installed = await Task.Run(() => Tools.ExternalTools.InstallAsync());
        Log.WriteLine(installed ? "The twinstudio tools are installed, the Tools menu and the disc's files use them" : "The twinstudio tools couldn't all be installed, see the messages above", installed ? Log.LogType.Info : Log.LogType.Warning);
    }

    public async Task OpenBuildDialog()
    {
        if (_projectManager.OpenedProject is not TT_Lab.Project.Project project)
        {
            return;
        }

        var viewModel = new BuildDialogViewModel(project);
        var dialog = new Views.BuildDialogView { DataContext = viewModel };
        await dialog.ShowDialog(MiscUtils.GetMainWindow());
        if (viewModel.Result != null)
        {
            _projectManager.Build(viewModel.Result);
        }
    }

    public void BuildPs2()
    {
        _projectManager.BuildPs2Project();
    }

    public void BuildPs2Iso()
    {
        _projectManager.BuildPs2Iso();
    }

    public void BuildXbox()
    {
        _projectManager.BuildXboxProject();
    }

    public void BuildXboxImage()
    {
        _projectManager.BuildXboxImage();
    }

    public async Task CloseProject()
    {
        if (!await EditorsViewModel.CloseAllEditors())
        {
            return;
        }

        EditorsViewModel.ClearRecentlyClosed();
        _projectManager.CloseProject();
    }

    public async Task OpenProject()
    {
        var proj = await MiscUtils.GetFileFromDialogueAsync("Choose TT Lab Project...", "TT Lab Project Files", ["*.tson", "*.xson"], Preferences.GetPreference<string>(Preferences.ProjectsPath));
        if (proj != string.Empty)
        {
            await OpenProjectAt(System.IO.Path.GetDirectoryName(proj)!);
        }
    }

    // The editors of the project that's open close first, like closing the project, they ask to save their changes
    private async Task OpenProjectAt(string path)
    {
        if (!await EditorsViewModel.CloseAllEditors())
        {
            return;
        }

        EditorsViewModel.ClearRecentlyClosed();
        new OpenProjectCommand(path).Execute();
    }

    public void ShowProjectTree() => DockFactory.ShowPanel(_projectTree);

    public void ShowScenes() => DockFactory.ShowPanel(EditorsViewModel.ScenesEditorsViewModel);

    public void ShowResources() => DockFactory.ShowPanel(EditorsViewModel.ResourcesEditorsViewModel);

    public void ShowLog() => DockFactory.ShowPanel(Logger);

    public void ShowChunkResources() => DockFactory.ShowPanel(_chunkResources);

    public void ShowChunkInspector() => DockFactory.ShowPanel(_chunkInspector);

    public void ShowHistory() => DockFactory.ShowPanel(_history);

    public void ShowPrefabs() => DockFactory.ShowPanel(_prefabs);

    // Open Asset (Ctrl+Shift+O): any asset of the project found by a fuzzy search of its name
    public async Task OpenAsset()
    {
        if (!ProjectOpened)
        {
            return;
        }

        if (await Controls.QuickOpenDialogue.Ask() is { } asset)
        {
            OpenEditor(asset);
        }
    }

    public void ReopenClosedEditor()
    {
        var closedEditor = EditorsViewModel.RecentlyClosed.FirstOrDefault();
        if (closedEditor != null)
        {
            ReopenEditor(closedEditor);
        }
    }

    public async Task SaveLayoutAs()
    {
        var path = await MiscUtils.GetSaveFileFromDialogueAsync("Save Layout...", LayoutFileFilterName, LayoutFileFilters, "Custom.layout.json", "json");
        if (path == string.Empty)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(path, DockFactory.SerializeLayout());
            Log.WriteLine($"Saved layout to {path}");
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Failed to save layout: {ex.Message}", Log.LogType.Error);
        }
    }

    public async Task LoadLayoutFrom()
    {
        var path = await MiscUtils.GetFileFromDialogueAsync("Load Layout...", LayoutFileFilterName, LayoutFileFilters);
        if (path == string.Empty)
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            DockFactory.ValidateLayout(json);
            ApplyLayout(() => DockFactory.DeserializeLayout(json));
            Log.WriteLine($"Loaded layout from {path}");
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Failed to load layout: {ex.Message}", Log.LogType.Error);
        }
    }

    public void ResetLayout()
    {
        ApplyLayout(DockFactory.CreateLayout);
    }

    public void SaveLayoutOnExit()
    {
        try
        {
            File.WriteAllText(AutoSavedLayoutPath, DockFactory.SerializeLayout());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save layout: {ex.Message}");
        }
    }

    public Task HandleAsync(ProjectManagerMessage message, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(() =>
            {
                if (!_managerPropsToShellProps.TryGetValue(message.PropertyName, out List<String>? affectedProps))
                {
                    return;
                }

                foreach (var prop in affectedProps)
                {
                    NotifyOfPropertyChange(prop);
                }
            },
            cancellationToken);
    }

    protected override async Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
    {
        if (close)
        {
            Preferences.Save();
        }

        await base.OnDeactivateAsync(close, cancellationToken);
    }

    private void ApplyLayout(Func<IRootDock> createLayout)
    {
        // Floating windows have to be closed while their panels still belong to the old layout
        if (Layout.ExitWindows.CanExecute(null))
        {
            Layout.ExitWindows.Execute(null);
        }

        Layout = createLayout();
        NotifyOfPropertyChange(nameof(Layout));
    }

    private IRootDock? LoadAutoSavedLayout()
    {
        if (!File.Exists(AutoSavedLayoutPath))
        {
            return null;
        }

        try
        {
            return DockFactory.DeserializeLayout(File.ReadAllText(AutoSavedLayoutPath));
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Failed to restore the previous layout, using the default one: {ex.Message}", Log.LogType.Warning);
            return null;
        }
    }

    private void ReopenEditor(ClosedEditor closedEditor)
    {
        var assetManager = AssetManager.Get();
        if (!_projectManager.ProjectOpened || !assetManager.DoesAssetExist(closedEditor.Uri))
        {
            Log.WriteLine($"Can't reopen {closedEditor.Name}, the asset no longer exists", Log.LogType.Warning);
            EditorsViewModel.ForgetRecentlyClosed(closedEditor);
            return;
        }

        OpenEditor(assetManager.GetAsset(closedEditor.Uri));
    }

    private void UpdateRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var path in _projectManager.RecentProjects)
        {
            RecentProjects.Add(new RecentProjectEntry(System.IO.Path.GetFileName(path), path, ReactiveCommand.CreateFromTask(() => OpenProjectAt(path))));
        }

        NotifyOfPropertyChange(nameof(HasRecents));
    }

    private void UpdateRecentlyClosedEditors()
    {
        RecentlyClosedEditors.Clear();
        foreach (var closedEditor in EditorsViewModel.RecentlyClosed)
        {
            RecentlyClosedEditors.Add(new RecentlyClosedEditorEntry(closedEditor.Name, ReactiveCommand.Create(() => ReopenEditor(closedEditor))));
        }

        NotifyOfPropertyChange(nameof(HasRecentlyClosedEditors));
    }

    public IRootDock Layout { get; private set; }

    public ObservableCollection<RecentlyClosedEditorEntry> RecentlyClosedEditors { get; } = [];

    public bool HasRecentlyClosedEditors => RecentlyClosedEditors.Count > 0;

    public ObservableCollection<RecentProjectEntry> RecentProjects { get; } = [];

    public Boolean TreeOptionsVisibility => ProjectOpened;

    public String WindowTitle => _projectManager.ProjectTitle;

    public LogViewModel Logger { get; }

    public DockFactory DockFactory { get; }

    public EditorsViewModel EditorsViewModel { get; }

    public bool HasRecents => RecentProjects.Count > 0;

    public Boolean ProjectOpened => _projectManager.ProjectOpened;

    public Boolean IsCreatingProject => _projectManager.IsCreatingProject;
}
