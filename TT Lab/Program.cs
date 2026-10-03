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
    public static void Main(string[] args)
    {
        Log.StartSession();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        // Once the windows are gone (their menus still used it), before the process ends
        TT_Lab.Util.DBusShutdown.Disconnect();
    }

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
                ShouldRenderOnUIThread = false,
                // Tooltips and menus drawn in the window itself: every one of them was a window of its own, and hovering across the
                // inspector's rows opened and closed one after another, which stalled the whole UI
                OverlayPopups = true
            })
            #endif
            #if _WINDOWS
            .With(new Win32PlatformOptions
            {
                WglProfiles = [new GlVersion(GlProfileType.OpenGL, 4, 6)],
                RenderingMode = [Win32RenderingMode.Wgl],
                ShouldRenderOnUIThread = false,
                OverlayPopups = true
            })
            #endif
            #if DEBUG
            .LogToDelegate(Console.WriteLine, LogEventLevel.Debug)
            #else
            .LogToDelegate(Console.WriteLine, LogEventLevel.Information)
            #endif
            .WithInterFont();
}