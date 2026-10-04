using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Serializer;
using Newtonsoft.Json.Linq;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;

namespace TT_Lab.Tests.Editor;

// Dock's tabs bind through every dockable's capability overrides and its dock's capability policy, which nothing set: starting TT Lab
// logged binding errors for every tab of the shell. The panels log through the global log, hence the collection
[Collection(ProjectCollection.Name)]
public class DockLayoutTests
{
    private const int ShellTabs = 8;

    [AvaloniaFact]
    public async Task TheShellsLayoutShowsWithoutBindingErrors()
    {
        var factory = CreateShellFactory();

        Assert.Empty(await ShowAndCollectBindingErrors(factory, factory.CreateLayout()));
    }

    // Layouts saved before the docks had their policies come back without them
    [AvaloniaFact]
    public async Task SavedLayoutsShowWithoutBindingErrors()
    {
        var factory = CreateShellFactory();
        await ShowAndCollectBindingErrors(factory, factory.CreateLayout());
        var saved = factory.SerializeLayout();
        Assert.Contains("DockCapabilityPolicy", saved);
        var savedBefore = JObject.Parse(saved);
        foreach (var policy in savedBefore.Descendants().OfType<JProperty>().Where(property => property.Name is "DockCapabilityPolicy" or "DockCapabilityOverrides").ToList())
        {
            policy.Remove();
        }

        Assert.Empty(await ShowAndCollectBindingErrors(factory, factory.DeserializeLayout(saved)));
        Assert.Empty(await ShowAndCollectBindingErrors(factory, factory.DeserializeLayout(savedBefore.ToString())));
    }

    private static DockFactory CreateShellFactory()
    {
        var aggregator = new TestProject.NullEventAggregator();
        var projectManager = new ProjectManager(aggregator);
        var scenes = new ScenesEditorsViewModel();
        var resources = new ResourcesEditorsViewModel();
        var log = new LogViewModel(aggregator, projectManager);
        var projectTree = new ProjectTreeViewModel(projectManager, aggregator);
        var chunkResources = new ChunkResourcesViewModel(scenes);
        var chunkInspector = new ChunkInspectorViewModel(scenes);
        var history = new HistoryViewModel(scenes, resources);
        var prefabs = new PrefabsViewModel(scenes, aggregator, new TT_Lab.Project.Prefabs.PrefabPictures());
        var panels = new Panels([scenes, resources, log, projectTree, chunkResources, chunkInspector, history, prefabs]);
        return new DockFactory(new DockSerializer(panels), scenes, resources, log, projectTree, chunkResources, chunkInspector, history, prefabs);
    }

    private static async Task<List<string>> ShowAndCollectBindingErrors(DockFactory factory, IRootDock layout)
    {
        var dock = new DockControl { Factory = factory, InitializeFactory = true, InitializeLayout = true, Layout = layout };
        var window = new Window { Content = dock, Width = 1280, Height = 720 };
        var errors = new BindingErrors(window);
        var sink = Logger.Sink;
        Logger.Sink = errors;
        try
        {
            window.Show();
            await WaitUntil(() => window.GetVisualDescendants().OfType<DocumentTabStripItem>().Count() == ShellTabs);
        }
        finally
        {
            Logger.Sink = sink;
            window.Close();
        }

        return errors.Lines;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private sealed class Panels(object[] panels) : IServiceProvider
    {
        public object? GetService(Type serviceType) => panels.FirstOrDefault(panel => panel.GetType() == serviceType);
    }

    // Other tests' controls may log while this one waits, only the window's own count
    private sealed class BindingErrors(Window window) : ILogSink
    {
        public List<string> Lines { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area) => area == LogArea.Binding && level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            Log(level, area, source, messageTemplate, []);
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (!IsEnabled(level, area) || !IsInWindow(source))
            {
                return;
            }

            Lines.Add($"{source}: {messageTemplate} {string.Join(", ", propertyValues)}");
        }

        private bool IsInWindow(object? source)
        {
            for (var element = source as StyledElement; element != null; element = element.Parent ?? element.TemplatedParent as StyledElement)
            {
                if (element == window)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
