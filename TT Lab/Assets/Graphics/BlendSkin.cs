using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics;

[SupportsViewport]
public class BlendSkin : SerializableAsset
{
    protected override bool SetIdFromDataHash => true;
    protected override String DataExt => ".glb";
    public override UInt32 Section => Constants.GRAPHICS_BLEND_SKINS_SECTION;
    public override String IconPath => "Blend_Skin.png";

    public BlendSkin() { }

    public BlendSkin(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinBlendSkin blendSkin) : base(id, name, package, needVariant, variant)
    {
        AssetData = new BlendSkinData(this, blendSkin);
    }

    public override Type GetEditorType()
    {
        return typeof(BlendSkinViewModel);
    }

    public override void PostResolveResources(Factory.ITwinItemFactory factory, ITwinSection section, ITwinItem? item)
    {
        if (item == null)
        {
            return;
        }

        var data = (BlendSkinData)GetData();
        var blendSkinItem = (ITwinBlendSkin)item;
        if (data.CompileScale != null)
        {
            blendSkinItem.CompileScale = data.CompileScale.Value;
        }

        base.PostResolveResources(factory, section, item);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new BlendSkinData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}