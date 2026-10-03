using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TT_Lab.Util;

namespace TT_Lab.Tests.Editor;

public sealed class ComboBoxDropDownTests
{
    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    // Popups are drawn in the window, so a combo box's dropdown asking to be brought into view (its selected item gets the focus as it
    // opens) scrolled the inspector around it, a bit further up at every click. The combo box itself still gets brought into view
    [AvaloniaFact]
    public void ADropDownDoesNotScrollWhatIsAroundItsComboBox()
    {
        var combo = new ComboBox { ItemsSource = new[] { "One", "Two", "Three" }, SelectedIndex = 1 };
        var scroller = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 900 }, combo, new Border { Height = 900 } } } };
        var window = new Window { Content = scroller, Width = 300, Height = 400 };
        window.Show();
        Pump();
        scroller.Offset = new Vector(0, 760);
        Pump();
        var reachedUnhandled = new List<Visual?>();
        scroller.AddHandler(Control.RequestBringIntoViewEvent, (_, e) =>
        {
            if (!e.Handled)
            {
                reachedUnhandled.Add(e.TargetObject);
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        combo.IsDropDownOpen = true;
        Pump();
        var item = Assert.IsAssignableFrom<Control>(combo.ContainerFromIndex(1));
        item.BringIntoView();
        Pump();

        Assert.DoesNotContain(item, reachedUnhandled);
        Assert.Equal(760, scroller.Offset.Y);

        combo.IsDropDownOpen = false;
        scroller.Offset = new Vector(0, 0);
        Pump();
        combo.BringIntoView();
        Pump();
        Assert.True(scroller.Offset.Y > 500, $"the combo box wasn't brought into view, offset {scroller.Offset.Y}");
        window.Close();
    }

    // TT Lab lets go of Avalonia's D-Bus connection itself once its lifetime is over, which it reaches by reflection
    [Fact]
    public void AvaloniasDBusConnectionIsWhereTTLabLooksForIt()
    {
        Assert.NotEmpty(DBusShutdown.ConnectionFields());
    }
}
