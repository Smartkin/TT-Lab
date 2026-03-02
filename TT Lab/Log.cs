using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit;
using TT_Lab.ViewModels;

namespace TT_Lab;

public static class Log
{
    private static LogViewModel? logBox;
    private const int MaxLines = 200;

    public enum LogType
    {
        Info,
        Warning,
        Error,
        Debug,
        Trace
    }

    public static void SetViewModel(LogViewModel log)
    {
        logBox = log;
    }

    public static async void WriteLine(string text, LogType type = LogType.Info)
    {
        if (logBox == null) throw new ArgumentNullException("logBox was not set to write the logs in!");
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            logBox.Text += $"[{type}]" + DateTime.Now + ": " + text + Environment.NewLine;
            if (logBox.LinesAmount >= MaxLines)
            {
                var lines = logBox.Text.Split(Environment.NewLine);
                logBox.Text = string.Join(Environment.NewLine, lines.Skip(lines.Length - MaxLines));
            }
        });
    }

    public static void Clear()
    {
        if (logBox == null) throw new ArgumentNullException("logBox was not set to write the logs in!");
        Dispatcher.UIThread.Post(() => logBox.Clear());
    }
}