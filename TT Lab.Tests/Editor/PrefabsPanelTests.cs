using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Caliburn.Micro;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Project.Prefabs;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

// The Prefabs panel lists the project's prefabs and works on the scene last worked on
[Collection(ProjectCollection.Name)]
public sealed class PrefabsPanelTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [AvaloniaFact]
    public void PanelListsTheProjectsPrefabsAndDeletesThem()
    {
        var library = new PrefabLibrary(_project.Project);
        library.Save(new Prefab
        {
            Name = "Crate", Kind = PrefabKind.Instance, Platform = "PS2", Package = _project.Project.GlobalPackagePS2.URI, LayoutID = 0,
            AssetType = typeof(ObjectInstance).FullName!, DataType = typeof(ObjectInstanceData).FullName, Data = new JObject()
        });
        var panel = new PrefabsViewModel(new ScenesEditorsViewModel(), new EventAggregator());

        panel.Refresh();

        var entry = Assert.Single(panel.Prefabs);
        Assert.Equal("Crate", entry.Name);
        Assert.Equal("Object instance of the main layout, PS2", entry.Details);
        // Nothing to place it into without a scene
        Assert.False(entry.CanPlace);
        Assert.Equal("Open a chunk to place it", entry.PlaceTip);
        Assert.False(panel.CanSave);

        entry.DeleteCommand.Execute().Subscribe();

        Assert.Empty(panel.Prefabs);
        Assert.Empty(library.Load());
    }

    private Prefab CratePrefab(string name) => new()
    {
        Name = name, Kind = PrefabKind.Instance, Platform = "PS2", Package = _project.Project.GlobalPackagePS2.URI, LayoutID = 0,
        AssetType = typeof(ObjectInstance).FullName!, DataType = typeof(ObjectInstanceData).FullName, Data = new JObject()
    };

    // The panel read the project's prefabs before they were there when it was made while the project was still opening (or with
    // none open), so showing it and the project opening read them again: a panel opened from the Window menu after the project
    // stayed empty until its tab was switched
    [AvaloniaFact]
    public async Task PanelReadsThePrefabsAgainWhenShownAndWhenAProjectOpens()
    {
        var library = new PrefabLibrary(_project.Project);
        var aggregator = new EventAggregator();
        var panel = new PrefabsViewModel(new ScenesEditorsViewModel(), aggregator);
        library.Save(CratePrefab("Crate"));
        Assert.Empty(panel.Prefabs);

        var window = new Window { Content = new PrefabsView { DataContext = panel }, Width = 400, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Crate"], panel.Prefabs.Select(entry => entry.Name));

        library.Save(CratePrefab("Nitro"));
        await aggregator.PublishOnUIThreadAsync(new ProjectManagerMessage(nameof(ProjectManager.ProjectOpened)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Crate", "Nitro"], panel.Prefabs.Select(entry => entry.Name).Order());
        window.Close();
    }
}
