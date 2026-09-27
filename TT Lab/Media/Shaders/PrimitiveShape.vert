layout (location = 0) in vec3 in_Position;
layout (location = 1) in vec3 in_Normal;

struct ShapeData
{
    mat4 Model;
    vec4 Color;
};

layout (std430, binding = 2) readonly buffer Shapes
{
    ShapeData shapes[];
};

uniform mat4 StartView;
uniform mat4 StartProjection;

out vec3 vNormal;
out vec4 vColor;

void main()
{
    ShapeData shape = shapes[gl_BaseInstance + gl_InstanceID];
    mat4 viewModel = StartView * shape.Model;
    gl_Position = StartProjection * viewModel * vec4(in_Position, 1.0);
    vNormal = mat3(viewModel) * in_Normal;
    vColor = shape.Color;
}
