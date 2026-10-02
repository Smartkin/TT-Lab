using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM
{
    public interface ITwinCollision : ITwinItem
    {
        /// <summary>
        /// The data's version word (3001), read and never checked
        /// </summary>
        UInt32 Version { get; set; }
        /// <summary>
        /// The tree of boxes ray casts and box queries walk, its leaves groups of triangles
        /// </summary>
        List<TwinCollisionNode> Nodes { get; set; }
        List<TwinCollisionGroup> Groups { get; set; }
        List<TwinCollisionTriangle> Triangles { get; set; }
        List<Vector4> Vertexes { get; set; }
    }
}
