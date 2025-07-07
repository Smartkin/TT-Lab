using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
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
    /// <param name="resolver">Data reference <see cref="IResolver">resolver</see></param>
    /// <returns>Decompiled script</returns>
    public static string Decompile(ITwinAgentLab agentLabObject, IResolver resolver = null)
    {
        using MemoryStream stream = new();
        StreamWriter writer = new(stream);
        StreamReader reader = new(stream);
        agentLabObject.Decompile(resolver, writer);
        writer.Flush();
        stream.Position = 0;
        return reader.ReadToEnd();
    }
    
}