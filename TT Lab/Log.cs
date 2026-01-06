using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TT_Lab;

public static class Log
{
    private static TextBox? logBox;

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

    public static async void WriteLine(string text)
    {
        if (logBox == null) throw new ArgumentNullException("logBox was not set to write the logs in!");
            
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            logBox.Text += DateTime.Now + ": " + text + Environment.NewLine;
            // if (logBox.GetLineCount() >= logBox.MaxLines)
            // {
            //     var lines = logBox.Text.Split(Environment.NewLine, StringSplitOptions.None);
            //     logBox.Text = string.Join(Environment.NewLine, lines.Skip(lines.Length - logBox.MaxLines));
            //     logBox.CaretIndex = logBox.Text.Length;
            // }
        });
    }

    public static void Clear()
    {
        if (logBox == null) throw new ArgumentNullException("logBox was not set to write the logs in!");
        Dispatcher.UIThread.Post(() => logBox.Clear());
    }
}