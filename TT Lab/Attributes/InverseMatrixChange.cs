using System;
using TT_Lab.Extensions;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Attributes;

/// <summary>
/// A matrix kept as the inverse of another (a chunk link's object matrix of its chunk matrix, a boss camera's world to arena matrix of
/// its arena to world), made again when that one changes. A matrix without an inverse leaves it as it was
/// </summary>
public sealed class InverseMatrixChange : IFieldChange
{
    public void DataChanged(PropertyNode listeningNode, PropertyNode changedNode)
    {
        if (changedNode.GetValue<Matrix4>()?.ToGlm() is not { } matrix || MathF.Abs(matrix.Determinant) < 1e-12f)
        {
            return;
        }

        listeningNode.SetValue(matrix.Inverse.ToTwin());
    }
}
