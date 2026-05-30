using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Editors.Instance.Cameras;

namespace TT_Lab.Views.Editors.Instance.Cameras;

public partial class CameraZoneView : BurnBridgeControl<CameraZoneViewModel>
{
    public CameraZoneView()
    {
        InitializeComponent();
    }
}