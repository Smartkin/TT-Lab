using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public partial class OGIView : BurnBridgeControl<OGIViewModel>
{
    public OGIView()
    {
        InitializeComponent();
    }
}