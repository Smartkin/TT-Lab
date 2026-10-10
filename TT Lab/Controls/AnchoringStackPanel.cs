using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TT_Lab.Controls;

/// <summary>
/// The rows of a struct's or an asset's properties, all made like a stack panel's (custom editors keep what they show, made again they
/// lost it), offered to the document's scroll viewer as anchors (<see cref="IScrollAnchorProvider"/>): when a list above what's shown gets
/// taller or shorter, its rows measured for the first time, the scroll viewer moves by as much and the rows shown stay where they are. The
/// scroll viewer only anchors on what's offered to it, with nothing but a struct's rows shown a list above them jumped them by the
/// difference
/// </summary>
public class AnchoringStackPanel : StackPanel
{
    private readonly HashSet<Control> _offered = new(ReferenceEqualityComparer.Instance);
    private IScrollAnchorProvider? _anchors;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _anchors = this.FindAncestorOfType<IScrollAnchorProvider>();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var row in _offered)
        {
            _anchors?.UnregisterAnchorCandidate(row);
        }

        _offered.Clear();
        _anchors = null;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_anchors == null)
        {
            return size;
        }

        _offered.RemoveWhere(row =>
        {
            if (row.GetVisualParent() == this)
            {
                return false;
            }

            _anchors.UnregisterAnchorCandidate(row);
            return true;
        });
        foreach (var row in Children)
        {
            if (_offered.Add(row))
            {
                _anchors.RegisterAnchorCandidate(row);
            }
        }

        return size;
    }
}
