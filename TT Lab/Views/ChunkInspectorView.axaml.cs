using System;
using System.Reactive.Disposables.Fluent;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.Controls;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views;

public partial class ChunkInspectorView : ReactiveUserControl<ChunkInspectorViewModel>
{
    // Each document keeps its inspector's views and scroll position while its tab is open, like the resources panel: building the
    // default chunk's 255 particle systems again on every switch back to its tab took seconds. The views go with their documents
    private readonly ConditionalWeakTable<DocumentViewModel, Control> _views = new();

    public ChunkInspectorView()
    {
        InitializeComponent();
        FocusKeeper.KeepFocusIn(this);
        // The mouse's back and forward buttons go along the inspector's trail
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Document)
                .Subscribe(ShowDocument)
                .DisposeWith(disposables);
        });
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (ViewModel?.Document is not { } document || !(properties.IsXButton1Pressed || properties.IsXButton2Pressed))
        {
            return;
        }

        if (properties.IsXButton1Pressed)
        {
            document.InspectBack();
        }
        else
        {
            document.InspectForward();
        }

        e.Handled = true;
    }

    private void CrumbClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: InspectorCrumb crumb })
        {
            ViewModel?.Document?.InspectCrumb(crumb);
        }
    }

    private void ShowDocument(DocumentViewModel? document)
    {
        InspectorHost.Child = document == null ? null : _views.GetValue(document, _ => new DocumentScrollViewer
        {
            Content = new ContentControl
            {
                [!ContentControl.ContentProperty] = new Binding(nameof(DocumentViewModel.Inspector)) { Source = document },
            },
        });
    }
}
