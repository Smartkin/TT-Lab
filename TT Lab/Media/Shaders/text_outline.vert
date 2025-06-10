#include <OgreUnifiedShader.h>

OGRE_UNIFORMS(
uniform mat4 worldViewProj;
)

void main(
 in vec4 iVertex : POSITION,
 in vec4 iColor : COLOR,
 in vec2 iTexcoord : TEXCOORD0,
 out vec4 gl_Position : POSITION0,
 out vec4 oColor : COLOR0,
 out vec2 oTexcoord : TEXCOORD0
)
{
    gl_Position = mul(worldViewProj, iVertex);
    oColor = iColor;
    oTexcoord = iTexcoord;
}