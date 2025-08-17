using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Project.Messages;
using TT_Lab.Rendering;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Project
{
    public class ProjectManager
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly object _treeLock = new();

        private IProject? _openedProject;
        private bool _isCreatingProject;
        private readonly CommandManager _commandManager = new();
        private readonly BindableCollection<MenuItem> _recentMenus = new();
        private BindableCollection<ResourceTreeElementViewModel> _projectTree = new();
        private readonly BindableCollection<ResourceTreeElementViewModel> _internalTree = new();
        private bool _workableProject = false;
        private string _searchAsset = "";


        public ProjectManager(IEventAggregator eventAggregator)
        {
            BindingOperations.EnableCollectionSynchronization(_projectTree, _treeLock);
            _eventAggregator = eventAggregator;

            var recents = Properties.Settings.Default.RecentProjects;
            if (recents == null)
            {
                return;
            }
            
            foreach (var recent in recents)
            {
                _recentMenus.Add(GenerateRecentMenu(recent!));
            }
        }

        public IProject? OpenedProject
        {
            get => _openedProject;
            set
            {
                _openedProject = value;
                _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage());
            }
        }

        public bool IsCreatingProject
        {
            get => _isCreatingProject;
            set
            {
                _isCreatingProject = value;
                _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage());
            }
        }

        public bool WorkableProject
        {
            get => _workableProject;
            set
            {
                if (value != _workableProject)
                {
                    _workableProject = value;
                    _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage());
                }
            }
        }

        public String SearchAsset
        {
            get => _searchAsset;
            set
            {
                if (value != _searchAsset)
                {
                    _searchAsset = value;
                    DoSearch();
                    _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage());
                }
            }
        }

        private void DoSearch()
        {
            lock (_treeLock)
            {
                ProjectTree = new BindableCollection<ResourceTreeElementViewModel>();
            }

            foreach (var item in _internalTree)
            {
                item.ClearChildren();
            }

            if (_searchAsset == string.Empty)
            {
                foreach (var item in _internalTree)
                {
                    item.IsExpanded = false;
                    item.LoadChildrenBack();
                }
                lock (_treeLock)
                {
                    ProjectTree.AddRange(_internalTree);
                }
            }
            else
            {
                foreach (var e in _internalTree)
                {
                    if (e.Asset.Type == typeof(Folder) || e.Asset.Type == typeof(Package))
                    {
                        lock (_treeLock)
                        {
                            ProjectTree.Add(e);
                        }
                    }
                    var asset = FilterAsset(e, _searchAsset);
                    if (asset != null)
                    {
                        asset.IsExpanded = true;
                    }
                }
            }
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTree)));
        }

        private ResourceTreeElementViewModel? FilterAsset(ResourceTreeElementViewModel asset, String filter)
        {
            if (asset.GetInternalChildren() == null)
            {
                if (!asset.Alias.ToUpper().Contains(filter.ToUpper()))
                {
                    return null;
                }
            }
            else
            {
                var foundChild = false;
                foreach (var c in asset.GetInternalChildren()!)
                {
                    var child = FilterAsset(c, filter);
                    if (child != null)
                    {
                        foundChild = true;
                        asset.IsExpanded = true;
                        asset.AddChild(child);
                    }
                }
                // If subsequent folders don't contain the search we don't need that hierarchy
                if (!foundChild) return null;
            }
            return asset;
        }

        public BindableCollection<ResourceTreeElementViewModel> ProjectTree
        {
            get => _projectTree;
            private set => _projectTree = value;
        }

        public bool ProjectOpened => OpenedProject != null;

        public BindableCollection<ResourceTreeElementViewModel> FullProjectTree => _internalTree;

        public string ProjectTitle => OpenedProject != null ? $"TT Lab - {OpenedProject.Name}" : "TT Lab";

        public BindableCollection<MenuItem> RecentlyOpened => _recentMenus;

        public bool HasRecents => RecentlyOpened.Count != 0;

        public void CreateProject(string name, string path, string? discContentPathPS2, string? discContentPathXbox, bool copyDiscContents)
        {
            Log.Clear();
            IsCreatingProject = true;
            DateTime projCreateStart = DateTime.Now;
            var ps2ContentProvided = false;
            var xboxContentProvided = false;
            if (!string.IsNullOrEmpty(discContentPathPS2))
            {
                var ps2DiscFiles = Directory.GetFiles(discContentPathPS2).Select(Path.GetFileName).Select(str => str?.ToLower()).ToArray();
                // Check for PS2 required root disc files
                if (!ps2DiscFiles.Contains("system.cnf"))
                {
                    Log.WriteLine("ERROR: Improper PS2 disc content provided!");
                    return;
                }
                ps2ContentProvided = true;
            }
            if (!string.IsNullOrEmpty(discContentPathXbox))
            {
                var xboxDiscFiles = Directory.GetFiles(discContentPathXbox).Select(Path.GetFileName).Select(str => str?.ToLower()).ToArray();
                // Check for XBOX required root disc files
                if (!xboxDiscFiles.Contains("default.xbe"))
                {
                    Log.WriteLine("ERROR: Improper XBox disc content provided!");
                    return;
                }
                xboxContentProvided = true;
            }
            if (!ps2ContentProvided && !xboxContentProvided)
            {
                Log.WriteLine("ERROR: No content was provided for creating a new project!");
                return;
            }

            OpenedProject = new Project(name, path, discContentPathPS2, discContentPathXbox);
            OpenedProject.CreateProjectStructure();
            Task.Factory.StartNew(() =>
            {
#if !DEBUG
                try
                {
#endif
                if (copyDiscContents)
                {
                    Log.WriteLine("Copying disc contents to project...");
                    OpenedProject.CopyDiscContents();
                }
                // Unpack assets
                Directory.SetCurrentDirectory("assets");
                Log.WriteLine("Creating base packages...");
                OpenedProject.CreateBasePackages();
                Log.WriteLine("Unpacking PS2 assets...");
                OpenedProject.UnpackAssetsPS2();
                Log.WriteLine("Unpacking XBox assets...");
                OpenedProject.UnpackAssetsXbox();

                Log.WriteLine($"Converting assets...");
                foreach (var asset in OpenedProject.AssetManager.GetAssets())
                {
                    asset.Import();
                }
                var assetsToImport = OpenedProject.AssetManager.GetAssetsToImport();
                while (!assetsToImport.IsEmpty)
                {
                    foreach (var asset in assetsToImport)
                    {
                        asset.Import();
                        // If asset is embedded it shouldn't be exported during game's build stage because its owner will do it for us
                        asset.SkipExport = true;
                        OpenedProject.AssetManager.AddAsset(asset);
                    }
                    assetsToImport = OpenedProject.AssetManager.GetAssetsToImport();
                }

                Log.WriteLine("Serializing assets...");
                OpenedProject.Serialize(); // Call to serialize the asset list and chunk list

                Log.WriteLine("Building project tree...");
                BuildProjectTree();

                Execute.OnUIThread(() =>
                {
                    AddRecentlyOpened(OpenedProject.ProjectPath);
                });
                
                WorkableProject = true;
                IsCreatingProject = false;
                _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectOpened)));
                _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTitle)));
                GC.Collect();
                Log.WriteLine($"Project created in {DateTime.Now - projCreateStart}");
#if !DEBUG
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Error when working with assets: {ex.Message}\n{ex.StackTrace}");
                }
#endif
            });
        }

        public void OpenProject(string path)
        {
            if (OpenedProject != null)
            {
                CloseProject();
            }

            Log.Clear();
#if !DEBUG
            try
            {
#endif
            // Check if path even exists to begin with
            if (!Directory.Exists(path))
            {
                RemoveRecentlyOpened(path);
                return;
            }
            // Check for PS2 and XBox project root files
            if (Directory.GetFiles(path, "*.tson").Length == 0 && Directory.GetFiles(path, "*.xson").Length == 0)
            {
                RemoveRecentlyOpened(path);
                return;
            }

            if (Directory.GetFiles(path, "*.tson").Length != 0)
            {
                var prFile = Directory.GetFiles(path, "*.tson")[0];
                Task.Factory.StartNew(() =>
                {
#if !DEBUG
                        try
                        {
#endif
                    Log.WriteLine($"Opening project {Path.GetFileName(prFile)}...");
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Project.Deserialize(prFile);
                    Log.WriteLine($"Building project tree...");
                    BuildProjectTree();
                    WorkableProject = true;
                    _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectOpened)));
                    _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTitle)));
                    // _ogreWindowManager.AddResourceLocation(OpenedProject!.ProjectPath);
                    Log.WriteLine($"Project opened in {stopwatch.Elapsed}");
                    GC.Collect();
#if !DEBUG
                        }
                        catch (Exception ex)
                        {
                            Log.WriteLine($"Error opening project: {ex.Message}\n{ex.StackTrace}");
                        }
#endif
                });
            }
#if !DEBUG
        }
            catch (Exception ex)
            {
                RemoveRecentlyOpened(path);
                throw;
            }
#endif
            AddRecentlyOpened(path);
        }

        public void BuildPs2Project()
        {
            WorkableProject = false;
            Task.Factory.StartNew(() =>
            {
                var pr = OpenedProject!;
                pr.PackAssetsPS2();
                WorkableProject = true;
            });
        }

        public void BuildPs2Iso()
        {
            WorkableProject = false;
            Task.Factory.StartNew(() =>
            {
                var pr = OpenedProject!;
                pr.CreatePs2ArchivesAndIso();
                WorkableProject = true;
            });
        }

        public void CloseProject()
        {
            // if (OpenedProject != null)
            // {
            //     _ogreWindowManager.RemoveResourceLocation($"{OpenedProject.ProjectPath}/assets");
            // }
            
            OpenedProject = null;
            WorkableProject = false;
            ProjectTree.Clear();
            _internalTree.Clear();
            Log.Clear();
            GC.Collect();
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(WorkableProject)));
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectOpened)));
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTitle)));
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTree)));
        }

        public void ExecuteCommand(ICommand command)
        {
            _commandManager.Execute(command);
        }

        public void Undo()
        {
            _commandManager.Undo();
        }

        public void Redo()
        {
            _commandManager.Redo();
        }

        private void BuildProjectTree()
        {
            var tree = (from asset in OpenedProject!.AssetManager.GetAssets()
                        where asset is Folder
                        let folder = (Folder)asset
                        where folder.GetData().To<FolderData>().Parent == null
                        orderby folder.Order
                        select folder.GetResourceTreeElement());
            ProjectTree = new BindableCollection<ResourceTreeElementViewModel>(tree);
            _internalTree.AddRange(ProjectTree);
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTree)));
        }

        private void AddRecentlyOpened(string path)
        {
            var recents = Properties.Settings.Default.RecentProjects;
            if (recents == null)
            {
                recents = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.RecentProjects = recents;
            }
            if (!recents.Contains(path))
            {
                recents.Insert(0, path);
                RecentlyOpened.Insert(0, GenerateRecentMenu(path));
                // Store only last 10 paths
                if (recents.Count > 10)
                {
                    recents.RemoveAt(10);
                    RecentlyOpened.RemoveAt(10);
                }
            }
            else
            {
                var index = recents.IndexOf(path);
                recents.RemoveAt(index);
                RecentlyOpened.RemoveAt(index);
                recents.Insert(0, path);
                RecentlyOpened.Insert(0, GenerateRecentMenu(path));
            }
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(RecentlyOpened)));
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(HasRecents)));
        }

        private void RemoveRecentlyOpened(string path)
        {
            if (Properties.Settings.Default.RecentProjects == null || !Properties.Settings.Default.RecentProjects.Contains(path)) return;

            var recents = Properties.Settings.Default.RecentProjects;
            var recentIdx = recents.IndexOf(path);
            recents.Remove(path);
            RecentlyOpened.RemoveAt(recentIdx);
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(RecentlyOpened)));
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(HasRecents)));
        }

        private static MenuItem GenerateRecentMenu(String recentPath)
        {
            return new MenuItem
            {
                Header = $"{recentPath}",
                Command = new OpenProjectCommand(recentPath),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
            };
        }
    }
}
