using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Editors.Instance.Cameras;

namespace TT_Lab.Views.Editors.Instance.Cameras;

public partial class CameraLineView : BurnBridgeControl<CameraLineViewModel>
{
    public CameraLineView()
    {
        InitializeComponent();
    }
}