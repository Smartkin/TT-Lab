using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.Views;

public partial class EditorsViewerView : ReactiveUserControl<EditorsViewerViewModel>
{
    public EditorsViewerView()
    {
        InitializeComponent();
        // A click or the focus anywhere in the viewer uses it, in whatever window it is: Dock tells nothing when a panel gets back the focus
        // its window's dock already gave it
        AddHandler(PointerPressedEvent, (_, _) => ViewModel?.NoteUsed(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, _) => ViewModel?.NoteUsed(), handledEventsToo: true);
        this.WhenActivated(disposables =>
        {
            var viewModel = ViewModel!;
            viewModel.EditorClosed += OnEditorClosed;
            Disposable.Create(() => viewModel.EditorClosed -= OnEditorClosed).DisposeWith(disposables);
        });
    }

    // The closed editor had the focus, closing the next one with the keyboard needs it to be in the one that's shown now
    private void OnEditorClosed(TabbedEditorViewModel closed)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual { } focused && focused.IsAttachedToVisualTree() && !this.IsVisualAncestorOf(focused))
        {
            return;
        }

        // The editor shown next gets its view once the layout ran
        Dispatcher.UIThread.Post(() =>
        {
            var active = ViewModel?.ActiveEditor;
            var editorView = this.GetVisualDescendants().OfType<InputElement>()
                .FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && active != null && element.DataContext == active);
            if (editorView?.Focus() != true)
            {
                Focus();
            }
        }, DispatcherPriority.Background);
    }
}
