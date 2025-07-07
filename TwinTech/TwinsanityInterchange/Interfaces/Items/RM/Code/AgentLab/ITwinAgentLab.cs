using System.IO;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

public interface ITwinAgentLab : ITwinSerializable
{
    void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0);
}