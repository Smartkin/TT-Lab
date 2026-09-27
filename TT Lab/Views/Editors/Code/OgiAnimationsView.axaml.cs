using System.Reactive.Disposables;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public partial class OgiAnimationsView : DocumentBaseView<OgiAnimationsViewModel>
{
    public OgiAnimationsView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
