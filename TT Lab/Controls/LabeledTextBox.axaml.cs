using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

namespace TT_Lab.Controls;

public partial class LabeledTextBox : UserControl
{
    [Description("Name of the textbox displayed above."), Category("Common Properties")]
    public string TextBoxName
    {
        get => GetValue(TextBoxNameProperty);
        set => SetValue(TextBoxNameProperty, value);
    }

    [Description("Input text."), Category("Common Properties")]
    public object Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    [Description("Allows updates only from the bindings"), Category("Common Properties")]
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }
    
    [Description("Whether the textbox label is horizontal or vertical in relation to the textbox"), Category("Common Properties")]
    public Orientation LayoutOrientation
    {
        get => GetValue(LayoutOrientationProperty);
        set => SetValue(LayoutOrientationProperty, value);
    }

    // Using a DependencyProperty as the backing store for LayoutOrientation.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<Orientation> LayoutOrientationProperty = AvaloniaProperty.Register<LabeledTextBox, Orientation>(nameof(LayoutOrientation), Orientation.Vertical);
    // DependencyProperty.Register(nameof(LayoutOrientation), typeof(Orientation), typeof(LabeledTextBox),
    //     new PropertyMetadata(Orientation.Vertical, OnLayoutOrientationChanged));

    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<LabeledTextBox, bool>(nameof(IsReadOnly));
    // DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(LabeledTextBox), new PropertyMetadata(false));

    // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<object> TextProperty = AvaloniaProperty.Register<LabeledTextBox, object>(nameof(Text), string.Empty, false, BindingMode.TwoWay);
    // DependencyProperty.Register(nameof(Text), typeof(object), typeof(LabeledTextBox),
    //     new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    // Using a DependencyProperty as the backing store for TextBoxName.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<string> TextBoxNameProperty = AvaloniaProperty.Register<LabeledTextBox, string>(nameof(TextBoxName), "Label");
    // DependencyProperty.Register(nameof(TextBoxName), typeof(string), typeof(LabeledTextBox),
    //     new PropertyMetadata("Label"));

    static LabeledTextBox()
    {
        LayoutOrientationProperty.Changed.AddClassHandler<LabeledTextBox>((box, args) => box.UpdateOrientation((Orientation)args.NewValue!));
    }
    
    public LabeledTextBox()
    {
        InitializeComponent();
    }
    
    private void UpdateOrientation(Orientation orientation)
    {
        if (Content is not DockPanel)
        {
            return;
        }
            
        switch (orientation)
        {
            case Orientation.Horizontal:
                DockPanel.SetDock(LblTextBoxName, Dock.Left);
                break;
            case Orientation.Vertical:
                DockPanel.SetDock(LblTextBoxName, Dock.Top);
                break;
        }
        ElementContainer.UpdateLayout();
    }
}