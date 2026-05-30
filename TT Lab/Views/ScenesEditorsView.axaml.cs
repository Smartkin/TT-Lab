using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class ScenesEditorsView : ReactiveUserControl<ScenesEditorsViewModel>
{
    public ScenesEditorsView()
    {
        InitializeComponent();
        
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, viewModel => viewModel.Tabs, view => view.ScenesEditorsDock.ItemsSource).DisposeWith(disposables);
            // this.OneWayBind(ViewModel, viewModel => viewModel.Tabs, view => view.SceneViewportsDock.ItemsSource).DisposeWith(disposables);
        });
    }
}