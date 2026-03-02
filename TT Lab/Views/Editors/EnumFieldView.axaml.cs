using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class EnumFieldView : DocumentBaseView<EnumFieldViewModel>
{
    public EnumFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.EnumValues, view => view.EnumChoices.ItemsSource).DisposeWith(disposables);
            
        this.Bind(ViewModel, viewModel => viewModel.SelectedValue, view => view.EnumChoices.SelectedItem).DisposeWith(disposables);
    }
}