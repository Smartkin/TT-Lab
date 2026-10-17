using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// The assets linked Blenders draw, what the project tree marks: the models they linked, the materials and pictures those draw and what
/// they made. Only on the UI thread
/// </summary>
public static class LinkMarks
{
    private static HashSet<LabURI> _linked = [];

    public static bool IsLinked(LabURI? uri) => uri != null && _linked.Contains(uri);

    internal static void Update(IEnumerable<BlenderLinkSession> sessions)
    {
        var linked = sessions.Where(session => session.IsWelcomed).SelectMany(session => session.Assets.Linked).ToHashSet();
        var changed = linked.Except(_linked).Concat(_linked.Except(linked)).ToList();
        _linked = linked;
        // The project closed, its rows with it
        if (Splat.Locator.Current.GetService(typeof(Project.ProjectManager)) is not Project.ProjectManager { OpenedProject: not null })
        {
            return;
        }

        var assetManager = AssetManager.Get();
        foreach (var uri in changed.Where(assetManager.DoesAssetExist))
        {
            assetManager.GetAsset(uri).GetResourceTreeElement().NotifyOfPropertyChange(nameof(ResourceTreeElementViewModel.IsLinkedToBlender));
        }
    }
}
