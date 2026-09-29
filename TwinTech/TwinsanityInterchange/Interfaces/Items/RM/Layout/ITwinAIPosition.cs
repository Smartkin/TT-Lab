using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinAIPosition : ITwinItem
    {
        /// <summary>
        /// AI position
        /// </summary>
        Vector4 Position { get; set; }
        /// <summary>
        /// Unknown parameter
        /// </summary>
        /// <summary>
        /// Bits 1, 2 and 4 on some positions of the retail levels, no reader found in the PAL executable
        /// </summary>
        UInt16 Flags { get; set; }
    }
}
