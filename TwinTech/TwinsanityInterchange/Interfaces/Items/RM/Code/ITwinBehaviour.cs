using System;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code
{
    public interface ITwinBehaviour : ITwinItem, ITwinAgentLab
    {
        /// <summary>
        /// Behaviour's priority in execution queue
        /// </summary>
        Byte Priority { get; set; }
    }
}
