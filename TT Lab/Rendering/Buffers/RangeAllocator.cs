using System.Collections.Generic;

namespace TT_Lab.Rendering.Buffers;

/// <summary>
/// Ranges of elements handed out of a growing array: freed ranges are joined with the free ones next to them and taken again by the
/// smallest that fits, the rest come off the end
/// </summary>
public sealed class RangeAllocator
{
    // Ranges of freed elements, by their first element
    private readonly SortedDictionary<int, int> _free = [];

    /// <summary>
    /// Past the last element ever handed out and not given back at the end
    /// </summary>
    public int End { get; private set; }

    public int Take(int count)
    {
        if (count <= 0)
        {
            return End;
        }

        var best = -1;
        var bestLength = 0;
        foreach (var (start, length) in _free)
        {
            if (length >= count && (best < 0 || length < bestLength))
            {
                best = start;
                bestLength = length;
            }
        }

        if (best < 0)
        {
            var first = End;
            End += count;
            return first;
        }

        _free.Remove(best);
        if (bestLength > count)
        {
            _free[best + count] = bestLength - count;
        }

        return best;
    }

    public void Give(int first, int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (_free.Remove(first + count, out var after))
        {
            count += after;
        }

        var before = -1;
        foreach (var (start, length) in _free)
        {
            if (start > first)
            {
                break;
            }

            if (start + length == first)
            {
                before = start;
            }
        }

        if (before >= 0)
        {
            count += first - before;
            first = before;
            _free.Remove(before);
        }

        if (first + count == End)
        {
            End = first;
            return;
        }

        _free[first] = count;
    }
}
