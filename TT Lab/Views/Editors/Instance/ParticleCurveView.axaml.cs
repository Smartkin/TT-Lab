using System.Reactive.Disposables;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Views.Editors.Instance;

public partial class ParticleCurveView : DocumentBaseView<ParticleCurveViewModel>
{
    public ParticleCurveView()
    {
        InitializeComponent();
        Graph.KeyDragged += (index, to) => ViewModel?.DragKey(index, to);
        Graph.KeyAddRequested += at => ViewModel?.AddKey(at);
        Graph.KeyRemoveRequested += index => ViewModel?.RemoveKey(index);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
