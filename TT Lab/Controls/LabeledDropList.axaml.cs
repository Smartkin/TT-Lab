using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;

namespace TT_Lab.Controls;

public partial class LabeledDropList : UserControl
{
    [Description("Name of the droplist displayed above."), Category("Common Properties")]
    public string DropListName
    {
        get => GetValue(DropListNameProperty);
        set => SetValue(DropListNameProperty, value);
    }

    [Description("List of dropdown items."), Category("Common Properties")]
    public ObservableCollection<object> Items
    {
        get => (ObservableCollection<object>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    [Description("Index of the selected item from the dropdown."), Category("Common Properties")]
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    [Description("Selected item from the dropdown."), Category("Common Properties")]
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }
    
    public DataTemplate ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }
    
    [Description("Whether the textbox label is horizontal or vertical in relation to the textbox"), Category("Common Properties")]
    public Orientation LayoutOrientation
    {
        get => GetValue(LayoutOrientationProperty);
        set => SetValue(LayoutOrientationProperty, value);
    }
    
    // Dependency Property for ItemTemplate
    public static readonly StyledProperty<DataTemplate> ItemTemplateProperty = AvaloniaProperty.Register<LabeledDropList, DataTemplate>(nameof(ItemTemplate));
    // DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(LabeledDropList),
    //     new PropertyMetadata(null));

    // Using a DependencyProperty as the backing store for SelectedItem.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<object?> SelectedItemProperty = AvaloniaProperty.Register<LabeledDropList, object?>(nameof(SelectedItem), null, false, BindingMode.TwoWay);
    // DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(LabeledDropList),
    //     new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    // Using a DependencyProperty as the backing store for SelectedIndex.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<LabeledDropList, int>(nameof(SelectedIndex), -1, false, BindingMode.TwoWay);
    // DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(LabeledDropList),
    //     new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    // Using a DependencyProperty as the backing store for Items.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<IEnumerable> ItemsProperty = AvaloniaProperty.Register<LabeledDropList, IEnumerable>(nameof(Items));
    // DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(LabeledDropList),
    //     new PropertyMetadata(null));

    // Using a DependencyProperty as the backing store for TextBoxName.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<string> DropListNameProperty = AvaloniaProperty.Register<LabeledDropList, string>(nameof(DropListName), "Label");
    // DependencyProperty.Register(nameof(DropListName), typeof(string), typeof(LabeledDropList),
    //     new PropertyMetadata("Label"));
    
    // Using a DependencyProperty as the backing store for LayoutOrientation.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<Orientation> LayoutOrientationProperty = AvaloniaProperty.Register<LabeledDropList, Orientation>(nameof(LayoutOrientation), Orientation.Vertical);
    // DependencyProperty.Register(nameof(LayoutOrientation), typeof(Orientation), typeof(LabeledDropList),
    //     new PropertyMetadata(Orientation.Vertical, OnLayoutOrientationChanged));

    static LabeledDropList()
    {
        LayoutOrientationProperty.Changed.AddClassHandler<LabeledDropList>((list, e) =>
            list.UpdateOrientation((Orientation)e.NewValue!));
    }
    
    public LabeledDropList()
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
                DockPanel.SetDock(LblDropBoxName, Dock.Left);
                break;
            case Orientation.Vertical:
                DockPanel.SetDock(LblDropBoxName, Dock.Top);
                break;
        }
        ElementContainer.UpdateLayout();
    }
}