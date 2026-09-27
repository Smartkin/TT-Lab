#include "Includes/ModelLayout.vert"

uniform TwinMaterial twin_material;

vec3 GetVertexOffset(sampler2D offsets, int shapeId, int vertexId)
{
	int shapeCoords = vertexId + ShapeStart + ShapeOffset[shapeId];
	int width = textureSize(offsets, 0).x;
	vec4 offset = texelFetch(offsets, ivec2(shapeCoords % width, shapeCoords / width), 0);
	return offset.xyz * BlendShape;
}

vec3 BlendVertex(vec3 position, sampler2D offsets, int vertexId, float weights[15])
{
	vec3 resultPosition = position;
	for (int i = 0; i < MAX_BLENDS && i < BlendShapesAmount; i += 1)
	{
		resultPosition += GetVertexOffset(offsets, i, vertexId) * weights[i];
	}
	return resultPosition;
}

void main()
{
	InstanceData instance = instances[gl_BaseInstance + gl_InstanceID];
	mat4 modelMatrix = instance.Model;
	mat4 viewModel = StartView * modelMatrix;
	vec3 processedPosition = in_Position;

	// Deform
	if (twin_material.deform_speed != vec2(0.0))
	{
		float vy = sin(Time + in_Position.y) * twin_material.deform_speed.y * 0.1;
		float vx = sin(Time + in_Position.y) * twin_material.deform_speed.x * 0.1;
		float vz = sin(Time + in_Position.y) * twin_material.deform_speed.x * 0.1;
		processedPosition *= vec3(vx + 1.0, vy + 1.0, vz + 1.0);
	}

	// Billboard
	if (twin_material.billboard_render > 0.5)
	{
		viewModel = StartView * mat4(vec4(normalize(cross(vec3(0.0, 1.0, 0.0), InverseView[2].xyz)), 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(normalize(cross(InverseView[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0), modelMatrix[3]);
	}

	// Vertex based animation
	if (UseMorphs)
	{
		processedPosition = BlendVertex(processedPosition, Morphs, gl_VertexID, MorphWeights);
	}

	// Skinning
	if (UseSkinning)
	{
		vec4 pos1 = (BoneMatrices[uint(in_BoneMatrixIndices.x)] * vec4(processedPosition, 1.0)) * in_BoneWeights.x;
		vec4 pos2 = (BoneMatrices[uint(in_BoneMatrixIndices.y)] * vec4(processedPosition, 1.0)) * in_BoneWeights.y;
		vec4 pos3 = (BoneMatrices[uint(in_BoneMatrixIndices.z)] * vec4(processedPosition, 1.0)) * in_BoneWeights.z;
		processedPosition = mix(processedPosition, pos1.xyz + pos2.xyz + pos3.xyz, in_BoneWeights.x + in_BoneWeights.y + in_BoneWeights.z);
	}

	Position = processedPosition;
	Emit = in_Emit * twin_material.double_color;
	ViewPosition = vec3(modelMatrix * vec4(processedPosition, 1.0));
	gl_Position = StartProjection * viewModel * vec4(processedPosition, 1.0);
	Texpos = in_Texpos;
	Normal = in_Normal;
	Color = vec4(in_Color.rgb * twin_material.double_color, in_Color.a);
	InstanceColor = instance.Color;
}
