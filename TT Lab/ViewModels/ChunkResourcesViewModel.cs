using System;
using System.Reactive;
using System.Reactive.Linq;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

/// <summary>
/// Resources of the chunk whose scene is being edited, a panel of its own so the scene gets the whole tab
/// </summary>
public partial class ChunkResourcesViewModel : Document
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private DocumentViewModel? _document;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    public ChunkResourcesViewModel(ScenesEditorsViewModel scenes)
    {
        // Saving from the panel saves the scene it shows, like saving from the scene's tab
        SaveCommand = ReactiveCommand.Create(() => scenes.ActiveEditor?.SaveTab());
        UndoCommand = ReactiveCommand.Create(() => scenes.ActiveEditor?.Document?.Undo());
        RedoCommand = ReactiveCommand.Create(() => scenes.ActiveEditor?.Document?.Redo());
        Id = "ChunkResources";
        Title = "Chunk Resources";
        FollowActiveEditor(scenes).Subscribe(document => Document = document);
    }

    // Tabs load their document in the background, it's shown once it's ready to be edited. Changes only say when to look again on the
    // UI thread: the scheduler runs work right away when it's already on it, so a change from the UI thread overtook an older one still
    // queued from the loading thread and the panel ended up on the tab's old state
    internal static IObservable<DocumentViewModel?> FollowActiveEditor(EditorsViewerViewModel editors)
    {
        return editors.WhenAnyValue(x => x.ActiveEditor)
            .Select(editor => editor == null
                ? Observable.Return<DocumentViewModel?>(null)
                : editor.WhenAnyValue(x => x.IsLoaded, x => x.Document)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Select(_ => editor.IsLoaded ? editor.Document : null))
            .Switch()
            .DistinctUntilChanged();
    }
}
