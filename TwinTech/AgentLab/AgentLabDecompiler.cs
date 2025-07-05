using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab;

/// <summary>
/// Decompiles the AgentLab script from bytecode to its approximate text representation
/// </summary>
public static class AgentLabDecompiler
{
    /// <summary>
    /// <inheritdoc cref="AgentLabDecompiler"/>
    /// </summary>
    /// <param name="agentLabObject">AgentLab script to decompile</param>
    /// <returns>Decompiled script</returns>
    public static string Decompile(ITwinAgentLab agentLabObject)
    {
        using MemoryStream stream = new();
        StreamWriter writer = new(stream);
        StreamReader reader = new(stream);
        agentLabObject.Decompile(writer);
        writer.Flush();
        stream.Position = 0;
        return reader.ReadToEnd();
    }
    
}