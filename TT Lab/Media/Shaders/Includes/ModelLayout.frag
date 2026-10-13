// Add any changes or additions you want for the model's format

#include "TwinMaterial.glsl"

in vec3 Position;
in vec2 Texpos;
in vec4 Color;
in vec4 Emit;
in vec3 Normal;
in vec3 ViewPosition;
// Tint of the drawn instance
in vec4 InstanceColor;
in vec2 EnvUv;
in float ViewDepth;
in vec2 ReflectOffset;
flat in int DrawMaterial;
flat in uvec2 DrawTexture;

layout (location = 0) out vec4 outColor;

#include "GlobalUniformsDeclaration.glsl"
// Fragment shader specific uniforms
uniform float Opacity = 1.0;
uniform float FlipY = 0.0;
uniform float DiffuseOnly = 0.0;
// The fragments a draw has (AlphaFailDraw): 0 every one but the failing ones the material leaves out, 1 only those passing the alpha test,
// 2 only those failing it, with the GL state of what their AFAIL writes (RenderBatch draws a material's apart this way)
uniform int AlphaSubpass = 0;
layout (binding = 0) uniform sampler2D Texture[MAX_TEXTURES];
layout (binding = 5) uniform sampler2D Screen;
// The chunk's fog's 256 texels, their color and how much of it goes in (GameFog)
layout (binding = 7) uniform sampler2D FogTable;
