using ReactiveUI.Avalonia;
using TT_Lab.Controls;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.Views.Composite;

public partial class SceneEditorTabView : ReactiveUserControl<SceneEditorTabViewModel>
{
    public SceneEditorTabView()
    {
        InitializeComponent();
        FocusKeeper.KeepFocusIn(this);
    }
}
