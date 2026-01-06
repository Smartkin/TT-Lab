using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class PreferencesView : BurnBridgeWindow<PreferencesViewModel>
{
    public PreferencesView()
    {
        InitializeComponent();
    }
}