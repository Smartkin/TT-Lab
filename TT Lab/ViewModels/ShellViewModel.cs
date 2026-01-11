using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SharpGLTF.Schema2;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Rendering;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.ViewModels;

public class ShellViewModel : Conductor<EditorsViewModel>, ILabManager
{
    private readonly IWindowManager _windowManager;
    private readonly IEventAggregator _eventAggregator;
    private readonly ProjectManager _projectManager;
    private readonly Dictionary<String, List<String>> _managerPropsToShellProps = new();
    private Boolean _dontRemind = false;

    public ShellViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, ProjectManager projectManager)
    {
        _windowManager = windowManager;
        _projectManager = projectManager;
        _eventAggregator = eventAggregator;
        _eventAggregator.SubscribeOnUIThread(this);

        _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectTitle), [nameof(WindowTitle)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectOpened), [nameof(ProjectOpened), nameof(TreeOptionsVisibility)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.RecentlyOpened), [nameof(RecentlyOpened)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectTree), [nameof(ProjectTree)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.HasRecents), [nameof(HasRecents)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.SearchAsset), [nameof(SearchAsset)]);
        _managerPropsToShellProps.Add(nameof(ProjectManager.IsCreatingProject), [nameof(IsCreatingProject), nameof(SadEasterEggVisibility)]);

        Preferences.Load();
    }

    public void SaveProject()
    {
        if (!_projectManager.ProjectOpened) return;

        _projectManager.WorkableProject = false;
        try
        {
            Log.WriteLine($"Saving {_projectManager.OpenedProject!.Name}...");
            var now = DateTime.Now;
            // ActiveItem.Save();
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

    public void AssetBlockMouseDown(object selectedItem, PointerPressedEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        if (selectedItem is not ResourceTreeElementViewModel asset)
        {
            return;
        }
            
        OpenEditor(asset.Asset);
    }

    public void OpenEditor(IAsset asset)
    {
        try
        {
            if (asset.Type == typeof(Folder) || asset.Type == typeof(Package)) return;

            var editorsViewModel = ActiveItem;
            if (asset.Type == typeof(LevelChunk))
            {
                // Automatically switch to Scenes Viewer tab
                editorsViewModel.ActivateItemAsync(editorsViewModel.Items[0]);
                _eventAggregator.PublishOnUIThreadAsync(new CreateEditorMessage<ChunkEditorViewModel>(asset.URI, typeof(ChunkEditorViewModel)));
                return;
            }
            
            // Automatically switch to Resources Editor tab
            editorsViewModel.ActivateItemAsync(editorsViewModel.Items[1]);
            var editorType = asset.GetEditorType();
            var message = new CreateEditorMessage<ResourceEditorViewModel>(asset.URI, editorType);
            _eventAggregator.PublishOnUIThreadAsync(message);
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

    // Props to https://stackoverflow.com/a/25765336
    public void LogViewerScroll(object scrollViewer, object evArgs)
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        
        var sv = (ScrollViewer)scrollViewer;
        var e = (ScrollChangedEventArgs)evArgs;
        var autoScrollToEnd = true;
        if (sv.Tag != null)
        {
            autoScrollToEnd = (bool)sv.Tag;
        }
        if (e.ExtentDelta.NearlyEquals(Vector.Zero))// user scroll
        {
            autoScrollToEnd = Math.Abs(sv.ScrollBarMaximum.Y - sv.Offset.Y) < 0.001f;
        }
        else// content change
        {
            if (autoScrollToEnd)
            {
                sv.ScrollToEnd();
            }
        }
        sv.Tag = autoScrollToEnd;
    }

    public void BuildPs2()
    {
        _projectManager.BuildPs2Project();
    }

    public void BuildPs2Iso()
    {
        _projectManager.BuildPs2Iso();
    }

    public async Task CloseProject()
    {
        var canClose = await ActiveItem.CanCloseAsync();
        if (!canClose)
        {
            return;
        }
        
        await DeactivateItemAsync(ActiveItem, true);
        _projectManager.CloseProject();
        await ActivateItemAsync(Locator.Current.GetService<EditorsViewModel>()!);
    }

    public async Task OpenProject()
    {
        var proj = await MiscUtils.GetFileFromDialogueAsync("Choose TT Lab Project...", "TT Lab Project Files", ["*.tson", "*.xson"], Preferences.GetPreference<string>(Preferences.ProjectsPath));
        if (proj != string.Empty)
        {
            var open = new OpenProjectCommand(System.IO.Path.GetDirectoryName(proj)!);
            open.Execute();
        }
    }

    public override async Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        if (_dontRemind)
        {
            return true;
        }

        return await ActiveItem.CanCloseAsync(cancellationToken);
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

    // protected override Task OnInitializedAsync(CancellationToken cancellationToken)
    // {
    //     ActivateItemAsync(IoC.Get<EditorsViewModel>(), cancellationToken);
    //     return base.OnInitializedAsync(cancellationToken);
    // }

    protected override Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        ActivateItemAsync(Locator.Current.GetService<EditorsViewModel>()!, cancellationToken);
        return base.OnActivatedAsync(cancellationToken);
    }

    protected override async Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
    {
        if (close)
        {
            // Properties.Settings.Default.Save();
            Preferences.Save();
        }
            
        await base.OnDeactivateAsync(close, cancellationToken);

        if (!cancellationToken.IsCancellationRequested && close)
        {
            _dontRemind = true;
        }

        await Task.CompletedTask;
    }

    public BindableCollection<MenuItem> RecentlyOpened => _projectManager.RecentlyOpened;

    public Boolean TreeOptionsVisibility => ProjectOpened;

    public String WindowTitle => _projectManager.ProjectTitle;

    public String SearchAsset
    {
        get => _projectManager.SearchAsset;
        set => _projectManager.SearchAsset = value;
    }

    public Boolean HasRecents => _projectManager.HasRecents;

    public BindableCollection<ResourceTreeElementViewModel> ProjectTree => _projectManager.ProjectTree;

    public Boolean ProjectOpened => _projectManager.ProjectOpened;

    public Boolean IsCreatingProject => _projectManager.IsCreatingProject;

    public Stream SadEasterEgg => new FileStream(ManifestResourceLoader.GetPathInExe("Images/SadTransparent.gif"), FileMode.Open, FileAccess.Read);
    
    public Boolean SadEasterEggVisibility => IsCreatingProject && Preferences.GetPreference<Boolean>(Preferences.SillinessEnabled);
}