#include "Includes/ModelLayout.frag"

uniform int TexturesAmount;
uniform int AlphaBlending[MAX_TEXTURES];
uniform bool AlphaTest[MAX_TEXTURES];
uniform float AlphaCompare[MAX_TEXTURES];
uniform int AlphaCompareMethod[MAX_TEXTURES];
uniform float FIX[MAX_TEXTURES];
uniform int SpecColA[MAX_TEXTURES];
uniform int SpecColB[MAX_TEXTURES];
uniform int SpecAlphaC[MAX_TEXTURES];
uniform int SpecColD[MAX_TEXTURES];


vec4 ShadeFragment(vec3 texCoord, vec4 col, vec3 diffuse, vec3 eyespaceNormal)
{
	vec4 color = vec4(1.0);
	for (int i = 0; i < TexturesAmount; ++i)
	{
		vec4 texCol = texture(Texture[i], vec2(texCoord.s, texCoord.t));
		if (AlphaBlending[i] == 1) {
			vec4 colorA = vec4(0.0);
			vec4 colorB = vec4(0.0);
			vec4 alphaC = vec4(FIX[i]);
			vec4 colorD = vec4(0.0);
			if (SpecColA[i] == 0) {
				colorA = texCol;
			}
			else if (SpecColA[i] == 1 || SpecColA[i] == 3) {
				colorA = color;
			}
			if (SpecColB[i] == 0) {
				colorB = texCol;
			}
			else if (SpecColB[i] == 1 || SpecColB[i] == 3) {
				colorB = color;
			}
			if (SpecAlphaC[i] == 0) {
				alphaC = vec4(texCol.a);
			}
			else if (SpecAlphaC[i] == 1  || SpecAlphaC[i] == 3) {
				alphaC = vec4(color.a);
			}
			if (SpecColD[i] == 0) {
				colorD = texCol;
			}
			else if (SpecColD[i] == 1  || SpecColD[i] == 3) {
				colorD = color;
			}
			color = (colorA - colorB) * alphaC + colorD;
		}
		else {
			color = texCol;
		}
	}
	color *= Opacity;
	return color;
}