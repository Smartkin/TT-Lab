using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.Views;
using ILogger = Splat.ILogger;
using LogLevel = Splat.LogLevel;

namespace TT_Lab.Tests.Editor;

// The Scenes and Resources panels, every editor's root and lists show through a view of a class they derive from, which ReactiveUI's
// locator warned about ("Failed to resolve view for view model type 'System.Object'") on start-up and for every editor opened
public class ViewLocatorTests
{
    [AvaloniaFact]
    public void ViewsOfABaseClassAreFoundWithoutWarnings()
    {
        var scenes = new ScenesEditorsViewModel();

        var (view, warnings) = BuildLogging(scenes);

        Assert.Same(scenes, Assert.IsType<EditorsViewerView>(view).ViewModel);
        Assert.DoesNotContain(warnings, warning => warning.Contains("resolve view"));
    }

    [AvaloniaFact]
    public void ViewModelsWithoutAViewSayWhichTheyAre()
    {
        var (view, warnings) = BuildLogging(new NothingToShowViewModel());

        Assert.Equal("No View found for: NothingToShowViewModel", Assert.IsType<TextBlock>(view).Text);
        Assert.Contains(warnings, warning => warning.Contains(typeof(NothingToShowViewModel).FullName!));
    }

    private static (Control? View, List<string> Warnings) BuildLogging(object viewModel)
    {
        var warnings = new List<string>();
        var logger = new Warnings(warnings);
        Locator.CurrentMutable.RegisterConstant<ILogManager>(new FuncLogManager(type => new WrappingFullLogger(new WrappingPrefixLogger(logger, type))));
        try
        {
            return (new ViewLocator().Build(viewModel), warnings);
        }
        finally
        {
            Locator.CurrentMutable.UnregisterCurrent(typeof(ILogManager));
        }
    }

    private sealed class NothingToShowViewModel;

    private sealed class Warnings(List<string> lines) : ILogger
    {
        public LogLevel Level => LogLevel.Debug;

        public void Write(string message, LogLevel logLevel) => Add(message, logLevel);

        public void Write(Exception exception, string message, LogLevel logLevel) => Add(message, logLevel);

        public void Write(string message, Type type, LogLevel logLevel) => Add(message, logLevel);

        public void Write(Exception exception, string message, Type type, LogLevel logLevel) => Add(message, logLevel);

        private void Add(string message, LogLevel level)
        {
            if (level < LogLevel.Warn)
            {
                return;
            }

            lock (lines)
            {
                lines.Add(message);
            }
        }
    }
}
