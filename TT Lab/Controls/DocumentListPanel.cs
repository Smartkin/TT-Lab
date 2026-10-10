using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Controls;

/// <summary>
/// The rows of the inspector's lists, made only where the document's scroll viewer shows them (and half as much again around it), whatever
/// their heights. Avalonia's VirtualizingStackPanel takes the rows it hasn't made for the average height of the ones it has: rows of other
/// heights (captions that wrap, structs expanded) made a notch of the wheel at the bottom of Crash's object add 963 pixels to his behaviour
/// slots and jump the document by as much, and with an expanded struct near the end of a long list the height flipped as the row was made
/// and dropped until the layout cycled for seconds. This panel keeps the height every row was measured at by its item, takes the average
/// only for rows never made, keeps the rows of expanded structs and the focused one made while they're out of sight (made again they lose
/// what their editors kept and cost the most to make) and offers the rows it shows to the scroll viewer as anchors
/// (<see cref="IScrollAnchorProvider"/>): when rows above what's shown turn out taller or shorter than estimated, the scroll viewer moves by
/// as much and what's shown stays where it is
/// </summary>
public class DocumentListPanel : VirtualizingPanel
{
    // Rows made beyond what's shown, above and below, as a share of the shown height: made a step at a time while the UI thread is idle, so
    // expanding a list and scrolling only wait for the rows coming into sight
    private const double Buffer = 0.5;
    private const double BufferStep = 0.125;
    // What a row never measured is taken for until one is
    private const double FirstEstimate = 28;
    // How far down rows get made before the panel knows what's shown
    private const double FirstSpan = 800;

    private static readonly AttachedProperty<object?> RecycleKeyProperty =
        AvaloniaProperty.RegisterAttached<DocumentListPanel, Control, object?>("RecycleKey");
    private static readonly object ItemIsItsOwnContainer = new();

    // The height each item's row was last measured at, by the item itself: lists get reset when their editors are put in order again,
    // and every height went back to the estimate
    private readonly Dictionary<object, double> _heights = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Control> _rows = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Control, object> _itemOfRow = new(ReferenceEqualityComparer.Instance);
    // Where the rows made by the last measure go: their index and top
    private readonly Dictionary<object, (int Index, double Top)> _placed = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Stack<Control>> _pool = new();
    private readonly HashSet<Control> _anchorCandidates = new(ReferenceEqualityComparer.Instance);
    private IScrollAnchorProvider? _anchors;
    private Rect _viewport;
    private bool _knowsViewport;
    // The span the last measure made rows for, in the panel's own coordinates
    private double _madeFrom;
    private double _madeTo;
    // A row brought into view stays made until the panel knows what's shown again
    private object? _scrollTarget;
    private bool _isInLayout;
    private double? _estimate;
    // How much of the buffer new rows get made for: none once what's shown leaves what's made, a step more each time the UI thread is idle
    private double _bufferShare;
    private bool _isGrowthPosted;

    public DocumentListPanel()
    {
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    /// <summary>
    /// How many rows are made right now, out of sight included
    /// </summary>
    internal int MadeRows => _rows.Count;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _anchors = this.FindAncestorOfType<IScrollAnchorProvider>();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var row in _anchorCandidates)
        {
            _anchors?.UnregisterAnchorCandidate(row);
        }

        _anchorCandidates.Clear();
        _anchors = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items;
        if (ItemContainerGenerator is null)
        {
            return default;
        }

        _isInLayout = true;
        try
        {
            var present = new HashSet<object>(items.Count, ReferenceEqualityComparer.Instance);
            foreach (var item in items)
            {
                if (item != null)
                {
                    present.Add(item);
                }
            }

            ForgetGone(present);
            var estimate = Estimate();
            // New rows get made for what's shown and the buffer made so far, rows already made stay while they're within the whole buffer
            var (from, to) = SpanToMake(_bufferShare);
            var (keepFrom, keepTo) = SpanToMake(Buffer);
            var childSize = new Size(availableSize.Width, double.PositiveInfinity);
            var made = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var width = 0.0;
            var top = 0.0;
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var height = item != null && _heights.TryGetValue(item, out var known) ? known : estimate;
                var isMade = item != null && _rows.ContainsKey(item);
                if (item != null && ((top + height >= from && top <= to) || (isMade && top + height >= keepFrom && top <= keepTo) || IsKept(item)))
                {
                    var row = GetOrMakeRow(item, index);
                    row.Measure(childSize);
                    height = row.DesiredSize.Height;
                    SetHeight(item, height);
                    width = Math.Max(width, row.DesiredSize.Width);
                    _placed[item] = (index, top);
                    made.Add(item);
                }

                top += height;
            }

            foreach (var (item, row) in _rows.ToList())
            {
                if (!made.Contains(item))
                {
                    Recycle(item, row);
                }
            }

            _madeFrom = from;
            _madeTo = to;
            if (_bufferShare < Buffer && _knowsViewport)
            {
                GrowBufferWhenIdle();
            }

            return new Size(width, top);
        }
        finally
        {
            _isInLayout = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (item, row) in _rows)
        {
            if (!_placed.TryGetValue(item, out var place) || !_heights.TryGetValue(item, out var height))
            {
                continue;
            }

            row.Arrange(new Rect(0, place.Top, finalSize.Width, height));
            if (_anchors != null && _anchorCandidates.Add(row))
            {
                _anchors.RegisterAnchorCandidate(row);
            }
        }

        return finalSize;
    }

    // What the rows measured mostly are, for the ones never made: their median, a few expanded structs among collapsed ones would make the
    // average of them take every row never made for a lot taller
    private double Estimate()
    {
        if (_estimate is { } estimate)
        {
            return estimate;
        }

        if (_heights.Count == 0)
        {
            return FirstEstimate;
        }

        var heights = _heights.Values.ToArray();
        Array.Sort(heights);
        _estimate = heights[heights.Length / 2];
        return _estimate.Value;
    }

    private void SetHeight(object item, double height)
    {
        if (!_heights.TryGetValue(item, out var old) || old != height)
        {
            _heights[item] = height;
            _estimate = null;
        }
    }

    // What's shown and the share of its height above and below it, or the first rows before the panel knows what's shown. Nothing when the
    // list is out of sight
    private (double From, double To) SpanToMake(double share)
    {
        if (!_knowsViewport)
        {
            return (0, FirstSpan);
        }

        if (_viewport.Height <= 0)
        {
            return (double.PositiveInfinity, double.NegativeInfinity);
        }

        var extra = _viewport.Height * share;
        return (_viewport.Top - extra, _viewport.Bottom + extra);
    }

    private void GrowBufferWhenIdle()
    {
        if (_isGrowthPosted)
        {
            return;
        }

        _isGrowthPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isGrowthPosted = false;
            if (_bufferShare >= Buffer || this.GetVisualRoot() == null)
            {
                return;
            }

            _bufferShare = Math.Min(Buffer, _bufferShare + BufferStep);
            InvalidateMeasure();
        }, DispatcherPriority.Background);
    }

    // Rows that stay made out of sight: an expanded struct's (made again it loses what its editors kept, scroll positions of lists inside
    // it, and costs the most to make), the one with the keyboard focus and the one being brought into view
    private bool IsKept(object item)
    {
        if (ReferenceEquals(item, _scrollTarget) || item is DocumentCompositeViewModel { IsExpanded: true })
        {
            return true;
        }

        return _rows.TryGetValue(item, out var row) && row.IsKeyboardFocusWithin;
    }

    // Items gone from the list since the last measure: the rows go back to the pool, the heights are forgotten. Done here and not when the
    // list says it changed, a list put in order again takes everything out and puts it back
    private void ForgetGone(HashSet<object> present)
    {
        foreach (var (item, row) in _rows.ToList())
        {
            if (!present.Contains(item))
            {
                Recycle(item, row);
            }
        }

        foreach (var item in _heights.Keys.ToList())
        {
            if (!present.Contains(item))
            {
                _heights.Remove(item);
                _estimate = null;
            }
        }

        if (_scrollTarget != null && !present.Contains(_scrollTarget))
        {
            _scrollTarget = null;
        }
    }

    private Control GetOrMakeRow(object item, int index)
    {
        var generator = ItemContainerGenerator!;
        if (_rows.TryGetValue(item, out var row))
        {
            if (_placed.TryGetValue(item, out var place) && place.Index != index)
            {
                generator.ItemContainerIndexChanged(row, place.Index, index);
            }

            return row;
        }

        if (!generator.NeedsContainer(item, index, out var recycleKey))
        {
            row = (Control)item;
            if (!row.IsSet(RecycleKeyProperty))
            {
                generator.PrepareItemContainer(row, item, index);
                AddInternalChild(row);
                row.SetValue(RecycleKeyProperty, ItemIsItsOwnContainer);
                generator.ItemContainerPrepared(row, item, index);
            }
        }
        else if (recycleKey != null && _pool.TryGetValue(recycleKey, out var pool) && pool.Count > 0)
        {
            row = pool.Pop();
            generator.PrepareItemContainer(row, item, index);
            generator.ItemContainerPrepared(row, item, index);
        }
        else
        {
            row = generator.CreateContainer(item, index, recycleKey);
            row.SetValue(RecycleKeyProperty, recycleKey);
            generator.PrepareItemContainer(row, item, index);
            AddInternalChild(row);
            generator.ItemContainerPrepared(row, item, index);
        }

        row.SetCurrentValue(IsVisibleProperty, true);
        _rows[item] = row;
        _itemOfRow[row] = item;
        return row;
    }

    private void Recycle(object item, Control row)
    {
        _rows.Remove(item);
        _itemOfRow.Remove(row);
        _placed.Remove(item);
        if (_anchorCandidates.Remove(row))
        {
            _anchors?.UnregisterAnchorCandidate(row);
        }

        var recycleKey = row.GetValue(RecycleKeyProperty);
        if (recycleKey is null)
        {
            RemoveInternalChild(row);
            return;
        }

        row.SetCurrentValue(IsVisibleProperty, false);
        if (recycleKey == ItemIsItsOwnContainer)
        {
            return;
        }

        ItemContainerGenerator!.ClearItemContainer(row);
        if (!_pool.TryGetValue(recycleKey, out var pool))
        {
            _pool[recycleKey] = pool = new Stack<Control>();
        }

        pool.Push(row);
    }

    // Measured again once what's shown leaves the span the last measure made rows for
    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var knew = _knowsViewport;
        _viewport = e.EffectiveViewport;
        _knowsViewport = true;
        if (!knew || _viewport.Height > 0 && (_viewport.Top < _madeFrom || _viewport.Bottom > _madeTo))
        {
            // Only the rows coming into sight right away, the buffer around them grows again when the UI thread is idle
            _bufferShare = 0;
            InvalidateMeasure();
        }
        else if (_scrollTarget != null)
        {
            // What's shown is known again, the rows around the one brought into view are what's made now
            _scrollTarget = null;
            InvalidateMeasure();
        }
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
    }

    protected override Control? ScrollIntoView(int index)
    {
        var items = Items;
        if (_isInLayout || index < 0 || index >= items.Count || items[index] is not { } item || !IsEffectivelyVisible)
        {
            return null;
        }

        if (_rows.TryGetValue(item, out var made) && _placed.ContainsKey(item))
        {
            made.BringIntoView();
            return made;
        }

        // Made, measured and put where it goes, so the scroll viewer can scroll to it; the layout after it makes the rows around it
        var row = GetOrMakeRow(item, index);
        row.Measure(new Size(Bounds.Width > 0 ? Bounds.Width : double.PositiveInfinity, double.PositiveInfinity));
        SetHeight(item, row.DesiredSize.Height);
        var top = TopOf(items, index);
        _placed[item] = (index, top);
        _scrollTarget = item;
        row.Arrange(new Rect(0, top, Bounds.Width, row.DesiredSize.Height));
        if (top + row.DesiredSize.Height > Bounds.Height)
        {
            // Added since the last layout: the panel isn't tall enough for the scroll viewers to reach it yet
            InvalidateMeasure();
            UpdateLayout();
        }

        row.BringIntoView();
        return row;
    }

    private double TopOf(IReadOnlyList<object?> items, int index)
    {
        var estimate = Estimate();
        var top = 0.0;
        for (var i = 0; i < index; i++)
        {
            top += items[i] is { } item && _heights.TryGetValue(item, out var height) ? height : estimate;
        }

        return top;
    }

    protected override Control? ContainerFromIndex(int index)
    {
        var items = Items;
        if (index < 0 || index >= items.Count || items[index] is not { } item)
        {
            return null;
        }

        return _rows.GetValueOrDefault(item);
    }

    protected override int IndexFromContainer(Control container)
    {
        if (!_itemOfRow.TryGetValue(container, out var item))
        {
            return -1;
        }

        if (_placed.TryGetValue(item, out var place) && place.Index < Items.Count && ReferenceEquals(Items[place.Index], item))
        {
            return place.Index;
        }

        for (var index = 0; index < Items.Count; index++)
        {
            if (ReferenceEquals(Items[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers() => _rows.Values;

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        var count = Items.Count;
        var fromControl = from as Control;
        if (count == 0 || fromControl is null && direction is not NavigationDirection.First and not NavigationDirection.Last)
        {
            return null;
        }

        var fromIndex = fromControl != null ? IndexFromContainer(fromControl) : -1;
        var toIndex = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.Next or NavigationDirection.Down => fromIndex + 1,
            NavigationDirection.Previous or NavigationDirection.Up => fromIndex - 1,
            _ => fromIndex
        };

        if (toIndex == fromIndex)
        {
            return from;
        }

        if (wrap)
        {
            toIndex = toIndex < 0 ? count - 1 : toIndex >= count ? 0 : toIndex;
        }

        return ScrollIntoView(toIndex);
    }
}
