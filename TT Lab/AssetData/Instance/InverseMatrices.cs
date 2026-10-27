using System;
using TT_Lab.Extensions;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance;

// Matrices the game keeps next to their inverses (a chunk link's object matrix): the inspector keeps them so (InverseMatrixChange), what's set
// outside it needn't (a link placed from the viewport's menu by TT Lab 1.1.0 and before, a data file edited by hand)
internal static class InverseMatrices
{
    /// <summary>
    /// The matrix the game gets next to the other: the one kept while it's the other's inverse but for rounding (the game's pairs are, within
    /// a thousandth relative), else the other's inverse. The one kept when the other has no inverse
    /// </summary>
    public static Matrix4 Written(Matrix4 kept, Matrix4 of, out bool isKept)
    {
        isKept = true;
        var matrix = of.ToGlm();
        if (MathF.Abs(matrix.Determinant) < 1e-12f)
        {
            return kept;
        }

        var inverse = matrix.Inverse;
        var own = kept.ToGlm();
        for (var column = 0; column < 4; column++)
        {
            for (var row = 0; row < 4; row++)
            {
                if (MathF.Abs(own[column, row] - inverse[column, row]) > 1e-3f * (1 + MathF.Abs(inverse[column, row])))
                {
                    isKept = false;
                    return inverse.ToTwin();
                }
            }
        }

        return kept;
    }
}
