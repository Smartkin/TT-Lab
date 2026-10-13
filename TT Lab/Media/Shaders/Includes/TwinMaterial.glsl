const int MAX_BONES = 64;
const int MAX_BLENDS = 15;
const int MAX_TEXTURES = 5;

vec4 texturePanorama(vec3 normal, sampler2D pano)
{
    vec2 st;
    st.x = atan(normal.x, normal.z); // Azimuth
    st.y = acos(normal.y);

    if (st.x < 0.0)
    {
        st.x += 6.2831853; // 2 * PI
    }
    st /= vec2(6.2831853, 3.1415926); // Normalize to [0,1]

    return texture(pano, st);
}

#ifndef TWIN_MATERIAL
#define TWIN_MATERIAL

struct TwinMaterial {
    float use_texture; // 0 is off, 1 is on
    // Cloth deformation: the mode, how fast the phases advance and how far each axis moves
    int deform_mode;
    float deform_speed;
    vec3 deform_amplitude;
    vec2 uv_offset; // the scrolls' and the shader animation's U and V (ShaderScroll), added to the UVs and the environment maps' lookups
    vec2 reflect_dist; // x is 1 or 0 for enabled/disabled, y is for actual distance
    // The GS's alpha test (GsAlphaTest): its method, -1 for none, the reference byte, and whether a draw of every fragment keeps the failing
    // ones (they write what the passing ones do, AlphaFailDraw.WithPassing)
    int alpha_method;
    float alpha_reference;
    float alpha_fail_draws;
    float alpha_blend; // 0 is off, 1 is on
    float env_map; // 0 is off, 1 reads the picture like the environment maps, 2 like the metallic shader
    // The GS's mip choice (GsTextureFiltering): whether the texture is minified through its mips, by the distance's log2 times 2^L plus K
    float mipmapped;
    float lod_scale;
    float lod_k;
    int fog; // which of the game's fogs go over it (GameFog): 0 neither, 1 the Xbox's, 2 both
    float editor_shading; // 0 is off, 1 shades flat by the triangles' facing (collision)
    float lit; // 0 is off, 1 lights the vertex colors with the object's lights
};

// A material as the context's MaterialTable keeps it (MaterialTable.Key.Write)
struct TwinMaterialEntry {
    vec4 deform; // the deformation's amplitude per axis and its speed
    vec4 offsets; // the UV offset and the reflection's
    ivec4 modes; // the deformation's mode, the alpha test's method and the fog
    vec4 alpha; // the alpha test's reference, whether failing fragments draw, alpha blending and the environment map
    vec4 sampling; // texture mapping, mips, the mip level's scale and K
    vec4 shading; // nothing, the editor's flat shading and lit
};

layout (std430, binding = 6) readonly buffer TwinMaterials
{
    TwinMaterialEntry twinMaterials[];
};

// The entry of the material drawn
uniform int MaterialEntry;

// An indirect draw's material entry and its texture's bindless handle (IndirectDraws), read at DrawBase + gl_DrawID
struct DrawEntry {
    int material;
    int unused;
    uvec2 texture;
};

layout (std430, binding = 7) readonly buffer DrawEntries
{
    DrawEntry drawEntries[];
};

// Where an indirect draw's entries start, -1 for a draw of one batch: it draws with MaterialEntry and the texture on unit 0
uniform int DrawBase = -1;

TwinMaterial LoadTwinMaterial(int entry)
{
    TwinMaterialEntry values = twinMaterials[entry];
    TwinMaterial material;
    material.use_texture = values.sampling.x;
    material.deform_mode = values.modes.x;
    material.deform_speed = values.deform.w;
    material.deform_amplitude = values.deform.xyz;
    material.uv_offset = values.offsets.xy;
    material.reflect_dist = values.offsets.zw;
    material.alpha_method = values.modes.y;
    material.alpha_reference = values.alpha.x;
    material.alpha_fail_draws = values.alpha.y;
    material.alpha_blend = values.alpha.z;
    material.env_map = values.alpha.w;
    material.mipmapped = values.sampling.y;
    material.lod_scale = values.sampling.z;
    material.lod_k = values.sampling.w;
    material.fog = values.modes.z;
    material.editor_shading = values.shading.y;
    material.lit = values.shading.z;
    return material;
}

#endif