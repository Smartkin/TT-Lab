using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Editors.Graphics;

namespace TT_Lab.Views.Editors.Graphics;

public partial class ShaderView : BurnBridgeControl<ShaderViewModel>
{
    public ShaderView()
    {
        InitializeComponent();
    }
}