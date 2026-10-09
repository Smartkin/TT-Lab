using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Threading;


namespace TT_Lab.Project;

/// <summary>
/// Tells the project manager when asset files or folders of the project got made, deleted or renamed outside TT Lab, so the tree
/// follows the file system, and when assets' data files got written (a script saved by a text editor, a model Blender exported over its
/// file; exporters often write elsewhere and rename over the file, which counts too), so open editors follow them. One watcher for both:
/// starting one on a whole project takes a fifth of a second and a watch per folder
/// </summary>
internal sealed class ProjectTreeWatcher : IDisposable
{
    // Files come in bursts (a copied folder, a build), the tree gets synced once they stop
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(500);

    private const string AssetsDirectory = "assets";
    private static readonly string[] DataExtensions = [".data", ".tlm", ".lab", ".png", ".wav", ".txt"];

    private readonly FileSystemWatcher _watcher;
    private readonly Subject<bool> _changes = new();
    private readonly IDisposable _subscription;
    private readonly string _root;
    private readonly HashSet<string> _written = [];
    private readonly Subject<bool> _writes = new();
    private readonly IDisposable _writesSubscription;

    public ProjectTreeWatcher(string projectPath, Action sync, Action<IReadOnlyCollection<string>>? dataWritten = null)
    {
        _root = Path.GetFullPath(projectPath);
        _watcher = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Changed += OnWritten;
        _watcher.Error += (_, e) =>
        {
            Log.WriteLine($"The project folder watcher lost changes: {e.GetException().Message}", Log.LogType.Debug);
            _changes.OnNext(true);
        };
        _subscription = _changes.Throttle(SettleTime, TaskPoolScheduler.Default)
            .Subscribe(_ => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    sync();
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Couldn't sync the project tree with the file system: {ex.Message}", Log.LogType.Warning);
                }
            }, DispatcherPriority.Background));
        _writesSubscription = _writes.Throttle(SettleTime, TaskPoolScheduler.Default)
            .Subscribe(_ =>
            {
                List<string> paths;
                lock (_written)
                {
                    paths = _written.ToList();
                    _written.Clear();
                }

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        dataWritten?.Invoke(paths);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine($"Couldn't reload what changed outside TT Lab: {ex.Message}", Log.LogType.Warning);
                    }
                }, DispatcherPriority.Background);
            });
        _watcher.EnableRaisingEvents = true;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (Matters(e.FullPath))
        {
            _changes.OnNext(true);
        }

        if (e.ChangeType == WatcherChangeTypes.Created)
        {
            OnWritten(sender, e);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (Matters(e.FullPath) || Matters(e.OldFullPath))
        {
            _changes.OnNext(true);
        }

        OnWritten(sender, e);
    }

    private void OnWritten(object sender, FileSystemEventArgs e)
    {
        if (!IsAssetData(e.FullPath))
        {
            return;
        }

        lock (_written)
        {
            _written.Add(e.FullPath);
        }

        _writes.OnNext(true);
    }

    // Data files in the assets folder, which builds never write to
    private bool IsAssetData(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return string.Equals(first, AssetsDirectory, StringComparison.OrdinalIgnoreCase) &&
               Array.Exists(DataExtensions, extension => extension.Equals(Path.GetExtension(fullPath), StringComparison.OrdinalIgnoreCase));
    }

    // The files the tree shows: asset files and the disc's music archives and videos
    private static readonly string[] ShownExtensions = [".json", ".mh", ".mb", ".pss"];

    // Shown files and directories in the folders the tree shows: builds write plenty under build, and a repository's .git changes with
    // every commit, its objects' files without an extension. A deleted path can't be told from a file any more, so anything without an
    // extension counts as a directory
    private bool Matters(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        if (!Array.Exists(ProjectManager.ShownRootDirectories, shown => string.Equals(shown, first, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var extension = Path.GetExtension(fullPath);
        return extension.Length == 0 || Array.Exists(ShownExtensions, shown => shown.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _subscription.Dispose();
        _changes.Dispose();
        _writesSubscription.Dispose();
        _writes.Dispose();
    }
}
