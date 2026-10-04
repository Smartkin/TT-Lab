using System;
using Twinsanity.TwinsanityInterchange.Common;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinAIPosition : ITwinItem
    {
        /// <summary>
        /// Where it is, W its radius (only the condition of the distance to the nearest point's edge and GetShortRoute's distance limits
        /// read it)
        /// </summary>
        Vector4 Position { get; set; }
        AiPositionFlags Flags { get; set; }
    }
}
