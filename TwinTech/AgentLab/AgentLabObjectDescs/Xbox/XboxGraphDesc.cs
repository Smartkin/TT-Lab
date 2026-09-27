using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs.Xbox;

public class XboxGraphDesc : GraphDesc
{
    public override ITwinBehaviourGraph Construct()
    {
        return new XboxBehaviourGraph();
    }
}