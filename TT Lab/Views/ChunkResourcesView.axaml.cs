using System;
using System.Reactive.Disposables.Fluent;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views;

public partial class ChunkResourcesView : ReactiveUserControl<ChunkResourcesViewModel>
{
    // Each chunk keeps its view while its tab is open, building it again on every switch lost where it was scrolled to and took a while for
    // big chunks. The views go with their documents
    private readonly ConditionalWeakTable<DocumentViewModel, Control> _views = new();

    public ChunkResourcesView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Document)
                .Subscribe(ShowDocument)
                .DisposeWith(disposables);
        });
    }

    private void ShowDocument(DocumentViewModel? document)
    {
        DocumentHost.Child = document == null ? null : _views.GetValue(document, _ => new ContentControl { Content = document });
    }
}
