using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

/// <summary>
/// A state of the document in its history, the step that led to it
/// </summary>
public sealed record HistoryEntry(UndoHistory.Entry Entry, string Description, string Time, double Indent, double Opacity, bool IsCurrent, bool IsSaved)
{
    public string Marker => IsCurrent ? "●" : string.Empty;

    public string Tip => IsSaved ? $"{Description}, saved" : Description;
}

/// <summary>
/// The undo tree of the editor last worked in, every editor keeps its own. Picking an entry takes the document there
/// </summary>
public partial class HistoryViewModel : Document
{
    private const double BranchIndent = 14.0;

    public static readonly Avalonia.Data.Converters.IValueConverter IndentConverter =
        new Avalonia.Data.Converters.FuncValueConverter<double, Avalonia.Thickness>(indent => new Avalonia.Thickness(indent, 0, 0, 0));

    [Reactive(SetModifier = AccessModifier.Private)]
    private DocumentViewModel? _document;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<HistoryEntry> _entries = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canUndo;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canRedo;

    private ObservableCollection<HistoryEntry> _rows = [];
    private HistoryEntry? _selectedEntry;
    private bool _isShowing;

    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    public HistoryViewModel(ScenesEditorsViewModel scenes, ResourcesEditorsViewModel resources)
    {
        Id = "History";
        Title = "History";
        UndoCommand = ReactiveCommand.Create(() => Document?.Undo(), this.WhenAnyValue(x => x.CanUndo));
        RedoCommand = ReactiveCommand.Create(() => Document?.Redo(), this.WhenAnyValue(x => x.CanRedo));

        // The editor last worked in, its viewer's active one, or the other viewer's when it has none
        var lastUsed = new BehaviorSubject<EditorsViewerViewModel>(scenes);
        scenes.Used += () => lastUsed.OnNext(scenes);
        resources.Used += () => lastUsed.OnNext(resources);
        ChunkResourcesViewModel.FollowActiveEditor(scenes)
            .CombineLatest(ChunkResourcesViewModel.FollowActiveEditor(resources), lastUsed.DistinctUntilChanged(),
                (scene, resource, used) => used == resources ? resource ?? scene : scene ?? resource)
            .DistinctUntilChanged()
            .Select(document => document == null
                ? Observable.Return<DocumentViewModel?>(null)
                : Observable.FromEvent(handler => document.History.Changed += handler, handler => document.History.Changed -= handler)
                    .StartWith(Unit.Default)
                    .Select(_ => (DocumentViewModel?)document))
            .Switch()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(Show);
    }

    /// <summary>
    /// The entry the document is at, picking another one goes there
    /// </summary>
    public HistoryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (_isShowing || value == null || Document == null || value.Entry == Document.History.Current)
            {
                this.RaiseAndSetIfChanged(ref _selectedEntry, value);
                return;
            }

            Document.History.GoTo(value.Entry);
        }
    }

    // Oldest first, branches started after undoing are indented by when they got started
    private void Show(DocumentViewModel? document)
    {
        _isShowing = true;
        try
        {
            Document = document;
            CanUndo = document?.History.CanUndo == true;
            CanRedo = document?.History.CanRedo == true;
            if (document == null)
            {
                _rows = [];
                Entries = _rows;
                SelectedEntry = null;
                return;
            }

            var history = document.History;
            var all = new List<UndoHistory.Entry>();
            Collect(history.Root, all);
            all.Sort((first, second) => first.Number.CompareTo(second.Number));
            var columns = all.Select(entry => entry.Branch).Distinct().Order().Select((branch, column) => (branch, column)).ToDictionary(pair => pair.branch, pair => pair.column);
            var done = new HashSet<UndoHistory.Entry>();
            for (var entry = history.Current; entry != null; entry = entry.Parent)
            {
                done.Add(entry);
            }

            var rows = all.Select(entry => new HistoryEntry(entry,
                entry.Parent != null && entry.Branch != entry.Parent.Branch ? $"↳ {entry.Description}" : entry.Description,
                entry.Time.ToString("HH:mm:ss"),
                columns[entry.Branch] * BranchIndent,
                done.Contains(entry) ? 1.0 : history.Current.IsAncestorOf(entry) ? 0.6 : 0.4,
                entry == history.Current,
                entry == history.Saved)).ToList();

            // Typing keeps changing the step the document is at, so the same tree only swaps the rows that differ
            if (rows.Count == _rows.Count && rows.Zip(_rows).All(pair => pair.First.Entry == pair.Second.Entry))
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    if (rows[i] != _rows[i])
                    {
                        _rows[i] = rows[i];
                    }
                }
            }
            else
            {
                _rows = new ObservableCollection<HistoryEntry>(rows);
                Entries = _rows;
            }

            SelectedEntry = _rows.FirstOrDefault(entry => entry.IsCurrent);
        }
        finally
        {
            _isShowing = false;
        }
    }

    private static void Collect(UndoHistory.Entry entry, List<UndoHistory.Entry> all)
    {
        all.Add(entry);
        foreach (var child in entry.Children)
        {
            Collect(child, all);
        }
    }
}
