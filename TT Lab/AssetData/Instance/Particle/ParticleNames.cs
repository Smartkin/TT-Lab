using System;
using System.Linq;

namespace TT_Lab.AssetData.Instance.Particle;

/// <summary>
/// The 16 character names of particle systems and emitters (8 of decal variants): the game reads them up to their NUL, the tools left
/// what was in the buffer after it (the default chunk's systems have such leftovers), which is kept apart from the name so the name stays
/// what the game matches and the file comes back the same
/// </summary>
internal static class ParticleNames
{
    public const int Length = 16;

    public static (string Name, string? Leftover) Split(char[] buffer)
    {
        var end = Array.IndexOf(buffer, '\0');
        if (end < 0)
        {
            return (new string(buffer), null);
        }

        var leftover = new string(buffer, end + 1, buffer.Length - end - 1).TrimEnd('\0');
        return (new string(buffer, 0, end), leftover.Length == 0 ? null : leftover);
    }

    public static char[] Join(string name, string? leftover, int length = Length)
    {
        var buffer = new char[length];
        var count = Math.Min(name.Length, length);
        name.CopyTo(0, buffer, 0, count);
        if (leftover == null || count + 1 >= length)
        {
            return buffer;
        }

        // After the name's NUL
        var kept = Math.Min(leftover.Length, length - count - 1);
        leftover.CopyTo(0, buffer, count + 1, kept);
        return buffer;
    }
}
