using System;
using System.Collections.Generic;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace TT_Lab.Assets.Code.Resolvers.Compiler;

/// <summary>
/// Resolves the behaviours states run: by name within the requester's package and the packages it depends on, or by URI
/// </summary>
public class LabStateGraphResolver : IStateGraphResolver
{
    private readonly IAsset? _requester;
    private readonly List<LabURI> _resolvedGraphs = [];

    public LabStateGraphResolver(IAsset? requester = null)
    {
        _requester = requester;
    }

    public Int16 ResolveGraphReference(string graphRef)
    {
        var graph = BehaviourReferences.Find(_requester, graphRef);
        if (graph == null)
        {
            var scope = _requester == null ? "the project" : $"{BehaviourReferences.PackageName(AssetManager.Get(), _requester.Package)} or the packages it depends on";
            throw new KeyNotFoundException($"{graphRef} isn't one behaviour of {scope}");
        }

        _resolvedGraphs.Add(graph.URI);
        return (short)graph.ID;
    }

    public IReadOnlyList<LabURI> ResolvedGraphs => _resolvedGraphs;
}
