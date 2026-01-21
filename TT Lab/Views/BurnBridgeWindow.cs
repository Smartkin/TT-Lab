using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Interfaces;

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
                    if (ViewModel is not null && ViewModel is IHaveResult result)
                    {
                        Close(result.GetResult());
                    }
                    else
                    {
                        Close();
                    }
                }

                return Task.CompletedTask;
            };
        }
        
        base.OnPropertyChanged(change);
    }
}