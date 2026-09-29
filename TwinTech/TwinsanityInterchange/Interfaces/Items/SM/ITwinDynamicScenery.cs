using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SM
{
    public interface ITwinDynamicScenery : ITwinItem
    {
        /// <summary>
        /// Unknown integer parameter
        /// </summary>
        /// <summary>
        /// The data's version word (0x10009), never read
        /// </summary>
        Int32 Version { get; set; }
        /// <summary>
        /// All the dynamic models for this scenery
        /// </summary>
        List<TwinDynamicSceneryModel> DynamicModels { get; set; }
    }
}
