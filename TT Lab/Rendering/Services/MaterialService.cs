using System.Collections.Generic;
using System.Diagnostics;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Materials;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Services;

public class MaterialService
{
    private readonly RenderContext _renderContext;
    private readonly MaterialFactory _materialFactory;
    private readonly Dictionary<string, Dictionary<LabShader, TwinMaterial>> _materials = [];

    public MaterialService(RenderContext renderContext, MaterialFactory materialFactory)
    {
        _renderContext = renderContext;
        _materialFactory = materialFactory;
        
        RegisterMaterial(LabURI.EmptyMaterial, MaterialData.GetEmptyMaterial());
    }

    public Dictionary<LabShader, TwinMaterial>? GetMaterial(LabURI material)
    {
        if (_materials.TryGetValue(material, out var materials))
        {
            return materials;
        }

        if (material == LabURI.Empty)
        {
            return null;
        }
        
        return RegisterMaterial(material, AssetManager.Get().GetAssetData<MaterialData>(material));
    }

    private Dictionary<LabShader, TwinMaterial> RegisterMaterial(LabURI materialUri, MaterialData materialData)
    {
        Debug.Assert(!_materials.ContainsKey(materialUri), $"Given material {materialUri} already registered");
        
        _materials[materialUri] = new Dictionary<LabShader, TwinMaterial>();
        foreach (var shader in materialData.Shaders)
        {
            _materials[materialUri][shader] = new TwinMaterial(_renderContext, _materialFactory.GetTwinMaterialFromShader(shader));
        }
        
        return _materials[materialUri];
    }
}