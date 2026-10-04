using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using ReactiveUI;
using Splat;

namespace TT_Lab.Util;

public class ViewLocator : IRecyclingDataTemplate, IEnableLogger
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

    // Views are registered for their view model's class, and several serve the classes deriving from it too. ReactiveUI's locator only looks
    // at the class itself and warned "Failed to resolve view for view model type 'System.Object'" about each of those before they got found
    private Control Resolve(object data)
    {
        for (var type = data.GetType(); type != null; type = type.BaseType)
        {
            if (Locator.Current.GetService(typeof(IViewFor<>).MakeGenericType(type)) is IViewFor view)
            {
                view.ViewModel = data;
                return (Control)view;
            }
        }

        this.Log().Warn($"No view for view model type '{data.GetType().FullName}'");
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