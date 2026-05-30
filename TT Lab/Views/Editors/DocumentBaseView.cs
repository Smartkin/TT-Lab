using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

[ExcludeFromViewRegistration]
public abstract class DocumentBaseView<T> : ReactiveUserControl<T> where T : DocumentBaseViewModel
{
    protected DocumentBaseView()
    {
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, x => x.IsVisible, view => view.IsVisible);
            this.OneWayBind(ViewModel, x => x.CanWrite, view => view.IsEnabled);
            this.OneWayBind(ViewModel, x => x.DepthDependentBrush, view => view.Background);
            
            HandleActivation(disposables);
        });
    }

    protected abstract void HandleActivation(CompositeDisposable disposables);
}