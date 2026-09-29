using System.Reactive.Disposables;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public partial class OgiAnimationsView : DocumentBaseView<OgiAnimationsViewModel>
{
    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public OgiAnimationsView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
