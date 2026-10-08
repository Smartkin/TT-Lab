#include "Includes/ModelLayout.frag"

const float FOG_CAMERA_MAX_DIST = 300.0;

uniform TwinMaterial twin_material;

void main()
{
    vec4 Diffuse = InstanceColor;
    vec2 screenUvs = gl_FragCoord.xy / Resolution;
    vec2 uvs = twin_material.uv_scroll_speed * vec2(Time) + twin_material.uv_offset + Texpos;
    uvs.y = mix(uvs.y, 1.0 - uvs.y, FlipY);
    vec4 textureColor = twin_material.use_texture > 0.5 ? texture(Texture[0], uvs) : vec4(1.0);
    vec3 resultColor = textureColor.rgb * Color.rgb;
    float resultAlpha = textureColor.a * Color.a;
    if (twin_material.reflect_dist.x > 0.0)
    {
        vec2 reflectUv = vec2(screenUvs.x + twin_material.reflect_dist.y, screenUvs.y + twin_material.reflect_dist.y);
        vec4 screenColorReflected = texture(Screen, reflectUv);
        resultColor = mix(resultColor, screenColorReflected.rgb * Color.rgb * resultColor, twin_material.reflect_dist.x);
    }

    vec3 eyeDirection = normalize(EyePosition - ViewPosition);
    // The editor's flat shading goes by the triangle's own normal, taken before the alpha test can discard fragments of the quad
    vec3 faceNormal = twin_material.editor_shading > 0.0 ? normalize(cross(dFdx(ViewPosition), dFdy(ViewPosition))) : vec3(0.0);

    // The picture read where the game's VU1 program reads it instead of at the UVs (EnvUv), scrolled like the UVs
    if (twin_material.env_map > 0.0)
    {
        vec4 panoramaTexture = texture(Texture[0], EnvUv + twin_material.uv_scroll_speed * vec2(Time));
        resultColor = panoramaTexture.rgb * Color.rgb;
        resultAlpha = mix(1.0, panoramaTexture.a * Color.a, twin_material.alpha_blend);
    }

    if (resultAlpha < twin_material.alpha_test)
    {
        discard;
        return;
    }

    vec4 resultBlend = vec4(resultColor, mix(1.0, resultAlpha, twin_material.alpha_blend));
    resultBlend.rgb *= Diffuse.rgb * twin_material.animated_color.rgb;
    resultBlend.a *= twin_material.animated_color.a;
    resultBlend.a = mix(resultBlend.a, resultBlend.a * Diffuse.a, twin_material.alpha_blend);

    // Flat by the triangle the fragment is on, lit from above and from the eye: collision's triangles face either way
    if (twin_material.editor_shading > 0.0)
    {
        float key = abs(dot(faceNormal, normalize(vec3(0.35, 1.0, 0.55))));
        float head = abs(dot(faceNormal, eyeDirection));
        resultBlend.rgb *= 0.3 + 0.45 * key + 0.25 * head;
    }

    // Fog
    float cameraDistance = distance(EyePosition, ViewPosition);
    float fogPower = 0.4 * (1.0 - exp(-(cameraDistance / FOG_CAMERA_MAX_DIST)));
    resultBlend.rgb = mix(resultBlend.rgb, FogColor, fogPower);
    outColor = mix(resultBlend, Diffuse, DiffuseOnly);
}
