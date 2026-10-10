using System;

namespace TT_Lab.Util;

/// <summary>
/// Fuzzy matching of what's typed against names: every character typed has to come in order, in any case, and the best way of lining
/// them up is scored, like code editors' quick open does: runs of matched characters, matches where words start (the text's start, after
/// a separator, a capital after a small letter, a digit after a letter) and an early first match score more, skipped characters cost
/// </summary>
public static class FuzzyMatch
{
    private const int Match = 16;
    private const int Consecutive = 12;
    private const int WordStart = 10;
    private const int TextStart = 14;
    private const int GapOpen = 3;
    private const int GapExtend = 1;
    // What the characters before the first match cost, at most
    private const int MostLeading = 12;
    private const int Unmatched = int.MinValue / 4;

    /// <summary>
    /// How well the pattern lines up with the text, none when its characters aren't all in it in order
    /// </summary>
    /// <param name="pattern">What's typed, in lower case</param>
    /// <param name="text">The name as it's shown, for where its words start</param>
    /// <param name="lower">The name in lower case</param>
    public static int? Score(string pattern, string text, string lower)
    {
        var m = pattern.Length;
        var n = lower.Length;
        if (m == 0)
        {
            return 0;
        }

        if (m > n || !IsSubsequence(pattern, lower))
        {
            return null;
        }

        // previous[j]: the best score of the pattern so far with its last character at j
        Span<int> previous = n <= 256 ? stackalloc int[n] : new int[n];
        Span<int> current = n <= 256 ? stackalloc int[n] : new int[n];
        for (var j = 0; j < n; j++)
        {
            previous[j] = lower[j] == pattern[0] ? Match + Bonus(text, j) - Math.Min(j, MostLeading) : Unmatched;
        }

        for (var i = 1; i < m; i++)
        {
            // The best previous[k] + GapExtend * k over the characters at least two before j, for a gap from k to j
            var bestBeforeGap = Unmatched;
            for (var j = 0; j < n; j++)
            {
                if (j >= 2 && previous[j - 2] != Unmatched)
                {
                    bestBeforeGap = Math.Max(bestBeforeGap, previous[j - 2] + GapExtend * (j - 2));
                }

                current[j] = Unmatched;
                if (lower[j] != pattern[i])
                {
                    continue;
                }

                var score = Unmatched;
                if (j >= 1 && previous[j - 1] != Unmatched)
                {
                    score = previous[j - 1] + Consecutive;
                }

                if (bestBeforeGap != Unmatched)
                {
                    score = Math.Max(score, bestBeforeGap - GapOpen - GapExtend * (j - 1));
                }

                if (score != Unmatched)
                {
                    current[j] = score + Match + Bonus(text, j);
                }
            }

            var swap = previous;
            previous = current;
            current = swap;
        }

        var best = Unmatched;
        foreach (var score in previous)
        {
            best = Math.Max(best, score);
        }

        // At the same match a shorter name is closer to what's typed
        return best == Unmatched ? null : best - (n - m) / 8;
    }

    private static bool IsSubsequence(string pattern, string lower)
    {
        var found = 0;
        for (var j = 0; j < lower.Length && found < pattern.Length; j++)
        {
            if (lower[j] == pattern[found])
            {
                found++;
            }
        }

        return found == pattern.Length;
    }

    // Where a word starts in the text
    private static int Bonus(string text, int j)
    {
        if (j == 0)
        {
            return TextStart;
        }

        var previous = text[j - 1];
        var character = text[j];
        if (!char.IsLetterOrDigit(previous))
        {
            return WordStart;
        }

        if (char.IsUpper(character) && char.IsLower(previous) || char.IsDigit(character) && char.IsLetter(previous) || char.IsLetter(character) && char.IsDigit(previous))
        {
            return WordStart;
        }

        return 0;
    }
}
