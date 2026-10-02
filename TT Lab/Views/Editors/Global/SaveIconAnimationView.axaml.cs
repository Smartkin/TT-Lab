using System.Reactive.Disposables;
using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.Views.Editors.Global;

public partial class SaveIconAnimationView : DocumentBaseView<SaveIconAnimationViewModel>
{
    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public SaveIconAnimationView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
