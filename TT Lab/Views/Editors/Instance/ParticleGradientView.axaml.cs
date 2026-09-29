using System.Reactive.Disposables;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Views.Editors.Instance;

public partial class ParticleGradientView : DocumentBaseView<ParticleGradientViewModel>
{
    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public ParticleGradientView()
    {
        InitializeComponent();
        Bar.DragStarted += () => ViewModel?.BeginDrag();
        Bar.DragEnded += () => ViewModel?.EndDrag();
        Bar.StopDragged += (index, time) => ViewModel?.MoveKey(index, (float)time);
        Bar.StopAddRequested += time => ViewModel?.AddStop(time);
        Bar.StopRemoveRequested += index => ViewModel?.RemoveKey(index);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
