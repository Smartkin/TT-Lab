#include "../Includes/ShadingLibrary.frag"

uniform vec2 Speed;
uniform bool Reflects = false;
uniform float ReflectDist = 0.15;
uniform bool EnvMap;

vec4 ShadeFragment(vec3 texCoord, vec4 col, vec3 diffuse, vec3 eyespaceNormal)
{
    vec2 uvs = Speed * vec2(Time) + texCoord.st;
    vec4 color = texture(Texture[0], uvs);

    if (Reflects)
    {
        vec4 current_texture = texture(Screen, gl_FragCoord.xy);
        color.rgb = mix(color.rgb, current_texture.rgb, 0.5);
        color.w *= 0.85;
    }

    if (EnvMap)
    {
        vec4 ndcPos;
        ndcPos.xy = ((2.0 * gl_FragCoord.xy) - (2.0 * vec2(0.0, 0.0))) / (Resolution.xy) - 1.0;
        ndcPos.z = (2.0 * gl_FragCoord.z - gl_DepthRange.near - gl_DepthRange.far) /
            (gl_DepthRange.far - gl_DepthRange.near);
        ndcPos.w = 1.0;
        vec4 albedo_tex = textureProj(Texture[0], inverse(Projection) * View * ndcPos);
        color.rgb = albedo_tex.rgb;
    }
#ifdef USE_ALPHA_BLENDING
    color *= Opacity;
#endif

    return color;
}