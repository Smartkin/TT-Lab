using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.ViewModels.Editors;
using Material = TT_Lab.Assets.Graphics.Material;

namespace TT_Lab.AssetData.Code;

/// <summary>
/// A material an OGI's meshes draw with and the parts of them drawing with it, like a material slot of Blender: another material picked
/// draws every one of them with that one. The parts keep their links in the models' data, which the OGI's file is written from
/// </summary>
public sealed class OgiMaterialSlot
{
    internal enum Mesh
    {
        Rigid,
        Skin,
        BlendSkin
    }

    /// <summary>
    /// A part drawing with the material: the model, which of the OGI's meshes it is (a rigid model's place among them) and its part
    /// </summary>
    internal readonly record struct Use(LabURI Model, Mesh Kind, Int32 Index, Int32 Part);

    private readonly List<Use> _uses;
    private LabURI _material;

    internal OgiMaterialSlot(LabURI material, List<Use> uses)
    {
        _material = material;
        _uses = uses;
    }

    // The inspector shows lists of what it can make elements of, though it makes none of these: the list can't be changed
    public OgiMaterialSlot() : this(LabURI.Empty, [])
    {
    }

    internal IReadOnlyList<Use> Uses => _uses;

    // Documents reaching the model through other links (a chunk's, through its objects) don't build the materials' values under it
    [Editable(MaxLinkGraphDepth = 2, Hint = "The material the parts below draw with. Another one picked draws every one of them with it, the model's file keeps it once it's saved")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Material))]
    public LabURI Material
    {
        get => _material;
        set
        {
            _material = value;
            var assets = AssetManager.Get();
            foreach (var use in _uses.Where(use => assets.DoesAssetExist(use.Model)))
            {
                var model = assets.GetAsset(use.Model);
                switch (use.Kind)
                {
                    case Mesh.Rigid:
                        model.GetData<RigidModelData>().Materials[use.Part] = value;
                        break;
                    case Mesh.Skin:
                        model.GetData<SkinData>().SubSkins[use.Part].Material = value;
                        break;
                    case Mesh.BlendSkin:
                        model.GetData<BlendSkinData>().Blends[use.Part].Material = value;
                        break;
                }
            }
        }
    }

    [Editable(Caption = "Drawn By", Hint = "The parts of the model's meshes that draw with the material")]
    [EditorReadOnly]
    public string Parts => string.Join(", ", _uses.GroupBy(use => (use.Kind, use.Index)).Select(mesh =>
    {
        var parts = mesh.Select(use => use.Part).ToList();
        var name = mesh.Key.Kind switch
        {
            Mesh.Skin => "the skin",
            Mesh.BlendSkin => "the blend skin",
            _ => $"rigid model {mesh.Key.Index}",
        };
        return parts.Count == 1 ? $"{name}'s part {parts[0]}" : $"{name}'s parts {string.Join(", ", parts.SkipLast(1))} and {parts[^1]}";
    }));

    /// <summary>
    /// A slot for every material the OGI's meshes draw with, in the order they're first drawn with: the rigid models', the skin's, the blend
    /// skin's
    /// </summary>
    internal static OgiMaterialSlot[] Of(OGIData ogi)
    {
        var assets = AssetManager.Get();
        var uses = new List<(LabURI Material, Use Use)>();
        for (var index = 0; index < ogi.RigidModelIds.Count; index++)
        {
            var model = ogi.RigidModelIds[index];
            if (model == LabURI.Empty || !assets.DoesAssetExist(model))
            {
                continue;
            }

            var materials = assets.GetAsset(model).GetData<RigidModelData>().Materials;
            uses.AddRange(materials.Select((material, part) => (material, new Use(model, Mesh.Rigid, index, part))));
        }

        if (ogi.Skin != LabURI.Empty && assets.DoesAssetExist(ogi.Skin))
        {
            uses.AddRange(assets.GetAsset(ogi.Skin).GetData<SkinData>().SubSkins.Select((part, index) => (part.Material, new Use(ogi.Skin, Mesh.Skin, 0, index))));
        }

        if (ogi.BlendSkin != LabURI.Empty && assets.DoesAssetExist(ogi.BlendSkin))
        {
            uses.AddRange(assets.GetAsset(ogi.BlendSkin).GetData<BlendSkinData>().Blends.Select((part, index) => (part.Material, new Use(ogi.BlendSkin, Mesh.BlendSkin, 0, index))));
        }

        return uses.GroupBy(use => use.Material).Select(material => new OgiMaterialSlot(material.Key, material.Select(use => use.Use).ToList())).ToArray();
    }
}
