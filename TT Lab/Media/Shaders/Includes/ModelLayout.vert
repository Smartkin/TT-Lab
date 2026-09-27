// Add any changes or additions you want for the model's format

#include "TwinMaterial.glsl"

layout (location = 0) in vec3 in_Position;
layout (location = 1) in vec4 in_Color;
layout (location = 2) in vec2 in_Texpos;
layout (location = 3) in vec3 in_Normal;
layout (location = 4) in vec4 in_Emit;
layout (location = 5) in vec3 in_BoneMatrixIndices;
layout (location = 6) in vec3 in_BoneWeights;

// Filled every frame by InstanceBuffer, every draw is instanced and finds its instances through gl_BaseInstance
struct InstanceData
{
    mat4 Model;
    vec4 Color;
};

layout (std430, binding = 0) readonly buffer Instances
{
    InstanceData instances[];
};

out vec3 Position;
out vec2 Texpos;
out vec4 Color;
out vec4 Emit;
out vec3 Normal;
out vec3 ViewPosition;
out vec4 InstanceColor;

#include "GlobalUniformsDeclaration.glsl"
// Vertex shader specific uniforms
uniform mat4 StartProjection;
uniform mat4 StartView;
uniform mat4 InverseView;
uniform mat4 BoneMatrices[MAX_BONES];
uniform vec3 BlendShape;
uniform int BlendShapesAmount;
uniform int ShapeOffset[MAX_BLENDS];
uniform int ShapeStart;
uniform float MorphWeights[MAX_BLENDS];
uniform bool UseSkinning = false;
uniform bool UseMorphs = false;
layout (binding = 6) uniform sampler2D Morphs;
