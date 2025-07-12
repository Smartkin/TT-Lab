using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs.PS2;

public class PS2CommandPackDesc : CommandPackDesc
{
    public override ITwinBehaviourCommandPack Construct()
    {
        return new PS2BehaviourCommandPack();
    }
}