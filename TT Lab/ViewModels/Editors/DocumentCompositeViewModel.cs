using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public abstract partial class DocumentCompositeViewModel : DocumentNodeViewModel
{
    [Reactive]
    private bool _isExpanded;

    [Reactive(SetModifier = AccessModifier.Protected)]
    private bool _isCollection;

    public abstract ReactiveCommand<Unit, Unit>? AddCommand { get; }
    
    public readonly ReadOnlyObservableCollection<DocumentNodeViewModel> Nodes;

    public const string EditorExplicitOrder = "DOCUMENT_MODEL_EXPLICIT_ORDER";

    private readonly SourceCache<DocumentNodeViewModel, string> _nodes;

    protected DocumentCompositeViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        IsCaptionVisible = false;
        _nodes = new SourceCache<DocumentNodeViewModel, String>(n => n.Property.Path);
        _nodes.DisposeWith(FullDeactivationDisposables);
        _nodes.Connect()
            .SortAndBind(out Nodes, new DocComparer()).Subscribe().DisposeWith(FullDeactivationDisposables);
        
        _nodes.Connect().Subscribe(x =>
        {
            var list = x.ToList();
            foreach (var item in list.Where(item => item.Reason == ChangeReason.Add))
            {
                item.Current.Depth = Depth + 1;
            }
        }).DisposeWith(FullDeactivationDisposables);
    }

    protected void AddNode(DocumentNodeViewModel node)
    {
        _nodes.AddOrUpdate(node);
    }

    protected void RemoveNode(DocumentNodeViewModel node)
    {
        _nodes.RemoveKey(node.Property.Path);
        _nodes.Remove(node);
    }
    
    protected virtual void ReindexNodes(int fromIdx)
    {
        var currentNodes = Nodes.ToArray();
        _nodes.Clear();

        foreach (var node in currentNodes)
        {
            AddNode(node);
        }
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        this.WhenAnyValue(x => x.IsExpanded)
            .Subscribe(x =>
            {
                if (x)
                {
                    OnExpanded(disposables);
                }
                else
                {
                    OnCollapsed(disposables);
                }
            }).DisposeWith(disposables);
    }

    protected void Rebuild()
    {
        _nodes.Clear();
        foreach (var viewModel in Property.Children
                     .Select(propertyChild => EditorDescRegistry.GetDesc(Document, propertyChild))
                     .Select(desc => desc.Construct()))
        {
            AddNode(viewModel);
        }
    }

    protected virtual void OnExpanded(CompositeDisposable disposables)
    {
        Rebuild();
    }

    protected virtual void OnCollapsed(CompositeDisposable disposables)
    {
        var nodesCopy = Nodes.ToArray();
        _nodes.Clear();
        
        foreach (var documentNodeViewModel in nodesCopy)
        {
            documentNodeViewModel.Close();
        }
    }

    private class DocComparer : IComparer<DocumentNodeViewModel>
    {
        public Int32 Compare(DocumentNodeViewModel? x, DocumentNodeViewModel? y)
        {
            if (x == null || y == null)
            {
                return 0;
            }

            if (x.Property.Index != null && y.Property.Index != null)
            {
                return x.Property.Index.Value.CompareTo(y.Property.Index.Value);
            }
            
            return string.Compare(x.Property.Path, y.Property.Path, StringComparison.Ordinal);
        }
    }
}