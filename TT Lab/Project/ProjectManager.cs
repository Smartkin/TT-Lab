using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
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
        private BindableCollection<ResourceTreeElementViewModel> _projectTree = new();
        private readonly BindableCollection<ResourceTreeElementViewModel> _internalTree = new();
        private bool _workableProject = false;
        private string _searchAsset = "";


        public ProjectManager(IEventAggregator eventAggregator)
        {
            // BindingOperations.EnableCollectionSynchronization(_projectTree, _treeLock);
            _eventAggregator = eventAggregator;
        }

        private const int MaxRecentProjects = 10;

        public event System.Action? RecentProjectsChanged;

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

        public IReadOnlyList<string> RecentProjects => Preferences.GetPreference<List<string>>(Preferences.RecentProjects);

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
                // Reading chunks and importing and writing assets all run in parallel, the gate keeps them within the memory budget. Each
                // version of the game is done before the next one's disc gets read. The items read from a disc take a good part of the
                // budget until their assets are written, more work starts until it's mostly used
                using (var gate = new MemoryGate((long)(Preferences.GetPreference<Double>(Preferences.BuildMemoryBudget) * 1024 * 1024), 0.8))
                {
                    var writer = new CreationWriter(OpenedProject.AssetManager, gate);
                    Log.WriteLine("Unpacking PS2 assets...");
                    OpenedProject.UnpackAssetsPS2(gate);
                    writer.ImportAndWrite();
                    writer.RemoveInternalAssets();
                    MergeVariants();
                    Log.WriteLine("Unpacking XBox assets...");
                    OpenedProject.UnpackAssetsXbox(gate);
                    writer.ImportAndWrite();
                    writer.RemoveInternalAssets();
                    MergeVariants();

                    Log.WriteLine("Serializing assets...");
                    OpenedProject.Serialize(writer.IsWritten); // Call to serialize the asset list and chunk list
                }

                Log.WriteLine("Post processing assets...");
                var assetsToPostProcess = OpenedProject.AssetManager.GetAssets();
                foreach (var asset in assetsToPostProcess)
                {
                    asset.PostDeserialize();
                }

                Log.WriteLine("Building project tree...");
                BuildProjectTree();

                Dispatcher.UIThread.Invoke(() =>
                {
                    AddRecentlyOpened(OpenedProject.ProjectPath);
                }, DispatcherPriority.Background);
                
                WorkableProject = true;
                IsCreatingProject = false;
                _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectOpened)));
                _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTitle)));
                MiscUtils.CollectReleasedMemory();
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

        // The variants of assets that only differ in values become the chunks' overrides of the first chunk's asset
        private void MergeVariants()
        {
            var start = DateTime.Now;
            var merged = new VariantMerger(OpenedProject!.AssetManager, Path.Combine(OpenedProject.ProjectPath, "assets")).Merge();
            Log.WriteLine($"Merged {merged} variants into their assets as chunks' overrides in {DateTime.Now - start}");
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
                Log.WriteLine($"Can't open the project, {path} doesn't exist anymore", Log.LogType.Warning);
                RemoveRecentlyOpened(path);
                return;
            }
            // Check for PS2 and XBox project root files
            if (Directory.GetFiles(path, "*.tson").Length == 0 && Directory.GetFiles(path, "*.xson").Length == 0)
            {
                Log.WriteLine($"Can't open the project, there's no TT Lab project in {path}", Log.LogType.Warning);
                RemoveRecentlyOpened(path);
                return;
            }

            if (Directory.GetFiles(path, "*.tson").Length != 0)
            {
                var prFile = Directory.GetFiles(path, "*.tson")[0];
                Task.Factory.StartNew(() =>
                {
                    try
                    {
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
                        MiscUtils.CollectReleasedMemory();
                    }
                    // Nothing observes this task, so projects TT Lab can't open have to be reported here even in debug builds
                    catch (ProjectException ex)
                    {
                        ReportOpeningError(ex.Message);
                    }
#if !DEBUG
                    catch (Exception ex)
                    {
                        ReportOpeningError(ex.Message);
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

        private static void ReportOpeningError(string message)
        {
            Log.WriteLine($"Error opening project: {message}", Log.LogType.Error);
            Dispatcher.UIThread.Post(() => new MessageDialogue("Can't open the project", message).ShowDialog(MiscUtils.GetMainWindow()));
        }

        public void BuildPs2Project()
        {
            WorkableProject = false;
            Task.Factory.StartNew(() =>
            {
                var pr = OpenedProject!;
#if !DEBUG
                try {
#endif
                pr.PackAssetsPS2();
#if !DEBUG
                } catch (Exception ex)
                {
                    Log.WriteLine($"Error building PS2 project: {ex.Message}");
                }
#endif
                WorkableProject = true;
            });
        }

        public void BuildPs2Iso()
        {
            WorkableProject = false;
            Task.Factory.StartNew(() =>
            {
                var pr = OpenedProject!;
#if !DEBUG
                try {
#endif
                pr.CreatePs2ArchivesAndIso();
#if !DEBUG
                } catch (Exception ex)
                {
                    Log.WriteLine($"Error creating PS2 ISO: {ex.Message}");
                }
#endif
                WorkableProject = true;
            });
        }

        public void BuildXboxProject()
        {
            RunBuild("Error building Xbox project", project => project.PackAssetsXbox());
        }

        public void BuildXboxImage()
        {
            RunBuild("Error putting the Xbox game together", project => project.CreateXboxGame());
        }

        private void RunBuild(string errorMessage, Action<IProject> build)
        {
            WorkableProject = false;
            Task.Factory.StartNew(() =>
            {
                var pr = OpenedProject!;
#if !DEBUG
                try {
#endif
                build(pr);
#if !DEBUG
                } catch (Exception ex)
                {
                    Log.WriteLine($"{errorMessage}: {ex.Message}");
                }
#endif
                WorkableProject = true;
            });
        }

        public void CloseProject()
        {
            OpenedProject = null;
            WorkableProject = false;
            ProjectTree.Clear();
            _internalTree.Clear();
            Log.Clear();
            MiscUtils.CollectReleasedMemory();
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
            var root = new Folder(OpenedProject!.Name)
            {
                Mark = FolderMark.Locked
            };
            
            var assetRoot = $"{OpenedProject!.ProjectPath}";
            var dirInfo = new DirectoryInfo(assetRoot);
            ExploreFolder(root, dirInfo, false);
            ProjectTree = new BindableCollection<ResourceTreeElementViewModel>(root.Children.Select(uri => OpenedProject!.AssetManager.GetAsset(uri).GetResourceTreeElement()));
            _internalTree.AddRange(ProjectTree);
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTree)));
        }

        private static readonly string[] _reservedLockedDirectories = ["assets", "disc", "build"];
        private void ExploreFolder(Folder folder, DirectoryInfo directory, bool setFolderAsParent = true)
        {
            var serializer = JsonSerializer.Create();
            var hasChunk = false;
            foreach (var fileInfo in directory.GetFiles("*.json"))
            {
                using var reader = new JsonTextReader(new StreamReader(fileInfo.FullName));
                // Only asset files matter, anything else like build outputs can live in the project's folders too
                if (serializer.Deserialize(reader) is not JObject deserialized || deserialized["Type"]?.ToObject<Type>() is not { } assetType
                    || deserialized["URI"]?.ToObject<LabURI>() is not { } assetUri)
                {
                    continue;
                }
                if (assetType == typeof(Package))
                {
                    folder.Mark |= FolderMark.IsPackage;
                    folder.Mark |= FolderMark.Locked;
                    folder.Mark &= ~FolderMark.Normal;
                    folder.Package = assetUri;
                    continue;
                }

                if (assetType == typeof(LevelChunk))
                {
                    hasChunk = true;
                    folder.Mark |= FolderMark.IsChunk;
                }
                folder.AddChild(assetUri);
            }

            if (hasChunk)
            {
                return;
            }
            
            foreach (var assetDirectory in directory.GetDirectories())
            {
                var directoryName = assetDirectory.Name;
                var newFolder = new Folder(directoryName)
                {
                    Parent = setFolderAsParent ? folder.URI : LabURI.Empty,
                    Mark = _reservedLockedDirectories.Contains(directoryName) ? FolderMark.Locked : FolderMark.Normal,
                    Package = setFolderAsParent ? folder.Package : LabURI.Empty,
                };
                OpenedProject!.AssetManager.AddAsset(newFolder);
                folder.AddChild(newFolder);
                ExploreFolder(newFolder, assetDirectory);
            }
        }

        private void AddRecentlyOpened(string path)
        {
            SetRecentProjects(WithRecentlyOpened(RecentProjects, path));
        }

        private void RemoveRecentlyOpened(string path)
        {
            SetRecentProjects(RecentProjects.Where(recent => !IsSamePath(recent, path)).ToList());
        }

        // Saved right away, the settings are only saved when TT Lab closes otherwise
        private void SetRecentProjects(List<string> recents)
        {
            if (recents.SequenceEqual(RecentProjects))
            {
                return;
            }

            Preferences.SetPreference(Preferences.RecentProjects, recents);
            Preferences.Save();
            RecentProjectsChanged?.Invoke();
        }

        internal static List<string> WithRecentlyOpened(IEnumerable<string> recents, string path)
        {
            path = Path.TrimEndingDirectorySeparator(path);
            return recents.Where(recent => !IsSamePath(recent, path)).Prepend(path).Take(MaxRecentProjects).ToList();
        }

        private static bool IsSamePath(string first, string second)
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(first), Path.TrimEndingDirectorySeparator(second),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
    }
}
