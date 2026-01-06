using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TT_Lab;

public static class Log
{
    private static TextBox? logBox;
    private const int MaxLines = 200;

    public enum LogType
    {
        Info,
        Warning,
        Error,
        Debug,
        Trace
    }

    public static void SetLogBox(TextBox log)
    {
        logBox = log;
    }

    public static async void WriteLine(string text, LogType type = LogType.Info)
    {
        if (logBox == null) throw new ArgumentNullException("logBox was not set to write the logs in!");
            
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            logBox.Text += $"[{type}]" + DateTime.Now + ": " + text + Environment.NewLine;
            if (logBox.GetLineCount() >= MaxLines)
            {
                var lines = logBox.Text.Split(Environment.NewLine);
                logBox.Text = string.Join(Environment.NewLine, lines.Skip(lines.Length - MaxLines));
                logBox.CaretIndex = logBox.Text.Length;
            }
        });
    }

    public static void Clear()
    {
        if (logBox == null) throw new ArgumentNullException("logBox was not set to write the logs in!");
        Dispatcher.UIThread.Post(() => logBox.Clear());
    }
}