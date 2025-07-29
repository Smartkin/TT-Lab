using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace TT_Lab.Assets.Code.Resolvers.Compiler;

public class LabGraphResolver : IGraphResolver
{
    private int _graphId;
    
    public LabGraphResolver(int graphId = -1)
    {
        _graphId = graphId;
    }
    
    public short ResolveGraphReference(string graphRef)
    {
        return (short)_graphId;
    }
}