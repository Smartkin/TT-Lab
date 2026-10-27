using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// What a chunk's scene does to the links of the chunk's other resources to the resources it takes out (deleted in the scene or the Chunk
/// Resources panel): each follows its member's <see cref="OnReferenceDeletedAttribute"/> like deleting fixes links, through the document's
/// nodes, so it's in the step taking them out and undo puts the links back with them. Chunk-scoped links are all in the chunk's own
/// resources. A link only a placeholder could stand in for (an AI path's position: no instance has one) stays and is told
/// </summary>
internal static class ChunkLinkFixes
{
    /// <summary>
    /// Fixes the links of the chunk's resources to the removed assets, the number of links fixed
    /// </summary>
    public static int Apply(DocumentViewModel document, IReadOnlySet<LabURI> removed)
    {
        if (removed.Count == 0 || document.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources)) is not { } resources)
        {
            return 0;
        }

        var links = resources.Children.Select(resource => resource.FindChild("[data]")).OfType<PropertyNode>()
            .SelectMany(LinksIn).Where(link => link.GetValue() is LabURI uri && removed.Contains(uri)).ToList();
        var fixedLinks = 0;
        // A list's elements from its last, the indexes of the others stay what they are
        foreach (var link in links.OrderByDescending(link => link.Index ?? -1))
        {
            var action = link.Metadata?.PropertyInfo.GetCustomAttribute<OnReferenceDeletedAttribute>()?.Action ?? DeletedReferenceAction.ReplaceWithPlaceholder;
            if (action == DeletedReferenceAction.ReplaceWithPlaceholder)
            {
                Log.WriteLine($"{OwnerOf(link)}'s {link.Metadata?.PropertyInfo.Name ?? link.Name} links {link.GetValue<LabURI>()}, which is taken out of the chunk: " +
                              "link another or delete it", Log.LogType.Warning);
                continue;
            }

            if (action == DeletedReferenceAction.Remove && link.Index.HasValue && link.Parent is { } list && list.GetValue() is System.Collections.IList { IsFixedSize: false })
            {
                list.RemoveElement(link);
            }
            else
            {
                link.SetValue(LabURI.Empty);
            }

            fixedLinks++;
        }

        return fixedLinks;
    }

    // The links of an asset's nodes, not of the assets they link to (each its own [data] node)
    private static IEnumerable<PropertyNode> LinksIn(PropertyNode node)
    {
        foreach (var child in node.Children.Where(child => child.Segment != "[data]"))
        {
            if (child.PropertyType.IsAssignableTo(typeof(LabURI)))
            {
                yield return child;
                continue;
            }

            foreach (var link in LinksIn(child))
            {
                yield return link;
            }
        }
    }

    private static string OwnerOf(PropertyNode link)
    {
        for (var node = link.Parent; node != null; node = node.Parent)
        {
            if (node.Segment == "[data]" && node.GetValue() is IAsset asset)
            {
                return asset.Alias;
            }
        }

        return "A resource";
    }
}
