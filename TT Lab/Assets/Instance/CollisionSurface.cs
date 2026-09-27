using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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
        AssetData = new CollisionSurfaceData(this, surface);
        Parameters.Add(EditorColorParameter, id < DefaultColors.Length ? DefaultColors[id] : GenerateColor(id));
    }

    public const string EditorColorParameter = "editor_surface_color";

    /// <summary>
    /// Color the editor and Blender show the surface's collision in
    /// </summary>
    public static Color GetEditorColor(IAsset? surface)
    {
        // Surfaces loaded from the project hold the color as JSON, freshly imported ones as the color itself
        return surface?.Parameters.GetValueOrDefault(EditorColorParameter) switch
        {
            Color color => color,
            JObject json => json.ToObject<Color>() ?? DefaultColor,
            _ => DefaultColor
        };
    }

    // Spreads the hues of surfaces past the default colors so they tell apart
    private static Color GenerateColor(UInt32 id)
    {
        var hue = id * 0.618034 % 1.0 * 6;
        var sector = (Int32)hue;
        var fraction = hue - sector;
        var (r, g, b) = sector switch
        {
            0 => (1.0, fraction, 0.0),
            1 => (1.0 - fraction, 1.0, 0.0),
            2 => (0.0, 1.0, fraction),
            3 => (0.0, 1.0 - fraction, 1.0),
            4 => (fraction, 0.0, 1.0),
            _ => (1.0, 0.0, 1.0 - fraction)
        };
        return new Color((Byte)(64 + r * 191), (Byte)(64 + g * 191), (Byte)(64 + b * 191));
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
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new CollisionSurfaceData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }

    protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new InstanceElementGenericViewModel<CollisionSurface>(URI, parent);
    }
}