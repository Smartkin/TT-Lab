using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Caliburn.Micro;

namespace TT_Lab.Command;

public class CollectionFilterCommand : ICommand
{
    private readonly Action<object> _filterAction;

    public CollectionFilterCommand(Action<object> filterAction)
    {
        _filterAction = filterAction;
    }
    
    public Boolean CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter = null)
    {
        Debug.Assert(parameter != null, "Parameter must be present and can not be null");
        _filterAction(parameter);
    }

    public void Unexecute()
    {
        throw new NotImplementedException();
    }

    public event EventHandler? CanExecuteChanged;
}