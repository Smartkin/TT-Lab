using System;
using System.Reactive.Disposables.Fluent;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public partial class AnimationView : ReactiveUserControl<AnimationViewModel>
{
    public event EventHandler<RoutedEventArgs>? Rendered
    {
        add => AddHandler(RenderedEvent, value);
        remove => RemoveHandler(RenderedEvent, value);
    }
    
    public static readonly RoutedEvent<RenderedEventArgs> RenderedEvent =
        RoutedEvent.Register<AnimationView, RenderedEventArgs>(
            nameof(Rendered),
            RoutingStrategies.Direct);
    
    public AnimationView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, viewModel => viewModel.CurrentAnimationFrame, view => view.AnimationTrack.Value).DisposeWith(disposables);
            this.OneWayBind(ViewModel, viewModel => viewModel.TotalFrames, view => view.AnimationTrack.Maximum).DisposeWith(disposables);
            
            this.OneWayBind(ViewModel, viewModel => viewModel.CurrentAnimationFrame, view => view.CurrentFrame.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, viewModel => viewModel.TotalFrames, view => view.TotalFrames.Text).DisposeWith(disposables);
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