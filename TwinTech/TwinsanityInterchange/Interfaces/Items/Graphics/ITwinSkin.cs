using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items
{
    public interface ITwinSkin : ITwinItem
    {
        /// <summary>
        /// Subskins the model consists of
        /// </summary>
        List<ITwinSubSkin> SubSkins { get; set; }
    }
}
