using System;
using System.Collections.Generic;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.Resolvers.Compiler;

public class DefaultGraphResolver : IGraphResolver
{
    private Dictionary<string, short> _graphToId = new();

    public DefaultGraphResolver()
    {
        // Linear behaviours reserved by the vanilla retail game
        foreach (var gameId in Enum.GetValues<ITwinBehaviourCommandsSequence.GameReservedIds>())
        {
            _graphToId.Add(gameId.ToString(), (short)gameId);
        }
    }

    public void AddNewGraphRef(string graphName, short id)
    {
        _graphToId.Add(graphName, id);
    }
    
    public short ResolveGraphReference(string graphRef)
    {
        return _graphToId.TryGetValue(graphRef, out var id) ? id : short.Parse(graphRef);
    }
}