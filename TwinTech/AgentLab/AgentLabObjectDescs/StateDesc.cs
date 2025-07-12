using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs;

public abstract class StateDesc
{
    public abstract ITwinBehaviourState Construct();
}