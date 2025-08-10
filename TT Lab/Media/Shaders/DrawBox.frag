#include "Includes/GlobalUniformsDeclaration.glsl"

in vec2 vUV;
out vec4 FragColor;

uniform mat4 StartProjection;
uniform mat4 StartView;
uniform mat4 invProjection;
uniform mat4 invView;
uniform vec3 boxCenter;
uniform vec3 boxSize;

void main() {
    // Step 1: Build ray direction in view space
    float tanHalfFovY = tan(uFovY * 0.5);
    vec3 rayDirVS = normalize(vec3(
                              vUV.x * uAspect * tanHalfFovY,
                              vUV.y * tanHalfFovY,
                              -1.0
                              ));

    // Step 2: Transform sphere + light to view space
    vec3 boxCenterVS = vec3(StartView * vec4(boxCenter, 1.0));
    vec3 lightDirVS = normalize(vec3(StartView * vec4(EyeDirection, 0.0)));

    vec3 rayOriginVS = vec3(0.0);
    
    vec4 ndc = vec4(vUV, 0.0, 1.0);
    vec4 viewDir4 = invProjection * ndc;
    viewDir4 /= viewDir4.w;
    vec3 rayDir = normalize((invView * vec4(viewDir4.xyz, 0.0)).xyz);

    vec3 minB = boxCenterVS - boxSize * 0.5;
    vec3 maxB = boxCenterVS + boxSize * 0.5;

    vec3 invDir = 1.0 / rayDir;
    vec3 t0s = (minB - EyePosition) * invDir;
    vec3 t1s = (maxB - EyePosition) * invDir;
    vec3 tsmaller = min(t0s, t1s);
    vec3 tbigger  = max(t0s, t1s);

    float tmin = max(max(tsmaller.x, tsmaller.y), tsmaller.z);
    float tmax = min(min(tbigger.x, tbigger.y), tbigger.z);

    if (tmax < 0.0 || tmin > tmax) discard;

    float t = (tmin > 0.0) ? tmin : tmax;
    vec3 hitPos = EyePosition + t * rayDir;

    vec4 clipPos = (invView * vec4(hitPos, 1.0));
    clipPos = StartProjection * clipPos;
    clipPos /= clipPos.w;
    gl_FragDepth = clipPos.z * 0.5 + 0.5;

    FragColor = vec4(vec3(0.5, 1.0, 0.5), 1.0);
}