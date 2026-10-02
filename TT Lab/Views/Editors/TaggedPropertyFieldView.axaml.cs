using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class TaggedPropertyFieldView : DocumentBaseView<TaggedPropertyFieldViewModel>
{
    public TaggedPropertyFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.Text, view => view.ValueField.Text).DisposeWith(disposables);
    }
}
