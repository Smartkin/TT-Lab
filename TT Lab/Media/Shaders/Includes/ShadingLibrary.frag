// Declare the necessary fragment library shading function declarations here
// MUST BE INCLUDED IN EVERY FRAGMENT SHADER WITH main() FUNCTION

#include "ModelLayout.frag"

vec4 ShadeFragment(vec2 texCoord, vec4 color, vec3 diffuse, vec3 eyespaceNormal);