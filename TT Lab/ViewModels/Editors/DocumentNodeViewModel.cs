using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public abstract partial class DocumentNodeViewModel : DocumentBaseViewModel, IActivatableViewModel, ILifecycleNode
{
    public event Action? Closed;
    
    [Reactive]
    private string _editorName = string.Empty;
    [Reactive]
    private bool _isEditable = true;
    [Reactive]
    private string? _hint;
    [Reactive]
    private Avalonia.Controls.Dock _orientation = Avalonia.Controls.Dock.Left;
    [Reactive]
    private Dictionary<string, object> _editorParameters = new();
    [Reactive]
    private int _dataVersion;
    [Reactive]
    private bool _isCaptionVisible = true;
    // Marks what got selected somewhere else, like in the viewport
    [Reactive]
    private bool _isHighlighted;
    
    private string _caption = string.Empty;

    public string Caption
    {
        get => _caption;
        set
        {
            if (_caption == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _caption, value);
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    public string Title => Caption;
    
    public PropertyNode Property { get; }
    
    protected readonly CompositeDisposable FullDeactivationDisposables = new();
    protected DocumentViewModel Document { get; private set; }
    
    private readonly List<ILifecycleNode> _dependencies = [];

    protected DocumentNodeViewModel(DocumentViewModel document, PropertyNode property, params DocumentNodeViewModel[] dependencies)
    {
        document.Lifecycle.Register(this);
        Property = property;
        
        Activator = new ViewModelActivator();
        this.WhenActivated(disposables =>
        {
            OnActivated(disposables);
            
            Disposable.Create(this, viewModel => viewModel.OnDeactivated(disposables)).DisposeWith(disposables);
        });

        foreach (var dependency in dependencies)
        {
            DependsOn(dependency);
        }

        Document = document;
    }
    
    public IReadOnlyList<ILifecycleNode> Dependencies => _dependencies;
    public void Initialize()
    {
        OnInitialized(FullDeactivationDisposables);
    }

    public T? GetEditorParameter<T>(string parameter, T? defaultValue = default)
    {
        if (!_editorParameters.TryGetValue(parameter, out var editorParameter))
        {
            return defaultValue;
        }
        
        return (T?)editorParameter;
    }

    public void Close()
    {
        Closed?.Invoke();
        OnClosed(FullDeactivationDisposables);
        FullDeactivationDisposables.Dispose();
    }

    public virtual bool CanClose()
    {
        return true;
    }

    protected virtual void OnInitialized(CompositeDisposable disposables) { }

    private bool _isOverridden;
    private (SerializableAsset View, string Path)? _overrideTarget;
    private bool _hasOverrideTarget;

    /// <summary>
    /// Whether the chunk has a value of its own here, for editors of an asset it shares with other chunks
    /// </summary>
    public bool IsOverridden
    {
        get => _isOverridden;
        private set => this.RaiseAndSetIfChanged(ref _isOverridden, value);
    }

    /// <summary>
    /// Editors of a chunk's view of an asset it shares, what they edit becomes the chunk's own value
    /// </summary>
    public bool CanOverride => GetOverrideTarget() != null;

    private (SerializableAsset View, string Path)? GetOverrideTarget()
    {
        if (_hasOverrideTarget)
        {
            return _overrideTarget;
        }

        _hasOverrideTarget = true;
        if (Document.PropertyGraph.Overrides == null)
        {
            return null;
        }

        // The closest link's data node, its asset is what the editor edits
        for (var node = Property.Parent; node != null; node = node.Parent)
        {
            if (!node.Path.EndsWith("[data]"))
            {
                continue;
            }

            var path = Property.Path[(node.Path.Length + 1)..];
            if (node.Target is SerializableAsset { OverriddenAsset: not null } view && !path.Contains("[custom_editor]"))
            {
                _overrideTarget = (view, path);
            }

            break;
        }

        return _overrideTarget;
    }

    // A link to an asset the chunk shares with other chunks, its view is the link's data
    private SerializableAsset? GetLinkedView()
    {
        return Property.Children.FirstOrDefault(child => child.Path.EndsWith("[data]"))?.Target is SerializableAsset { OverriddenAsset: not null } view ? view : null;
    }

    // Links show that the chunk has values of its own in what they link to, which could go unnoticed otherwise
    protected void UpdateIsOverridden()
    {
        if (Document.PropertyGraph.Overrides is not { } overrides)
        {
            return;
        }

        var hasOwnValue = GetOverrideTarget() is { } target && ChunkOverrideSession.HasValueAt(overrides.GetOwnValues(target.View).Keys, target.Path);
        IsOverridden = hasOwnValue || (GetLinkedView() is { } view && overrides.HasOwnValues(view));
    }

    private void OverridesOnViewChanged(SerializableAsset view)
    {
        if (GetOverrideTarget()?.View == view || GetLinkedView() == view)
        {
            UpdateIsOverridden();
        }
    }

    /// <summary>
    /// Puts the shared asset's value back in place of the chunk's own
    /// </summary>
    public void RevertOverride()
    {
        if (GetOverrideTarget() is not { } target)
        {
            return;
        }

        Property.SetValue(Document.PropertyGraph.Overrides!.GetAssetValue(target.View, target.Path, Property.PropertyType));
        PropertyOnChanged();
    }

    /// <summary>
    /// Makes the chunk's value the shared asset's, for every chunk without a value of its own there
    /// </summary>
    public void ApplyOverrideToAsset()
    {
        if (GetOverrideTarget() is not { } target)
        {
            return;
        }

        Document.PropertyGraph.Overrides!.ApplyToAsset(target.View, target.Path);
    }

    protected virtual void OnActivated(CompositeDisposable disposables)
    {
        if (Document.PropertyGraph.Overrides is { } overrides && (GetOverrideTarget() != null || Property.PropertyType == typeof(LabURI)))
        {
            overrides.ViewChanged += OverridesOnViewChanged;
            Disposable.Create(() => overrides.ViewChanged -= OverridesOnViewChanged).DisposeWith(disposables);
            UpdateIsOverridden();
        }

        Property.Changed += PropertyOnChanged;
        Property.ReadOnlyChanged += PropertyOnReadOnlyChanged;
        ApplyEditorAttributes();
        RxSchedulers.MainThreadScheduler.Schedule(this, (_, state) =>
        {
            state.ApplyValidationRules(disposables);
            return Disposable.Empty;
        }).DisposeWith(disposables);
    }

    private void PropertyOnReadOnlyChanged()
    {
        IsReadOnly = Property.IsReadOnly;
    }

    protected virtual void PropertyOnChanged()
    {
    }

    protected virtual void OnDeactivated(CompositeDisposable disposables)
    {
        Property.ReadOnlyChanged -= PropertyOnReadOnlyChanged;
        Property.Changed -= PropertyOnChanged;
        RxSchedulers.MainThreadScheduler.Schedule(this, (_, state) =>
        {
            if (state.HasValidationContext)
            {
                state.ClearValidationRules();
            }

            return Disposable.Empty;
        }).DisposeWith(disposables);
    }

    protected virtual void OnClosed(CompositeDisposable disposables) { }

    protected virtual void ApplyValidationRules(CompositeDisposable disposables) { }
    
    protected virtual void ApplyEditorAttributes() {}

    [ObservableAsProperty]
    public Avalonia.Controls.Dock EditorOrientation => _orientation switch
    {
        Avalonia.Controls.Dock.Left => Avalonia.Controls.Dock.Right,
        Avalonia.Controls.Dock.Right => Avalonia.Controls.Dock.Left,
        Avalonia.Controls.Dock.Top => Avalonia.Controls.Dock.Bottom,
        _ => Avalonia.Controls.Dock.Top
    };

    public ViewModelActivator Activator { get; }
    
    private void DependsOn(DocumentNodeViewModel node)
    {
        _dependencies.Add(node);
    }
}