using System;
using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
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

public delegate void SelectedItemChangedEventHandler(object sender, SelectedItemChangedEventArgs e);

public partial class EditableListBox : UserControl
{
    public event SelectedItemChangedEventHandler SelectedItemChanged
    {
        add => AddHandler(SelectedItemChangedEvent, value);
        remove => RemoveHandler(SelectedItemChangedEvent, value);
    }
    
    public static readonly RoutedEvent SelectedItemChangedEvent =
        RoutedEvent.Register<EditableListBox, SelectedItemChangedEventArgs>(nameof(SelectedItemChanged), RoutingStrategies.Bubble);

    public static readonly StyledProperty<string> ListBoxNameProperty = AvaloniaProperty.Register<EditableListBox, string>(nameof(ListBoxName), "Editable List");

    public string ListBoxName
    {
        get => GetValue(ListBoxNameProperty);
        set => SetValue(ListBoxNameProperty, value);
    }

    public static readonly StyledProperty<int> SizeLimitProperty = AvaloniaProperty.Register<EditableListBox, int>(nameof(SizeLimit), int.MaxValue);

    public int SizeLimit
    {
        get => GetValue(SizeLimitProperty);
        set => SetValue(SizeLimitProperty, value);
    }
    
    public static readonly StyledProperty<IEnumerable?> ItemsProperty = AvaloniaProperty.Register<EditableListBox, IEnumerable?>(nameof(Items));

    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly StyledProperty<DataTemplate> ItemTemplateProperty = AvaloniaProperty.Register<EditableListBox, DataTemplate>(nameof(ItemTemplate));

    public DataTemplate ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<EditableListBox, object?>(nameof(SelectedItem), null, false, BindingMode.TwoWay);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }
    
    public static readonly StyledProperty<ICommand> AddItemCommandProperty = AvaloniaProperty.Register<EditableListBox, ICommand>(nameof(AddItemCommand));

    public ICommand AddItemCommand
    {
        get => GetValue(AddItemCommandProperty);
        set => SetValue(AddItemCommandProperty, value);
    }
    
    public static readonly StyledProperty<ICommand> RemoveItemCommandProperty = AvaloniaProperty.Register<EditableListBox, ICommand>(nameof(RemoveItemCommand));

    public ICommand RemoveItemCommand
    {
        get => GetValue(RemoveItemCommandProperty);
        set => SetValue(RemoveItemCommandProperty, value);
    }
    
    public static readonly StyledProperty<ICommand> DuplicateItemCommandProperty = AvaloniaProperty.Register<EditableListBox, ICommand>(nameof(DuplicateItemCommand));

    public ICommand DuplicateItemCommand
    {
        get => GetValue(DuplicateItemCommandProperty);
        set => SetValue(DuplicateItemCommandProperty, value);
    }

    public int Zero => 0;

    public EditableListBox()
    {
        InitializeComponent();
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