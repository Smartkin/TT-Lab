using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Themes.Simple;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Discord;
using TT_Lab.ViewModels;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

// Popups are drawn in their window, and the app's Simple theme gives tooltips neither a width nor wrapping: a long hint went past the
// window's edge (the PCSX2 arguments' in the Preferences). The windows here get the Simple theme like the app's, the tests' is Fluent
public sealed class ToolTipTests : IDisposable
{
    private readonly string _wasArguments = Preferences.GetPreference<string>(Preferences.Pcsx2Arguments);
    private readonly List<string> _wasCollapsed = Preferences.GetPreference<List<string>>(Preferences.CollapsedPreferenceSections);

    public ToolTipTests()
    {
        Preferences.SetPreference(Preferences.CollapsedPreferenceSections, new List<string>());
    }

    public void Dispose()
    {
        Preferences.SetPreference(Preferences.Pcsx2Arguments, _wasArguments);
        Preferences.SetPreference(Preferences.CollapsedPreferenceSections, _wasCollapsed);
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static ToolTip OpenHint(Control control)
    {
        ToolTip.SetIsOpen(control, true);
        Pump();
        var tipProperty = (AvaloniaProperty)typeof(ToolTip).GetField("ToolTipProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var tip = Assert.IsType<ToolTip>(control.GetValue(tipProperty));
        Assert.True(tip.IsEffectivelyVisible);
        return tip;
    }

    private static void AssertFits(ToolTip tip, Window window)
    {
        Assert.True(tip.Bounds.Width <= window.Bounds.Width - 2 * ToolTips.WindowMargin,
            $"The hint is {tip.Bounds.Width} wide in a {window.Bounds.Width} wide window");
        Assert.All(tip.GetVisualDescendants().OfType<TextBlock>(), text =>
        {
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
            Assert.True(text.Bounds.Width <= tip.Bounds.Width, $"'{text.Text}' is {text.Bounds.Width} wide in a {tip.Bounds.Width} wide hint");
        });
    }

    [AvaloniaFact]
    public void ThePcsx2ArgumentsHintFitsThePreferences()
    {
        var presence = new DiscordPresence(() => new PresenceInputs(ProjectPhase.None, null, null, false, true), () => new FakePresenceClient(),
            action => action(), () => DateTime.UtcNow);
        var view = new PreferencesView { DataContext = new PreferencesViewModel(presence) };
        view.Styles.Add(new SimpleTheme());
        view.Show();
        Pump();

        var tip = OpenHint(view.FindControl<TextBox>("Pcsx2Arguments")!);

        AssertFits(tip, view);
        var text = tip.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(text.TextLayout.TextLines.Count > 1, "The hint didn't wrap");
    }

    // Hints made of text blocks (the viewport's controls) wrap theirs, and a narrow window (a floating panel) gets narrower hints
    [AvaloniaFact]
    public void HintsOfTheirOwnControlsWrapInNarrowWindows()
    {
        var line = string.Join(' ', Enumerable.Repeat("Drag on the scene to select everything within the rectangle", 4));
        var button = new Button { Content = "?" };
        ToolTip.SetTip(button, new StackPanel { Children = { new TextBlock { Text = "Viewport controls", FontWeight = FontWeight.Bold }, new TextBlock { Text = line } } });
        var window = new Window { Content = button, Width = 300, Height = 400 };
        window.Styles.Add(new SimpleTheme());
        window.Show();
        Pump();

        var tip = OpenHint(button);

        AssertFits(tip, window);
        Assert.True(tip.GetVisualDescendants().OfType<TextBlock>().Last().TextLayout.TextLines.Count > 1, "The line didn't wrap");
    }

    [AvaloniaFact]
    public void HintsKeepAReadingWidthInWideWindows()
    {
        Assert.Equal(ToolTips.ReadingWidth, ToolTips.WidthIn.Convert(1920.0, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(300 - 2 * ToolTips.WindowMargin, ToolTips.WidthIn.Convert(300.0, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(ToolTips.ReadingWidth, ToolTips.WidthIn.Convert(double.NaN, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture));
    }
}
