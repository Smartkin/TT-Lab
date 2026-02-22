using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class UriLinkView : ReactiveUserControl<UriLinkViewModel>
{
    public UriLinkView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, viewModel => viewModel.LinkText, view => view.UriDisplay.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, viewModel => viewModel.SelectUriFromLinkCommand, view => view.ChangeLink, nameof(ChangeLink.Click)).DisposeWith(disposables);
            this.BindCommand(ViewModel, viewModel => viewModel.OpenDocumentCommand, view => view.OpenDocument, nameof(OpenDocument.Click)).DisposeWith(disposables);
        });
    }
}