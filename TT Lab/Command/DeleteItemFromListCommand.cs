using System;
using System.Collections;
using System.Collections.Generic;

namespace TT_Lab.Command
{
    public class DeleteItemFromListCommand : ICommand
    {
        private class DelItemInfo
        {
            public object? Item;
            public Int32 Index;
        }
        public event EventHandler? CanExecuteChanged;

        private Int32 _index = -1;
        private Stack<DelItemInfo> _deletedItems = new Stack<DelItemInfo>();
        private IList _list;

        public DeleteItemFromListCommand(IList list)
        {
            _list = list;
        }

        public Int32 Index
        {
            set
            {
                _index = value;
                CanExecuteChanged?.Invoke(this, new EventArgs());
            }
            private get => _index;
        }

        public Boolean CanExecute(Object? parameter)
        {
            return _index != -1 && _index < _list.Count;
        }

        public void Execute(Object? parameter = null)
        {
            var item = _list[_index];
            _deletedItems.Push(new DelItemInfo
            {
                Item = item,
                Index = _index
            });
            _list.RemoveAt(_index);
        }

        public void Unexecute()
        {
            var info = _deletedItems.Pop();
            _list.Insert(info.Index, info.Item);
        }
    }
}
