using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using TT_Lab.Attributes;

namespace TT_Lab.ViewModels.Editors;

public abstract partial class DocumentPartViewModel : DocumentBaseViewModel, IActivatableViewModel
{
    public event Action? Closed;
    
    [Reactive]
    private string _editorName = string.Empty;
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

    [ObservableAsProperty]
    private string _title;
    
    private readonly Func<int> _idGenerator = GetIdGenerator();
    private bool _isInitialized;
    private readonly CompositeDisposable _fullDeactivationDisposables = new();
    
    protected DocumentViewModel Document;
    public Dictionary<string, List<IFieldChange>>? FieldLinks;

    private static Func<int> GetIdGenerator()
    {
        var id = 0;
        return () => id++;
    }

    protected DocumentPartViewModel(DocumentViewModel document)
    {
        Activator = new ViewModelActivator();
        this.WhenActivated(disposables =>
        {
            if (!_isInitialized)
            {
                OnInitialized(_fullDeactivationDisposables);
                _isInitialized = true;
            }
            
            OnActivated(disposables);
            
            Disposable.Create(this, viewModel => viewModel.OnDeactivated(disposables)).DisposeWith(disposables);
        });

        _titleHelper = this.WhenAnyValue(x => x.Caption)
            .ToProperty(this, x => x.Title);
        
        Document = document;
    }

    protected void SetDocument(DocumentViewModel document)
    {
        Document = document;
    }

    public T? GetEditorParameter<T>(string parameter, T? defaultValue = default)
    {
        if (!_editorParameters.TryGetValue(parameter, out var editorParameter))
        {
            return defaultValue;
        }
        
        return (T?)editorParameter;
    }

    public virtual void Save()
    {
        if (IsPartOfCollection)
        {
            return;
        }
        
        Document.DocumentModel.GetType().GetProperty(SaveLocation)!.SetValue(Document.DocumentModel, GetFinalData(), null);
    }

    public int GetNewEditorId()
    {
        return _idGenerator();
    }

    public void Close()
    {
        Closed?.Invoke();
        OnClosed(_fullDeactivationDisposables);
        _fullDeactivationDisposables.Dispose();
    }

    public virtual bool CanClose()
    {
        return true;
    }

    protected virtual void OnInitialized(CompositeDisposable disposables) { }

    protected virtual void OnActivated(CompositeDisposable disposables) { }

    protected virtual void OnDeactivated(CompositeDisposable disposable) { }

    protected virtual void OnClosed(CompositeDisposable disposables) { }

    [ObservableAsProperty]
    public Avalonia.Controls.Dock EditorOrientation => _orientation switch
    {
        Avalonia.Controls.Dock.Left => Avalonia.Controls.Dock.Right,
        Avalonia.Controls.Dock.Right => Avalonia.Controls.Dock.Left,
        Avalonia.Controls.Dock.Top => Avalonia.Controls.Dock.Bottom,
        _ => Avalonia.Controls.Dock.Top
    };

    public virtual bool UseDefaultCaption => true;
    
    public int Id { get; set; }
    public bool IsPartOfCollection { get; set; }
    public string SaveLocation { get; set; }
    public MemberInfo Metadata { get; set; }
    public Type PropertyType { get; set; }
    public abstract object? GetFinalData();
    public ViewModelActivator Activator { get; }
    
}