using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class ResourceBrowserView : BurnBridgeWindow<ResourceBrowserViewModel>
{
    public ResourceBrowserView()
    {
        InitializeComponent();
    }
}