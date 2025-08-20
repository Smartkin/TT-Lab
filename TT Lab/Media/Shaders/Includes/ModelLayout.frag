// Add any changes or additions you want for the model's format

#include "TwinMaterial.glsl"

in vec3 Position;
in vec2 Texpos;
in vec4 Color;
in vec4 Emit;
in vec3 Normal;
in vec3 ViewPosition;
in mat4 Projection;
in mat4 View;
in mat4 Model;

layout (location = 0) out vec4 outColor;

#include "GlobalUniformsDeclaration.glsl"
// Fragment shader specific uniforms
uniform vec4 Diffuse = vec4(1.0);
uniform float Opacity = 1.0;
uniform float FlipY = 0.0;
uniform float DiffuseOnly = 0.0;
layout (binding = 0) uniform sampler2D Texture[MAX_TEXTURES];
layout (binding = 5) uniform sampler2D Screen;