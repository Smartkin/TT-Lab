layout (binding = 5) uniform sampler2D Screen;

in vec2 vUV;
out vec4 oFragColor;

void main()
{
    vec2 uv = vUV * 0.5 + vec2(0.5);
    uv.x = 1.0 - uv.x;
    vec4 screen = texture(Screen, uv);
    oFragColor = screen;
}