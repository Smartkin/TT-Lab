using Avalonia;
using System;
using Avalonia.Logging;
using Avalonia.OpenGL;
using ReactiveUI.Avalonia;

namespace TT_Lab;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            #if _LINUX
            .With(new X11PlatformOptions
            {
                GlProfiles = [new GlVersion(GlProfileType.OpenGL, 4, 6)],
                RenderingMode = [X11RenderingMode.Glx],
                ShouldRenderOnUIThread = false
            })
            #endif
            #if _WINDOWS
            .With(new Win32PlatformOptions
            {
                WglProfiles = [new GlVersion(GlProfileType.OpenGL, 4, 6)],
                RenderingMode = [Win32RenderingMode.Wgl],
                ShouldRenderOnUIThread = false
            })
            #endif
            #if DEBUG
            .LogToDelegate(Console.WriteLine, LogEventLevel.Debug)
            #else
            .LogToDelegate(Console.WriteLine, LogEventLevel.Information)
            #endif
            .WithInterFont();
}