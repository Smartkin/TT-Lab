using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.AgentLabObjectDescs;

public abstract class CommandsSequenceDesc
{
    public abstract ITwinBehaviourCommandsSequence Construct();
}