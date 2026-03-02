using Newtonsoft.Json;
using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics;

public class Texture : SerializableAsset
{
    protected override bool SetIdFromDataHash => true;
    protected override String DataExt => ".png";
    public override UInt32 Section => Constants.GRAPHICS_TEXTURES_SECTION;
    public override String IconPath => "Texture.png";

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public ITwinTexture.TextureFunction TextureFunction { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public ITwinTexture.TexturePixelFormat PixelFormat { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Boolean GenerateMipmaps { get; set; }

    public Texture(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinTexture texture) : base(id, name, package, needVariant, variant)
    {
        AssetData = new TextureData(this, texture);
        Raw = false;
        TextureFunction = texture.TexFun;
        PixelFormat = texture.TextureFormat;
        GenerateMipmaps = texture.MipLevels > 1;
    }

    public Texture()
    {
    }

    public override Type GetEditorType()
    {
        return typeof(TextureViewModel);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new TextureData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}