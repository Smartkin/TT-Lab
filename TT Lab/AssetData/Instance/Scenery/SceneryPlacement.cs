using System;
using Newtonsoft.Json;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Scenery;

/// <summary>
/// A mesh or LOD placed in the scenery. The build puts it into the tree the game culls the scenery with (<see cref="SceneryTree"/>)
/// </summary>
[ReferencesAssets]
public class SceneryPlacement
{
    /// <summary>
    /// The placed <see cref="Assets.Graphics.Mesh"/>, or the <see cref="Assets.Graphics.LodModel"/> when <see cref="IsLod"/>
    /// </summary>
    public LabURI Model { get; set; } = LabURI.Empty;

    public Boolean IsLod { get; set; }

    [Editable(Caption = "Placed", Hint = "What's placed: one of the scenery's meshes, or a LOD drawn with another of its meshes the further the camera gets")]
    [EditorReadOnly]
    [JsonIgnore]
    public String Description
    {
        get
        {
            var assetManager = AssetManager.Get();
            var name = assetManager.DoesAssetExist(Model) ? assetManager.GetAsset(Model).Alias : Model.ToString();
            return IsLod ? $"LOD {name}" : name;
        }
    }

    [Editable(Hint = "Where it is, how it's turned and how big, the scenery mode's gizmo moves, turns and scales it")]
    public Matrix4 Matrix { get; set; } = new();

    /// <summary>
    /// The box the game culls it with in the model's space, the corners in XYZ with the radius of the farthest one in the first's W
    /// </summary>
    public BoundingBox Box { get; set; } = new() { V1 = new Vector4(), V2 = new Vector4() };

    /// <summary>
    /// The tree node it's in, the octants from the root down (<see cref="SceneryTree"/>), kept while that node still holds it; null for one
    /// placed where the build puts new ones
    /// </summary>
    public String? Node { get; set; }
}
