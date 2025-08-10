
out vec2 vUV;

const vec2 p[4] = vec2[4]
(
    vec2(-1, -1),
    vec2( 1, -1),
    vec2( 1,  1),
    vec2(-1,  1)
);

void main()
{
    vUV = p[gl_VertexID];
    gl_Position = vec4(p[gl_VertexID], 0, 1);
}