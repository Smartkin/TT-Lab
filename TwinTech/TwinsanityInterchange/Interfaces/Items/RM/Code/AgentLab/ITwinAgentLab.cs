using System.IO;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

public interface ITwinAgentLab : ITwinSerializable
{
    void Decompile(StreamWriter writer, int tabs = 0);
}