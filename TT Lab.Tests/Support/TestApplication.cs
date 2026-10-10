using Avalonia;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using TT_Lab.Tests.Support;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

namespace TT_Lab.Tests.Support;

/// <summary>
/// Headless application that [AvaloniaFact] tests run in, with the themes the editor's controls need
/// </summary>
public class TestApplication : Application
{
    public override void Initialize()
    {
        DataTemplates.Add(new TT_Lab.Util.ViewLocator());
        TT_Lab.Controls.ComboBoxDropDowns.KeepScrollRequestsInside();
        Styles.Add(new FluentTheme());
        Styles.Add(new Dock.Avalonia.Themes.Simple.DockSimpleTheme());
        Styles.Add(new StyleInclude(new Uri("avares://TT Lab.Tests")) { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
        Styles.Add(new StyleInclude(new Uri("avares://TT Lab.Tests")) { Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml") });
        Styles.Add(TT_Lab.Controls.ToolTips.FitInWindows());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Editors apply their validation on the main thread scheduler, which is the thread pool unless it's set
        var mainThread = new TestMainThreadScheduler();
        RxApp.MainThreadScheduler = mainThread;
        RxSchedulers.MainThreadScheduler = mainThread;
        Splat.Locator.CurrentMutable.RegisterViewsForViewModels(typeof(TT_Lab.App).Assembly);
        base.OnFrameworkInitializationCompleted();
    }

    // Skia draws so bitmaps hold their pixels and textures encode to PNG
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseReactiveUI();
}
