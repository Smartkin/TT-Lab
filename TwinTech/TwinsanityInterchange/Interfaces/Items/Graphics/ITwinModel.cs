using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items
{
    public interface ITwinModel : ITwinItem
    {
        /// <summary>
        /// Submodels the model consists of
        /// </summary>
        List<ITwinSubModel> SubModels { get; set; }
    }
}
