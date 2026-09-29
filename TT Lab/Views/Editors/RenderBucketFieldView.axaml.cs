using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class RenderBucketFieldView : DocumentBaseView<RenderBucketFieldViewModel>
{
    public RenderBucketFieldView()
    {
        InitializeComponent();
    }

    // The combo box drops its selection when its items change
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Buckets, view => view.BucketChoices.ItemsSource).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.SelectedBucket, view => view.BucketChoices.SelectedItem).DisposeWith(disposables);
    }
}
