using System;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Attributes;

/// <summary>
/// Grays a field out while the game doesn't read it: the field's owner (its node's target) says, the fields it goes by are linked to
/// it, one <see cref="EditorLinkedFieldAttribute"/> each
/// </summary>
public abstract class ReadWhen<TOwner> : IFieldChange where TOwner : class
{
    protected abstract Boolean IsRead(TOwner owner);

    // What the owner is in can matter too
    protected virtual Boolean IsRead(TOwner owner, PropertyNode node) => IsRead(owner);

    public void DataChanged(PropertyNode listeningNode, PropertyNode changedNode)
    {
        if (listeningNode.Target is TOwner owner)
        {
            listeningNode.IsReadOnly = !IsRead(owner, listeningNode);
        }
    }

    public void Linked(PropertyNode listeningNode, PropertyNode changedNode) => DataChanged(listeningNode, changedNode);
}

// The camera rig's followers only take the rate with AtRate (CameraRig's FollowSubtype)
public sealed class FollowRateRead : ReadWhen<CameraSubBase>
{
    protected override Boolean IsRead(CameraSubBase owner) => ((UInt32)owner.Follow & 3) == (UInt32)CameraSubBase.FollowMode.AtRate;
}

// A spline only takes the subtype's offset with TakesOffset (CameraSplineCamera::Read)
public sealed class SplineOffsetRead : ReadWhen<CameraSpline>
{
    protected override Boolean IsRead(CameraSpline owner) => (owner.SplineFlags & ITwinCamera.SplineCameraFlags.TakesOffset) != 0;
}

// The boss camera's curves are only read with Uses Distance Curves (BossCameraAt)
public sealed class BossCurvesRead : ReadWhen<BossCamera>
{
    protected override Boolean IsRead(BossCamera owner) => owner.UsesDistanceCurves;
}
