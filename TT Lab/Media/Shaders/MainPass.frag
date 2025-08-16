#include "Includes/ModelLayout.frag"
#include "lygia/lighting/shadingData/shadingData.glsl"
#include "lygia/lighting/specular/gaussian.glsl"
#include "lygia/lighting/sphereMap.glsl"

uniform TwinMaterial twin_material;

void main()
{
    vec2 screenUvs = gl_FragCoord.xy / Resolution;
    vec2 uvs = twin_material.uv_scroll_speed * vec2(Time) + Texpos;
    vec4 textureColor = mix(vec4(1.0), texture(Texture[0], uvs), twin_material.use_texture);
    vec3 resultColor = textureColor.rgb * Color.rgb;
    float resultAlpha = textureColor.a * Color.a;
    vec4 screenColor = texture(Screen, screenUvs);
    vec2 reflectUv = vec2(screenUvs.x + twin_material.reflect_dist.y, screenUvs.y + twin_material.reflect_dist.y);
    vec4 screenColorReflected = texture(Screen, reflectUv);
    resultColor = mix(resultColor, screenColorReflected.rgb * Color.rgb * resultColor, twin_material.reflect_dist.x);

    vec3 surfaceNormal = normalize(Normal);
//    if (!gl_FrontFacing)
//    {
//        surfaceNormal = -surfaceNormal;
//    }
    vec3 eyeDirection = normalize(EyePosition - ViewPosition);
    
    // We have 2 ways either sphere mapping or doing the function BetaM wrote. I personally like doing sphere mapping :^)
    vec4 panoramaTexture = texture(Texture[0], sphereMap(surfaceNormal, EyePosition)); //texturePanorama(normalize(eyeDirection * vec3(-1, -1, 1)), Texture[0]);
    vec3 envMapColor = panoramaTexture.rgb * Color.rgb;
    float envMapAlpha = mix(1.0, panoramaTexture.a * Color.a, twin_material.alpha_blend);
    resultColor = mix(resultColor, envMapColor, twin_material.env_map);
    resultAlpha = mix(resultAlpha, envMapAlpha, twin_material.env_map);
    
    if (resultAlpha < twin_material.alpha_test)
    {
        discard;
        return;
    }
    
    float diffuse = max(dot(surfaceNormal, eyeDirection), 0.0);
    float specular = mix(0.0, specularGaussian(diffuse, 20.0), twin_material.metalic_specular);
    vec4 resultBlend = vec4(resultColor, mix(1.0, resultAlpha, twin_material.alpha_blend));
    resultBlend.rgb *= mix(vec3(1.0), Color.rgb * (specular + diffuse), twin_material.metalic_specular);
    resultBlend.rgb *= Diffuse.rgb;
    resultBlend.a = mix(resultBlend.a, resultBlend.a * Diffuse.a, twin_material.alpha_blend);
    outColor = resultBlend;
}