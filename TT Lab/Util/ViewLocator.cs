using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TT_Lab.Util;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;
        
        var view = ReactiveUI.ViewLocator.Current.ResolveView(data);
        
        if (view != null)
        {
            view.ViewModel = data;
            return (Control)view;
        }

        var baseType = data.GetType().BaseType;

        while (baseType != null)
        {
            var baseView = ReactiveUI.ViewLocator.Current.ResolveView(data);

            if (baseView != null)
            {
                baseView.ViewModel = data;
                return (Control)baseView;
            }
                
            baseType = baseType.BaseType;
        }

        return new TextBlock
        {
            Text = "Not found View for: " + data.GetType().Name
        };
    }

    public Boolean Match(object? data)
    {
        return data?.GetType().Name.EndsWith("ViewModel") ?? false;
    }
}