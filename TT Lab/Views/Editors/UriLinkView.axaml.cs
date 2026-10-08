using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class UriLinkView : DocumentBaseView<UriLinkViewModel>
{
    public UriLinkView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.LinkText, view => view.UriDisplay.Text).DisposeWith(disposables);
        this.BindCommand(ViewModel, viewModel => viewModel.SelectUriFromLinkCommand, view => view.ChangeLink, nameof(ChangeLink.Click)).DisposeWith(disposables);
        this.BindCommand(ViewModel, viewModel => viewModel.OpenDocumentCommand, view => view.OpenDocument, nameof(OpenDocument.Click)).DisposeWith(disposables);
        this.BindCommand(ViewModel, viewModel => viewModel.AddDependencyCommand, view => view.AddDependency, nameof(AddDependency.Click)).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.IsMissingDependency, view => view.MissingDependency.IsVisible).DisposeWith(disposables);
        this.WhenAnyValue(view => view.ViewModel!.IsMissingDependency)
            .Subscribe(missing => UriDisplay.Classes.Set("missingDependency", missing)).DisposeWith(disposables);
        this.WhenAnyValue(view => view.ViewModel!.AddDependencyHint)
            .Subscribe(hint => ToolTip.SetTip(AddDependency, hint)).DisposeWith(disposables);
        this.WhenAnyValue(view => view.ViewModel!.MissingDependencyText)
            .Subscribe(text => ToolTip.SetTip(MissingDependencySign, text)).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.CanAddDependency, view => view.AddDependency.IsVisible).DisposeWith(disposables);
    }
}
