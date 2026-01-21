using System;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public partial class AnimationView : BurnBridgeControl<AnimationViewModel>
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