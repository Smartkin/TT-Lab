#include "Includes/GlobalUniformsDeclaration.glsl"

in vec2 vUV;
out vec4 FragColor;

uniform vec3 SpherePosition; // Sphere center in world space
uniform float SphereRadius;

uniform vec4 Diffuse;
uniform mat4 StartProjection;
uniform mat4 StartView;

void main() {
    // Step 1: Build ray direction in view space
    float tanHalfFovY = tan(uFovY * 0.5);
    vec3 rayDirVS = normalize(vec3(
                              vUV.x * uAspect * tanHalfFovY,
                              vUV.y * tanHalfFovY,
                              -1.0
                              ));

    // Step 2: Transform sphere + light to view space
    vec3 sphereCenterVS = vec3(StartView * vec4(SpherePosition, 1.0));
    vec3 lightDirVS = normalize(vec3(StartView * vec4(EyeDirection, 0.0)));

    vec3 rayOriginVS = vec3(0.0);

    // Step 3: Analytic ray-sphere intersection
    vec3 oc = rayOriginVS - sphereCenterVS;
    float b = dot(oc, rayDirVS);
    float c = dot(oc, oc) - SphereRadius * SphereRadius;
    float h = b * b - c;

    if (h < 0.0) discard; // no hit

    h = sqrt(h);
    float t = -b - h;
    if (t < 0.0) t = -b + h;
    if (t < 0.0) discard;

    // Step 4: Intersection point in view space
    vec3 pVS = rayOriginVS + rayDirVS * t;
    vec3 normalVS = normalize(pVS - sphereCenterVS);

    // Step 5: Lighting
    float diff = max(dot(normalVS, lightDirVS), 0.0);
    FragColor = vec4(Diffuse.rgb * vec3(diff), Diffuse.a);

    // Step 6: Depth output
    // Convert view-space point to clip space
    vec4 clipPos = StartProjection * vec4(pVS, 1.0);
    float ndcDepth = clipPos.z / clipPos.w; // NDC z in [-1,1]
    gl_FragDepth = ndcDepth * 0.5 + 0.5;    // Convert to [0,1] for depth buffer
}