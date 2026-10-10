using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

// A link the inspector lets be edited, of the kind of asset its field takes. What it can be replaced with is what its own link browser
// offers (its scope, exclusions and the version's assets)
public sealed class ReplaceableLink
{
    private readonly UriLinkViewModel _editor;
    private HashSet<LabURI>? _candidates;

    internal ReplaceableLink(PropertyNode node, UriLinkViewModel editor, Type kind, string caption)
    {
        Node = node;
        Path = node.Path;
        _editor = editor;
        Kind = kind;
        Caption = caption;
        Original = node.GetValue<LabURI>() ?? LabURI.Empty;
    }

    public PropertyNode Node { get; }
    public string Path { get; }
    public Type Kind { get; }
    public string Caption { get; }

    // What it linked when the dialogue listed it
    public LabURI Original { get; }

    public IReadOnlySet<LabURI> Candidates => _candidates ??= _editor.GetBrowseCandidates().ToHashSet();
}

// A part of the asset with links in it, the way the inspector shows it: an object, a list or one of its elements
public sealed class LinkBranch(string caption)
{
    public string Caption { get; } = caption;
    public List<LinkBranch> Branches { get; } = [];
    public List<ReplaceableLink> Links { get; } = [];

    public IEnumerable<ReplaceableLink> AllLinks => Links.Concat(Branches.SelectMany(branch => branch.AllLinks));
}

// The links ticked and what replaces each kind of them, a kind without a replacement keeps its links
public sealed record LinkReplacementChoice(IReadOnlyList<ReplaceableLink> Links, IReadOnlyDictionary<Type, LabURI> Replacements);

internal static class LinkReplacement
{
    // What the user ticks and picks, the tests answer it themselves
    internal static Func<LinkBranch, Task<LinkReplacementChoice?>> Ask { get; set; } = ReplaceLinksDialogue.Ask;

    public static async Task<int> ReplaceAsync(DocumentViewModel document, PropertyNode root)
    {
        var choice = await Ask(Find(document, root));
        return choice == null ? 0 : Apply(document, root, choice);
    }

    // The asset's links the inspector lets be edited, in its tree: what's hidden, read only or edited by a custom editor isn't, nor what's
    // in the assets the links link
    public static LinkBranch Find(DocumentViewModel document, PropertyNode root) => Find(document, root, UndoHistory.NameOf(root));

    private static LinkBranch Find(DocumentViewModel document, PropertyNode node, string caption)
    {
        var branch = new LinkBranch(caption);
        foreach (var child in ShownChildren(document, node))
        {
            var editor = EditorDescRegistry.GetDesc(document, child).Construct();
            if (!editor.IsVisible || editor.IsReadOnly)
            {
                continue;
            }

            var childCaption = child.Index != null && child.Parent != null ? DocumentCollectionViewModel.CaptionOf(child.Parent, child) : editor.Caption;
            switch (editor)
            {
                case UriLinkViewModel link:
                    branch.Links.Add(new ReplaceableLink(child, link, link.ReadReplacementKind(), childCaption));
                    break;
                case DocumentModelViewModel or DocumentCollectionViewModel:
                    var inner = Find(document, child, childCaption);
                    if (inner.AllLinks.Any())
                    {
                        branch.Branches.Add(inner);
                    }

                    break;
            }
        }

        return branch;
    }

    // Like the inspector's composites: side pane editors left out, inline ones' children in their place
    private static IEnumerable<PropertyNode> ShownChildren(DocumentViewModel document, PropertyNode node)
    {
        foreach (var child in node.Children)
        {
            if (document.ShowsInSidePane(child))
            {
                continue;
            }

            if (child.Metadata?.EditorParams?.GetValueOrDefault(DocumentCompositeViewModel.EditorInline) is true)
            {
                foreach (var inlined in ShownChildren(document, child))
                {
                    yield return inlined;
                }

                continue;
            }

            yield return child;
        }
    }

    // What every one of the links takes, their rules together
    public static List<LabURI> CandidatesFor(IEnumerable<ReplaceableLink> links)
    {
        HashSet<LabURI>? common = null;
        foreach (var link in links)
        {
            if (common == null)
            {
                common = [..link.Candidates];
            }
            else
            {
                common.IntersectWith(link.Candidates);
            }
        }

        return common?.ToList() ?? [];
    }

    // One step of the history. A link only gets what its own rules take, and one that a replacement before it changed or took out (an
    // instance fitted to another object) is left as it is
    public static int Apply(DocumentViewModel document, PropertyNode root, LinkReplacementChoice choice)
    {
        var replaced = 0;
        using (document.History.BeginGroup($"Replaced links in '{UndoHistory.NameOf(root)}'"))
        {
            foreach (var link in choice.Links)
            {
                if (!choice.Replacements.TryGetValue(link.Kind, out var replacement) || replacement == link.Original || !link.Candidates.Contains(replacement))
                {
                    continue;
                }

                var node = link.Node.Graph == document.PropertyGraph && document.PropertyGraph.Find(link.Path) == link.Node ? link.Node : null;
                if (node == null || node.GetValue<LabURI>() != link.Original)
                {
                    continue;
                }

                document.AddResource(replacement);
                node.SetValue(replacement);
                replaced++;
            }
        }

        return replaced;
    }
}
