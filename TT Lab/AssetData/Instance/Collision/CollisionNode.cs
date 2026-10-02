using System;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace TT_Lab.AssetData.Instance.Collision;

public class CollisionNode
{
    public Vector3 Min { get; set; }
    public Vector3 Max { get; set; }
    public Int32 FirstChild { get; set; }
    public Int32 SecondChild { get; set; }

    public CollisionNode()
    {
        Min = new Vector3();
        Max = new Vector3();
    }

    public CollisionNode(TwinCollisionNode node)
    {
        Min = CloneUtils.Clone(node.Min);
        Max = CloneUtils.Clone(node.Max);
        FirstChild = node.FirstChild;
        SecondChild = node.SecondChild;
    }
}
