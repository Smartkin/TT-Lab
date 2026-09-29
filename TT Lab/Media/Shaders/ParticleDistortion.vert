// The Distortion blend mode's hexagons (the game's VU1 program at 0x2e0960): six triangles around the particle, inside its quad and
// pointing along its height, drawn over a copy of the frame. The middle shows the copy where it is and has the particle's alpha, the
// rim's six corners have none and show the copy moved by their offset from the middle times the system's distortion, which fades out
// with the depth to nothing 40 units away
struct ParticleData
{
    vec4 PositionAngle;
    vec4 Size;
    vec4 Color;
    vec4 TextureRect;
};

layout (std430, binding = 4) readonly buffer Particles
{
    ParticleData particles[];
};

uniform mat4 StartView;
uniform mat4 StartProjection;
uniform mat4 InverseView;
uniform vec2 Distortion;

out vec2 vScreen;
out vec4 vColor;

const float Pi = 3.14159265;
const float FadeDepth = 40.0;

vec4 Project(vec3 world)
{
    return StartProjection * StartView * vec4(world, 1.0);
}

void main()
{
    ParticleData particle = particles[gl_BaseInstance + gl_InstanceID];
    int triangle = gl_VertexID / 3;
    int corner = gl_VertexID % 3;
    vec3 center = particle.PositionAngle.xyz;
    vec4 middle = Project(center);
    vColor = particle.Color;
    vec2 middleScreen = middle.xy / middle.w * 0.5 + 0.5;
    if (corner == 0)
    {
        gl_Position = middle;
        vScreen = middleScreen;
        return;
    }

    // The corners at 0, 60, ... 300 degrees, a quarter of the game's doubled width and height along its axes
    float turn = float((triangle + corner - 1) % 6) * Pi / 3.0;
    vec2 local = vec2(sin(turn) * particle.Size.x, cos(turn) * particle.Size.y) * 0.5;
    float c = cos(particle.PositionAngle.w);
    float s = sin(particle.PositionAngle.w);
    local = vec2(local.x * c - local.y * s, local.x * s + local.y * c);
    // Across the view like the game's, so every corner is at the middle's depth
    vec3 world = center + InverseView[0].xyz * local.x + InverseView[1].xyz * local.y;
    vec4 position = Project(world);
    gl_Position = position;
    // The game moves the corner's point of the copy by its offset times X / 16384 and -Y / 4096, in its screen units that's the offset
    // scaled by 1 - X * fade * depth / 2 sideways and 1 + 2 * Y * fade * depth up and down
    float fade = max(1.0 - middle.w / FadeDepth, 0.0);
    vec2 scale = vec2(1.0 - Distortion.x * fade * middle.w / 2.0, 1.0 + 2.0 * Distortion.y * fade * middle.w);
    vec2 screen = position.xy / position.w * 0.5 + 0.5;
    vScreen = middleScreen + (screen - middleScreen) * scale;
    vColor.a = 0.0;
}
