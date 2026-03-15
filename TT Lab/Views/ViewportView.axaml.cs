using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class ViewportView : ReactiveUserControl<ViewportViewModel>
{
    public ViewportView()
    {
        InitializeComponent();
    }
    
    private void ViewportView_OnLoaded(object sender, RoutedEventArgs e)
    {
        InvalidateVisual();
    }

    private void ViewportView_OnSourceUpdated(object? sender, EventArgs eventArgs)
    {
        InvalidateVisual();
    }

    private void ViewportView_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateVisual();
    }
}