using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Caliburn.Micro;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Project.Prefabs;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using Action = System.Action;

namespace TT_Lab.ViewModels;

/// <summary>
/// A prefab of the project as the panel lists it
/// </summary>
public sealed class PrefabEntry
{
    public PrefabEntry(Prefab prefab, string details, string? cantPlace, Action place, Action delete)
    {
        Prefab = prefab;
        Details = details;
        CanPlace = cantPlace == null;
        PlaceTip = cantPlace ?? "Drag it into the scene, or place it at the cursor from the menu";
        PlaceCommand = ReactiveCommand.Create(place);
        DeleteCommand = ReactiveCommand.Create(delete);
    }

    private Bitmap? _preview;
    private bool _previewLoaded;

    public Prefab Prefab { get; }
    public string Name => Prefab.Name;
    public string Details { get; }
    public bool CanPlace { get; }
    public string PlaceTip { get; }
    public ReactiveCommand<Unit, Unit> PlaceCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public bool HasPreview => Prefab.PreviewPath != null;

    /// <summary>
    /// The picture taken when it got saved, read when the tile first shows
    /// </summary>
    public Bitmap? Preview
    {
        get
        {
            if (_previewLoaded)
            {
                return _preview;
            }

            _previewLoaded = true;
            if (Prefab.PreviewPath == null)
            {
                return null;
            }

            try
            {
                _preview = new Bitmap(Prefab.PreviewPath);
            }
            catch (Exception e)
            {
                Log.WriteLine($"Prefab {Name}'s picture couldn't be read: {e.Message}", Log.LogType.Debug);
            }

            return _preview;
        }
    }
}

/// <summary>
/// The project's prefabs, a panel: saves the selection of the scene being edited and places prefabs into it, dragged into the
/// viewport or at its cursor
/// </summary>
public partial class PrefabsViewModel : Document, IHandle<ProjectManagerMessage>
{
    /// <summary>
    /// Format of a prefab being dragged from the panel, the data is the <see cref="Prefab"/>
    /// </summary>
    public const string DragFormat = "TT_Lab.Prefab";

    private const string NoSceneHint = "Open a chunk's scene to save its selection as a prefab or to place one";

    [Reactive(SetModifier = AccessModifier.Private)]
    private DocumentViewModel? _document;

    [Reactive]
    private string _prefabName = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canSave;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _hint = NoSceneHint;

    private ViewportViewModel? _viewport;
    private IDisposable? _viewportFollow;

    public ObservableCollection<PrefabEntry> Prefabs { get; } = [];
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public PrefabsViewModel(ScenesEditorsViewModel scenes, IEventAggregator eventAggregator)
    {
        Id = "Prefabs";
        Title = "Prefabs";
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.CanSave));
        RefreshCommand = ReactiveCommand.Create(Refresh);
        ChunkResourcesViewModel.FollowActiveEditor(scenes).Subscribe(Follow);
        // The list is read again whenever it could be stale: a project opened or closed, the tab picked (OnSelected) or the panel
        // shown (PrefabsView), so a panel opened after the project shows what the project has
        eventAggregator.SubscribeOnUIThread(this);
    }

    public override void OnSelected()
    {
        base.OnSelected();
        Refresh();
    }

    public Task HandleAsync(ProjectManagerMessage message, CancellationToken cancellationToken)
    {
        if (message.PropertyName == nameof(ProjectManager.ProjectOpened))
        {
            Refresh();
        }

        return Task.CompletedTask;
    }

    // The panel works on the scene last worked on, following what its viewport's selection can be saved as
    private void Follow(DocumentViewModel? document)
    {
        Document = document;
        _viewportFollow?.Dispose();
        _viewportFollow = null;
        _viewport = document?.Viewport;
        if (_viewport == null)
        {
            CanSave = false;
            Hint = NoSceneHint;
        }
        else
        {
            _viewportFollow = _viewport.WhenAnyValue(x => x.CanSavePrefab, x => x.PrefabHint, x => x.PrefabDefaultName)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    CanSave = _viewport.CanSavePrefab;
                    Hint = _viewport.PrefabHint;
                    PrefabName = _viewport.PrefabDefaultName;
                });
        }

        Refresh();
    }

    /// <summary>
    /// Reads the project's prefabs again, other scenes save them as well
    /// </summary>
    public void Refresh()
    {
        Prefabs.Clear();
        var library = PrefabLibrary.ForOpenedProject();
        if (library == null)
        {
            return;
        }

        var chunk = Document?.DocumentModel as LevelChunk;
        foreach (var prefab in library.Load())
        {
            string? cantPlace;
            try
            {
                cantPlace = chunk == null ? "Open a chunk to place it" : library.CanPlace(prefab, chunk, Document, out var reason) ? null : reason;
            }
            catch (Exception e)
            {
                cantPlace = e.Message;
            }

            Prefabs.Add(new PrefabEntry(prefab, Describe(prefab), cantPlace, () => Place(prefab), () =>
            {
                library.Delete(prefab);
                Refresh();
            }));
        }
    }

    public void Place(Prefab prefab)
    {
        _viewport?.PlacePrefab(prefab);
    }

    private async Task SaveAsync()
    {
        if (_viewport != null && await _viewport.SavePrefabAsync(PrefabName))
        {
            Refresh();
        }
    }

    private static string Describe(Prefab prefab)
    {
        try
        {
            return PrefabLibrary.Describe(prefab);
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
