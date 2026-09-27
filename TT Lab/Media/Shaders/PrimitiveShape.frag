in vec3 vNormal;
in vec4 vColor;

uniform float Opacity = 1.0;

layout (location = 0) out vec4 outColor;

void main()
{
    // Lit from the camera, enough to make the shapes read as 3D without depending on anything in the scene
    float light = 0.55 + 0.45 * abs(normalize(vNormal).z);
    outColor = vec4(vColor.rgb * light, vColor.a * Opacity);
}
