using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Discord;
using TT_Lab.ViewModels;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

// The Preferences window's sections collapse and stay so in the settings, the Discord section turns Rich Presence on and its dot shows
// the connection, and PCSX2's extra arguments are typed under Direct Game Launch
public sealed class PreferencesWindowTests : IDisposable
{
    private readonly List<string> _wasCollapsed = Preferences.GetPreference<List<string>>(Preferences.CollapsedPreferenceSections);
    private readonly bool _wasEnabled = Preferences.GetPreference<bool>(Preferences.DiscordRichPresence);
    private readonly string _wasArguments = Preferences.GetPreference<string>(Preferences.Pcsx2Arguments);

    public PreferencesWindowTests()
    {
        Preferences.SetPreference(Preferences.CollapsedPreferenceSections, new List<string>());
        Preferences.SetPreference(Preferences.DiscordRichPresence, false);
    }

    public void Dispose()
    {
        Preferences.SetPreference(Preferences.CollapsedPreferenceSections, _wasCollapsed);
        Preferences.SetPreference(Preferences.DiscordRichPresence, _wasEnabled);
        Preferences.SetPreference(Preferences.Pcsx2Arguments, _wasArguments);
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static PreferencesView Show(DiscordPresence presence)
    {
        var view = new PreferencesView { DataContext = new PreferencesViewModel(presence) };
        view.Show();
        Pump();
        return view;
    }

    private static DiscordPresence Presence(Func<IPresenceClient>? makeClient = null) =>
        new(() => new PresenceInputs(ProjectPhase.None, null, null, false, true), makeClient ?? (() => new FakePresenceClient()), action => action(), () => DateTime.UtcNow);

    private static List<Expander> Sections(PreferencesView view) => view.GetVisualDescendants().OfType<Expander>().ToList();

    private static string Title(Expander section) => ((TextBlock)section.Header!).Text!;

    [AvaloniaFact]
    public void SectionsCollapseAndAreRemembered()
    {
        var view = Show(Presence());
        var sections = Sections(view);
        Assert.Equal(["General", "Direct Game Launch", "Discord Rich Presence"], sections.Select(Title));
        Assert.All(sections, section => Assert.True(section.IsExpanded));

        sections[1].IsExpanded = false;
        sections[2].IsExpanded = false;
        sections[2].IsExpanded = true;
        Pump();
        Assert.Equal(["Direct Game Launch"], Preferences.GetPreference<List<string>>(Preferences.CollapsedPreferenceSections));

        var again = Show(Presence());
        Assert.Equal([true, false, true], Sections(again).Select(section => section.IsExpanded));
    }

    [AvaloniaFact]
    public void TheDiscordSectionTurnsItOnAndItsDotShowsTheConnection()
    {
        FakePresenceClient? client = null;
        var presence = Presence(() => client = new FakePresenceClient());
        var view = Show(presence);
        var section = Sections(view).Single(expander => Title(expander) == "Discord Rich Presence");
        var enabled = section.GetVisualDescendants().OfType<LabeledCheckBox>().Single();
        var dot = section.GetVisualDescendants().OfType<Ellipse>().Single();
        Color Dot()
        {
            Pump();
            return ((ISolidColorBrush)dot.Fill!).Color;
        }

        Assert.Equal("Enabled", enabled.CheckBoxName);
        Assert.False(enabled.Checked);
        Assert.Equal(Color.FromRgb(0x55, 0x55, 0x55), Dot());

        enabled.Checked = true;
        Assert.True(Preferences.GetPreference<bool>(Preferences.DiscordRichPresence));
        presence.Tick();
        Assert.Equal(Color.FromRgb(0xE8, 0xC3, 0x3A), Dot());
        client!.RaiseReady();
        Assert.Equal(Color.FromRgb(0x3D, 0xBA, 0x4E), Dot());
        client.RaiseFailed();
        Assert.Equal(Color.FromRgb(0xD9, 0x44, 0x3A), Dot());

        enabled.Checked = false;
        presence.Tick();
        Assert.Equal(Color.FromRgb(0x55, 0x55, 0x55), Dot());
    }

    [AvaloniaFact]
    public void Pcsx2sExtraArgumentsAreAPreference()
    {
        var view = Show(Presence());
        var arguments = view.FindControl<TextBox>("Pcsx2Arguments")!;

        arguments.Text = "-fullscreen -bigpicture";
        Pump();

        Assert.Equal("-fullscreen -bigpicture", Preferences.GetPreference<string>(Preferences.Pcsx2Arguments));
    }
}
