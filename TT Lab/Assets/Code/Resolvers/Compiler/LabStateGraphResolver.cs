using System;
using System.Collections.Generic;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace TT_Lab.Assets.Code.Resolvers.Compiler;

public class LabStateGraphResolver : IStateGraphResolver
{
    private readonly List<LabURI> _resolvedGraphs = [];
    
    public Int16 ResolveGraphReference(string graphRef)
    {
        _resolvedGraphs.Add((LabURI)graphRef);
        return (short)AssetManager.Get().GetAsset((LabURI)graphRef).ID;
    }
    
    public IReadOnlyList<LabURI> ResolvedGraphs => _resolvedGraphs;
}