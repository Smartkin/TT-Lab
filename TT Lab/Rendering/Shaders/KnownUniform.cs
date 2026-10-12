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
    MaterialUseTexture,
    MaterialDeformMode,
    MaterialDeformSpeed,
    MaterialDeformAmplitude,
    MaterialBillboardRender,
    MaterialUvOffset,
    MaterialReflectDist,
    MaterialAlphaMethod,
    MaterialAlphaReference,
    MaterialAlphaFailDraws,
    MaterialAlphaBlend,
    MaterialEnvMap,
    MaterialMipmapped,
    MaterialLodScale,
    MaterialLodK,
    MaterialFog,
    MaterialEditorShading,
    MaterialLit,
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
            names[(int)uniform] = uniform switch
            {
                KnownUniform.MaterialUseTexture => TwinMaterialDesc.UseTexturePath,
                KnownUniform.MaterialDeformMode => TwinMaterialDesc.DeformModePath,
                KnownUniform.MaterialDeformSpeed => TwinMaterialDesc.DeformSpeedPath,
                KnownUniform.MaterialDeformAmplitude => TwinMaterialDesc.DeformAmplitudePath,
                KnownUniform.MaterialBillboardRender => TwinMaterialDesc.BillboardRenderPath,
                KnownUniform.MaterialUvOffset => TwinMaterialDesc.UvOffsetPath,
                KnownUniform.MaterialReflectDist => TwinMaterialDesc.ReflectDistPath,
                KnownUniform.MaterialAlphaMethod => TwinMaterialDesc.AlphaMethodPath,
                KnownUniform.MaterialAlphaReference => TwinMaterialDesc.AlphaReferencePath,
                KnownUniform.MaterialAlphaFailDraws => TwinMaterialDesc.AlphaFailDrawsPath,
                KnownUniform.MaterialAlphaBlend => TwinMaterialDesc.AlphaBlendPath,
                KnownUniform.MaterialEnvMap => TwinMaterialDesc.EnvMapPath,
                KnownUniform.MaterialMipmapped => TwinMaterialDesc.MipmappedPath,
                KnownUniform.MaterialLodScale => TwinMaterialDesc.LodScalePath,
                KnownUniform.MaterialLodK => TwinMaterialDesc.LodKPath,
                KnownUniform.MaterialFog => TwinMaterialDesc.FogPath,
                KnownUniform.MaterialEditorShading => TwinMaterialDesc.EditorShadingPath,
                KnownUniform.MaterialLit => TwinMaterialDesc.LitPath,
                _ => uniform.ToString()
            };
        }

        return names;
    }
}
