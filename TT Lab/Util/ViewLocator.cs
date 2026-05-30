using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using ReactiveUI;
using Splat;

namespace TT_Lab.Util;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        var view = ReactiveUI.ViewLocator.Current.ResolveView(data);
        
        if (view != null)
        {
            view.ViewModel = data;
            return (Control)view;
        }

        var baseType = data.GetType().BaseType;

        while (baseType != null)
        {
            var viewType = typeof(IViewFor<>).MakeGenericType(baseType);

            if (Locator.Current.GetService(viewType) is IViewFor baseView)
            {
                baseView.ViewModel = data;
                return (Control)baseView;
            }
            
            baseType = baseType.BaseType;
        }

        return new TextBlock
        {
            Text = "No View found for: " + data.GetType().Name
        };
    }

    public Boolean Match(object? data)
    {
        return data switch
        {
            null => false,
            IDockable => true,
            _ => data.GetType().Name.EndsWith("ViewModel")
        };
    }
}