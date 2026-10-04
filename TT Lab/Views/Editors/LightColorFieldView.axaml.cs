using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class LightColorFieldView : DocumentBaseView<LightColorFieldViewModel>
{
    public LightColorFieldView()
    {
        InitializeComponent();
    }

    // The picker would hand the color it showed to the next light
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.ShownColor, view => view.Picker.Color).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.StoredText, view => view.Stored.Text).DisposeWith(disposables);
    }
}
