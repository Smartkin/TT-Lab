using System.Reactive.Disposables;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Views.Editors.Instance;

public partial class ParticleGradientView : DocumentBaseView<ParticleGradientViewModel>
{
    public ParticleGradientView()
    {
        InitializeComponent();
        Bar.StopDragged += (index, time) => ViewModel?.MoveKey(index, (float)time);
        Bar.StopAddRequested += time => ViewModel?.AddStop(time);
        Bar.StopRemoveRequested += index => ViewModel?.RemoveKey(index);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
