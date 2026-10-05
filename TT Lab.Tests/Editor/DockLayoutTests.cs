using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
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

    // Only the front tab of each dock shows at first: the Inspector's and the History's headers, which bind through the document of the
    // editor last used, logged an error for every value of it while there was none
    [AvaloniaFact]
    public async Task EveryPanelShowsWithoutBindingErrors()
    {
        var factory = CreateShellFactory();
        var layout = factory.CreateLayout();
        var panels = PanelsOf(layout).ToList();
        Assert.Equal(ShellTabs, panels.Count);

        Assert.Empty(await ShowAndCollectBindingErrors(factory, layout, panels.Select(panel => (Func<Window, Task>)(async window =>
        {
            factory.SetActiveDockable(panel);
            await WaitUntil(() => window.GetVisualDescendants().OfType<UserControl>().Any(view => view.DataContext == panel && view.IsEffectivelyVisible));
        })).ToArray()));
    }

    private static IEnumerable<IDocument> PanelsOf(IDockable dockable) => dockable switch
    {
        IDock dock => (dock.VisibleDockables ?? []).SelectMany(PanelsOf),
        IDocument panel => [panel],
        _ => [],
    };

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

    private static async Task<List<string>> ShowAndCollectBindingErrors(DockFactory factory, IRootDock layout, params Func<Window, Task>[] steps)
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
            errors.Settle();
            foreach (var step in steps)
            {
                await step(window);
                errors.Settle();
            }

            return errors.Lines;
        }
        finally
        {
            Logger.Sink = sink;
            window.Close();
        }
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

    // Other tests' controls may log while this one waits, only the window's own count. A view gets its view model before it's in the
    // window, so what its bindings logged then counts once it's there
    private sealed class BindingErrors(Window window) : ILogSink
    {
        private readonly List<(object? Source, string Line)> _pending = [];

        public List<string> Lines { get; } = [];

        public void Settle()
        {
            foreach (var entry in _pending.Where(entry => IsInWindow(entry.Source)).ToList())
            {
                Lines.Add(entry.Line);
                _pending.Remove(entry);
            }
        }

        public bool IsEnabled(LogEventLevel level, string area) => area == LogArea.Binding && level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            Log(level, area, source, messageTemplate, []);
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (!IsEnabled(level, area))
            {
                return;
            }

            _pending.Add((source, $"{source}: {messageTemplate} {string.Join(", ", propertyValues)}"));
            Settle();
        }

        // A key binding is in no tree, it's the window's when one of its elements has it
        private bool IsInWindow(object? source)
        {
            if (source is KeyBinding keyBinding)
            {
                return window.GetSelfAndVisualDescendants().OfType<InputElement>().Any(element => element.KeyBindings.Contains(keyBinding));
            }

            if (source is Visual visual && visual.GetVisualRoot() == window)
            {
                return true;
            }

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
