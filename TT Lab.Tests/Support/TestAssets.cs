using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.DynamicScenery;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using static TT_Lab.Tests.Support.TestGeometry;
using Material = TT_Lab.Assets.Graphics.Material;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Skin = TT_Lab.Assets.Graphics.Skin;
using Texture = TT_Lab.Assets.Graphics.Texture;
using Vector3 = System.Numerics.Vector3;

namespace TT_Lab.Tests.Support;

/// <summary>
/// Made-up graphics assets of a test project, and saving, loading and exporting them
/// </summary>
public sealed class TestAssets(TestProject project, int seed = 31)
{
    private readonly Random _random = new(seed);

    public PS2ItemFactory Factory { get; } = new() { GlobalPackage = project.Project.GlobalPackagePS2, ChunkPath = "levels/test" };

    /// <summary>
    /// A save icon of two shapes: a quad and a roof over it that the second shape raises, an animation blending them, and a texture of
    /// stripes (runs) with a gradient row (texels that differ)
    /// </summary>
    public static PS2SaveIcon MakeSaveIcon(bool compressed = false)
    {
        var icon = new PS2SaveIcon { ShapeCount = 2, TextureType = compressed ? 0xFu : 0x6u, FrameLength = 60, AnimationSpeed = 1.5f, PlayOffset = 2 };
        // The icon's Y goes down: the roof's top is above the quad
        (short X, short Y, short Z, short U, short V)[] corners =
        [
            (-4096, 0, 0, 0, 4096), (4096, 0, 0, 4096, 4096), (4096, -8192, 0, 4096, 2048),
            (-4096, 0, 0, 0, 4096), (4096, -8192, 0, 4096, 2048), (-4096, -8192, 0, 0, 2048),
            (-4096, -8192, 0, 0, 2048), (4096, -8192, 0, 4096, 2048), (0, -12288, 0, 2048, 0),
            (-4096, -8192, 0, 0, 2048), (0, -12288, 0, 2048, 0), (0, -12288, 2048, 2048, 0)
        ];
        foreach (var (x, y, z, u, v) in corners)
        {
            var raised = (short)(y <= -12288 ? y - 2048 : y);
            icon.Vertexes.Add(new SaveIconVertex
            {
                Positions = [x, y, z, 0, x, raised, z, 0],
                Normal = [0, 0, -4096, 0],
                U = u,
                V = v,
                Color = 0xFFBFBFBF
            });
        }

        icon.Frames.Add(new SaveIconFrame { Shape = 0, Keys = [new SaveIconKey(0, 1), new SaveIconKey(30, 0), new SaveIconKey(60, 1)] });
        icon.Frames.Add(new SaveIconFrame { Shape = 1, Keys = [new SaveIconKey(0, 0), new SaveIconKey(30, 1), new SaveIconKey(60, 0)] });
        for (var row = 0; row < PS2SaveIcon.TextureSize; row++)
        {
            for (var column = 0; column < PS2SaveIcon.TextureSize; column++)
            {
                icon.Texture[row * PS2SaveIcon.TextureSize + column] = row == 5
                    ? PS2SaveIcon.FromRgba((byte)(column * 2), 0x40, 0xC0, 0xFF)
                    : PS2SaveIcon.FromRgba((byte)(row / 16 * 32), 0x80, (byte)(column / 32 * 64), 0xFF);
            }
        }

        return icon;
    }

    public SaveIcon AddSaveIcon(string name = "Crash", PS2SaveIcon? icon = null)
    {
        var saveIcon = project.Add(new SaveIcon { GlobalPath = "Startup" }, name);
        saveIcon.SetData(new SaveIconData(saveIcon, SaveIconTlm.ToBytes(icon ?? MakeSaveIcon())));
        return saveIcon;
    }

    public Model AddModel(string name)
    {
        var model = project.Add(new Model(), name);
        var data = new ModelData(model);
        data.SetParts(new[] { RigidSubModel(_random, TwinVifPadding.NopPerByte, [38, 12], true), RigidSubModel(_random, TwinVifPadding.QuadWord, [7], false) }.Select(RigidPart));
        model.SetData(data);
        return model;
    }

    public RigidModel AddRigidModel(string name, params LabURI[] materials)
    {
        var rigidModel = project.Add(new RigidModel(), name);
        rigidModel.SetData(new RigidModelData(rigidModel) { Model = AddModel($"{name} Model").URI, Materials = [..materials] });
        return rigidModel;
    }

    public Mesh AddMesh(string name)
    {
        var mesh = project.Add(new Mesh(), name);
        var material = AddMaterial($"{name} Material");
        mesh.SetData(new MeshData(mesh) { Model = AddModel($"{name} Model").URI, Materials = [material.URI, material.URI] });
        return mesh;
    }

    public Skin AddSkin(LabURI material, string name = "Crash Skin")
    {
        var skin = project.Add(new Skin(), name);
        var data = new SkinData(skin);
        data.SubSkins.AddRange(new[] { SubSkin(_random, TwinVifPadding.NopPerByte, 0, [38, 20]), SubSkin(_random, TwinVifPadding.QuadWord, 0, [9]) }.Select(subSkin =>
        {
            subSkin.Compile();
            var read = Deserialize(new PS2SubSkin(), Serialize(subSkin));
            var part = StripParts.FromSkin(read);
            return new SubSkinData(material, new ModelPart { Vertexes = part.Vertexes, Faces = part.Faces, Layout = part.Layout, Compression = read.Compression });
        }));
        skin.SetData(data);
        return skin;
    }

    public BlendSkin AddBlendSkin(LabURI material, string name = "Crash Face")
    {
        const int shapes = 2;
        var blendSkin = project.Add(new BlendSkin(), name);
        var data = new BlendSkinData(blendSkin) { BlendsAmount = shapes };
        var subBlend = SubBlend(_random, shapes, 0, [20, 14]);
        subBlend.Compile();
        var read = Deserialize(new PS2SubBlendSkin(shapes), Serialize(subBlend));
        var part = StripParts.FromBlend(read.Models, shapes, out var shapeOffsets);
        data.Blends.Add(new SubBlendData(material, new ModelPart
        {
            Vertexes = part.Vertexes, Faces = part.Faces, Layout = part.Layout, Compression = read.Models[0].Compression, ShapeOffsets = shapeOffsets
        }));
        blendSkin.SetData(data);
        return blendSkin;
    }

    public Material AddMaterial(string name, params LabURI[] textures)
    {
        var material = project.Add(new Material(), name);
        var data = new MaterialData(material)
        {
            Name = name,
            DmaChainIndex = 2,
            // The game's flags don't always match the shaders
            ActivatedShaders = Enums.AppliedShaders.UnlitBillboard | Enums.AppliedShaders.LitEnvironmentMap,
            Shaders =
            [
                new LabShader { ShaderType = TwinShader.Type.StandardLit, TxtMapping = textures.Length > 0 ? TwinShader.TextureMapping.ON : TwinShader.TextureMapping.OFF,
                    TextureId = textures.ElementAtOrDefault(0) ?? LabURI.Empty, LeftoverVector = new Vector4(Single.NaN, 1, 2, 3) },
                new LabShader { ShaderType = TwinShader.Type.LitEnvironmentMap, ABlending = TwinShader.AlphaBlending.ON, TextureId = textures.ElementAtOrDefault(1) ?? LabURI.Empty }
            ]
        };
        material.SetData(data);
        return material;
    }

    /// <summary>
    /// A material the game draws skins with: its skinned shader first, like every skin's of the game
    /// </summary>
    public Material AddSkinMaterial(string name)
    {
        var material = AddMaterial(name);
        ((IAsset)material).GetData<MaterialData>().Shaders[0].ShaderType = TwinShader.Type.LitSkinnedModel;
        return material;
    }

    public Texture AddTexture(string name, uint argb)
    {
        var texture = project.Add(new Texture(), name);
        texture.SetData(TextureData.CreateSolidColor(texture, 16, argb));
        return texture;
    }

    /// <summary>
    /// A skeleton of 3 joints with a skin, a blend skin, a rigid model on the middle joint and an exit point on the last one
    /// </summary>
    public OGI AddOgi(string name = "Crash")
    {
        var ogi = project.Add(new OGI(), name, 0x5);
        var material = AddSkinMaterial($"{name} Fur");
        var skin = AddSkin(material.URI, $"{name} Skin");
        var blendSkin = AddBlendSkin(material.URI, $"{name} Face");
        var rigidModel = AddRigidModel($"{name} Mask", material.URI, material.URI);
        var data = new OGIData(ogi)
        {
            // Around the meshes at rest, like the game's
            BoundingBox = [new Vector4(-1, 0, -1, 1), new Vector4(28, 2, 1.5f, 1)],
            // The game has rotations that aren't unit quaternions and negative zeros
            Joints =
            [
                new TwinJoint { Index = 0, ParentIndex = 255, Id = 255, ChildCount = 1, Detail = 1, LocalTranslation = new Vector4(0, 0, -0.0f, 1), LocalRotation = new Vector4(0, 0, 0, 0.5f),
                    WorldTranslation = new Vector4(0, 0, -0.0f, 1), UnusedRotation = new Vector4(0.1f, 0.2f, 0.3f, 0.4f), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) },
                new TwinJoint { Index = 1, ParentIndex = 0, Id = 3, ChildCount = 1, Detail = 0, LocalTranslation = new Vector4(0, 1.1f, 0.2f, 1), LocalRotation = new Vector4(0.5f, -0.0f, 0, 0.5f),
                    WorldTranslation = new Vector4(0, 1.1f, 0.2f, 1), UnusedRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0.25f, 0, 0.97f) },
                new TwinJoint { Index = 2, ParentIndex = 1, Id = 255, LocalTranslation = new Vector4(0.3f, 0.4f, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1),
                    WorldTranslation = new Vector4(0.3f, 1.5f, 0.2f, 1), UnusedRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) }
            ],
            // The tools counted the joints with an ID, which leaves ID 3 unbound like the game's models whose IDs have gaps
            JointIdCount = 1,
            SkinInverseMatrices = Enumerable.Range(0, 3).Select(i => System.Numerics.Matrix4x4.CreateTranslation(0, -i * 0.7f, 0.01f * i).ToTwin()).ToList(),
            ExitPoints = [new TwinExitPoint { ID = 0, ParentJointIndex = 2, Matrix = MatrixWithNegativeZeros() }],
            RigidModelIds = [rigidModel.URI],
            RigidModelJointIndices = [1],
            CollisionHulls = [TwinCollisionHull.CreateBox(new Vector4(-0.5f, 0, -0.5f, 1), new Vector4(0.5f, 1, 0.5f, 1))],
            CollisionHullJoints = [2],
            Skin = skin.URI,
            BlendSkin = blendSkin.URI
        };
        ogi.SetData(data);
        return ogi;
    }

    public static Matrix4 MatrixWithNegativeZeros()
    {
        return new Matrix4
        {
            Column1 = new Vector4(1, -0.0f, 0, 0),
            Column2 = new Vector4(-0.0f, 1, 0, 0),
            Column3 = new Vector4(0, -0.0f, 1, 0),
            Column4 = new Vector4(0, 0, -0.0f, 1)
        };
    }

    public static ModelPart RigidPart(PS2SubModel subModel)
    {
        subModel.Compile();
        var part = StripParts.FromRigid(Deserialize(new PS2SubModel(), Serialize(subModel)));
        return new ModelPart { Vertexes = part.Vertexes, Faces = part.Faces, Layout = part.Layout };
    }

    /// <summary>
    /// Saves the asset's data to its file and loads it back
    /// </summary>
    public T Reload<T>(IAsset asset) where T : AbstractAssetData
    {
        asset.Serialize(SerializationFlags.SaveData);
        return asset.GetData<T>();
    }

    public IAsset Get(LabURI uri) => project.AssetManager.GetAsset(uri);

    public T Get<T>(LabURI uri) where T : IAsset => project.AssetManager.GetAsset<T>(uri);

    /// <summary>
    /// The game's bytes of the asset
    /// </summary>
    public byte[] Export(IAsset asset)
    {
        var item = asset.GetData<AbstractAssetData>().Export(Factory);
        item.Compile();
        return Serialize(item);
    }

    /// <summary>
    /// A scenery with meshes and a LOD placed in 5 nodes of its tree, kept values of two of them, a light of every kind, a collision and an
    /// animated dynamic model
    /// </summary>
    public Scenery AddScenery()
    {
        var scenery = project.Add(new Scenery { Chunk = "levels/test" }, "Scenery 0");
        var rock = AddMesh("Rock");
        var tree = AddMesh("Tree");
        var lod = project.Add(new LodModel(), "Bush");
        lod.SetData(new LodModelData(lod) { Type = Enums.LodType.COMPRESSED, MinDrawDistance = 10, MaxDrawDistance = 300, ModelsDrawDistances = [50, 100, 200], Meshes = [rock.URI, tree.URI] });
        var collision = new Collision { Package = scenery.Package, Chunk = scenery.Chunk, InvariantName = "Collision", Alias = "Collision", IsInternal = true, InternalOwner = scenery };
        AddCollision(collision);
        var dynamicScenery = new DynamicScenery { Package = scenery.Package, Chunk = scenery.Chunk, InvariantName = "Dynamic Scenery", Alias = "Dynamic Scenery", IsInternal = true, InternalOwner = scenery };
        var dynamicSceneryData = new DynamicSceneryData(dynamicScenery)
        {
            DynamicModels = [new DynamicSceneryModelData { Mesh = tree.URI, UsesLod = true, BoundingBox = [new Vector4(-1, -1, -1, 1), new Vector4(1, 1, 1, 1)] }]
        };
        dynamicSceneryData.DynamicModels[0].ReadAnimationFromKeys(4, [0, 0, 0, 1, 0, 0, 2, 0.5f, 0, 3, 1, 0], [0, 0, 0, 0, 0.25f, 0, 0, 0.5f, 0, 0, 0.75f, 0.1f]);
        dynamicScenery.SetData(dynamicSceneryData);
        project.AssetManager.AddAsset(dynamicScenery);

        // The rocks are 19 units long along X. The first is kept in the root, which the rule would put two octants down, and the LOD in an
        // octant the rule would put in one of its octants
        var data = new SceneryData(scenery)
        {
            FogColor = 3,
            UnusedByte = 0x12,
            TreeDepth = 2,
            BoundsMin = new System.Numerics.Vector3(-100, -20, -100),
            BoundsMax = new System.Numerics.Vector3(100, 20, 100),
            AmbientLights = [new AmbientLight { Color = new Vector4(0.2f, 0.2f, 0.3f, 1), Position = new Vector4(1, 2, 3, 1), Enabled = false, Intensity = 10 }],
            DirectionalLights = [new DirectionalLight { Direction = new Vector4(0.6f, 0.8f, 0, 0), Leftover = 3, Intensity = 1.5f, Position = new Vector4(0, 50, 0, 1), Color = new Vector4(1, 1, 0.9f, 1) }],
            PointLights = [new PointLight { AttenuationPower = -2, Position = new Vector4(10, 3, 10, 1), Intensity = 7.5f, Color = new Vector4(1, 0.5f, 0, 1) }],
            SpotLights = [new SpotLight { Direction = new Vector4(1, 2, 3, 4), InnerConeCosine = 0.5f, OuterConeCosine = 0.4f, ConeAngle = 21845, FalloffAngle = 1166, AttenuationPower = 65535, SpotExponent = 3, Intensity = 2, Position = new Vector4(-10, 0, 0, 1) }],
            Placements =
            [
                Place(rock, new Vector3(0, 0, 0), ""),
                Place(rock, new Vector3(-75, 0, -75), "57"),
                PlaceLod(lod, new Vector3(-25, 0, -25), "5"),
                Place(tree, new Vector3(50, 0, 50), "02"),
                Place(rock, new Vector3(60, 5, 60), "02")
            ],
            Collision = collision.URI,
            DynamicScenery = dynamicScenery.URI
        };
        var built = data.BuildTree(_ => 0);
        // The tools' rounding of a leaf's box and a node's own light bits, and values of a node that isn't there anymore
        var leaf = built.Single(node => node.MeshIDs.Count == 1 && node is TwinSceneryLeaf);
        data.TreeNodes =
        [
            Kept("57", leaf, 1e-4f, Enumerable.Range(0, 128).Select(i => i % 3 == 0).ToArray()),
            Kept("", built[0], 0, null),
            Kept("33", leaf, 0, null)
        ];
        scenery.SetData(data);
        return scenery;
    }

    private static SceneryTreeNode Kept(string path, TwinSceneryBaseType node, float rounding, bool[]? lights)
    {
        return new SceneryTreeNode
        {
            Path = path,
            BoundsCenter = new Vector4(node.BoundsCenter.X + rounding, node.BoundsCenter.Y, node.BoundsCenter.Z, node.BoundsCenter.W - rounding),
            BoundsMin = new Vector4(node.BoundsMin.X - rounding, node.BoundsMin.Y, node.BoundsMin.Z, node.BoundsMin.W),
            BoundsMax = new Vector4(node.BoundsMax.X, node.BoundsMax.Y + rounding, node.BoundsMax.Z, node.BoundsMax.W),
            BoundsHalfSize = new Vector4(node.BoundsHalfSize.X, node.BoundsHalfSize.Y, node.BoundsHalfSize.Z + rounding, node.BoundsHalfSize.W),
            LightsEnabler = lights
        };
    }

    private SceneryPlacement Place(Mesh mesh, Vector3 position, string node)
    {
        return new SceneryPlacement { Model = mesh.URI, Matrix = System.Numerics.Matrix4x4.CreateTranslation(position).ToTwin(), Box = MeshBox(mesh), Node = node };
    }

    private SceneryPlacement PlaceLod(LodModel lod, Vector3 position, string node)
    {
        var box = MeshBox(Get<Mesh>(((IAsset)lod).GetData<LodModelData>().Meshes[0]));
        return new SceneryPlacement { Model = lod.URI, IsLod = true, Matrix = System.Numerics.Matrix4x4.CreateTranslation(position).ToTwin(), Box = box, Node = node };
    }


    // The box the game stores for a placed mesh is its model's, a bit bigger than the vertexes like some of the game's
    private BoundingBox MeshBox(Mesh mesh)
    {
        var model = Get(((IAsset)mesh).GetData<MeshData>().Model).GetData<ModelData>();
        var positions = model.Vertexes.SelectMany(vertexes => vertexes).Select(vertex => new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z)).ToList();
        var min = positions.Aggregate(Vector3.Min) - new Vector3(0.12f);
        var max = positions.Aggregate(Vector3.Max) + new Vector3(0.12f);
        return new BoundingBox { V1 = new Vector4(min.X, min.Y, min.Z, Vector3.Max(Vector3.Abs(min), Vector3.Abs(max)).Length()), V2 = new Vector4(max.X, max.Y, max.Z, 0) };
    }


    /// <summary>
    /// Two surfaces taking turns and a vertex no triangle uses
    /// </summary>
    public (Collision, CollisionData) AddCollision(Collision collision)
    {
        var floor = project.Add(new CollisionSurface { Chunk = "default" }, "Floor", 1);
        var wall = project.Add(new CollisionSurface { Chunk = "default" }, "Wall", 2);
        floor.Parameters[CollisionSurface.EditorColorParameter] = CollisionSurface.DefaultColors[1];
        wall.Parameters[CollisionSurface.EditorColorParameter] = CollisionSurface.DefaultColors[2];
        var data = new CollisionData(collision);
        for (var z = 0; z < 3; z++)
        {
            for (var x = 0; x < 3; x++)
            {
                data.Vertexes.Add(new Vector4(x * 5, x * z, z * 5, 1));
            }
        }

        data.Vertexes.Insert(4, new Vector4(99, 99, 99, 1));
        int[][] faces = [[0, 1, 3], [1, 5, 3], [1, 2, 5], [2, 6, 5], [3, 5, 7], [5, 8, 7], [5, 6, 8], [6, 9, 8]];
        for (var i = 0; i < faces.Length; i++)
        {
            data.Triangles.Add(new CollisionTriangle { Face = new IndexedFace(faces[i][0], faces[i][1], faces[i][2]), Surface = i % 2 == 0 ? floor.URI : wall.URI });
        }

        collision.SetData(data);
        if (collision.IsInternal)
        {
            project.AssetManager.AddAsset(collision);
        }

        return (collision, data);
    }
}
