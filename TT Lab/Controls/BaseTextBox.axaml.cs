using System;
using Avalonia;
using Avalonia.Controls;

namespace TT_Lab.Controls;

public partial class BaseTextBox : TextBox
{
    public event EventHandler UndoPerformed;
    public event EventHandler RedoPerformed;
        
    public BaseTextBox()
    {
        InitializeComponent();
        UndoPerformed += OnUndoPerformed;
        RedoPerformed += OnRedoPerformed;
        UndoLimit = 1;
    }

    private void OnRedoPerformed(object? sender, EventArgs e)
    {
        var handler = RedoPerformed;
        handler?.Invoke(sender, e);
    }

    private void OnUndoPerformed(object? sender, EventArgs e)
    {
        var handler = UndoPerformed;
        handler?.Invoke(sender, e);
    }
}