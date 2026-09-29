layout (binding = 0) uniform sampler2D Frame;

in vec2 vScreen;
in vec4 vColor;

out vec4 FragColor;

void main()
{
    // The copy's color times the particle's, 128 leaving it as it is
    FragColor = vec4(texture(Frame, clamp(vScreen, 0.0, 1.0)).rgb * vColor.rgb, vColor.a);
}
