using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Xaml.Interactions.Core;

namespace TT_Lab.Controls;

public class SelectedItemChangedEventArgs(object sender, RoutedEvent @event, object? selectedItem)
    : RoutedEventArgs(@event, sender)
{
    public T? GetSelectedItem<T>()
    {
        return (T?)selectedItem;
    }
}

public class EditableListBoxAddedItemEventArgs : RoutedEventArgs;
public class EditableListBoxRemovedItemEventArgs : RoutedEventArgs;
public class EditableListBoxDuplicatedItemEventArgs : RoutedEventArgs;

public delegate void SelectedItemChangedEventHandler(object sender, SelectedItemChangedEventArgs e);

public partial class EditableListBox : UserControl
{
    public event EventHandler<EditableListBoxAddedItemEventArgs> AddItem
    {
        add => AddHandler(AddItemEvent, value);
        remove => RemoveHandler(AddItemEvent, value);
    }
    
    public event EventHandler<EditableListBoxRemovedItemEventArgs> DeleteItem
    {
        add => AddHandler(DeleteItemEvent, value);
        remove => RemoveHandler(DeleteItemEvent, value);
    }
    
    public event EventHandler<EditableListBoxDuplicatedItemEventArgs> DuplicateItem
    {
        add => AddHandler(DuplicateItemEvent, value);
        remove => RemoveHandler(DuplicateItemEvent, value);
    }
    
    public event SelectedItemChangedEventHandler SelectedItemChanged
    {
        add => AddHandler(SelectedItemChangedEvent, value);
        remove => RemoveHandler(SelectedItemChangedEvent, value);
    }
    
    public static readonly RoutedEvent SelectedItemChangedEvent =
        RoutedEvent.Register<EditableListBox, SelectedItemChangedEventArgs>(nameof(SelectedItemChanged), RoutingStrategies.Bubble);
    // EventManager.RegisterRoutedEvent("SelectedItemChanged",
    // RoutingStrategy.Bubble, typeof(SelectedItemChangedEventHandler), typeof(EditableListBox));
    
    public static readonly RoutedEvent AddItemEvent =
        RoutedEvent.Register<EditableListBox, EditableListBoxAddedItemEventArgs>(nameof(AddItem), RoutingStrategies.Bubble);
    // // EventManager.RegisterRoutedEvent("AddItem",
    // // RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(EditableListBox));
    //
    public static readonly RoutedEvent DeleteItemEvent =
        RoutedEvent.Register<EditableListBox, EditableListBoxRemovedItemEventArgs>(nameof(DeleteItem), RoutingStrategies.Bubble);
    // // EventManager.RegisterRoutedEvent("DeleteItem",
    // // RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(EditableListBox));
    //
    public static readonly RoutedEvent DuplicateItemEvent =
        RoutedEvent.Register<EditableListBox, EditableListBoxDuplicatedItemEventArgs>(nameof(DuplicateItem), RoutingStrategies.Bubble);
    // EventManager.RegisterRoutedEvent("DuplicateItem",
    // RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(EditableListBox));

    public static readonly StyledProperty<string> ListBoxNameProperty = AvaloniaProperty.Register<EditableListBox, string>(nameof(ListBoxName), "Editable List");
    // DependencyProperty.Register(
    // nameof(ListBoxName), typeof(string), typeof(EditableListBox), new PropertyMetadata("Editable List"));

    public string ListBoxName
    {
        get => GetValue(ListBoxNameProperty);
        set => SetValue(ListBoxNameProperty, value);
    }

    public static readonly StyledProperty<int> SizeLimitProperty = AvaloniaProperty.Register<EditableListBox, int>(nameof(SizeLimit), int.MaxValue);
    // DependencyProperty.Register(
    // nameof(SizeLimit), typeof(int), typeof(EditableListBox), new PropertyMetadata(int.MaxValue));

    public int SizeLimit
    {
        get => GetValue(SizeLimitProperty);
        set => SetValue(SizeLimitProperty, value);
    }
    
    public static readonly StyledProperty<IEnumerable?> ItemsProperty = AvaloniaProperty.Register<EditableListBox, IEnumerable?>(nameof(Items));
    // DependencyProperty.Register(
    // nameof(Items), typeof(IEnumerable), typeof(EditableListBox), new PropertyMetadata(null));

    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly StyledProperty<DataTemplate> ItemTemplateProperty = AvaloniaProperty.Register<EditableListBox, DataTemplate>(nameof(ItemTemplate));
    // DependencyProperty.Register(
    // nameof(ItemTemplate), typeof(DataTemplate), typeof(EditableListBox), new PropertyMetadata(default(DataTemplate)));

    public DataTemplate ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly AttachedProperty<object> DataTriggerAttachedValueProperty =
        AvaloniaProperty.RegisterAttached<EditableListBox, object>("DataTriggerAttachedValue", typeof(EditableListBox));
    // DependencyProperty.RegisterAttached(
    // "DataTriggerAttachedValue", typeof(object), typeof(EditableListBox), new PropertyMetadata(null, OnDataTriggerValueChanged));

    public static readonly StyledProperty<object> SelectedItemProperty = AvaloniaProperty.Register<EditableListBox, object>(nameof(SelectedItem));
    // DependencyProperty.Register(
    // nameof(SelectedItem), typeof(object), typeof(EditableListBox), new PropertyMetadata(null, OnSelectedItemChanged));

    private void OnSelectedItemChanged()
    {
    }

    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnDataTriggerValueChanged(DataTrigger d, AvaloniaPropertyChangedEventArgs e)
    {
        d.Value = e.NewValue;
    }

    public static object GetDataTriggerAttachedValue(AvaloniaObject d)
    {
        return d.GetValue(DataTriggerAttachedValueProperty);
    }

    public static void SetDataTriggerAttachedValue(AvaloniaObject d, object value)
    {
        d.SetValue(DataTriggerAttachedValueProperty, value);
    }

    public int Zero => 0;

    static EditableListBox()
    {
        DataTriggerAttachedValueProperty.Changed.AddClassHandler<DataTrigger>(OnDataTriggerValueChanged);
    }

    public EditableListBox()
    {
        InitializeComponent();
    }

    private void OnAddItemClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new EditableListBoxAddedItemEventArgs { RoutedEvent = AddItemEvent });
    }
    
    private void OnDeleteItemClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new EditableListBoxRemovedItemEventArgs { RoutedEvent = DeleteItemEvent });
    }
    
    private void OnDuplicateItemClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new EditableListBoxDuplicatedItemEventArgs { RoutedEvent = DuplicateItemEvent });
    }

    private void OnItemsStorageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            return;
        }

        e.Handled = true;
        RaiseEvent(new SelectedItemChangedEventArgs(this, SelectedItemChangedEvent, e.AddedItems[0]));
        SelectedItem = e.AddedItems[0];
    }
}