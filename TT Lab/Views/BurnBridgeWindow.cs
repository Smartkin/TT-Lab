using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace TT_Lab.Views;

[ExcludeFromViewRegistration]
public class BurnBridgeWindow<TViewModel> : ReactiveWindow<TViewModel> where TViewModel : class, Caliburn.Micro.IScreen, Caliburn.Micro.IViewAware
{
    protected BurnBridgeWindow()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        
        this.WhenActivated(async (CompositeDisposable disposables) =>
        {
            await ViewModel!.ActivateAsync();
            
            Disposable.Create(async () =>
            {
                await ViewModel!.DeactivateAsync(true);
            }).DisposeWith(disposables);
        });
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ViewModelProperty)
        {
            ViewModel!.AttachView(this);
        }
        
        base.OnPropertyChanged(change);
    }
}