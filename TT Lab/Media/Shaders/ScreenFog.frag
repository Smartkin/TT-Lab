// The PS2's fog over the sky and the cleared background (GameFog): their depth is 0, which picks the table's first entry, blended
// over the frame by how much of its color goes in
uniform vec4 FogColor;

out vec4 oFragColor;

void main()
{
    oFragColor = FogColor;
}
