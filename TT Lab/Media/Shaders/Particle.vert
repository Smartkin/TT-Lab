// Every particle is a quad facing the camera, made from its corner's index
struct ParticleData
{
    vec4 PositionAngle;
    // the size, then the jibber's offset in the same view aligned units
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

out vec2 vTexpos;
out vec4 vColor;

const vec2 Corners[6] = vec2[](vec2(-0.5, -0.5), vec2(0.5, -0.5), vec2(0.5, 0.5), vec2(-0.5, -0.5), vec2(0.5, 0.5), vec2(-0.5, 0.5));

void main()
{
    ParticleData particle = particles[gl_BaseInstance + gl_InstanceID];
    vec2 corner = Corners[gl_VertexID];
    vec2 local = corner * particle.Size.xy;
    float c = cos(particle.PositionAngle.w);
    float s = sin(particle.PositionAngle.w);
    local = vec2(local.x * c - local.y * s, local.x * s + local.y * c) + particle.Size.zw;
    vec3 world = particle.PositionAngle.xyz + InverseView[0].xyz * local.x + InverseView[1].xyz * local.y;
    gl_Position = StartProjection * StartView * vec4(world, 1.0);
    // The texture's rows go from the top down
    vTexpos = mix(particle.TextureRect.xy, particle.TextureRect.zw, vec2(corner.x + 0.5, 0.5 - corner.y));
    vColor = particle.Color;
}
