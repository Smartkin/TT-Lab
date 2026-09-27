using System;
using System.Reactive;
using System.Reactive.Linq;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

/// <summary>
/// Inspector of the chunk whose scene is being edited
/// </summary>
public partial class ChunkInspectorViewModel : Document
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private DocumentNodeViewModel? _inspected;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    public ChunkInspectorViewModel(ScenesEditorsViewModel scenes)
    {
        // Saving from the panel saves the scene it shows, like saving from the scene's tab
        SaveCommand = ReactiveCommand.Create(() => scenes.ActiveEditor?.SaveTab());
        UndoCommand = ReactiveCommand.Create(() => scenes.ActiveEditor?.Document?.Undo());
        RedoCommand = ReactiveCommand.Create(() => scenes.ActiveEditor?.Document?.Redo());
        Id = "ChunkInspector";
        Title = "Inspector";
        ChunkResourcesViewModel.FollowActiveEditor(scenes)
            .Select(document => document == null
                ? Observable.Return<(DocumentViewModel?, DocumentNodeViewModel?)>((null, null))
                : document.WhenAnyValue(x => x.Inspector)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Select(_ => ((DocumentViewModel?)document, document.Inspector)))
            .Switch()
            .Subscribe(Show);
    }

    private DocumentViewModel? _document;

    // Picking something to inspect brings the panel to the front, switching to another scene only shows what that one inspects
    private void Show((DocumentViewModel? Document, DocumentNodeViewModel? Inspector) inspected)
    {
        var isPicked = inspected.Document != null && inspected.Document == _document && inspected.Inspector != null && inspected.Inspector != Inspected;
        _document = inspected.Document;
        Inspected = inspected.Inspector;
        if (isPicked)
        {
            Locator.Current.GetService<DockFactory>()?.RevealPanel(this);
        }
    }
}
