layout (binding = 5) uniform sampler2D Screen;

in vec2 vUV;
out vec4 oFragColor;

void main()
{
    vec2 uv = vUV * 0.5 + vec2(0.5);
    uv.x = 1.0 - uv.x;
    vec4 screen = texture(Screen, uv);
    // The screen shows no alpha, like the PS2's: GL blends it along with the color, and a subtracting preset (the alt earth sky's sun)
    // left pixels the window showed through
    oFragColor = vec4(screen.rgb, 1.0);
}