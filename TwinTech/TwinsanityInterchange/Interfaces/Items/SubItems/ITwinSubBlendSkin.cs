using System;
using System.Collections.Generic;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    public interface ITwinSubBlendSkin : ITwinSerializable
    {
        /// <summary>
        /// Material to render model with
        /// </summary>
        UInt32 Material { get; set; }
        /// <summary>
        /// Models the blend consists of
        /// </summary>
        List<ITwinBlendSkinModel> Models { get; set; }
    }
}
