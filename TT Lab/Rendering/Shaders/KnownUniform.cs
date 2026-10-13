using System;

namespace TT_Lab.Rendering.Shaders;

/// <summary>
/// Uniforms set every frame or every draw, their locations get resolved once when a program gets linked
/// </summary>
public enum KnownUniform
{
    StartProjection,
    StartView,
    InverseView,
    EyePosition,
    EyeDirection,
    Fov,
    Aspect,
    FogColor,
    FogMode,
    Time,
    Resolution,
    FlipY,
    DiffuseOnly,
    UseSkinning,
    UseMorphs,
    BoneMatrices,
    BlendShape,
    BlendShapesAmount,
    ShapeOffset,
    ShapeStart,
    MorphWeights,
    MaterialEntry,
    DrawBase,
    AlphaSubpass,
    ViewProjection,
    ViewportSize,
    LightDirection,
    GameClipScale,
    Opacity,
    DepthBias,
    AlphaAsColor,
    Distortion,
    FollowsCamera,
}

public static class KnownUniforms
{
    private static readonly string[] Names = CreateNames();

    public static string GetName(KnownUniform uniform) => Names[(int)uniform];

    public static int Count => Names.Length;

    private static string[] CreateNames()
    {
        var names = new string[Enum.GetValues<KnownUniform>().Length];
        foreach (var uniform in Enum.GetValues<KnownUniform>())
        {
            names[(int)uniform] = uniform.ToString();
        }

        return names;
    }
}
