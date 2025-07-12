using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs;

public abstract class CommandDesc
{
    public abstract ITwinBehaviourCommand Construct();
}