using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using TT_Lab.ViewModels;

namespace TT_Lab;

public static class Log
{
    private static LogViewModel? logBox;
    private static StreamWriter? sessionWriter;
    private static readonly object SessionLock = new();
    private const int MaxLines = 500;
    private const int MaxSessionLogs = 5;

    public enum LogType
    {
        Info,
        Warning,
        Error,
        Debug,
        Trace
    }

    public static string LogsDirectory { get; } = GetLogsDirectory();

    public static string? SessionLogPath { get; private set; }

    public static void SetViewModel(LogViewModel log)
    {
        logBox = log;
    }

    public static void StartSession()
    {
        lock (SessionLock)
        {
            if (sessionWriter != null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(LogsDirectory);
                DeleteOldSessionLogs();

                SessionLogPath = Path.Combine(LogsDirectory, $"session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Environment.ProcessId}.log");
                // Flushing every line hands it to the OS right away so the log survives the application crashing
                sessionWriter = new StreamWriter(new FileStream(SessionLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create the session log in {LogsDirectory}: {ex.Message}");
            }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteToSessionLog(FormatLine("Crash", $"{args.ExceptionObject}"));
        TaskScheduler.UnobservedTaskException += (_, args) => WriteToSessionLog(FormatLine(LogType.Error.ToString(), $"Unobserved task exception: {args.Exception}"));
    }

    public static async void WriteLine(string text, LogType type = LogType.Info)
    {
        var line = FormatLine(type.ToString(), text);
        WriteToSessionLog(line);

        if (type is LogType.Debug or LogType.Trace || logBox == null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Edited in place, replacing the whole text copied the log for every line and builds log thousands of them
            var document = logBox.Text;
            document.Insert(document.TextLength, line + Environment.NewLine);
            if (document.LineCount > MaxLines)
            {
                document.Remove(0, document.GetLineByNumber(document.LineCount - MaxLines + 1).Offset);
                logBox.CaretOffset = document.TextLength;
            }
        });
    }

    public static void Clear()
    {
        if (logBox == null) throw new ArgumentNullException("logBox was not set to write the logs in!");
        Dispatcher.UIThread.Post(() => logBox.Clear());
    }

    private static string FormatLine(string type, string text)
    {
        return $"[{type}]" + DateTime.Now + ": " + text;
    }

    private static void WriteToSessionLog(string line)
    {
        lock (SessionLock)
        {
            try
            {
                sessionWriter?.WriteLine(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to the session log: {ex.Message}");
            }
        }
    }

    private static void DeleteOldSessionLogs()
    {
        // Timestamped names sort chronologically, keep room for the session being started
        var oldSessions = new DirectoryInfo(LogsDirectory).GetFiles("session_*.log")
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .Skip(MaxSessionLogs - 1);
        foreach (var session in oldSessions)
        {
            try
            {
                session.Delete();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Most likely still in use by another running instance
            }
        }
    }

    private static string GetLogsDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TT Lab", "Logs");
        }

        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (string.IsNullOrEmpty(stateHome))
        {
            stateHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        }

        return Path.Combine(stateHome, "tt-lab", "logs");
    }
}
