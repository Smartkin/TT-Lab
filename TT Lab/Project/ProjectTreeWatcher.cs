using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Threading;


namespace TT_Lab.Project;

/// <summary>
/// Tells the project manager when asset files or folders of the project got made, deleted or renamed outside TT Lab, so the tree
/// follows the file system
/// </summary>
internal sealed class ProjectTreeWatcher : IDisposable
{
    // Files come in bursts (a copied folder, a build), the tree gets synced once they stop
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(500);

    // Builds write plenty under build, prefabs are the Prefabs panel's, and the assets' data files don't show in the tree
    private static readonly string[] IgnoredDirectories = ["build", Prefabs.PrefabLibrary.FolderName, Build.BuildProfileLibrary.FolderName];

    private readonly FileSystemWatcher _watcher;
    private readonly Subject<bool> _changes = new();
    private readonly IDisposable _subscription;
    private readonly string _root;

    public ProjectTreeWatcher(string projectPath, Action sync)
    {
        _root = Path.GetFullPath(projectPath);
        _watcher = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
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
        _watcher.EnableRaisingEvents = true;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (Matters(e.FullPath))
        {
            _changes.OnNext(true);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (Matters(e.FullPath) || Matters(e.OldFullPath))
        {
            _changes.OnNext(true);
        }
    }

    // The files the tree shows: asset files and the disc's music archives and videos
    private static readonly string[] ShownExtensions = [".json", ".mh", ".mb", ".pss"];

    // Shown files and directories anywhere but the ignored folders. A deleted path can't be told from a file any more, so
    // anything without an extension counts as a directory
    private bool Matters(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        if (Array.Exists(IgnoredDirectories, ignored => string.Equals(ignored, first, StringComparison.OrdinalIgnoreCase)))
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
    }
}
