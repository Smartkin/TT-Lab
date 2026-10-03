using System;
using System.Linq;
using System.Reflection;

namespace TT_Lab.Util;

/// <summary>
/// Avalonia's connection to the session's D-Bus (input methods, the platform's settings, the windows' menus, dialogs) let go of once
/// the lifetime is over. Left to the process's end, the connection went down on its own thread, its observers handed that to the UI
/// thread's dispatcher (a synchronous Send) which had stopped, and every quit logged the TaskCanceledException as a crash. Disposing
/// it tells the observers nothing. Not sooner: the lifetime's Exit comes while a window is being torn down, whose menu then found the
/// connection disposed. Avalonia keeps it in an internal class, reached by reflection: recheck it after updating Avalonia
/// </summary>
public static class DBusShutdown
{
    // Where Avalonia keeps the connection it made, asking its property would connect
    internal static FieldInfo[] ConnectionFields()
    {
        var helper = Type.GetType("Avalonia.FreeDesktop.DBusHelper, Avalonia.FreeDesktop");
        return helper?.GetFields(BindingFlags.NonPublic | BindingFlags.Static).Where(field => typeof(IDisposable).IsAssignableFrom(field.FieldType)).ToArray() ?? [];
    }

    public static void Disconnect()
    {
        try
        {
            foreach (var connection in ConnectionFields().Select(field => field.GetValue(null)).OfType<IDisposable>())
            {
                connection.Dispose();
            }
        }
        catch (Exception exception)
        {
            Log.WriteLine($"Couldn't let go of the D-Bus connection: {exception.Message}", Log.LogType.Debug);
        }
    }
}
