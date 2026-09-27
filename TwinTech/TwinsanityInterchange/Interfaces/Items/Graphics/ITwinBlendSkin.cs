using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items
{
    public interface ITwinBlendSkin : ITwinItem
    {
        /// <summary>
        /// Amount of blends/morphs
        /// </summary>
        public Int32 BlendsAmount { get; set; }
        /// <summary>
        /// Submodels/Subblends the model consists of
        /// </summary>
        List<ITwinSubBlendSkin> SubBlends { get; set; }
    }
}
