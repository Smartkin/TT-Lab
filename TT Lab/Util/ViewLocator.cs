using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using ReactiveUI;
using Splat;

namespace TT_Lab.Util;

public class ViewLocator : IRecyclingDataTemplate
{
    // The view type each view model type got, so a view showing another view model of the type can be told apart without resolving it
    private readonly Dictionary<Type, Type> _viewTypes = new();

    public Control? Build(object? data) => Build(data, null);

    public Control? Build(object? data, Control? existing)
    {
        if (data is null)
        {
            return null;
        }

        if (existing is IRecyclableView { CanBeRecycled: true } recyclable && _viewTypes.TryGetValue(data.GetType(), out var viewType) && existing.GetType() == viewType)
        {
            recyclable.Recycle(data);
            return existing;
        }

        var control = Resolve(data);
        if (control is IViewFor)
        {
            _viewTypes[data.GetType()] = control.GetType();
        }

        return control;
    }

    private static Control Resolve(object data)
    {
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