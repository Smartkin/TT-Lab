using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class ChunkResourcesTreeView : DocumentBaseView<ChunkResourcesTreeViewModel>
{
    // The tree keeps what's expanded and picked of its own
    public override bool CanBeRecycled => false;

    public ChunkResourcesTreeView()
    {
        InitializeComponent();
        Tree.AddHandler(ContextRequestedEvent, TreeContextRequested, RoutingStrategies.Bubble, true);
        Tree.DoubleTapped += TreeDoubleTapped;
        Tree.KeyDown += TreeKeyDown;
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        if (ViewModel == null)
        {
            return;
        }

        var viewModel = ViewModel;
        viewModel.RevealRequested += Reveal;
        Disposable.Create(() => viewModel.RevealRequested -= Reveal).DisposeWith(disposables);
    }

    private static ChunkResourceRow? RowOf(object? source)
    {
        return (source as StyledElement)?.DataContext as ChunkResourceRow;
    }

    private void TreeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (ViewModel == null || RowOf(e.Source) is not { } row)
        {
            return;
        }

        var entries = ViewModel.GetMenu(row);
        if (entries.Count == 0)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                flyout.Items.Add(new Separator());
                continue;
            }

            flyout.Items.Add(new MenuItem
            {
                Header = entry.Header,
                IsEnabled = entry.IsEnabled && entry.Action != null,
                Command = entry.Action == null ? null : ReactiveCommand.Create(entry.Action),
            });
        }

        flyout.ShowAt(e.Source as Control ?? Tree, true);
        e.Handled = true;
    }

    private void TreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RowOf(e.Source) is { Kind: ChunkResourceRowKind.Resource } row)
        {
            ViewModel?.Show(row);
            e.Handled = true;
        }
    }

    private void TreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel?.SelectedRow is not { } row || row.IsRenaming)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Delete when ChunkResourcesTreeViewModel.CanDelete(row):
                ViewModel.Delete(row);
                e.Handled = true;
                break;
            case Key.F2:
                ViewModel.BeginRename(row);
                e.Handled = true;
                break;
            case Key.Enter when row.Kind == ChunkResourceRowKind.Resource:
                ViewModel.Show(row);
                e.Handled = true;
                break;
        }
    }

    private void RenameAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.PropertyChanged += (_, change) =>
            {
                if (change.Property == IsVisibleProperty && box.IsVisible)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        box.Focus();
                        box.SelectAll();
                    });
                }
            };
        }
    }

    private void RenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            ViewModel?.EndRename(row, e.Key == Key.Enter);
            Tree.Focus();
            e.Handled = true;
        }
    }

    private void RenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            ViewModel?.EndRename(row, true);
        }
    }

    // After the folders it's in got expanded and laid out
    private void Reveal(ChunkResourceRow row)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Tree.TreeContainerFromItem(row) is { } container)
            {
                container.BringIntoView();
            }
        }, DispatcherPriority.Loaded);
    }
}
