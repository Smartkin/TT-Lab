using System;
using System.Collections.Generic;

namespace TT_Lab.Util;

/// <summary>
/// Orders names by the numbers in them by their values ("Mesh 2" before "Mesh 10") and the rest without case
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x == null || y == null)
        {
            return x == null ? y == null ? 0 : -1 : 1;
        }

        var i = 0;
        var j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsAsciiDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsAsciiDigit(y[j]))
                {
                    j++;
                }

                var numberX = x.AsSpan(startX, i - startX).TrimStart('0');
                var numberY = y.AsSpan(startY, j - startY).TrimStart('0');
                var byValue = numberX.Length != numberY.Length ? numberX.Length.CompareTo(numberY.Length) : numberX.CompareTo(numberY, StringComparison.Ordinal);
                if (byValue != 0)
                {
                    return byValue;
                }

                continue;
            }

            var byLetter = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
            if (byLetter != 0)
            {
                return byLetter;
            }

            i++;
            j++;
        }

        var byLength = (x.Length - i).CompareTo(y.Length - j);
        // Names only different in their case or leading zeros still go in one order
        return byLength != 0 ? byLength : string.CompareOrdinal(x, y);
    }
}
