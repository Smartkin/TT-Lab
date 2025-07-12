using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs;

public abstract class GraphDesc
{
    public abstract ITwinBehaviourGraph Construct();
}