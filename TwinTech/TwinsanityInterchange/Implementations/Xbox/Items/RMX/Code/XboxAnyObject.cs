using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code
{
    public class XboxAnyObject : PS2AnyObject, ITwinObject
    {
        protected override ITwinBehaviourCommandPack CreateCommandPack()
        {
            return new AgentLab.XboxBehaviourCommandPack();
        }
    }
}
