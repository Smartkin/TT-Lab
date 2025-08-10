using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Assets.Instance;

public class CollisionSurface : SerializableInstance
{
    public static readonly Color[] DefaultColors =
    [
        new(192,192,192,255),
        new(  0,  0,192,255),
        new(  0,  0,127,255),
        new(255, 96,  0,255),
        new(255,  0,  0,127),
        new(  0,  0,  0,127),
        new(  0,255,  0,255),
        new( 96, 96,127,255),
        new( 64, 32,  0,255),
        new( 96, 96, 96,255),
        new(192,192,  0,255),
        new( 32, 16,  0,255),
        new(  0,  0,255, 255),
        new( 32, 32, 32,255),
        new( 32, 32, 64,255),
        new(230,230,255,255),
        new(200,200,255,255),
        new( 32, 32,192,255),
        new(192,192,192,255),
        new(  0,255,  0,255),
        new(  0,127,  0, 255),
        new( 32, 32, 32,255),
        new( 64, 64,127,255),
        new(  0,  0,255,255),
        new(255,  0,  0,255),
        new(127,127,192,255),
        new(  0,  0,127,255),
        new(255,  0,255,255)
    ];
    public static readonly Color DefaultColor = new(127, 127, 127);

    public override UInt32 Section => Constants.LAYOUT_SURFACES_SECTION;
    public override String IconPath => "Collision_Surface.png";

    public CollisionSurface(LabURI package, UInt32 id, String name, String chunk, Int32 layId, ITwinSurface surface) : base(package, id, name, chunk, layId)
    {
        assetData = new CollisionSurfaceData(surface);
        if (id < DefaultColors.Length)
        {
            Parameters.Add("editor_surface_color", DefaultColors[id]);
        }
        else
        {
            Parameters.Add("editor_surface_color", DefaultColors);
        }
    }

    public CollisionSurface()
    {
    }

    public override Type GetEditorType()
    {
        return typeof(CollisionSurfaceViewModel);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || assetData.Disposed)
        {
            assetData = new CollisionSurfaceData();
            assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            IsLoaded = true;
        }
        return assetData;
    }

    protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new InstanceElementGenericViewModel<CollisionSurface>(URI, parent);
    }
}