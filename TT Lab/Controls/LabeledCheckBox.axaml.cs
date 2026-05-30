using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

namespace TT_Lab.Controls;

public partial class LabeledCheckBox : UserControl
{
    public LabeledCheckBox()
    {
        InitializeComponent();
    }
    
    [Description("Name of the checkbox displayed above."), Category("Common Properties")]
    public string CheckBoxName
    {
        get => GetValue(CheckBoxNameProperty);
        set => SetValue(CheckBoxNameProperty, value);
    }

    [Description("Whether the checkbox is checked"), Category("Common Properties")]
    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    [Description("Whether the checkbox label is horizontal or vertical in relation to the checkbox"), Category("Common Properties")]
    public Orientation LayoutOrientation
    {
        get => GetValue(LayoutOrientationProperty);
        set => SetValue(LayoutOrientationProperty, value);
    }

    // Using a DependencyProperty as the backing store for LayoutOrientation.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<Orientation> LayoutOrientationProperty = AvaloniaProperty.Register<LabeledCheckBox, Orientation>(nameof(LayoutOrientation), Orientation.Vertical);
        // DependencyProperty.Register(nameof(LayoutOrientation), typeof(Orientation), typeof(LabeledCheckBox),
        //     new PropertyMetadata(Orientation.Vertical, OnLayoutOrientationChanged));

    // Using a DependencyProperty as the backing store for Checked.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<bool> CheckedProperty = AvaloniaProperty.Register<LabeledCheckBox, bool>(nameof(Checked), false, false, BindingMode.TwoWay);
        // DependencyProperty.Register(nameof(Checked), typeof(bool), typeof(LabeledCheckBox),
        //     new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    // Using a DependencyProperty as the backing store for TextBoxName.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<string> CheckBoxNameProperty = AvaloniaProperty.Register<LabeledCheckBox, string>(nameof(CheckBoxName), "Label");

    static LabeledCheckBox()
    {
        LayoutOrientationProperty.Changed.AddClassHandler<LabeledCheckBox>((t, e) => t.UpdateOrientation());
    }

    private void UpdateOrientation()
    {
        if (Content is StackPanel stackPanel)
        {
            stackPanel.Orientation = LayoutOrientation;
        }
    }
}