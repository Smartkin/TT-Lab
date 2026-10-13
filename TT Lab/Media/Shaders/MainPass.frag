#include "Includes/ModelLayout.frag"

// The drawn material's values, read from the material table first thing
TwinMaterial twin_material;

// The lens every camera of the game has (GameFog)
const float GAME_NEAR = 0.1;
const float GAME_FAR = 1500.0;

// The GS's mip level (GsTextureFiltering): log2 of the clip w times 2^L plus K, magnified up to 0, the nearest level past it (the sampler's
// filter), at most the texture's last. Types that don't minify through mips always draw its largest level
vec4 SampleTexture(vec2 uv)
{
    float level = twin_material.mipmapped > 0.5 ? log2(max(ViewDepth, 1.0e-6)) * twin_material.lod_scale + twin_material.lod_k : 0.0;
#ifdef TT_BINDLESS
    // An indirect draw's texture with the material's sampler, through its handle
    if (DrawTexture != uvec2(0))
    {
        return textureLod(sampler2D(DrawTexture), uv, level);
    }
#endif
    return textureLod(Texture[0], uv, level);
}

// The GS's alpha test (GsAlphaTest): the fragment's alpha as the GS has it, 127 times TT Lab's rounded down, against the reference byte
bool AlphaPasses(float alpha)
{
    int method = twin_material.alpha_method;
    float value = floor(alpha * 127.0 + 0.001);
    float reference = twin_material.alpha_reference;
    switch (method)
    {
        case 0:
            return false;
        case 2:
            return value < reference;
        case 3:
            return value <= reference;
        case 4:
            return value == reference;
        case 5:
            return value >= reference;
        case 6:
            return value > reference;
        case 7:
            return value != reference;
        default:
            return true;
    }
}

// The chunk's fog at the fragment (GameFog): its color and how much of it goes in
vec4 FogAt(float depth)
{
    if (FogMode == 1)
    {
        // The PS2's depth of the fragment, whose bits 8 to 15 pick the table's entry where it's below 65536
        float w = max(depth, GAME_NEAR);
        float gameDepth = 8388607.0 * 2.0 * GAME_NEAR * (GAME_FAR - w) / (w * (GAME_FAR - GAME_NEAR));
        if (gameDepth >= 65536.0)
        {
            return vec4(0.0);
        }

        return texelFetch(FogTable, ivec2(clamp(int(gameDepth / 256.0), 0, 255), 0), 0);
    }

    if (FogMode == 2)
    {
        // The Xbox's clip depth times 0.02 on the ramp's 256 texels, clamped, the sky at its end
        float clipDepth = FollowsCamera ? 1.0e6 : depth * (GAME_FAR + GAME_NEAR) / (GAME_FAR - GAME_NEAR) - 2.0 * GAME_FAR * GAME_NEAR / (GAME_FAR - GAME_NEAR);
        return texelFetch(FogTable, ivec2(clamp(int(floor(clipDepth * 0.02 * 256.0)), 0, 255), 0), 0);
    }

    return vec4(0.0);
}

// The PS2's fog goes over the buckets 0 to 3, and over the sky and the background in a pass of its own, the Xbox's over every material
bool Fogged()
{
    if (FogMode == 1)
    {
        return twin_material.fog == 2 && !FollowsCamera;
    }

    return FogMode == 2 && twin_material.fog >= 1;
}

void main()
{
    twin_material = LoadTwinMaterial(DrawMaterial);
    vec4 Diffuse = InstanceColor;
    vec2 screenUvs = gl_FragCoord.xy / Resolution;
    vec2 uvs = twin_material.uv_offset + Texpos;
    uvs.y = mix(uvs.y, 1.0 - uvs.y, FlipY);
    vec4 textureColor = twin_material.use_texture > 0.5 ? SampleTexture(uvs) : vec4(1.0);
    vec3 resultColor = textureColor.rgb * Color.rgb;
    float resultAlpha = textureColor.a * Color.a;
    // The reflection surface draws the copy of the frame drawn so far (ScreenCopy), never its texture: at the pixel's place moved by the
    // normal on the screen, clamped, times its lit colors, 128 being 1 for the copy (it isn't halved like a texture). The offset is on the
    // game's screen axes: GL's x is the other way round (TT Lab shows its frame mirrored) and its rows go up where the game's go down
    if (twin_material.reflect_dist.x > 0.0)
    {
        vec2 reflectUv = clamp(screenUvs - ReflectOffset, 0.0, 1.0);
        resultColor = texture(Screen, reflectUv).rgb * Color.rgb * (255.0 / 128.0);
        resultAlpha = Color.a;
    }

    vec3 eyeDirection = normalize(EyePosition - ViewPosition);
    // The editor's flat shading goes by the triangle's own normal, taken before the alpha test can discard fragments of the quad
    vec3 faceNormal = twin_material.editor_shading > 0.0 ? normalize(cross(dFdx(ViewPosition), dFdy(ViewPosition))) : vec3(0.0);

    // The picture read where the game's VU1 program reads it instead of at the UVs (EnvUv, scrolled like the UVs)
    if (twin_material.env_map > 0.0)
    {
        vec4 panoramaTexture = SampleTexture(EnvUv);
        resultColor = panoramaTexture.rgb * Color.rgb;
        resultAlpha = mix(1.0, panoramaTexture.a * Color.a, twin_material.alpha_blend);
    }

    // What fails the test is left out, but where it writes what passing fragments do, or in the draw of the failing fragments of a material
    // whose failing fragments write something else (AlphaSubpass 2, the passing ones drawn before it)
    if (twin_material.alpha_method >= 0)
    {
        bool passes = AlphaPasses(resultAlpha);
        if (AlphaSubpass == 1 ? !passes : AlphaSubpass == 2 ? passes : !passes && twin_material.alpha_fail_draws < 0.5)
        {
            discard;
            return;
        }
    }

    vec4 resultBlend = vec4(resultColor, mix(1.0, resultAlpha, twin_material.alpha_blend));
    resultBlend.rgb *= Diffuse.rgb;
    resultBlend.a = mix(resultBlend.a, resultBlend.a * Diffuse.a, twin_material.alpha_blend);

    // Flat by the triangle the fragment is on, lit from above and from the eye: collision's triangles face either way
    if (twin_material.editor_shading > 0.0)
    {
        float key = abs(dot(faceNormal, normalize(vec3(0.35, 1.0, 0.55))));
        float head = abs(dot(faceNormal, eyeDirection));
        resultBlend.rgb *= 0.3 + 0.45 * key + 0.25 * head;
    }

    if (Fogged())
    {
        vec4 fog = FogAt(ViewDepth);
        resultBlend.rgb = mix(resultBlend.rgb, fog.rgb, fog.a);
    }

    outColor = mix(resultBlend, Diffuse, DiffuseOnly);
}
