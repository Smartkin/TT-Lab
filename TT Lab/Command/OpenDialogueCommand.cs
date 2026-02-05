using System;
using Avalonia.Controls;
using Splat;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.Command;

public class OpenDialogueCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public class DialogueResult
    {
        public object? Result;
    }

    private readonly Func<Window> _getWindow;

    public OpenDialogueCommand(Func<Window> getWindow)
    {
        _getWindow = getWindow;
    }

    public Boolean CanExecute(Object? parameter)
    {
        return true;
    }

    public void Execute(Object? parameter = null)
    {
        _getWindow.Invoke().ShowDialog((Window)((ShellViewModel)Locator.Current.GetService<ILabManager>()!).GetView());
    }

    public void Unexecute()
    {
        throw new NotImplementedException();
    }
}