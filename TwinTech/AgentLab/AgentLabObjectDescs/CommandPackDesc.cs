using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs;

public abstract class CommandPackDesc
{
    public abstract ITwinBehaviourCommandPack Construct();
}