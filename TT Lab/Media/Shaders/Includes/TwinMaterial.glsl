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
    bool two_sided_lighting;
    float perform_fog; // 0 is off, 1 is on
    float use_texture; // 0 is off, 1 is on
    // Cloth deformation: the mode, how fast the phases advance and how far each axis moves
    int deform_mode;
    float deform_speed;
    vec3 deform_amplitude;
    float billboard_render;
    float double_color;
    vec2 uv_scroll_speed;
    vec2 uv_offset; // the shader animation's U and V tracks
    vec4 animated_color; // the shader animation's color tracks, white without
    vec2 reflect_dist; // x is 1 or 0 for enabled/disabled, y is for actual distance
    float alpha_test;
    float alpha_blend; // 0 is off, 1 is on
    float env_map; // 0 is off, 1 reads the picture like the environment maps, 2 like the metallic shader
    int blend_func;
    float editor_shading; // 0 is off, 1 shades flat by the triangles' facing (collision)
    float lit; // 0 is off, 1 lights the vertex colors with the object's lights
};

#endif