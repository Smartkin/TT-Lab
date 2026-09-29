using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentModelViewModel : DocumentCompositeViewModel
{
    [Reactive]
    private bool _isConstructible;
    
    [Reactive]
    private string _typeConstructed;
    
    public ObservableCollection<DocumentModelConstructorViewModel> Constructors { get; }

    public DocumentModelViewModel(DocumentViewModel document, PropertyNode property, params DocumentNodeViewModel[] dependencies) : base(document, property, dependencies)
    {
        Constructors =
        [
            new DocumentModelConstructorViewModel(this, typeof(NullConstructor), "null")
        ];
        if (property.Metadata != null)
        {
            foreach (var metadataTypeConstructor in property.Metadata.TypeConstructors.Keys)
            {
                Constructors.Add(new DocumentModelConstructorViewModel(this, metadataTypeConstructor, metadataTypeConstructor.Name));
            }
        }
        _typeConstructed = Property.GetValue()?.GetType().Name ?? "null";
    }

    protected override void PropertyOnChanged()
    {
        base.PropertyOnChanged();

        TypeConstructed = Property.GetValue()?.GetType().Name ?? "null";
    }

    public override ReactiveCommand<Unit, Unit>? AddCommand => null;
    
    // The node makes its children again for the new value's type, and taking the change back with undo does the same
    public void ConstructType(Type typeToConstruct)
    {
        Property.SetValue(typeToConstruct == typeof(NullConstructor) ? null : Property.Metadata!.TypeConstructors[typeToConstruct]());
    }

    private class NullConstructor;
}

public partial class DocumentModelConstructorViewModel(DocumentModelViewModel requester, Type typeToConstruct, string caption) : ReactiveObject
{
    [Reactive]
    private string _caption = caption;
    
    [ReactiveCommand]
    private void Construct()
    {
        requester.ConstructType(typeToConstruct);
    }
}