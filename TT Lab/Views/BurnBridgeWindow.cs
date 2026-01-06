using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
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
        if (change.Property == ViewModelProperty && change.NewValue is not null && !ReferenceEquals(change.OldValue, change.NewValue))
        {
            ViewModel!.AttachView(this);
            ViewModel!.Deactivated += (sender, args) =>
            {
                if (args.WasClosed)
                {
                    Close();
                }

                return Task.CompletedTask;
            };
        }
        
        base.OnPropertyChanged(change);
    }
}