using System;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace TT_Lab.AssetData.Instance.Collision;

public class CollisionGroup
{
    public UInt32 Count { get; set; }
    public UInt32 FirstTriangle { get; set; }

    public CollisionGroup() { }

    public CollisionGroup(TwinCollisionGroup group)
    {
        Count = group.Count;
        FirstTriangle = group.FirstTriangle;
    }
}
