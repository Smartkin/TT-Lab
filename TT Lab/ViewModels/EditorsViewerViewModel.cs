using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables.Fluent;
using System.Threading;
using System.Threading.Tasks;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using DynamicData;
using ReactiveUI;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

public abstract class EditorsViewerViewModel : ReactiveObject, IActivatableViewModel
{
    public IFactory Factory { get; }
    private readonly SourceCache<TabbedEditorViewModel, string> _documentTabs;

    public ReadOnlyObservableCollection<TabbedEditorViewModel> Tabs;

    protected EditorsViewerViewModel(IFactory factory)
    {
        Factory = factory;
        _documentTabs = new SourceCache<TabbedEditorViewModel, String>(x => x.EditableResource);
        this.WhenActivated(disposables =>
        {
            _documentTabs.Connect().Bind(out Tabs).Subscribe().DisposeWith(disposables);
        });
    }

    public void Clear()
    {
        _documentTabs.Clear();
    }

    public void OpenTab(TabbedEditorViewModel editor)
    {
        _documentTabs.AddOrUpdate(editor);
    }
    
    public virtual async Task CloseEditorTab(TabbedEditorViewModel editor)
    {
        await editor.CloseTab();
    }

    public virtual void SaveEditorTab(TabbedEditorViewModel editor)
    {
        editor.Document.Save();
    }

    public virtual void Save()
    {
        foreach (var item in Tabs)
        {
            item.Document.Save();
        }
    }

    public ViewModelActivator Activator { get; } = new();
}