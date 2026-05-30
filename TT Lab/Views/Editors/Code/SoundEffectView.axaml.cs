using System;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public class RenderedEventArgs : RoutedEventArgs;

public partial class SoundEffectView : ReactiveUserControl<SoundEffectViewModel>
{
    public event EventHandler<RoutedEventArgs>? Rendered
    {
        add => AddHandler(RenderedEvent, value);
        remove => RemoveHandler(RenderedEvent, value);
    }
    
    public static readonly RoutedEvent<RenderedEventArgs> RenderedEvent =
        RoutedEvent.Register<SoundEffectView, RenderedEventArgs>(
            nameof(Rendered),
            RoutingStrategies.Direct);
    
    public SoundEffectView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, viewModel => viewModel.SoundProgress, view => view.PlayerTrack.Value).DisposeWith(disposables);
            this.OneWayBind(ViewModel, viewModel => viewModel.SoundDuration, view => view.PlayerTrack.Maximum).DisposeWith(disposables);

            this.OneWayBind(ViewModel, viewModel => viewModel.CurrentTime, view => view.CurrentTime.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, viewModel => viewModel.TotalTimeLength, view => view.TrackLength.Text).DisposeWith(disposables);
        });
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            RaiseEvent(new RoutedEventArgs(RenderedEvent, this));
            InvalidateVisual();
        }, DispatcherPriority.Background);
    }
}