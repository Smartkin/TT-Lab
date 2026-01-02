using System;
using Avalonia.Controls;

namespace TT_Lab.Command
{
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
            _getWindow.Invoke().ShowDialog(null);
        }

        public void Unexecute()
        {
            throw new NotImplementedException();
        }
    }
}
