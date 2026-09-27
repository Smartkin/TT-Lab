// A grid is a single quad, its lines are worked out per pixel which keeps them sharp at any distance

struct GridData
{
    // Maps the square from -1 to 1 on XY onto the grid's plane
    mat4 Model;
    vec4 Color;
    vec4 FirstAxisColor;
    vec4 SecondAxisColor;
    // Cells from the center to the edge, cells between major lines
    vec4 Parameters;
};

layout (std430, binding = 3) readonly buffer Grids
{
    GridData grids[];
};

uniform mat4 StartView;
uniform mat4 StartProjection;
// Fraction of the distance to the camera grids get pulled closer by so they don't flicker on surfaces they lie on
uniform float DepthBias = 0.0;

out vec2 vPlane;
flat out vec4 vColor;
flat out vec4 vFirstAxisColor;
flat out vec4 vSecondAxisColor;
flat out vec4 vParameters;

const vec2 Corners[6] = vec2[6](vec2(-1.0, -1.0), vec2(1.0, -1.0), vec2(1.0, 1.0), vec2(-1.0, -1.0), vec2(1.0, 1.0), vec2(-1.0, 1.0));

void main()
{
    GridData grid = grids[gl_BaseInstance + gl_InstanceID];
    vec2 corner = Corners[gl_VertexID];
    vec4 viewPosition = StartView * grid.Model * vec4(corner, 0.0, 1.0);
    viewPosition.xyz *= 1.0 - DepthBias;
    gl_Position = StartProjection * viewPosition;
    vPlane = corner;
    vColor = grid.Color;
    vFirstAxisColor = grid.FirstAxisColor;
    vSecondAxisColor = grid.SecondAxisColor;
    vParameters = grid.Parameters;
}
