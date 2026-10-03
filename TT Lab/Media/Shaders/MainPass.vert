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

// The game keeps 16 rows of 3 phases per cloth material, each starting at a random angle below pi, and every frame moves them all by
// speed * time * 2pi (mode 2: by speed * time), hands VU1 the sine of each (modes 1 and 2 the cosine, mode 2 times the cosine of a
// second set of phases sped up 2, 2.3 and 2 times) next to the amplitudes (FUN_001d1cf0, FUN_001d1948). The VU1 program (0x21,
// 0x1E) moves every vertex by the amplitudes times a wave per axis, the row of each axis being the low 4 bits of the float bits of
// the coordinate times 0.000373 (MTIR of the scaled position): a hash of the position, so vertexes at the same spot move together
float ClothPhase(int row, int axis)
{
	return fract(sin(dot(vec2(row, axis), vec2(12.9898, 78.233))) * 43758.5453) * 3.1415927;
}

float ClothWave(int row, int axis)
{
	int mode = twin_material.deform_mode;
	float advance = twin_material.deform_speed * Time * (mode == 2 ? 1.0 : 6.2831855);
	float phase = ClothPhase(row, axis) + advance;
	float wave = mode == 0 ? sin(phase) : cos(phase);
	if (mode == 2)
	{
		wave *= cos((ClothPhase(row + 16, axis) + advance) * (axis == 1 ? 2.3 : 2.0));
	}
	return wave;
}

vec3 ClothOffset(vec3 position)
{
	ivec3 rows = floatBitsToInt(position * 0.000373) & 15;
	return twin_material.deform_amplitude * vec3(ClothWave(rows.x, 0), ClothWave(rows.y, 1), ClothWave(rows.z, 2));
}

// The game's environment map (VU1 program 0x1D) is a lookup by the scene's strongest lights: H is the half vector of the direction
// to the eye and the normal, d the dot products of H with the three lights in the object's space, normalized together, and the
// picture is read at (0.5 + 0.5 d.x, 0.5 - 0.5 d.y) clamped, plus the material's scroll. Dot products don't mind the space, so world here
vec2 EnvironmentUv(vec3 worldPosition, vec3 worldNormal)
{
	vec3 toEye = normalize(EyePosition - worldPosition);
	vec3 halfVector = normalize(toEye + normalize(worldNormal));
	vec3 d = normalize(vec3(dot(EnvLight0, halfVector), dot(EnvLight1, halfVector), dot(EnvLight2, halfVector)));
	return clamp(vec2(0.5 + 0.5 * d.x, 0.5 - 0.5 * d.y), 0.0, 1.0) + twin_material.uv_offset;
}

// The lit VU1 programs (0x2dbeb0, 0x2e3b40): the vertex color's bytes times the ambient plus every light's color times the dot product of its
// direction and the normal (neither normalized) clamped at 0, clamped to 255, which the GS takes as twice the texture's color at most. The
// game turns the lights into the object's space, dot products are the same in the world's
vec3 LitColor(LightSet lights, vec3 normal, vec3 color)
{
	vec3 light = lights.Ambient.rgb;
	for (int i = 0; i < 3; i++)
	{
		light += lights.Slots[i].Color.rgb * max(dot(lights.Slots[i].Direction.xyz, normal), 0.0);
	}
	return min(color * light, vec3(1.0)) * (255.0 / 128.0);
}

void main()
{
	InstanceData instance = instances[gl_BaseInstance + gl_InstanceID];
	mat4 modelMatrix = instance.Model;
	// The game moves the sky to the camera before drawing it (FUN_001ba350), so it's never any nearer
	if (FollowsCamera)
	{
		modelMatrix[3].xyz += EyePosition;
	}

	mat4 viewModel = StartView * modelMatrix;
	vec3 processedPosition = in_Position;
	vec3 processedNormal = in_Normal;

	// Cloth deformation
	if (twin_material.deform_speed != 0.0 || twin_material.deform_amplitude != vec3(0.0))
	{
		processedPosition += ClothOffset(in_Position);
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
		vec3 normal1 = mat3(BoneMatrices[uint(in_BoneMatrixIndices.x)]) * in_Normal * in_BoneWeights.x;
		vec3 normal2 = mat3(BoneMatrices[uint(in_BoneMatrixIndices.y)]) * in_Normal * in_BoneWeights.y;
		vec3 normal3 = mat3(BoneMatrices[uint(in_BoneMatrixIndices.z)]) * in_Normal * in_BoneWeights.z;
		processedNormal = mix(in_Normal, normal1 + normal2 + normal3, in_BoneWeights.x + in_BoneWeights.y + in_BoneWeights.z);
	}

	Position = processedPosition;
	Emit = in_Emit * twin_material.double_color;
	ViewPosition = vec3(modelMatrix * vec4(processedPosition, 1.0));
	vec3 worldNormal = mat3(modelMatrix) * processedNormal;
	EnvUv = twin_material.env_map > 0.0 ? EnvironmentUv(ViewPosition, worldNormal) : vec2(0.0);
	gl_Position = StartProjection * viewModel * vec4(processedPosition, 1.0);
	Texpos = in_Texpos;
	Normal = in_Normal;
	Color = twin_material.lit > 0.5 && instance.Lighting.x >= 0
		? vec4(LitColor(lightSets[instance.Lighting.x], worldNormal, in_Color.rgb), in_Color.a)
		: vec4(in_Color.rgb * twin_material.double_color, in_Color.a);
	InstanceColor = instance.Color;
}
