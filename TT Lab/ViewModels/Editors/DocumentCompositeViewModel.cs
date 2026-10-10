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

    // Node leading to what's being revealed that the view has to scroll to. The view takes it, views of this node created again after
    // scrolling it out of sight used to scroll back to it and fight the user
    [Reactive(SetModifier = AccessModifier.Private)]
    private NodeScrollRequest? _scrollRequest;

    private PropertyNode? _pendingReveal;
    private int _builtChildrenVersion;
    private bool _isBuilt;

    public abstract ReactiveCommand<Unit, Unit>? AddCommand { get; }

    public readonly ReadOnlyObservableCollection<DocumentNodeViewModel> Nodes;

    public const string EditorExplicitOrder = "DOCUMENT_MODEL_EXPLICIT_ORDER";
    // The property's own properties are laid out with the ones next to it instead of in an editor of its own, like an asset's data
    public const string EditorInline = "DOCUMENT_MODEL_INLINE";

    // Keyed by the editors themselves: paths move when elements go in or out before them, a list's element put back by undo replaced
    // the editor of the one that had moved to its path
    private readonly SourceCache<DocumentNodeViewModel, DocumentNodeViewModel> _nodes;

    protected DocumentCompositeViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        IsCaptionVisible = false;
        _nodes = new SourceCache<DocumentNodeViewModel, DocumentNodeViewModel>(n => n);
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
        node.ApplyPresentation();
    }

    private void OnPresentationChanged(PropertyNode node)
    {
        Nodes.FirstOrDefault(child => child.Property == node)?.ApplyPresentation();
    }

    protected void RemoveNode(DocumentNodeViewModel node)
    {
        _nodes.RemoveKey(node);
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

        // The children's presentations, which may have changed while it wasn't shown
        foreach (var node in Nodes)
        {
            node.ApplyPresentation();
        }

        var graph = Document.PropertyGraph;
        graph.PresentationChanged += OnPresentationChanged;
        Disposable.Create(() => graph.PresentationChanged -= OnPresentationChanged).DisposeWith(disposables);

        this.WhenAnyValue(x => x.IsExpanded)
            .Subscribe(x =>
            {
                // Views of nodes scrolled out of sight get recycled, coming back into view mustn't build the nodes again, unless the
                // value got another type meanwhile
                if (x && _isBuilt)
                {
                    if (_builtChildrenVersion != Property.ChildrenVersion)
                    {
                        Rebuild();
                    }

                    ContinueReveal();
                }
                else if (x)
                {
                    OnExpanded(disposables);
                    _isBuilt = true;
                }
                else if (_isBuilt)
                {
                    OnCollapsed(disposables);
                    _isBuilt = false;
                }
            }).DisposeWith(disposables);
    }

    // The value got another type (a subtype picked, or taken back with undo), its old nodes show values it doesn't have
    protected override void PropertyOnChanged()
    {
        base.PropertyOnChanged();
        if (_isBuilt && IsExpanded && _builtChildrenVersion != Property.ChildrenVersion)
        {
            Rebuild();
        }
    }

    protected void Rebuild()
    {
        _builtChildrenVersion = Property.ChildrenVersion;
        var viewModels = GetShownChildren(Property)
            .Select((propertyChild, order) =>
            {
                var editor = EditorDescRegistry.GetDesc(Document, propertyChild).Construct();
                editor.DeclarationOrder = order;
                // Added past AddNode, after the activation applied the children's presentations
                editor.ApplyPresentation();
                return editor;
            })
            .ToList();
        // A single change for all of them, one per node made the list update thousands of times
        _nodes.Edit(nodes =>
        {
            nodes.Clear();
            nodes.AddOrUpdate(viewModels);
        });
    }

    private IEnumerable<PropertyNode> GetShownChildren(PropertyNode property)
    {
        foreach (var child in property.Children)
        {
            if (Document.ShowsInSidePane(child))
            {
                continue;
            }

            if (!IsInline(child))
            {
                yield return child;
                continue;
            }

            foreach (var inlined in GetShownChildren(child))
            {
                yield return inlined;
            }
        }
    }

    private static bool IsInline(PropertyNode property) => property.Metadata?.EditorParams?.GetValueOrDefault(EditorInline) is true;

    protected virtual void OnExpanded(CompositeDisposable disposables)
    {
        Rebuild();
        ContinueReveal();
    }

    /// <summary>
    /// Expands everything down to the node, highlights it and expands it as well. Nodes only get created once expanded views get
    /// activated, so it carries on level by level as that happens
    /// </summary>
    public void Reveal(PropertyNode target)
    {
        if (target == Property)
        {
            Document.Highlight(this);
            IsExpanded = true;
            return;
        }

        _pendingReveal = target;
        if (IsExpanded && _isBuilt)
        {
            ContinueReveal();
        }
        else
        {
            IsExpanded = true;
        }
    }

    public NodeScrollRequest? TakeScrollRequest()
    {
        var request = ScrollRequest;
        ScrollRequest = null;
        return request;
    }

    private void ContinueReveal()
    {
        var target = _pendingReveal;
        if (target == null)
        {
            return;
        }

        _pendingReveal = null;
        var child = Nodes.FirstOrDefault(node => IsSelfOrAncestor(node.Property, target));
        if (child == null)
        {
            return;
        }

        // A new request every time, even for the node asked for last time, the user may have scrolled away from it since
        if (child is DocumentCompositeViewModel composite)
        {
            ScrollRequest = new NodeScrollRequest(child, child.Property == target);
            composite.Reveal(target);
            return;
        }

        // A field is as far as it goes, parts of it like a vector's component have no node of their own
        ScrollRequest = new NodeScrollRequest(child, true);
        Document.Highlight(child);
    }

    private static bool IsSelfOrAncestor(PropertyNode node, PropertyNode target)
    {
        for (var current = target; current != null; current = current.Parent)
        {
            if (current == node)
            {
                return true;
            }
        }

        return false;
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

            var orderComparison = GetExplicitOrder(x).CompareTo(GetExplicitOrder(y));
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            // As declared, they went by their paths: alphabetically
            var declarationComparison = x.DeclarationOrder.CompareTo(y.DeclarationOrder);
            return declarationComparison != 0 ? declarationComparison : string.Compare(x.Property.Path, y.Property.Path, StringComparison.Ordinal);
        }

        // Inlined properties go where the property they're inlined from would have gone
        private static (Int32 Place, Int32 Order) GetExplicitOrder(DocumentNodeViewModel node)
        {
            var order = GetExplicitOrder(node.Property);
            return node.Property.Parent is { } parent && IsInline(parent) ? (GetExplicitOrder(parent), order) : (order, 0);
        }

        private static Int32 GetExplicitOrder(PropertyNode property)
        {
            return property.Metadata?.EditorParams?.GetValueOrDefault(EditorExplicitOrder) is Int32 order ? order : 0;
        }
    }
}

/// <summary>
/// Node a composite's view has to bring into view, the node being revealed has to be shown entirely while the ones on the way to it only
/// need to be in sight to get their views created
/// </summary>
public record NodeScrollRequest(DocumentNodeViewModel Node, bool IsTarget);
