using System;
using System.Collections.Generic;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets.Factory;

/// <summary>
/// What the chunk being built has resolved so far
/// </summary>
/// <remarks>
/// Every instance resolves its object, so an asset only goes into the chunk the first time. Assets being resolved are skipped when
/// something they refer to refers back to them
/// </remarks>
public sealed class ChunkResolution
{
    private readonly HashSet<IAsset> _inProgress = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<(IAsset Asset, ITwinSection Section)> _resolved = new(new ResolvedComparer());

    /// <summary>
    /// What the objects resolved into the chunk refer to, by the URI they were resolved with
    /// </summary>
    public Dictionary<LabURI, GameObject.ReferencedResourceUris> ObjectReferences { get; } = new();

    /// <summary>
    /// The objects and graphs the code of the graphs resolved into the chunk refers to
    /// </summary>
    public Dictionary<LabURI, (IReadOnlyList<LabURI> Objects, IReadOnlyList<LabURI> Graphs)> GraphReferences { get; } = new();

    /// <summary>
    /// Starts resolving the asset into the section
    /// </summary>
    /// <returns>Whether the asset has to be resolved, it doesn't when it already is or it's being resolved</returns>
    public bool Begin(IAsset asset, ITwinSection section)
    {
        if (_inProgress.Contains(asset) || _resolved.Contains((asset, section)))
        {
            return false;
        }

        _inProgress.Add(asset);
        return true;
    }

    public void End(IAsset asset, ITwinSection section)
    {
        _inProgress.Remove(asset);
        _resolved.Add((asset, section));
    }

    private sealed class ResolvedComparer : IEqualityComparer<(IAsset Asset, ITwinSection Section)>
    {
        public bool Equals((IAsset Asset, ITwinSection Section) x, (IAsset Asset, ITwinSection Section) y)
        {
            return ReferenceEquals(x.Asset, y.Asset) && ReferenceEquals(x.Section, y.Section);
        }

        public int GetHashCode((IAsset Asset, ITwinSection Section) obj)
        {
            return HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Asset), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Section));
        }
    }
}
