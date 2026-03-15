using System;
using System.Collections.Generic;
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
    private string _caption = string.Empty;
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
    
    [ObservableAsProperty]
    private string _title;
    
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

        _titleHelper = this.WhenAnyValue(x => x.Caption)
            .ToProperty(this, x => x.Title).DisposeWith(FullDeactivationDisposables);
        
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

    protected virtual void OnActivated(CompositeDisposable disposables)
    {
        Property.Changed += PropertyOnChanged;
        ApplyEditorAttributes();
        RxSchedulers.MainThreadScheduler.Schedule(this, (_, state) =>
        {
            state.ApplyValidationRules(disposables);
            return Disposable.Empty;
        }).DisposeWith(FullDeactivationDisposables);
    }

    protected virtual void PropertyOnChanged()
    {
    }

    protected virtual void OnDeactivated(CompositeDisposable disposables)
    {
        Property.Changed -= PropertyOnChanged;
        RxSchedulers.MainThreadScheduler.Schedule(this, (_, state) =>
        {
            state.ClearValidationRules();
            return Disposable.Empty;
        }).DisposeWith(FullDeactivationDisposables);
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