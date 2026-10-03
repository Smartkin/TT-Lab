using System;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.AssetData.Instance.Scenery;

/// <summary>
/// The lights new chunks get and new lights start as. The game's tools kept colors adding up to 1 and the brightness in the intensity:
/// every retail scenery has one ambient light of a third grey at 3 to 6, the directional lights are about as strong
/// </summary>
public static class DefaultLights
{
    public const Single ThirdGrey = 1.0f / 3.0f;
    public const Single AmbientIntensity = 4.5f;
    public const Single Intensity = 3.0f;

    private static Vector4 Grey => new(ThirdGrey, ThirdGrey, ThirdGrey, 0.0f);

    public static AmbientLight Ambient()
    {
        var light = new AmbientLight { Color = Grey, Intensity = AmbientIntensity, Position = new Vector4(0.0f, 5.0f, 0.0f, 1.0f) };
        light.ComputeBounds();
        return light;
    }

    /// <summary>
    /// White light from above, a little from the side so the faces of a box aren't all lit alike
    /// </summary>
    public static DirectionalLight Directional()
    {
        var direction = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0.3f, 1.0f, 0.3f));
        var light = new DirectionalLight
        {
            Color = Grey,
            Intensity = Intensity,
            Position = new Vector4(0.0f, 10.0f, 0.0f, 1.0f),
            Direction = new Vector4(direction.X, direction.Y, direction.Z, 0.0f)
        };
        light.ComputeBounds();
        return light;
    }

    public static PointLight Point()
    {
        var light = new PointLight { Color = Grey, Intensity = Intensity, Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f), AttenuationPower = 1 };
        light.ComputeBounds();
        return light;
    }

    /// <summary>
    /// Shining down in a cone of 60 degrees that fades out over 15 more
    /// </summary>
    public static SpotLight Spot()
    {
        var light = new SpotLight
        {
            Color = Grey,
            Intensity = Intensity,
            Position = new Vector4(0.0f, 0.0f, 0.0f, 1.0f),
            Direction = new Vector4(0.0f, -1.0f, 0.0f, 0.0f),
            AttenuationPower = 1
        };
        light.SetCone(60.0f, 15.0f);
        light.ComputeBounds();
        return light;
    }
}
