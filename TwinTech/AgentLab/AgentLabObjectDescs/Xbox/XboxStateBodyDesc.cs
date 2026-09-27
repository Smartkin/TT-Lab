using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs.Xbox;

public class XboxStateBodyDesc : StateBodyDesc
{
    public override ITwinBehaviourStateBody Construct()
    {
        return new XboxBehaviourStateBody();
    }
}