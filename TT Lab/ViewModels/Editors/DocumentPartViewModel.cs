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

namespace TT_Lab.ViewModels.Editors;

public abstract partial class DocumentPartViewModel : ReactiveValidationObject, IActivatableViewModel
{
    [Reactive]
    private string _editorName = string.Empty;
    [Reactive]
    private string _caption = string.Empty;
    [Reactive]
    private string? _hint;
    [Reactive]
    private Dock _orientation = Dock.Left;
    [Reactive]
    private Dictionary<string, object> _editorParameters = new();
    
    private Func<int> _idGenerator = GetIdGenerator();
    private bool _isInitialized;
    private readonly CompositeDisposable _fullDeactivationDisposables = new();
    
    protected DocumentViewModel Document;

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
        
        Document = document;
    }

    protected void SetDocument(DocumentViewModel document)
    {
        Document = document;
    }

    public virtual void Save(string propName)
    {
        Document.DocumentModel.GetType().GetProperty(propName)!.SetValue(Document.DocumentModel, GetData(), null);
    }

    public int GetNewEditorId()
    {
        return _idGenerator();
    }

    public void Close()
    {
        OnClosed(_fullDeactivationDisposables);
        _fullDeactivationDisposables.Dispose();
    }

    protected virtual void OnInitialized(CompositeDisposable disposables) { }

    protected virtual void OnActivated(CompositeDisposable disposables) { }

    protected virtual void OnDeactivated(CompositeDisposable disposable) { }

    protected virtual void OnClosed(CompositeDisposable disposables) { }

    [ObservableAsProperty]
    public Dock EditorOrientation => _orientation switch
    {
        Dock.Left => Dock.Right,
        Dock.Right => Dock.Left,
        Dock.Top => Dock.Bottom,
        _ => Dock.Top
    };

    public virtual bool UseDefaultCaption => true;
    
    public int Id { get; set; }
    public MemberInfo Metadata { get; set; }
    public abstract object? GetData();
    public ViewModelActivator Activator { get; }
    
}