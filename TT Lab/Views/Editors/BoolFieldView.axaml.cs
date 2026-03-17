using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class BoolFieldView : DocumentBaseView<BoolFieldViewModel>
{
    public BoolFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.CurrentValue, view => view.BoolField.IsChecked).DisposeWith(disposables);
        
        this.WhenAnyValue(view => view.BoolField.IsChecked)
            .Skip(1)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .InvokeCommand(ViewModel?.SetValueCommand)
            .DisposeWith(disposables);
    }
}