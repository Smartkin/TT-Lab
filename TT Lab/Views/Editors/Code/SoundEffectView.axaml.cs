using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public class RenderedEventArgs : RoutedEventArgs;

public partial class SoundEffectView : BurnBridgeControl<SoundEffectViewModel>
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