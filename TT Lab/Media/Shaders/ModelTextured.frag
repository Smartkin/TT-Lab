#include "Includes/ShadingLibrary.frag"

void main()
{
    outColor = ShadeFragment(Texpos, Color, Diffuse, EyespaceNormal);
}