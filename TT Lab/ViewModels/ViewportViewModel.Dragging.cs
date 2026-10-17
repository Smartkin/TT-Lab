using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using TT_Lab.Assets;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects.Gizmo;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels;

// A gizmo drag is one step of the document's history, however long it takes and however it ends. What would make the dragged objects
// again waits for the drag to end: made again under it (a model Blender wrote, which an instance being dragged draws), the object got
// deselected, which stopped the drag and left its step open for good, every later change of the document going into it unseen
public partial class ViewportViewModel
{
    private readonly SelectionDrag _drag = new();

    private void BeginDragStep()
    {
        var name = SelectedObject == _trianglePivot && _selectedTriangles.Length > 0 ? (_selectedTriangles.Length > 1 ? $"{_selectedTriangles.Length} collision triangles" : "a collision triangle")
            : SelectedObject?.DuplicatedElement?.GetValue() is AssetData.Instance.Scenery.SceneryPlacement ? (SelectionCount > 1 ? $"{SelectionCount} meshes" : "a mesh")
            : (SelectedObject?.Property.Find("[data]")?.GetValue() as IAsset)?.Alias ?? SelectedObject?.DocumentName ?? "the selection";
        var verb = ActiveTool switch
        {
            TransformMode.ROTATE => "Rotated",
            TransformMode.SCALE => "Scaled",
            _ => "Moved",
        };
        _drag.Begin(_document?.History, $"{verb} {name}", SelectedObjects.Select(viewportObject => viewportObject.Property));
    }

    private void EndDragStep() => _drag.End();

    // The objects that waited are made again once what ended the drag is done with the selection
    private void OnDragEnded(IReadOnlyList<PropertyNode> waiting)
    {
        if (waiting.Count == 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_closeDisposables.IsDisposed)
            {
                return;
            }

            foreach (var property in waiting)
            {
                RebuildViewportObjects(property);
            }
        });
    }

    /// <summary>
    /// A gizmo drag of the selection: one step of the document's history that ends whenever the gizmo's drag does (let go of, given up on,
    /// its selection deselected), and the resources whose objects wait for it to be made again
    /// </summary>
    internal sealed class SelectionDrag
    {
        private IDisposable? _step;
        private PropertyNode[] _dragged = [];
        private readonly List<PropertyNode> _waiting = [];

        /// <summary>
        /// Raised once the drag is over and its step ended, with the resources whose objects waited for it
        /// </summary>
        public event Action<IReadOnlyList<PropertyNode>>? Ended;

        public bool IsDragging { get; private set; }

        /// <summary>
        /// Ends the drag whenever the gizmo's ends, whatever ended it
        /// </summary>
        public void Follow(TransformGizmo gizmo)
        {
            gizmo.DragEnded += End;
        }

        /// <summary>
        /// The drag of the resources' objects starts, a step of the history until it ends. One left going ends first
        /// </summary>
        public void Begin(UndoHistory? history, string description, IEnumerable<PropertyNode> dragged)
        {
            End();
            _step = history?.BeginGroup(description);
            _dragged = dragged.Distinct().ToArray();
            IsDragging = true;
        }

        /// <summary>
        /// Whether the resource's objects are being dragged, they're made again once the drag is over then
        /// </summary>
        public bool Defer(PropertyNode resource)
        {
            if (!IsDragging || Array.IndexOf(_dragged, resource) < 0)
            {
                return false;
            }

            if (!_waiting.Contains(resource))
            {
                _waiting.Add(resource);
            }

            return true;
        }

        public void End()
        {
            if (!IsDragging && _step == null)
            {
                return;
            }

            var step = _step;
            _step = null;
            IsDragging = false;
            _dragged = [];
            step?.Dispose();
            var waiting = _waiting.ToList();
            _waiting.Clear();
            Ended?.Invoke(waiting);
        }
    }
}
