using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs.Xbox;

public class XboxCommandPackDesc : CommandPackDesc
{
    public override ITwinBehaviourCommandPack Construct()
    {
        return new XboxBehaviourCommandPack();
    }
}