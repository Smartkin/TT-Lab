// Declare the necessary vertex library shading function declarations here
// MUST BE INCLUDED IN EVERY VERTEX SHADER WITH main() FUNCTION

#include "ModelLayout.vert"

mat4 GetViewModelMatrix(mat4 view, mat4 model);
mat4 GetNormalMatrix(mat4 viewModel);
vec3 ShadeVertex(mat4 normalMat, vec3 vertex, vec3 normal);
vec3 PositionVertex(vec3 position, int vertexId);

vec3 GetVertexOffset(sampler2D offsets, int shapeId, int vertexId)
{
	int shapeCoords = vertexId + ShapeStart + ShapeOffset[shapeId];
	int xCoord = shapeCoords % 256;
	int yCoord = shapeCoords / 256;
	vec4 offset = texelFetch(offsets, ivec2(xCoord, yCoord), 0);
	offset.r = offset.r * 127.0;
	offset.g = offset.g * 127.0;
	offset.b = offset.b * 127.0;
	return vec3(offset.r * BlendShape.x, offset.g * BlendShape.y, offset.b * BlendShape.z);
}

vec3 BlendVertex(vec3 position, sampler2D offsets, int vertexId, float weights[15])
{
	vec3 resultPosition = position;
	for (int i = 0; i < MAX_BLENDS; i += 1)
	{
		resultPosition += GetVertexOffset(offsets, i, vertexId) * weights[i];
	}
	return resultPosition;
}