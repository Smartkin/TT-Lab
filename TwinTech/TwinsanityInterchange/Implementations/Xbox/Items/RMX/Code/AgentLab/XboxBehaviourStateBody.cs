using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab
{
    public class XboxBehaviourStateBody : PS2BehaviourStateBody, ITwinBehaviourStateBody
    {
        protected override PS2BehaviourCommand CreateCommand()
        {
            return new XboxBehaviourCommand();
        }
    }
}
