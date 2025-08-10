#include "Includes/ShadingLibrary.vert"

void main()
{
	mat4 viewModel = GetViewModelMatrix(StartView, StartModel);
    vec3 processedPosition = in_Position;
#ifdef USE_BLEND_SKINNING
	processedPosition = BlendVertex(processedPosition, Morphs, gl_VertexID, MorphWeights);
	processedPosition = PositionVertex(processedPosition, gl_VertexID);
#endif
	Projection = StartProjection;
	View = StartView;
	Model = StartModel;
	gl_Position = StartProjection * viewModel * vec4(processedPosition, 1.0);
#ifdef USE_TEXTURES
	Texpos = in_Texpos;
	mat4 normalMat = transpose(inverse(viewModel));
	Diffuse = ShadeVertex(normalMat, in_Position, in_Normal);
	EyespaceNormal = normalize(normalMat * vec4(in_Normal, 0.0)).xyz;
#else
	Texpos = vec2(0.0, 0.0);
	Diffuse = vec3(1.0, 1.0, 1.0);
	EyespaceNormal = vec3(0.0, 1.0, 0.0);
#endif
	Color = in_Color;
}