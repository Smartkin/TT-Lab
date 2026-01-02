using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TT_Lab.Views.Composite;

public partial class Vector3View : UserControl
{
    [Description("Whether the fields will be vertical or horizontal"), Category("Common Properties")]
    public bool VerticalLayout
    {
        get => GetValue(VerticalLayoutProperty);
        set => SetValue(VerticalLayoutProperty, value);
    }

    // Using a DependencyProperty as the backing store for VerticalLayout.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<bool> VerticalLayoutProperty = AvaloniaProperty.Register<Vector3View, bool>(nameof(VerticalLayout));
    // DependencyProperty.Register(nameof(VerticalLayout), typeof(bool), typeof(Vector3View),
    //     new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnLayoutChanged)));

    static Vector3View()
    {
        VerticalLayoutProperty.Changed.AddClassHandler<Vector3View>((view, e) => view.OnLayoutChanged(e));
    }
    
    public Vector3View()
    {
        InitializeComponent();
    }

    private void OnLayoutChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var control = this;
        var isVert = control.VerticalLayout;
        if (e.Property.Name == nameof(control.VerticalLayout))
        {
            isVert = (bool)e.NewValue;
        }
        
        if (isVert)
        {
            control.FormationGrid.Columns = 0;
            control.FormationGrid.Rows = 3;
        }
        else
        {
            control.FormationGrid.Columns = 3;
            control.FormationGrid.Rows = 1;
        }
    }
}