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
    
    public void ConstructType(Type typeToConstruct)
    {
        if (typeToConstruct == typeof(NullConstructor))
        {
            foreach (var propertyChild in Property.Children)
            {
                Property.Graph!.Deindex(propertyChild);
            }
            Property.Children.Clear();
            Property.SetValue(null);
            if (IsExpanded)
            {
                Rebuild();
            }
            return;
        }

        var constructedValue = Property.Metadata!.TypeConstructors[typeToConstruct]();
        Property.SetValue(constructedValue);
        foreach (var propertyChild in Property.Children)
        {
            Property.Graph!.Deindex(propertyChild);
        }
        Property.Children.Clear();
        var newChildren = PropertyGraphBuilder.BuildNode(Property.Target, Property.Metadata, Property.Path, Property.Graph!.Tracker, typeToConstruct, Property.Index);
        foreach (var newChild in newChildren.Children)
        {
            Property.Graph.Index(newChild);
            Property.AddChild(newChild);
        }
        
        if (IsExpanded)
        {
            Rebuild();
        }
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