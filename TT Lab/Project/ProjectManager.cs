using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Project.Messages;
using TT_Lab.Project.Migration;
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
        private Folder? _treeRoot;
        private ProjectTreeWatcher? _treeWatcher;

        /// <summary>
        /// Asset data files written in the project, TT Lab's own writes included (<see cref="AssetFileStamps"/> tells them apart), on the UI thread
        /// </summary>
        public event Action<IReadOnlyCollection<string>>? AssetFilesChanged;


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

                Log.WriteLine("Making prefabs of the chunks' object instances...");
                var prefabsStart = DateTime.Now;
                var project = (Project)OpenedProject;
                var library = new Prefabs.PrefabLibrary(project);
                var (prefabs, _) = Prefabs.InstancePrefabs.Make(project, library);
                Log.WriteLine($"Made {prefabs} prefabs of the different object instances in {DateTime.Now - prefabsStart}");
                Log.WriteLine("Making prefabs of the sceneries' meshes and LODs...");
                prefabsStart = DateTime.Now;
                var (sceneryPrefabs, _) = Prefabs.SceneryPrefabs.Make(project, library);
                Log.WriteLine($"Made {sceneryPrefabs} prefabs of the different scenery meshes and LODs in {DateTime.Now - prefabsStart}");

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
                TakePrefabPictures();
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
                        if (!MigrateIfOlder(prFile))
                        {
                            return;
                        }

                        if (!UnpackRetailAssetsIfMissing(prFile))
                        {
                            return;
                        }

                        Project.Deserialize(prFile);
                        Log.WriteLine($"Building project tree...");
                        BuildProjectTree();
                        WorkableProject = true;
                        _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectOpened)));
                        _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTitle)));
                        // _ogreWindowManager.AddResourceLocation(OpenedProject!.ProjectPath);
                        Log.WriteLine($"Project opened in {stopwatch.Elapsed}");
                        MiscUtils.CollectReleasedMemory();
                        TakePrefabPictures();
                    }
                    // Nothing observes this task, so projects TT Lab can't open have to be reported here even in debug builds
                    catch (ProjectException ex)
                    {
                        ReportOpeningError(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(ex.ToString(), Log.LogType.Debug);
                        // The assets are read in parallel, an unreadable file comes wrapped
                        ReportOpeningError((ex is AggregateException aggregate ? aggregate.Flatten().InnerExceptions[0] : ex).Message);
                    }
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

        /// <summary>
        /// Asks whether a project an older TT Lab made gets migrated to open it, tests answer it themselves
        /// </summary>
        internal Func<string, Task<bool>> AskToMigrate { get; set; } = MigrationDialogue.Ask;

        // A project of a version TT Lab can bring to its own is migrated once the user agrees, false when they don't. Versions it can't
        // migrate are left to the project's reading, which says why it can't open them
        private bool MigrateIfOlder(string projectFile)
        {
            var version = ProjectMigration.ReadVersion(projectFile);
            if (version == Project.CURRENT_VERSION || !ProjectMigration.CanMigrate(version))
            {
                return true;
            }

            var name = Path.GetFileNameWithoutExtension(projectFile);
            var question = $"{name} was made with TT Lab {version}. Migrate it to TT Lab {Project.CURRENT_VERSION} to open it?\n\n" +
                           $"Every file the migration changes is kept as it was in a backup in the project's folder. TT Lab {version} can't open the project once it's migrated.";
            if (!Dispatcher.UIThread.InvokeAsync(() => AskToMigrate(question)).GetAwaiter().GetResult())
            {
                Log.WriteLine($"Didn't open {name}, it's of TT Lab {version} and wasn't migrated", Log.LogType.Warning);
                return false;
            }

            TellMigrated(MigrationSummary(name, ProjectMigration.Migrate(projectFile)));
            return true;
        }

        /// <summary>
        /// Asks for the folders of the game's files a project without the game's packages is unpacked from, tests answer it themselves
        /// </summary>
        internal Func<RetailDiscsDialogue.Request, Task<IReadOnlyDictionary<Project.GamePlatform, string>?>> AskForRetailDiscs { get; set; } = RetailDiscsDialogue.Ask;

        /// <summary>
        /// Where the preferences have each version's game files, tests give their own
        /// </summary>
        internal Func<Project.GamePlatform, string?> PreferredDiscFolder { get; set; } = platform =>
            Preferences.GetPreference<string>(platform == Project.GamePlatform.Xbox ? Preferences.XboxDiscContentPath : Preferences.Ps2DiscContentPath);

        // A project shared without the game's assets (its own packages without the game's, which a repository can't have) gets the versions
        // of the game its packages use unpacked from the game's files again, from the preferences' folders when they have them and without
        // asking then. False when it doesn't open without them
        private bool UnpackRetailAssetsIfMissing(string projectFile)
        {
            var stored = Project.ReadProjectFile(projectFile);
            var check = RetailAssets.Of(projectFile, stored.Name);
            if (check.Problem != null)
            {
                throw new ProjectException(check.Problem);
            }

            if (!check.IsMissingAny)
            {
                return true;
            }

            var folders = RetailAssets.FromPreferences(check, PreferredDiscFolder);
            if (!RetailAssets.Suffice(check, folders))
            {
                var answer = Dispatcher.UIThread.InvokeAsync(() => AskForRetailDiscs(new RetailDiscsDialogue.Request(check, folders))).GetAwaiter().GetResult();
                if (answer == null)
                {
                    Log.WriteLine($"Didn't open {stored.Name}, it doesn't have the game's packages and they weren't unpacked", Log.LogType.Warning);
                    return false;
                }

                folders = answer.ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            UnpackRetailAssets(stored, check, folders);
            return true;
        }

        // The game's packages of the versions the project lacks, unpacked the way creating a project unpacks them: the game's files copied into
        // the project's disc folder, the assets of the versions given read from them, the packages of a version not given made empty. The
        // project's own packages and its file stay as they are, but for where the game's files are now
        private void UnpackRetailAssets(Project stored, RetailAssets.Check check, IReadOnlyDictionary<Project.GamePlatform, string> folders)
        {
            var start = DateTime.Now;
            var ps2 = folders.GetValueOrDefault(Project.GamePlatform.PS2);
            var xbox = folders.GetValueOrDefault(Project.GamePlatform.Xbox);
            var missing = check.Versions.Where(version => version.Missing).Select(version => version.Platform).ToHashSet();
            var made = missing.SelectMany(platform => new[] { RetailAssets.GlobalPackageName(platform, stored.Name), RetailAssets.PackageName(platform, stored.Name) }).ToHashSet();
            Log.WriteLine($"{stored.Name} doesn't have the game's packages, unpacking {string.Join(" and ", folders.Keys.Select(RetailAssets.Describe))} assets from "
                          + string.Join(" and ", folders.Values));
            var project = new Project(stored.Name, stored.Path, ps2, xbox) { Location = stored.Location };
            IsCreatingProject = true;
            OpenedProject = project;
            try
            {
                project.CreateProjectStructure();
                if (folders.Count > 0)
                {
                    Log.WriteLine("Copying disc contents to project...");
                    project.CopyDiscContents();
                }

                Directory.SetCurrentDirectory("assets");
                project.CreateBasePackages();
                // The project's own package and the versions it has are kept as they are
                var kept = new HashSet<IAsset> { project.BasePackage };
                if (!missing.Contains(Project.GamePlatform.PS2))
                {
                    kept.UnionWith([project.GlobalPackagePS2, project.Ps2Package]);
                }

                if (!missing.Contains(Project.GamePlatform.Xbox))
                {
                    kept.UnionWith([project.GlobalPackageXbox, project.XboxPackage]);
                }

                using var gate = new MemoryGate((long)(Preferences.GetPreference<Double>(Preferences.BuildMemoryBudget) * 1024 * 1024), 0.8);
                var writer = new CreationWriter(project.AssetManager, gate);
                if (ps2 != null)
                {
                    Log.WriteLine("Unpacking PS2 assets...");
                    project.UnpackAssetsPS2(gate);
                    writer.ImportAndWrite();
                    writer.RemoveInternalAssets();
                    MergeVariants();
                }
                else
                {
                    // A version not given is made empty, its packages off like a project made without its disc
                    project.GlobalPackagePS2.Enabled = false;
                    project.Ps2Package.Enabled = false;
                }

                if (xbox != null)
                {
                    Log.WriteLine("Unpacking XBox assets...");
                    project.UnpackAssetsXbox(gate);
                    writer.ImportAndWrite();
                    writer.RemoveInternalAssets();
                    MergeVariants();
                }

                Log.WriteLine("Serializing assets...");
                project.Serialize(asset => kept.Contains(asset) || writer.IsWritten(asset), writeProjectFile: false, tidiedPackages: made);
            }
            catch (Exception)
            {
                // What was made of the game's packages goes, the next opening unpacks them again
                foreach (var name in made)
                {
                    var directory = Path.Combine(project.ProjectPath, "assets", name);
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }

                throw;
            }
            finally
            {
                OpenedProject = null;
                IsCreatingProject = false;
            }

            if (ps2 != null)
            {
                stored.DiscContentPathPS2 = project.DiscContentPathPS2;
            }

            if (xbox != null)
            {
                stored.DiscContentPathXbox = project.DiscContentPathXbox;
            }

            stored.WriteProjectFile();
            MiscUtils.CollectReleasedMemory();
            Log.WriteLine($"Unpacked the game's assets in {DateTime.Now - start}");
        }

        /// <summary>
        /// Tells what migrating a project did and left to the user, tests take it themselves
        /// </summary>
        internal Action<string> TellMigrated { get; set; } = summary =>
            Dispatcher.UIThread.Post(() => new MessageDialogue("Project migrated", summary).ShowDialog(MiscUtils.GetMainWindow()));

        // The first few of what the migration left to the user, the log has every one
        internal static string MigrationSummary(string name, MigrationResult result)
        {
            const int shownNotes = 6;
            var summary = new StringBuilder($"{name} is migrated from TT Lab {result.From} to {Project.CURRENT_VERSION}: {result.Changed} files changed");
            summary.Append(result.Backup != null ? $", kept as they were in {Path.GetFileName(result.Backup)} in the project's folder." : ".");
            foreach (var note in result.Notes.Take(shownNotes))
            {
                summary.Append("\n\n• ").Append(note);
            }

            if (result.Notes.Count > shownNotes)
            {
                summary.Append($"\n\n{result.Notes.Count - shownNotes} more are in the log.");
            }

            return summary.ToString();
        }

        private static void ReportOpeningError(string message)
        {
            Log.WriteLine($"Error opening project: {message}", Log.LogType.Error);
            Dispatcher.UIThread.Post(() => new MessageDialogue("Can't open the project", message).ShowDialog(MiscUtils.GetMainWindow()));
        }

        public void BuildPs2Project()
        {
            RunBuild("Error building PS2 project", project => project.PackAssetsPS2());
        }

        public void BuildPs2Iso()
        {
            RunBuild("Error creating PS2 ISO", project => project.CreatePs2ArchivesAndIso());
        }

        public void Build(TT_Lab.Project.Build.BuildProfile profile)
        {
            RunBuild($"Error building with the {profile.Name} profile", project => project.Build(profile));
        }

        public void BuildXboxProject()
        {
            RunBuild("Error building Xbox project", project => project.PackAssetsXbox());
        }

        public void BuildXboxImage()
        {
            RunBuild("Error putting the Xbox game together", project => project.CreateXboxGame());
        }

        // Nothing observes the build's task, so a build that fails has to say so here, debug builds included: the exception went
        // nowhere and the build looked like it stopped for no reason, with the project never handed back
        internal void RunBuild(string errorMessage, Action<IProject> build)
        {
            WorkableProject = false;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    build(OpenedProject!);
                }
                catch (Exception ex)
                {
                    foreach (var line in BuildFailureLines(errorMessage, ex))
                    {
                        Log.WriteLine(line, Log.LogType.Error);
                    }

                    Log.WriteLine(ex.ToString(), Log.LogType.Debug);
                }
                finally
                {
                    WorkableProject = true;
                }
            });
        }

        private const int MaxReportedBuildFailures = 5;

        // One line per failure, the chunks building in parallel each fail on their own when they share the cause
        internal static List<string> BuildFailureLines(string errorMessage, Exception exception)
        {
            var failures = exception is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToList() : [exception];
            if (failures.Count == 1)
            {
                return [$"{errorMessage}: {failures[0].Message}"];
            }

            var lines = new List<string> { $"{errorMessage}, {failures.Count} chunks failed:" };
            lines.AddRange(failures.Take(MaxReportedBuildFailures).Select(failure => $"  {failure.Message}"));
            if (failures.Count > MaxReportedBuildFailures)
            {
                lines.Add($"  and {failures.Count - MaxReportedBuildFailures} more, all of them are in the session log");
            }

            return lines;
        }

        // The prefabs saved without a picture (the ones made of the instances) get theirs in the background
        private void TakePrefabPictures()
        {
            if (OpenedProject is Project project)
            {
                Locator.Current.GetService<Prefabs.PrefabPictures>()?.TakeMissing(project);
            }
        }

        public void CloseProject()
        {
            Locator.Current.GetService<Prefabs.PrefabPictures>()?.Stop();
            StopTreeWatcher();
            AssetFileStamps.Clear();
            AssetData.Instance.Particle.ParticleSystemNames.Clear();
            _treeRoot = null;
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
            ExploreFolder(root, dirInfo, false, false);
            _treeRoot = root;
            ProjectTree = new BindableCollection<ResourceTreeElementViewModel>(root.Children.Select(uri => OpenedProject!.AssetManager.GetAsset(uri).GetResourceTreeElement()));
            _internalTree.Clear();
            _internalTree.AddRange(ProjectTree);
            _eventAggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectTree)));
            StopTreeWatcher();
            try
            {
                _treeWatcher = new ProjectTreeWatcher(assetRoot, SyncProjectTree, paths => AssetFilesChanged?.Invoke(paths));
            }
            catch (Exception ex)
            {
                // Watching is a convenience, the tree and the editors stay as opened without it
                Log.WriteLine($"The project tree and the editors won't follow the file system: {ex.Message}", Log.LogType.Warning);
            }
        }

        internal void StopTreeWatcher()
        {
            _treeWatcher?.Dispose();
            _treeWatcher = null;
        }

        /// <summary>
        /// Puts what the file system has now into the tree: assets and folders made outside TT Lab show up, deleted ones go away
        /// </summary>
        public void SyncProjectTree()
        {
            if (OpenedProject == null || _treeRoot == null)
            {
                return;
            }

            var changed = false;
            foreach (var folderUri in _treeRoot.Children.ToList())
            {
                var folder = OpenedProject.AssetManager.GetAsset<Folder>(folderUri);
                var directory = new DirectoryInfo(Path.Combine(OpenedProject.ProjectPath, folder.Alias));
                if (!directory.Exists)
                {
                    RemoveFolderTree(folder);
                    _treeRoot.Children.Remove(folderUri);
                    _internalTree.Remove(folder.GetResourceTreeElement());
                    changed = true;
                    continue;
                }

                changed |= SyncFolder(folder, directory);
            }

            foreach (var directory in new DirectoryInfo(OpenedProject.ProjectPath).GetDirectories().OrderBy(child => child.Name, TreeOrder))
            {
                if (_hiddenRootDirectories.Contains(directory.Name, TreeOrder) || _treeRoot.Children.Any(uri => OpenedProject.AssetManager.GetAsset(uri).Alias == directory.Name))
                {
                    continue;
                }

                var folder = ExploreNewFolder(_treeRoot, directory, false, true);
                var element = folder.GetResourceTreeElement();
                var index = _internalTree.TakeWhile(existing => TreeOrder.Compare(existing.Alias, element.Alias) < 0).Count();
                _internalTree.Insert(index, element);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            DoSearch();
        }

        // Whether anything under the folder changed
        private bool SyncFolder(Folder folder, DirectoryInfo directory)
        {
            var assetManager = OpenedProject!.AssetManager;
            var element = folder.GetResourceTreeElement();
            var changed = false;
            var files = new Dictionary<LabURI, (Type Type, FileInfo File)>();
            foreach (var fileInfo in directory.GetFiles("*.json"))
            {
                if (ReadAssetHeader(fileInfo) is { } header && header.Type != typeof(Package))
                {
                    files[header.Uri] = (header.Type, fileInfo);
                }
            }

            var discFiles = folder.Mark.HasFlag(FolderMark.Disc)
                ? DiscFile.ListIn(OpenedProject.ProjectPath, directory).ToDictionary(file => file.URI)
                : new Dictionary<LabURI, DiscFile>();
            var isChunk = folder.Mark.HasFlag(FolderMark.IsChunk);
            foreach (var childUri in folder.Children.ToList())
            {
                var child = assetManager.GetAsset(childUri);
                if (child is DiscFile)
                {
                    if (discFiles.ContainsKey(childUri))
                    {
                        continue;
                    }

                    folder.Children.Remove(childUri);
                    element.RemoveChild(child.GetResourceTreeElement());
                    assetManager.RemoveAsset(child);
                    changed = true;
                    continue;
                }

                if (child is Folder childFolder)
                {
                    var childDirectory = new DirectoryInfo(Path.Combine(directory.FullName, childFolder.Alias));
                    if (childDirectory.Exists)
                    {
                        changed |= SyncFolder(childFolder, childDirectory);
                        continue;
                    }

                    if (HoldsUnsaved(childFolder))
                    {
                        continue;
                    }

                    RemoveFolderTree(childFolder);
                    folder.Children.Remove(childUri);
                    element.RemoveChild(childFolder.GetResourceTreeElement());
                    changed = true;
                    continue;
                }

                // Assets made in TT Lab have no file until what they were made for is saved
                if (files.ContainsKey(childUri) || child is SerializableAsset { IsUnsaved: true })
                {
                    continue;
                }

                folder.Children.Remove(childUri);
                element.RemoveChild(child.GetResourceTreeElement());
                // An asset whose file is in another directory only leaves this folder, the one its file is in lists it
                if (!HasFile(child))
                {
                    assetManager.RemoveAsset(child);
                }

                changed = true;
            }

            foreach (var (uri, (type, fileInfo)) in files.OrderBy(file => file.Value.File.Name, TreeOrder))
            {
                if (folder.Children.Contains(uri))
                {
                    continue;
                }

                var asset = LoadAssetFile(type, fileInfo, uri);
                if (asset == null)
                {
                    continue;
                }

                if (type == typeof(LevelChunk))
                {
                    folder.Mark |= FolderMark.IsChunk;
                    isChunk = true;
                }

                folder.AddChild(uri);
                element.AddNewChild(asset.GetResourceTreeElement(element));
                changed = true;
            }

            foreach (var discFile in discFiles.Values.OrderBy(file => file.Alias, TreeOrder))
            {
                if (folder.Children.Contains(discFile.URI))
                {
                    continue;
                }

                AddDiscFile(folder, discFile);
                element.AddNewChild(assetManager.GetAsset(discFile.URI).GetResourceTreeElement(element));
                changed = true;
            }

            if (isChunk)
            {
                return changed;
            }

            foreach (var childDirectory in directory.GetDirectories().OrderBy(child => child.Name, TreeOrder))
            {
                if (folder.Children.Any(uri => assetManager.GetAsset(uri) is Folder existing && existing.Alias == childDirectory.Name))
                {
                    continue;
                }

                var newFolder = ExploreNewFolder(folder, childDirectory, true, true);
                element.AddNewChild(newFolder.GetResourceTreeElement(element));
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Reads a directory TT Lab made into the tree under the folder the way opening the project does, its folders with their marks and
        /// rows (the copies of a folder, chunk or package)
        /// </summary>
        internal Folder AddDirectoryToTree(Folder parent, string directory)
        {
            var folder = ExploreNewFolder(parent, new DirectoryInfo(directory), true, true);
            var row = parent.GetResourceTreeElement();
            row.AddNewChild(folder.GetResourceTreeElement(row));
            RefreshTreeSearch();
            return folder;
        }

        /// <summary>
        /// Filters the tree again when it's searched, after its rows changed
        /// </summary>
        internal void RefreshTreeSearch()
        {
            if (!string.IsNullOrEmpty(_searchAsset))
            {
                DoSearch();
            }
        }

        private static bool HasFile(IAsset asset)
        {
            return asset is SerializableAsset serializable && File.Exists(Path.Combine(serializable.FullPath, $"{serializable.Name}.json"));
        }

        private bool HoldsUnsaved(Folder folder)
        {
            var assetManager = OpenedProject!.AssetManager;
            return folder.Children.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset)
                .Any(child => child is SerializableAsset { IsUnsaved: true } || child is Folder childFolder && HoldsUnsaved(childFolder));
        }

        private void AddDiscFile(Folder folder, DiscFile discFile)
        {
            var assetManager = OpenedProject!.AssetManager;
            if (!assetManager.DoesAssetExist(discFile.URI))
            {
                assetManager.AddAsset(discFile);
            }

            folder.AddChild(discFile.URI);
        }

        // An asset file made outside TT Lab, or one it already has (its own saves raise the same events)
        private IAsset? LoadAssetFile(Type type, FileInfo fileInfo, LabURI uri)
        {
            var assetManager = OpenedProject!.AssetManager;
            if (assetManager.DoesAssetExist(uri))
            {
                return assetManager.GetAsset(uri);
            }

            try
            {
                var asset = (IAsset)Activator.CreateInstance(type)!;
                asset.Deserialize(File.ReadAllText(fileInfo.FullName));
                assetManager.AddAsset(asset);
                asset.PostDeserialize();
                return asset;
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Couldn't load {fileInfo.FullName} into the project tree: {ex.Message}", Log.LogType.Warning);
                return null;
            }
        }

        private void RemoveFolderTree(Folder folder)
        {
            var assetManager = OpenedProject!.AssetManager;
            foreach (var childUri in folder.Children)
            {
                if (!assetManager.DoesAssetExist(childUri))
                {
                    continue;
                }

                var child = assetManager.GetAsset(childUri);
                if (child is Folder childFolder)
                {
                    RemoveFolderTree(childFolder);
                }

                assetManager.RemoveAsset(child);
            }

            folder.Children.Clear();
            assetManager.RemoveAsset(folder);
        }

        private static readonly string[] _reservedLockedDirectories = ["assets", "disc", "build"];
        private const string DiscDirectory = "disc";
        // The project's own folders that aren't assets: what builds write, the prefabs (the Prefabs panel's) and the build profiles
        private static readonly string[] _hiddenRootDirectories = ["build", Prefabs.PrefabLibrary.FolderName, TT_Lab.Project.Build.BuildProfileLibrary.FolderName];

        // Folders and assets in the order of their names, the file system's order looked random
        private static readonly StringComparer TreeOrder = StringComparer.OrdinalIgnoreCase;

        // Only asset files matter, anything else like build outputs can live in the project's folders too
        private static (Type Type, LabURI Uri)? ReadAssetHeader(FileInfo fileInfo)
        {
            try
            {
                using var reader = new JsonTextReader(new StreamReader(fileInfo.FullName));
                if (JsonSerializer.Create().Deserialize(reader) is not JObject deserialized || deserialized["Type"]?.ToObject<Type>() is not { } assetType
                    || deserialized["URI"]?.ToObject<LabURI>() is not { } assetUri)
                {
                    return null;
                }

                return (assetType, assetUri);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                return null;
            }
        }

        private Folder ExploreNewFolder(Folder folder, DirectoryInfo directory, bool setFolderAsParent, bool loadAssets)
        {
            var mark = _reservedLockedDirectories.Contains(directory.Name) ? FolderMark.Locked : FolderMark.Normal;
            if (setFolderAsParent ? folder.Mark.HasFlag(FolderMark.Disc) : directory.Name == DiscDirectory)
            {
                mark |= FolderMark.Disc;
            }

            var newFolder = new Folder(directory.Name)
            {
                Parent = setFolderAsParent ? folder.URI : LabURI.Empty,
                Mark = mark,
                Package = setFolderAsParent ? folder.Package : LabURI.Empty,
            };
            OpenedProject!.AssetManager.AddAsset(newFolder);
            folder.AddChild(newFolder);
            ExploreFolder(newFolder, directory, true, loadAssets);
            return newFolder;
        }

        // Opening a project has every asset loaded before the tree gets built, folders that turn up later load theirs
        private void ExploreFolder(Folder folder, DirectoryInfo directory, bool setFolderAsParent, bool loadAssets)
        {
            var hasChunk = false;
            foreach (var fileInfo in directory.GetFiles("*.json").OrderBy(file => file.Name, TreeOrder))
            {
                if (ReadAssetHeader(fileInfo) is not var (assetType, assetUri))
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

                if (loadAssets && LoadAssetFile(assetType, fileInfo, assetUri) == null)
                {
                    continue;
                }


                if (assetType == typeof(LevelChunk))
                {
                    hasChunk = true;
                    folder.Mark |= FolderMark.IsChunk;
                }
                folder.AddChild(assetUri);
            }

            if (folder.Mark.HasFlag(FolderMark.Disc))
            {
                foreach (var discFile in DiscFile.ListIn(OpenedProject!.ProjectPath, directory).OrderBy(file => file.Alias, TreeOrder))
                {
                    AddDiscFile(folder, discFile);
                }
            }

            if (hasChunk)
            {
                return;
            }
            
            foreach (var assetDirectory in directory.GetDirectories().OrderBy(child => child.Name, TreeOrder))
            {
                var directoryName = assetDirectory.Name;
                if (!setFolderAsParent && _hiddenRootDirectories.Contains(directoryName, TreeOrder))
                {
                    continue;
                }

                ExploreNewFolder(folder, assetDirectory, setFolderAsParent, loadAssets);
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
