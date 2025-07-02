using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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

namespace TT_Lab.ViewModels
{
    public class ShellViewModel : Conductor<EditorsViewModel>, ILabManager
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly ProjectManager _projectManager;
        private OgreWindowManager? _ogreWindowManager;
        private readonly Dictionary<String, List<String>> _managerPropsToShellProps = new();
        private readonly DispatcherTimer _renderTimer = new();
        private Boolean _dontRemind = false;
        private Boolean _deadgeRender = false;
        private Thread _mainThread;

        public ShellViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, ProjectManager projectManager, OgreWindowManager ogreWindowManager)
        {
            _mainThread = Thread.CurrentThread;
            _windowManager = windowManager;
            _projectManager = projectManager;
            _eventAggregator = eventAggregator;
            _ogreWindowManager = ogreWindowManager;
            _eventAggregator.SubscribeOnUIThread(this);

            _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectTitle), new List<String> { nameof(WindowTitle) });
            _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectOpened), new List<String> { nameof(ProjectOpened), nameof(TreeOptionsVisibility) });
            _managerPropsToShellProps.Add(nameof(ProjectManager.RecentlyOpened), new List<String> { nameof(RecentlyOpened) });
            _managerPropsToShellProps.Add(nameof(ProjectManager.ProjectTree), new List<String> { nameof(ProjectTree) });
            _managerPropsToShellProps.Add(nameof(ProjectManager.HasRecents), new List<String> { nameof(HasRecents) });
            _managerPropsToShellProps.Add(nameof(ProjectManager.SearchAsset), new List<String> { nameof(SearchAsset) });
            _managerPropsToShellProps.Add(nameof(ProjectManager.IsCreatingProject), new List<String>{ nameof(IsCreatingProject), nameof(SadEasterEggVisibility) });

            CompositionTarget.Rendering += PerformRender;
            
            Preferences.Load();
        }

        private void PerformRender(Object? sender, EventArgs e)
        {
            Debug.Assert(Thread.CurrentThread.GetHashCode() == _mainThread.GetHashCode(), "Rendering indeed happens in separate thread.");
            if (_deadgeRender)
            {
                Console.WriteLine($"RENDERER IS DEAD BUT HOW DID WE CRASH AND NOT RETURN??? {_deadgeRender}");
                Debug.WriteLine($"RENDERER IS DEAD BUT HOW DID WE CRASH AND NOT RETURN??? {_deadgeRender}");
                return;
            }

            if (_ogreWindowManager == null)
            {
                return;
            }
            
            lock (_ogreWindowManager.RenderLockObject)
            {
                try
                {
                    _ogreWindowManager?.Render();
                }
                catch (AccessViolationException ex)
                {
                    Console.WriteLine("Bruh literally how?");
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public Task About()
        {
            return _windowManager.ShowDialogAsync(IoC.Get<AboutViewModel>());
        }

        public Task CreateProject()
        {
            return _windowManager.ShowDialogAsync(IoC.Get<ProjectCreationViewModel>());
        }

        public Task OpenPreferences()
        {
            return _windowManager.ShowDialogAsync(IoC.Get<PreferencesViewModel>());
        }

        public void SaveProject()
        {
            if (!_projectManager.ProjectOpened) return;

            _projectManager.WorkableProject = false;
            try
            {
                Log.WriteLine($"Saving {_projectManager.OpenedProject!.Name}...");
                var now = DateTime.Now;
                ActiveItem.Save();
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

        public void AssetBlockMouseDown(object selectedItem, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
            {
                return;
            }

            var asset = (ResourceTreeElementViewModel)selectedItem;
            OpenEditor(asset.Asset);
        }

        public void OpenEditor(IAsset asset)
        {
            try
            {
                if (asset.Type == typeof(Folder) || asset.Type == typeof(Package)) return;

                var editorsViewModel = ActiveItem;
                if (asset.Type == typeof(ChunkFolder))
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

        public void AssetBlockMouseMove(TreeView projectTree, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var asset = (ResourceTreeElementViewModel)projectTree.SelectedItem;
            var data = new DraggedData
            {
                Data = asset
            };
            DragDrop.DoDragDrop(projectTree, data, DragDropEffects.Copy);
        }

        public async Task PauseRendering()
        {
            // if (_deadgeRender)
            // {
            //     return;
            // }
            //
            // CompositionTarget.Rendering -= PerformRender;
            //
            // await Task.Delay(100);
            //
            // if (_deadgeRender)
            // {
            //     return;
            // }
            //
            // CompositionTarget.Rendering += PerformRender;
        }

        public Task StopRendering()
        {
            lock (_ogreWindowManager!.RenderLockObject)
            {
                _deadgeRender = true;
                // _ogreWindowManager.CloseAndTerminateAll();
                CompositionTarget.Rendering -= PerformRender;
            }

            return Task.CompletedTask;
        }

        // Props to https://stackoverflow.com/a/25765336
        public void LogViewerScroll(ScrollViewer sv, ScrollChangedEventArgs e)
        {
            bool autoScrollToEnd = true;
            if (sv.Tag != null)
            {
                autoScrollToEnd = (bool)sv.Tag;
            }
            if (e.ExtentHeightChange == 0)// user scroll
            {
                autoScrollToEnd = sv.ScrollableHeight == sv.VerticalOffset;
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
            await ActivateItemAsync(IoC.Get<EditorsViewModel>());
        }

        public void OpenProject()
        {
            var recents = Properties.Settings.Default.RecentProjects;
            var proj = MiscUtils.GetFileFromDialogue("PS2 TT Lab Project|*.tson|XBox TT Lab Project|*.xson", (recents != null && recents.Count != 0 ? recents[0] : "")!);
            if (proj != string.Empty)
            {
                var open = new OpenProjectCommand(System.IO.Path.GetDirectoryName(proj)!);
                open.Execute();
            }
        }

        public override async Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            _deadgeRender = true;
            if (_dontRemind)
            {
                await StopRendering();
                return true;
            }

            if (await ActiveItem.CanCloseAsync(cancellationToken))
            {
                await StopRendering();
                return true;
            }
            
            return false;
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

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(IoC.Get<EditorsViewModel>(), cancellationToken);
            return base.OnInitializeAsync(cancellationToken);
        }

        protected override async Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
        {
            if (close)
            {
                _deadgeRender = true;
                Properties.Settings.Default.Save();
                Preferences.Save();
            }
            
            await base.OnDeactivateAsync(close, cancellationToken);

            if (!cancellationToken.IsCancellationRequested && close)
            {
                _dontRemind = true;
                CompositionTarget.Rendering -= PerformRender;
            }
            else
            {
                _deadgeRender = false;
            }
        }

        public BindableCollection<MenuItem> RecentlyOpened => _projectManager.RecentlyOpened;

        public Visibility TreeOptionsVisibility => ProjectOpened ? Visibility.Visible : Visibility.Collapsed;

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
        
        public Visibility SadEasterEggVisibility => IsCreatingProject && Preferences.GetPreference<Boolean>(Preferences.SillinessEnabled) ? Visibility.Visible : Visibility.Collapsed;
    }
}
