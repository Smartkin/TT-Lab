using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class DocumentView : ReactiveUserControl<DocumentViewModel>
{
    public DocumentView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, viewModel => viewModel.DocumentEditors, view => view.EditorsContainer.ItemsSource)
                .DisposeWith(disposables);
        });
    }
}