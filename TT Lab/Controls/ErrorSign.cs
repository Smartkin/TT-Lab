using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace TT_Lab.Controls;

/// <summary>
/// The error of a field's rules: a red sign next to the field with the error as its tooltip, shown while there's one. Written over the text
/// box the error hid what was typed and got cut off at the field's end
/// </summary>
public class ErrorSign : Border
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<ErrorSign, string?>(nameof(Text));

    private static readonly IBrush SignFill = new SolidColorBrush(Color.FromRgb(0xD9, 0x44, 0x3A));
    private static readonly IBrush SignEdge = new SolidColorBrush(Color.FromRgb(0x7A, 0x1C, 0x14));
    private static readonly Geometry Mark = Geometry.Parse("M7.2,3.6 H8.8 L8.5,9.4 H7.5 Z M7.1,10.6 H8.9 V12.4 H7.1 Z");

    public ErrorSign()
    {
        // A background takes the pointer over the whole sign, for the tooltip
        Background = Brushes.Transparent;
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(4, 0, 2, 0);
        IsVisible = false;
        var circle = new Ellipse { Width = 15, Height = 15, Fill = SignFill, Stroke = SignEdge, StrokeThickness = 1 };
        Canvas.SetLeft(circle, 0.5);
        Canvas.SetTop(circle, 0.5);
        Child = new Viewbox
        {
            Width = 18,
            Height = 18,
            Child = new Canvas { Width = 16, Height = 16, Children = { circle, new Path { Data = Mark, Fill = Brushes.White } } }
        };
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            IsVisible = !string.IsNullOrEmpty(Text);
            ToolTip.SetTip(this, IsVisible ? Text : null);
        }
    }
}
