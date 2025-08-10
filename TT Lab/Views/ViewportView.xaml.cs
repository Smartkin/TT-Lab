using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlmSharp;

namespace TT_Lab.Views;

public partial class ViewportView : UserControl
{
    public ViewportView()
    {
        InitializeComponent();
    }

    private void ViewportView_OnLoaded(object sender, RoutedEventArgs e)
    {
        InvalidateVisual();
        
    }

    private void ViewportView_OnSourceUpdated(object? sender, DataTransferEventArgs e)
    {
        InvalidateVisual();
    }

    private void ViewportView_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateVisual();
    }
}