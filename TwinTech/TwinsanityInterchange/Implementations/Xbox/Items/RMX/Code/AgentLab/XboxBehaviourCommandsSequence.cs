using System;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab
{
    public class XboxBehaviourCommandsSequence : PS2BehaviourCommandsSequence, ITwinBehaviourCommandsSequence
    {
        protected override String Marker => "Xbox";

        protected override PS2BehaviourCommandPack CreatePack()
        {
            return new XboxBehaviourCommandPack();
        }

        protected override PS2BehaviourCommand CreateCommand()
        {
            return new XboxBehaviourCommand();
        }
    }
}
