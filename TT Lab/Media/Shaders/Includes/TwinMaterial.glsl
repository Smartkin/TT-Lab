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

    return textureLod(pano, st, 0.0); // No mipmaps (LOD = 0)
}

#ifndef TWIN_MATERIAL
#define TWIN_MATERIAL

struct TwinMaterial {
    float use_texture; // 0 is off, 1 is on
    vec2 deform_speed;
    float billboard_render;
    float double_color;
    vec2 uv_scroll_speed;
    vec2 reflect_dist; // x is 1 or 0 for enabled/disabled, y is for actual distance
    float alpha_test;
    float alpha_blend; // 0 is off, 1 is on
    float metalic_specular;
    float env_map; // 0 is off, 1 is on
    int blend_func;
};

#endif