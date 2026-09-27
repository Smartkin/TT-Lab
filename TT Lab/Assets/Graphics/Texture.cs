using Newtonsoft.Json;
using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Common;
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

    // The header has the texture's size twice. The game leaves the second one at 0 for Crash's textures every chunk has and for one of the
    // startup fonts, which likely means it doesn't reserve memory for them in the chunk
    [JsonProperty]
    public Boolean ReservesMemory { get; set; } = true;

    // Written back as read. The icons of PSM files have zeros where the textures of chunks have an address of the tools' memory
    [JsonProperty]
    public TwinTextureLeftovers? Leftovers { get; set; }

    public Texture(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinTexture texture) : base(id, name, package, needVariant, variant)
    {
        AssetData = new TextureData(this, texture);
        Raw = false;
        TextureFunction = texture.TexFun;
        PixelFormat = texture.TextureFormat;
        GenerateMipmaps = texture.MipLevels > 1;
        ReservesMemory = texture.TextureFormat is not (ITwinTexture.TexturePixelFormat.PSMT8 or ITwinTexture.TexturePixelFormat.PSMCT32)
            || texture.UnkBytes3 is not { Length: 2 } || texture.UnkBytes3[0] != 0 || texture.UnkBytes3[1] != 0;
        Leftovers = texture.Leftovers;
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