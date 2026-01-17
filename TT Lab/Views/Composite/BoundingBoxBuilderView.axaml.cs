using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.Views.Composite;

public partial class BoundingBoxBuilderView : BurnBridgeControl<BoundingBoxBuilderViewModel>
{
    public BoundingBoxBuilderView()
    {
        InitializeComponent();
    }
}