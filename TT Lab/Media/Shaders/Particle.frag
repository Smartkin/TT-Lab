layout (binding = 0) uniform sampler2D Page;

// The Brighten preset blends by the alpha alone, which GL only takes as the fragment's color
uniform bool AlphaAsColor;

in vec2 vTexpos;
in vec4 vColor;

out vec4 FragColor;

void main()
{
    // Texture rectangles are in pixels of the page
    vec4 texel = texture(Page, vTexpos / vec2(textureSize(Page, 0)));
    // Pages come premultiplied from their bitmaps
    vec3 color = texel.a > 0.0 ? texel.rgb / texel.a : vec3(0.0);
    float alpha = texel.a * vColor.a;
    // Every page shader of the game drops what's under 5 of the GS's 128
    if (alpha < 5.0 / 128.0)
    {
        discard;
    }

    FragColor = AlphaAsColor ? vec4(alpha) : vec4(color * vColor.rgb, alpha);
}
