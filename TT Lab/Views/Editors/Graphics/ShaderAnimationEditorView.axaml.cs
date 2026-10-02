using System.Reactive.Disposables;
using TT_Lab.ViewModels.Editors.Graphics;

namespace TT_Lab.Views.Editors.Graphics;

public partial class ShaderAnimationEditorView : DocumentBaseView<ShaderAnimationEditorViewModel>
{
    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public ShaderAnimationEditorView()
    {
        InitializeComponent();
        Timeline.DragStarted += () => ViewModel?.BeginDrag();
        Timeline.DragEnded += () => ViewModel?.EndDrag();
        Timeline.KeyDragged += (track, frame, value) => ViewModel?.DragKey(track, frame, value);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
