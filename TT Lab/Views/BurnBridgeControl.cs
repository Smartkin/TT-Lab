using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI.Avalonia;
using ReactiveUI;

namespace TT_Lab.Views;

[ExcludeFromViewRegistration]
public abstract class BurnBridgeControl<TViewModel> : ReactiveUserControl<TViewModel> where TViewModel : class, Caliburn.Micro.IScreen, Caliburn.Micro.IViewAware
{
    protected BurnBridgeControl()
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
        if (change.Property == ViewModelProperty && change.NewValue is not null && !ReferenceEquals(change.OldValue, change.NewValue))
        {
            ViewModel!.AttachView(this);
        }
        
        base.OnPropertyChanged(change);
    }
}