using System;
using TT_Lab.Rendering.UniformDescs;

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
    MaterialPerformFog,
    MaterialUseTexture,
    MaterialDoubleColor,
    MaterialDeformSpeed,
    MaterialBillboardRender,
    MaterialUvScrollSpeed,
    MaterialReflectDist,
    MaterialAlphaTest,
    MaterialAlphaBlend,
    MaterialMetalicSpecular,
    MaterialEnvMap,
    ViewProjection,
    ViewportSize,
    LightDirection,
    Opacity,
    DepthBias,
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
            names[(int)uniform] = uniform switch
            {
                KnownUniform.MaterialPerformFog => TwinMaterialDesc.PerformFogPath,
                KnownUniform.MaterialUseTexture => TwinMaterialDesc.UseTexturePath,
                KnownUniform.MaterialDoubleColor => TwinMaterialDesc.DoubleColorPath,
                KnownUniform.MaterialDeformSpeed => TwinMaterialDesc.DeformSpeedPath,
                KnownUniform.MaterialBillboardRender => TwinMaterialDesc.BillboardRenderPath,
                KnownUniform.MaterialUvScrollSpeed => TwinMaterialDesc.UvScrollSpeedPath,
                KnownUniform.MaterialReflectDist => TwinMaterialDesc.ReflectDistPath,
                KnownUniform.MaterialAlphaTest => TwinMaterialDesc.AlphaTestPath,
                KnownUniform.MaterialAlphaBlend => TwinMaterialDesc.AlphaBlendPath,
                KnownUniform.MaterialMetalicSpecular => TwinMaterialDesc.MetalicSpecularPath,
                KnownUniform.MaterialEnvMap => TwinMaterialDesc.EnvMapPath,
                _ => uniform.ToString()
            };
        }

        return names;
    }
}
