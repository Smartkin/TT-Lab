using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab;

namespace TT_Lab.Assets.Code;

/// <summary>
/// The behaviours a state can run as its child: by name within the requester's package and the packages it depends on, or by URI
/// </summary>
public static class BehaviourReferences
{
    private const string UriPrefix = "res://";

    public static bool IsUri(string reference) => reference.StartsWith(UriPrefix, StringComparison.Ordinal);

    /// <summary>
    /// The graph a reference in the requester's script means, null when there's none in its scope or several of the name
    /// </summary>
    public static BehaviourGraph? Find(IAsset? requester, string reference)
    {
        var assets = AssetManager.Get();
        if (IsUri(reference))
        {
            var uri = (LabURI)reference;
            return assets.DoesAssetExist(uri) ? assets.GetAsset(uri) as BehaviourGraph : null;
        }

        if (requester != null)
        {
            return assets.FindByName<BehaviourGraph>(requester, reference);
        }

        var matches = assets.GetAllAssetsOf<BehaviourGraph>().Where(graph => graph.InvariantName == reference && string.IsNullOrEmpty(graph.Variation)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// How the requester's script refers to the graph: its name when that finds it from there, its URI otherwise
    /// </summary>
    public static string ReferenceTo(IAsset requester, BehaviourGraph graph)
    {
        var name = graph.InvariantName;
        return CanBeNamed(name) && Find(requester, name) == graph ? name : graph.URI;
    }

    /// <summary>
    /// A suggestion for every behaviour the requester's scripts can name
    /// </summary>
    public static IReadOnlyList<AgentLabCompletionItem> GetCompletionItems(IAsset requester)
    {
        var assets = AssetManager.Get();
        return assets.GetAssetsInScopeOf<BehaviourGraph>(requester)
            .Where(graph => CanBeNamed(graph.InvariantName))
            .GroupBy(graph => graph.InvariantName)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AgentLabCompletionItem(group.Key, AgentLabCompletionKind.Behaviour, $"behaviour {group.Key} ({PackageName(assets, group.First().Package)})"))
            .ToList();
    }

    public static string PackageName(AssetManager assets, LabURI package)
    {
        return assets.DoesAssetExist(package) ? assets.GetAsset(package).Name : package.GetPackageName();
    }

    private static bool CanBeNamed(string name) => AgentLabLexer.IsIdentifier(name) && !AgentLabLexer.IsReservedKeyword(name);
}
