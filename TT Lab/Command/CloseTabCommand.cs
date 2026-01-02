using System;
using Avalonia.Controls;

namespace TT_Lab.Command
{
    public class CloseTabCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private TabControl _tabsContainer;
        private object? _tabToClose;

        private CloseTabCommand(TabControl tabs)
        {
            _tabsContainer = tabs;
        }

        public CloseTabCommand(TabControl tabs, object tabToClose) : this(tabs)
        {
            _tabToClose = tabToClose;
        }

        public Boolean CanExecute(Object? parameter)
        {
            return true;
        }

        public void Execute(Object? parameter = null)
        {
            if (_tabToClose != null)
            {
                _tabsContainer.Items.Remove(_tabToClose);
                _tabToClose = null;
                return;
            }
            if (_tabsContainer.SelectedIndex != -1)
            {
                _tabsContainer.Items.RemoveAt(_tabsContainer.SelectedIndex);
            }
        }

        public void Unexecute()
        {
            throw new NotImplementedException();
        }
    }
}
