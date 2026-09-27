// Lines get expanded into screen facing quads here, which makes their width independent from the distance to the camera

struct LineData
{
    // Width in pixels is stored in Start.w
    vec4 Start;
    vec4 End;
    vec4 StartColor;
    vec4 EndColor;
};

layout (std430, binding = 1) readonly buffer Lines
{
    LineData lines[];
};

uniform mat4 StartView;
uniform mat4 StartProjection;
uniform vec2 ViewportSize;
// Fraction of the distance to the camera lines get pulled closer by so they don't flicker on surfaces they lie on
uniform float DepthBias = 0.0;

out vec4 vColor;
flat out vec2 vStart;
flat out vec2 vEnd;
flat out float vHalfWidth;

// x picks the end of the line, y the side of it
const vec2 Corners[6] = vec2[6](vec2(0.0, -1.0), vec2(1.0, -1.0), vec2(1.0, 1.0), vec2(0.0, -1.0), vec2(1.0, 1.0), vec2(0.0, 1.0));

vec4 ToClip(vec3 position)
{
    vec4 viewPosition = StartView * vec4(position, 1.0);
    viewPosition.xyz *= 1.0 - DepthBias;
    return StartProjection * viewPosition;
}

void main()
{
    LineData line = lines[gl_BaseInstance + gl_InstanceID];
    vec4 clipStart = ToClip(line.Start.xyz);
    vec4 clipEnd = ToClip(line.End.xyz);

    // Screen positions of points behind the camera are meaningless, so the line gets cut at the near plane
    float nearStart = clipStart.z + clipStart.w;
    float nearEnd = clipEnd.z + clipEnd.w;
    if (nearStart < 0.0 && nearEnd < 0.0)
    {
        gl_Position = vec4(0.0, 0.0, -2.0, 1.0);
        vColor = vec4(0.0);
        vStart = vec2(0.0);
        vEnd = vec2(0.0);
        vHalfWidth = 0.0;
        return;
    }

    if (nearStart < 0.0)
    {
        clipStart = mix(clipStart, clipEnd, nearStart / (nearStart - nearEnd));
    }
    else if (nearEnd < 0.0)
    {
        clipEnd = mix(clipEnd, clipStart, nearEnd / (nearEnd - nearStart));
    }

    vec2 screenStart = (clipStart.xy / clipStart.w * 0.5 + 0.5) * ViewportSize;
    vec2 screenEnd = (clipEnd.xy / clipEnd.w * 0.5 + 0.5) * ViewportSize;
    vec2 direction = screenEnd - screenStart;
    float screenLength = length(direction);
    direction = screenLength > 0.0001 ? direction / screenLength : vec2(1.0, 0.0);
    vec2 normal = vec2(-direction.y, direction.x);
    float halfWidth = max(line.Start.w, 1.0) * 0.5;
    // A pixel more on every side for anti aliasing, extending past the ends leaves room for round caps
    float extent = halfWidth + 1.0;

    vec2 corner = Corners[gl_VertexID];
    vec4 clip = corner.x < 0.5 ? clipStart : clipEnd;
    vec2 screen = (corner.x < 0.5 ? screenStart : screenEnd) + normal * corner.y * extent + direction * (corner.x * 2.0 - 1.0) * extent;
    gl_Position = vec4((screen / ViewportSize * 2.0 - 1.0) * clip.w, clip.z, clip.w);
    vColor = corner.x < 0.5 ? line.StartColor : line.EndColor;
    vStart = screenStart;
    vEnd = screenEnd;
    vHalfWidth = halfWidth;
}
