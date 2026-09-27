in vec4 vColor;
flat in vec2 vStart;
flat in vec2 vEnd;
flat in float vHalfWidth;

uniform float Opacity = 1.0;

layout (location = 0) out vec4 outColor;

void main()
{
    // Distance to the segment instead of to its infinite line gives the ends round caps, which also closes the gaps at polyline joints
    vec2 toFragment = gl_FragCoord.xy - vStart;
    vec2 segment = vEnd - vStart;
    float along = clamp(dot(toFragment, segment) / max(dot(segment, segment), 0.0001), 0.0, 1.0);
    float distanceToLine = length(toFragment - segment * along);
    float coverage = clamp(vHalfWidth + 0.5 - distanceToLine, 0.0, 1.0);
    if (coverage <= 0.0)
    {
        discard;
    }

    outColor = vec4(vColor.rgb, vColor.a * coverage * Opacity);
}
