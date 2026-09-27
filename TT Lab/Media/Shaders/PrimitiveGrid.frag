in vec2 vPlane;
flat in vec4 vColor;
flat in vec4 vFirstAxisColor;
flat in vec4 vSecondAxisColor;
flat in vec4 vParameters;

uniform float Opacity = 1.0;

layout (location = 0) out vec4 outColor;

// Coverage of the pixel by lines repeating every spacing cells
float Lines(vec2 cells, float spacing, float width)
{
    vec2 coordinate = cells / spacing;
    vec2 cellsPerPixel = max(fwidth(coordinate), vec2(0.000001));
    vec2 distanceInPixels = abs(fract(coordinate - 0.5) - 0.5) / cellsPerPixel;
    vec2 coverage = clamp(width * 0.5 + 0.5 - distanceInPixels, 0.0, 1.0);
    // Lines only a few pixels apart fade out instead of turning into moire, far away or when seen from the side
    vec2 visibility = clamp((1.0 / cellsPerPixel - 3.0) / 6.0, 0.0, 1.0);
    return max(coverage.x * visibility.x, coverage.y * visibility.y);
}

void main()
{
    float radius = length(vPlane);
    if (radius >= 1.0)
    {
        discard;
    }

    vec2 cells = vPlane * vParameters.x;
    float minor = Lines(cells, 1.0, 1.0);
    float major = Lines(cells, vParameters.y, 1.5);
    vec4 color = vec4(vColor.rgb, vColor.a * max(minor * 0.5, major));

    // The two lines through the center go along the plane's axes
    vec2 cellsPerPixel = max(fwidth(cells), vec2(0.000001));
    float firstAxis = clamp(1.5 - abs(cells.y) / cellsPerPixel.y, 0.0, 1.0) * vFirstAxisColor.a;
    float secondAxis = clamp(1.5 - abs(cells.x) / cellsPerPixel.x, 0.0, 1.0) * vSecondAxisColor.a;
    if (secondAxis > color.a)
    {
        color = vec4(vSecondAxisColor.rgb, secondAxis);
    }

    if (firstAxis > color.a)
    {
        color = vec4(vFirstAxisColor.rgb, firstAxis);
    }

    color.a *= (1.0 - smoothstep(0.55, 1.0, radius)) * Opacity;
    if (color.a < 0.002)
    {
        discard;
    }

    outColor = color;
}
