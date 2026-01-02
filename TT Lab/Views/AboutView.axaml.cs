using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace TT_Lab.Views;

public partial class AboutView : Window
{
    public AboutView()
    {
        InitializeComponent();
    }
    //
    // private void Hyperlink_RequestNavigate(object? sender, RoutedEventArgs e)
    // {
    //     Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
    //     e.Handled = true;
    // }
}