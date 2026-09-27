using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs.Xbox;

public class XboxCommandDesc : CommandDesc
{
    public override ITwinBehaviourCommand Construct()
    {
        return new XboxBehaviourCommand();
    }
}