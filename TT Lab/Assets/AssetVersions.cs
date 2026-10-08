using System;
using Splat;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Assets;

/// <summary>
/// The version of the game assets are of. A package of one version can use the other version's assets (a PS2 model's texture on the Xbox,
/// depending on the package that has it) but for game objects, their instances and behaviours: some behaviour commands differ between the
/// versions, what runs them has to be of the version that plays it
/// </summary>
public static class AssetVersions
{
    /// <summary>
    /// Whether assets of the kind are only used by their own version
    /// </summary>
    public static bool IsVersionBound(Type type)
    {
        return type.IsAssignableTo(typeof(GameObject)) || type.IsAssignableTo(typeof(ObjectInstance)) || type.IsAssignableTo(typeof(Behaviour))
               || type.IsAssignableTo(typeof(BehaviourCommandsSequence));
    }

    public static bool IsVersionBound(IAsset asset) => IsVersionBound(asset.GetType());

    private static TT_Lab.Project.Project? OpenedProject => Locator.Current.GetService<ProjectManager>()?.OpenedProject as TT_Lab.Project.Project;

    public static string Describe(GamePlatform platform) => platform == GamePlatform.Xbox ? "Xbox" : "PS2";

    /// <summary>
    /// Why an asset of the package can't use the asset, null when it can
    /// </summary>
    public static string? WhyNotUsableBy(LabURI? package, IAsset used)
    {
        if (package == null || used.Package == null || !IsVersionBound(used) || OpenedProject is not { } project)
        {
            return null;
        }

        var user = project.GetPlatform(package);
        var own = project.GetPlatform(used.Package);
        return user == own
            ? null
            : $"{used.Alias} is the {Describe(own)} version's: game objects, their instances and behaviours can't be used by the {Describe(user)} version, some behaviour commands differ";
    }
}
