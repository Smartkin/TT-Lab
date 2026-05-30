using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Interfaces;
using Action = System.Action;

namespace TT_Lab.Util;

public class DirtyTracker
{
    private bool _isDirty;
    private readonly Action _onDirtyChanged;
    private readonly Dictionary<int, List<string>> _markerToPropMap = new();
    private readonly Dictionary<int, IDirtyMarker> _children = [];

    public bool IsDirty
    {
        get => _isDirty || _children.Values.Any(child => child.IsDirty);
        private set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                _onDirtyChanged.Invoke();
            }
        }
    }

    public DirtyTracker(IDirtyMarker viewModel)
    {
        _onDirtyChanged = () =>
        {
            viewModel.NotifyOfPropertyChange(nameof(IsDirty));
        };

        _markerToPropMap.Add(viewModel.GetStorageHash(), new List<string>());
        RetrieveDirtyMarkedAttributes(viewModel);
    }

    public DirtyTracker(IDirtyMarker viewModel, Action onDirtyChanged) : this(viewModel)
    {
        _onDirtyChanged = onDirtyChanged;
    }

    public void MarkDirtyByProperty(Object? sender, PropertyChangedEventArgs e)
    {
        ChildPropertyChanged(sender, e);
    }

    public void MarkDirty()
    {
        IsDirty = true;
        _onDirtyChanged.Invoke();
    }

    public void ResetDirty()
    {
        _isDirty = false;
        foreach (var child in _children)
        {
            child.Value.ResetDirty();
        }
        _onDirtyChanged.Invoke();
    }

    public void AddChild(IDirtyMarker? child)
    {
        if (child == null || _children.ContainsKey(child.GetStorageHash()))
        {
            return;
        }

        _children.Add(child.GetStorageHash(), child);
        _markerToPropMap.Add(child.GetStorageHash(), []);
        RetrieveDirtyMarkedAttributes(child);
        child.PropertyChanged += ChildPropertyChanged;
    }

    public void AddBindableCollection<T>(BindableCollection<T> collection) where T : IDirtyMarker
    {
        collection.CollectionChanged += (s, e) =>
        {
            MarkDirty();
            
            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems.Cast<IDirtyMarker>())
                {
                    AddChild(newItem);
                }
            }

            if (e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems.Cast<IDirtyMarker>())
                {
                    RemoveChild(oldItem);
                }
            }
        };
    }

    public void RemoveChild(IDirtyMarker? child)
    {
        if (child == null || !_children.ContainsKey(child.GetStorageHash()))
        {
            return;
        }
        
        _markerToPropMap.Remove(child.GetStorageHash());
        _children.Remove(child.GetStorageHash());
        child.PropertyChanged -= ChildPropertyChanged;
    }
    
    private void ChildPropertyChanged(Object? sender, PropertyChangedEventArgs e)
    {
        var marker = (IDirtyMarker)sender!;
        Debug.Assert(_markerToPropMap.ContainsKey(marker.GetStorageHash()), $"Given marker {marker} was not found.");
        var propMap = _markerToPropMap[marker.GetStorageHash()];
        if (e.PropertyName == null || !propMap.Contains(e.PropertyName))
        {
            return;
        }

        if (e.PropertyName == nameof(IsDirty) && !marker.IsDirty)
        {
            return;
        }
        
        MarkDirty();
    }

    private void RetrieveDirtyMarkedAttributes(IDirtyMarker viewModel)
    {
        var properties = viewModel.GetType().GetProperties();
        var propList = _markerToPropMap[viewModel.GetStorageHash()];
        propList.Add(nameof(IsDirty));
        foreach (var property in properties)
        {
            if (Attribute.GetCustomAttribute(property, typeof(MarkDirtyAttribute)) is not MarkDirtyAttribute markDirtyAttribute)
            {
                continue;
            }
            
            propList.Add(markDirtyAttribute.MarkTarget);
        }
    }
}