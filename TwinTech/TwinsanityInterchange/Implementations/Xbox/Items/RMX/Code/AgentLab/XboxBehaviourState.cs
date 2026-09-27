using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab
{
    public class XboxBehaviourState : PS2BehaviourState, ITwinBehaviourState
    {
        protected override PS2BehaviourState CreateState()
        {
            return new XboxBehaviourState();
        }

        protected override PS2BehaviourStateBody CreateStateBody()
        {
            return new XboxBehaviourStateBody();
        }
    }
}
