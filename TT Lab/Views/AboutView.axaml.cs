using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class AboutView : ReactiveWindow<AboutViewModel>
{
    public AboutView()
    {
        InitializeComponent();
    }
}