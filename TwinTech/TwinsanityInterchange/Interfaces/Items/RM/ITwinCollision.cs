using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM
{
    public interface ITwinCollision : ITwinItem
    {
        /// <summary>
        /// Unknown integer parameter
        /// </summary>
        /// <summary>
        /// The data's version word (3001), read and never checked
        /// </summary>
        UInt32 Version { get; set; }
        List<TwinCollisionTrigger> Triggers { get; set; }
        List<TwinGroupInformation> Groups { get; set; }
        List<TwinCollisionTriangle> Triangles { get; set; }
        List<Vector4> Vectors { get; set; }
    }
}
