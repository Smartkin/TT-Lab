#include "Includes/ModelLayout.vert"

uniform TwinMaterial twin_material;

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

void main()
{
	mat4 viewModel = StartView * StartModel;
    vec3 processedPosition = in_Position;
    
    // Deform
    float vy = sin(Time + in_Position.y) * twin_material.deform_speed.y * 0.1;
    float vx = sin(Time + in_Position.y) * twin_material.deform_speed.x * 0.1;
    float vz = sin(Time + in_Position.y) * twin_material.deform_speed.x * 0.1;
    processedPosition *= vec3(vx + 1.0, vy + 1.0, vz + 1.0);
    
    // Billboard
	mat4 invView = inverse(StartView);
	viewModel = viewModel * (1.0 - twin_material.billboard_render) + StartView * mat4(vec4(normalize(cross(vec3(0.0, 1.0, 0.0), invView[2].xyz)), 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(normalize(cross(invView[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0), StartModel[3]) * twin_material.billboard_render;

	// Skinning
	vec4 pos1 = (BoneMatrices[uint(in_BoneMatrixIndices.x)] * vec4(processedPosition, 1.0)) * in_BoneWeights.x;
	vec4 pos2 = (BoneMatrices[uint(in_BoneMatrixIndices.y)] * vec4(processedPosition, 1.0)) * in_BoneWeights.y;
	vec4 pos3 = (BoneMatrices[uint(in_BoneMatrixIndices.z)] * vec4(processedPosition, 1.0)) * in_BoneWeights.z;
	processedPosition = mix(processedPosition, pos1.xyz + pos2.xyz + pos3.xyz, in_BoneWeights.x + in_BoneWeights.y + in_BoneWeights.z);
    
	// Vertex based animation
	processedPosition = BlendVertex(processedPosition, Morphs, gl_VertexID, MorphWeights);
	
	Position = processedPosition;
	Projection = StartProjection;
	View = StartView;
	Model = StartModel;
	Emit = in_Emit * twin_material.double_color;
	ViewPosition = vec3(StartModel * vec4(processedPosition, 1.0));
	gl_Position = StartProjection * viewModel * vec4(processedPosition, 1.0);
	Texpos = in_Texpos;
	Normal = in_Normal;
	Color = in_Color * twin_material.double_color;
}