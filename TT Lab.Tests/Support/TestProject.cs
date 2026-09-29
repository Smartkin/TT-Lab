using System.Reflection;
using Caliburn.Micro;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Project;

namespace TT_Lab.Tests.Support;

// TT Lab reaches the opened project through the service locator and works relative to the current directory, so tests using a
// project can't run in parallel with each other
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProjectCollection
{
    public const string Name = "Project";
}

/// <summary>
/// Empty project with the base packages in a temporary directory, opened for as long as it lives
/// </summary>
public sealed class TestProject : IDisposable
{
    private readonly string _previousDirectory = Directory.GetCurrentDirectory();
    private readonly ProjectManager _projectManager;

    public TestProject()
    {
        Root = Path.Combine(Path.GetTempPath(), "TT Lab Tests", Guid.NewGuid().ToString("N"));
        _projectManager = new ProjectManager(new NullEventAggregator());
        Locator.CurrentMutable.RegisterConstant(_projectManager);
        Project = new TT_Lab.Project.Project("Test", Root, string.Empty, string.Empty);
        _projectManager.OpenedProject = Project;
        Project.CreateProjectStructure();
        Project.CreateBasePackages();
    }

    public string Root { get; }
    public TT_Lab.Project.Project Project { get; }
    public AssetManager AssetManager => Project.AssetManager;
    public string AssetsPath => Path.Combine(Project.ProjectPath, "assets");

    /// <summary>
    /// Registers an asset in the Global PS2 package
    /// </summary>
    public T Add<T>(T asset, string name, UInt32 id = 0, Package? package = null) where T : SerializableAsset
    {
        asset.Package = (package ?? Project.GlobalPackagePS2).URI;
        asset.InvariantName = name;
        asset.Alias = name;
        asset.Variation ??= string.Empty;
        asset.ID = id;
        asset.RegenerateUri();
        AssetManager.AddAsset(asset);
        return asset;
    }

    /// <summary>
    /// Saves the project and builds its tree the way opening it does, every directory under a package becomes a folder
    /// </summary>
    public void BuildProjectTree(params string[] directories)
    {
        Project.Serialize();
        foreach (var directory in directories)
        {
            Directory.CreateDirectory(Path.Combine(AssetsPath, directory));
        }

        typeof(ProjectManager).GetMethod("BuildProjectTree", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_projectManager, []);
    }

    public Folder GetFolder(Package package, params string[] path)
    {
        var folder = package.GetPackageFolder();
        foreach (var name in path)
        {
            folder = AssetManager.GetAsset<Folder>(folder.FindChild<Folder>(name));
        }

        return folder;
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_previousDirectory);
        _projectManager.StopTreeWatcher();
        Locator.CurrentMutable.UnregisterAll<ProjectManager>();
        try
        {
            Directory.Delete(Root, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file still held open only leaves some garbage in the temporary directory
        }
    }

    internal sealed class NullEventAggregator : IEventAggregator
    {
        public bool HandlerExistsFor(Type messageType) => false;
        public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) { }
        public void Unsubscribe(object subscriber) { }
        public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
