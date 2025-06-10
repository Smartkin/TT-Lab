// A reminder because this behaviour is not really documented but at least as far as HLSL
// compiler is concerned if a uniform is declared but never used for something meaningful
// then it will be optimized out by the shader compiler and if you do param binding in Ogre's scripts then Ogre will throw
// an error that the parameter wasn't found

#include <OgreUnifiedShader.h>

SAMPLER2D(uTexture, 0);

OGRE_UNIFORMS(
    vec4 outlineColor;
    vec2 texelSize;
    float outlineWidth;
)

void main(
 in vec4 gl_Position : POSITION0,
 in vec4 iColor : COLOR,
 in vec2 iUv : TEXCOORD0,
 out vec4 gl_FragColor : COLOR
)
{
    float alpha = texture2D(uTexture, iUv).a;
    
    if (alpha > 0.5)
    {
        gl_FragColor = iColor;
        return;
    }

    float outlineAlpha = 0;
    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            vec2 offset = vec2(x, y) * texelSize * outlineWidth;
            outlineAlpha += texture2D(uTexture, iUv + offset).a;
        }
    }

    if (outlineAlpha > 0.5)
    {
        gl_FragColor = outlineColor;
        return;
    }
    
    gl_FragColor = vec4(0.0, 0.0, 0.0, 0.0);
}
