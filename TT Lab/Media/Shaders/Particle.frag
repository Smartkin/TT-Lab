layout (binding = 0) uniform sampler2D Page;

in vec2 vTexpos;
in vec4 vColor;

out vec4 FragColor;

void main()
{
    // Texture rectangles are in pixels of the page
    vec4 texel = texture(Page, vTexpos / vec2(textureSize(Page, 0)));
    // Pages come premultiplied from their bitmaps
    vec3 color = texel.a > 0.0 ? texel.rgb / texel.a : vec3(0.0);
    FragColor = vec4(color * vColor.rgb, texel.a * vColor.a);
    if (FragColor.a < 0.01)
    {
        discard;
    }
}
