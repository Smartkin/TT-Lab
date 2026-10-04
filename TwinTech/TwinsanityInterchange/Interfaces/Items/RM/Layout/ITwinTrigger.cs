using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinTrigger : ITwinItem
    {
        /// <summary>
        /// Trigger's information
        /// </summary>
        TwinTrigger Trigger { get; set; }
        /// <summary>
        /// The four messages, sent by the header's bits 11, 8, 9 and 10 (<see cref="Enumerations.Enums.TriggerFlags"/>)
        /// </summary>
        UInt16[] TriggerMessages { get; set; }
    }
}
