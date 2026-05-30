using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.Views.Composite;

public partial class Matrix4View : BurnBridgeControl<Matrix4ViewModel>
{
    public Matrix4View()
    {
        InitializeComponent();
    }
}