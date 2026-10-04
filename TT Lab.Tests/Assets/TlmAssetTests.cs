using Avalonia.Headless.XUnit;
using System.Numerics;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using TwinShader = Twinsanity.TwinsanityInterchange.Common.TwinShader;

namespace TT_Lab.Tests.Assets;

// Assets saved to their TT Lab model file and loaded back from it export to the same bytes as before
[Collection(ProjectCollection.Name)]
public sealed class TlmAssetTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public TlmAssetTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    [Fact]
    public void ModelsComeBackTheSame()
    {
        var model = _assets.AddModel("Model");
        var before = _assets.Export(model);

        _assets.Reload<ModelData>(model);

        Assert.Equal(before, _assets.Export(model));
    }

    // Parts refer to the project's materials, which Blender shows from the project's files
    [AvaloniaFact]
    public void RigidModelsReferToTheProjectsMaterials()
    {
        var material = _assets.AddMaterial("Varnish", _assets.AddTexture("Wood", 0xFF8040C0).URI);
        var other = _assets.AddMaterial("Paint");
        var rigidModel = _assets.AddRigidModel("Chair", material.URI, other.URI);
        var before = _assets.Export(rigidModel);
        var assetsBefore = _project.AssetManager.GetAssets().Count;

        var data = _assets.Reload<RigidModelData>(rigidModel);

        Assert.Equal(before, _assets.Export(rigidModel));
        Assert.Equal([material.URI, other.URI], data.Materials);
        var file = TlmFile.Load(rigidModel.FullDataPath);
        Assert.Equal([material.URI.ToString(), other.URI.ToString()], file.Materials.OfType<JsonObject>().Select(json => json.GetString("uri")));
        // Only the model the file holds is made again
        Assert.Equal(assetsBefore + 1, _project.AssetManager.GetAssets().Count);
    }

    [Fact]
    public void SkinsComeBackTheSame()
    {
        var skin = _assets.AddSkin(_assets.AddSkinMaterial("Fur").URI);
        var before = _assets.Export(skin);

        _assets.Reload<SkinData>(skin);

        Assert.Equal(before, _assets.Export(skin));
    }

    [Fact]
    public void BlendSkinsComeBackTheSame()
    {
        var blendSkin = _assets.AddBlendSkin(_assets.AddSkinMaterial("Fur").URI);
        var before = _assets.Export(blendSkin);

        _assets.Reload<BlendSkinData>(blendSkin);

        Assert.Equal(before, _assets.Export(blendSkin));
    }

    [Fact]
    public void OgisKeepTheirSkeletonAndModels()
    {
        var ogi = _assets.AddOgi();
        var data = ((IAsset)ogi).GetData<OGIData>();
        var before = _assets.Export(ogi);
        var skinBefore = _assets.Export(_assets.Get(data.Skin));
        var blendSkinBefore = _assets.Export(_assets.Get(data.BlendSkin));
        var rigidModelBefore = _assets.Export(_assets.Get(data.RigidModelIds[0]));

        var read = _assets.Reload<OGIData>(ogi);

        Assert.Equal(before, _assets.Export(ogi));
        Assert.Equal(skinBefore, _assets.Export(_assets.Get(read.Skin)));
        Assert.Equal(blendSkinBefore, _assets.Export(_assets.Get(read.BlendSkin)));
        Assert.Equal(rigidModelBefore, _assets.Export(_assets.Get(read.RigidModelIds[0])));
    }

    [Fact]
    public void OgisAreTheTreeBlenderShows()
    {
        var file = ((IAsset)_assets.AddOgi()).GetData<OGIData>().WriteTlm();

        var root = file.Root!;
        Assert.Equal(OGIData.TlmKind, root.GetKind());
        Assert.Equal([OGIData.ArmatureKind, SkinData.TlmKind, BlendSkinData.TlmKind, OGIData.RigidBodiesKind, OGIData.ExitPointsKind, OGIData.CollisionHullsKind], root.GetChildren().Select(child => child.GetKind()));
        var hull = root.FindChild(OGIData.CollisionHullsKind)!.GetChildren().Single();
        Assert.Equal((TlmHulls.Kind, 2), (hull.GetKind(), hull.GetInt(TlmNodes.JointKey)));
        var body = root.FindChild(OGIData.RigidBodiesKind)!.GetChildren().Single();
        Assert.Equal((OGIData.BodyKind, 1), (body.GetKind(), body.GetInt(TlmNodes.JointKey)));
        var exitPoint = root.FindChild(OGIData.ExitPointsKind)!.GetChildren().Single();
        Assert.Equal((OGIData.ExitPointKind, 2), (exitPoint.GetKind(), exitPoint.GetInt(TlmNodes.JointKey)));
        Assert.Equal([-1, 0, 1], root.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray().OfType<JsonObject>().Select(joint => joint.GetInt("parent")));
    }

    // A bone moved in Blender puts its joint where it is, the other joints keep the game's values
    [Fact]
    public void MovedBonesMoveTheirJoint()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var joints = data.Joints.Select(Bytes).ToList();
        var file = data.WriteTlm();
        var joint = Joint(file, 2);
        var bind = TlmNodes.FromColumnVectorJson(joint.GetFloats("bind"))!.Value * Matrix4x4.CreateTranslation(1, 0, 0);
        joint["bind"] = TlmNodes.ToColumnVectorJson(bind);

        data.ReadTlm(file);

        Assert.Equal(joints.Take(2), data.Joints.Take(2).Select(Bytes));
        // The test skeleton's bind poses have no turns, its joint moved away from its parent's bind pose by as much
        Assert.Equal(1.0f, data.Joints[2].LocalTranslation.X, 4);
        Assert.Equal(bind.Translation.X, data.Joints[2].WorldTranslation.X, 4);
        Matrix4x4.Invert(bind, out var inverse);
        AssertClose(inverse, data.SkinInverseMatrices[2].ToSystem());
    }

    // Bones can't be scaled, bones still where the scaled bind pose puts them keep it
    [Fact]
    public void ScaledBindPosesSurviveBonesWithoutScale()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var scaled = Matrix4x4.CreateScale(1.02f) * data.SkinInverseMatrices[1].ToSystem();
        data.SkinInverseMatrices[1] = scaled.ToTwin();
        var file = data.WriteTlm();
        var joint = Joint(file, 1);
        Matrix4x4.Decompose(TlmNodes.FromColumnVectorJson(joint.GetFloats("bind"))!.Value, out _, out var rotation, out var translation);
        joint["bind"] = TlmNodes.ToColumnVectorJson(Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation));

        data.ReadTlm(file);

        Assert.Equal(scaled, data.SkinInverseMatrices[1].ToSystem());
    }

    [Fact]
    public void BonesAddedInBlenderAreNewJoints()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray().Add(new JsonObject
        {
            ["parent"] = 2,
            ["name"] = "Tail",
            ["bind"] = TlmNodes.ToColumnVectorJson(Matrix4x4.CreateTranslation(0.3f, 2.5f, 0.2f))
        });

        data.ReadTlm(file);

        Assert.Equal(4, data.Joints.Count);
        Assert.Equal((3, 2), (data.Joints[3].Index, data.Joints[3].ParentIndex));
        Assert.Equal(1, data.Joints[2].ChildCount);
        Assert.Equal(4, data.SkinInverseMatrices.Count);
        Assert.Equal(-2.5f, data.SkinInverseMatrices[3].ToSystem().M42, 4);
    }

    // The game takes every joint without a parent for its root, and a model of four root bones made in Blender broke the viewer, which
    // found no parent for the second one: such a file is refused saying why, like the add-on refuses to export it
    [Fact]
    public void SeveralRootBonesAreRefused()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray()[2]!["parent"] = -1;

        var error = Assert.Throws<InvalidDataException>(() => data.ReadTlm(file));

        Assert.StartsWith("The armature has 2 root bones (Joint 0, Joint 2)", error.Message);
    }

    // The game builds a skeleton in joint order, each joint under its parent's, and draws a joint with the matrix of its place in a walk of
    // the skeleton from the root down: a joint's index has to be its place in that walk
    [Fact]
    public void JointsBeforeTheirParentAreRefused()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        var joints = file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray();
        joints[1]!["parent"] = 2;
        joints[2]!["parent"] = 0;

        var error = Assert.Throws<InvalidDataException>(() => data.ReadTlm(file));

        Assert.StartsWith("Joint 2 is joint 2, but the game walks a skeleton from the root down", error.Message);
    }

    // Every joint after its parent isn't enough: a bone added under the first joint's child after the joints of the root's other children
    // took their matrices
    [Fact]
    public void JointsOutOfTheGamesWalkAreRefused()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        var joints = file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray();
        joints[2]!["parent"] = 0;
        joints.Add(new JsonObject { ["index"] = 3, ["parent"] = 1, ["name"] = "Tail", ["bind"] = TlmNodes.ToColumnVectorJson(Matrix4x4.Identity) });

        var error = Assert.Throws<InvalidDataException>(() => data.ReadTlm(file));

        Assert.StartsWith("Tail is joint 3, but the game walks a skeleton from the root down", error.Message);
        Assert.EndsWith("gets to it as joint 2: export the model again with the current add-on, which numbers the joints that way", error.Message);
    }

    [Fact]
    public void JointIndexesLeftOutAreRefused()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray()[2]!["index"] = 3;

        var error = Assert.Throws<InvalidDataException>(() => data.ReadTlm(file));

        Assert.StartsWith("The armature's joints leave out the index 2", error.Message);
    }

    // The game gives a joint 12 children (none for a 13th, whose own children it then reads from nothing) and draws a skin with 63 joints
    [Theory]
    [InlineData(12, 1, "Joint 0 has 13 child bones and the game gives a joint 12 at most")]
    [InlineData(61, 0, "The armature has 64 bones and the game draws a skin with 63 at most")]
    public void SkeletonsBeyondTheGamesLimitsAreRefused(int added, int underRoot, string reason)
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        var joints = file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray();
        // Under the root, or each under the one before
        for (var index = 3; index < 3 + added; index++)
        {
            joints.Add(new JsonObject { ["index"] = index, ["parent"] = underRoot == 1 ? 0 : index - 1, ["name"] = $"Joint {index}", ["bind"] = TlmNodes.ToColumnVectorJson(Matrix4x4.Identity) });
        }

        var error = Assert.Throws<InvalidDataException>(() => data.ReadTlm(file));

        Assert.Equal(reason, error.Message);
    }

    // Exit points are locators on their joint, which Blender moves with the bone
    [Fact]
    public void ExitPointsFollowTheirLocators()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var matrix = Bytes(data.ExitPoints[0].Matrix);
        var file = data.WriteTlm();

        data.ReadTlm(file);

        // Negative zeros and all
        Assert.Equal(matrix, Bytes(data.ExitPoints[0].Matrix));
        Assert.Equal((0U, 2U), (data.ExitPoints[0].ID, data.ExitPoints[0].ParentJointIndex));

        var exitPoint = file.Root!.FindChild(OGIData.ExitPointsKind)!.GetChildren().Single();
        exitPoint["translation"] = new JsonArray(0.5f, 0, 0);
        exitPoint[TlmNodes.JointKey] = 1;
        data.ReadTlm(file);

        Assert.Equal(1U, data.ExitPoints[0].ParentJointIndex);
        Assert.Equal(0.5f, data.ExitPoints[0].Matrix.ToSystem().M41);
    }

    // The game finds an exit point by its place and never reads its ID (BindExitPoints, a character's hand is 0 and its head 1): exit
    // points go in the order of their IDs, then the file's, and get their places as IDs
    [Fact]
    public void ExitPointsAreInTheOrderOfTheirIds()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        var exitPoints = file.Root!.FindChild(OGIData.ExitPointsKind)!;
        var template = exitPoints.GetChildren().Single();
        exitPoints[TlmNodes.ChildrenKey] = new JsonArray(new[] { 5, 2, 2 }.Select((id, i) =>
        {
            var exitPoint = template.DeepClone().AsObject();
            exitPoint.GetData()["Id"] = id;
            exitPoint[TlmNodes.JointKey] = i;
            return (JsonNode)exitPoint;
        }).ToArray());

        Assert.True(data.ReadTlm(file));

        Assert.Equal([(0U, 1U), (1U, 2U), (2U, 0U)], data.ExitPoints.Select(exitPoint => (exitPoint.ID, exitPoint.ParentJointIndex)));
        Assert.False(data.ReadTlm(data.WriteTlm()));
    }

    // The game binds the joint IDs below the header's count (SetAnimatorOgi), the tools wrote how many joints have an ID: the game's count
    // stays while that's still the number, then every ID gets bound
    [Fact]
    public void JointIdsTheGameBindsFollowTheJoints()
    {
        var ogi = _assets.AddOgi();
        var data = ((IAsset)ogi).GetData<OGIData>();
        var file = data.WriteTlm();
        var joints = file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray().OfType<JsonObject>().ToList();
        // A model of the game whose ID 3 is unbound keeps its count
        Assert.Equal(1, file.Root!.GetData().GetInt("JointIdCount"));

        data.ReadTlm(file);
        Assert.Equal(1, _assets.Export(ogi)[2]);

        joints[2].GetData()["Id"] = 5;
        data.ReadTlm(file);
        Assert.Equal(6, _assets.Export(ogi)[2]);

        // What's worked out isn't kept in the file, made in Blender a model has none
        Assert.False(data.WriteTlm().Root!.GetData().ContainsKey("JointIdCount"));
        file.Root!.GetData()["JointIdCount"] = -1;
        joints[2].GetData()["Id"] = 0;
        data.ReadTlm(file);
        Assert.Equal(4, _assets.Export(ogi)[2]);
    }

    // The game's matrix is kept next to the transform, not among the values Blender edits
    [Fact]
    public void ExitPointsKeepTheGamesMatrixNextToTheirTransform()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var matrix = Bytes(data.ExitPoints[0].Matrix);
        var file = data.WriteTlm();
        var exitPoint = file.Root!.FindChild(OGIData.ExitPointsKind)!.GetChildren().Single();

        Assert.Equal(["Id"], exitPoint.GetData().Select(pair => pair.Key));
        Assert.Equal(16, exitPoint.GetFloats(TlmNodes.MatrixKey).Length);

        data.ReadTlm(file);

        Assert.Equal(matrix, Bytes(data.ExitPoints[0].Matrix));
    }

    // The shapes of a blend skin are the shape keys of its mesh, Blender adds and removes them
    [Fact]
    public void BlendSkinsHaveTheShapesTheirMeshHas()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        var shape = file.Root!.FindChild(BlendSkinData.TlmKind)!;
        Assert.Null(shape[TlmNodes.DataKey]);
        foreach (var part in shape[TlmNodes.MeshKey]!["parts"]!.AsArray().OfType<JsonObject>())
        {
            var shapes = part["shapes"]!.AsArray();
            shapes.Add(shapes[0]!.DeepClone());
        }

        data.ReadTlm(file);

        var blendSkin = _assets.Get(data.BlendSkin).GetData<BlendSkinData>();
        Assert.Equal(3, blendSkin.BlendsAmount);
        Assert.All(blendSkin.Blends, blend => Assert.Equal(3, blend.ShapeOffsets.Count));
    }

    // A hull's mesh moved or edited in Blender gets the planes and axes the game reads worked out again, an untouched one keeps the game's
    [Fact]
    public void HullsEditedInBlenderGetNewPlanes()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var planes = data.CollisionHulls[0].Planes.Select(Bytes).ToList();
        var file = data.WriteTlm();
        var node = file.Root!.FindChild(OGIData.CollisionHullsKind)!.GetChildren().Single();
        node["twin_vertices"] = node["vertices"]!.DeepClone();
        node["twin_faces"] = node["faces"]!.DeepClone();

        data.ReadTlm(file);
        Assert.Equal(planes, data.CollisionHulls[0].Planes.Select(Bytes));

        node["translation"] = new JsonArray(0, 2, 0);
        data.ReadTlm(file);

        var moved = data.CollisionHulls[0];
        Assert.Equal(2.0f, moved.Vertexes.Min(vertex => vertex.Y));
        Assert.True(moved.DescribesFaces());
        Assert.NotEqual(planes, moved.Planes.Select(Bytes));
        Assert.Equal(2U, data.CollisionHullJoints[0]);

        // The top of the box raised in Blender: its four corners moved, the box is still a box
        node.Remove("translation");
        var vertexes = file.Read<float>(node["vertices"]);
        for (var vertex = 4; vertex < 8; vertex++)
        {
            vertexes[vertex * 4 + 1] += 0.5f;
        }

        node["vertices"] = file.Write(vertexes.AsSpan());
        data.ReadTlm(file);

        var raised = data.CollisionHulls[0];
        Assert.True(raised.DescribesFaces());
        Assert.Equal(1.5f, raised.Vertexes[4].Y);
        Assert.Equal(-1.5f, raised.Planes[5].W, 5);
    }

    // Blender keeps rigid bodies where they were put, they're moved into their joint's space
    [Fact]
    public void MovedBodiesAreBakedIntoTheirModel()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var positions = Positions(data.RigidModelIds[0]);
        var file = data.WriteTlm();
        file.Root!.FindChild(OGIData.RigidBodiesKind)!.GetChildren().Single()["translation"] = new JsonArray(0, 2, 0);

        data.ReadTlm(file);

        Assert.Equal(positions.Select(position => position + new Vector3(0, 2, 0)), Positions(data.RigidModelIds[0]));

        List<Vector3> Positions(LabURI rigidModel)
        {
            var model = _assets.Get(_assets.Get(rigidModel).GetData<RigidModelData>().Model).GetData<ModelData>();
            return model.Vertexes.SelectMany(vertexes => vertexes).Select(vertex => new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z)).ToList();
        }
    }

    private static JsonObject Joint(TlmFile file, int index)
    {
        return file.Root!.FindChild(OGIData.ArmatureKind)!["joints"]!.AsArray().OfType<JsonObject>().Single(joint => joint.GetInt("index") == index);
    }

    private static byte[] Bytes(Twinsanity.TwinsanityInterchange.Interfaces.ITwinSerializable item) => TestGeometry.Serialize(item);

    private static void AssertClose(Matrix4x4 expected, Matrix4x4 actual)
    {
        Assert.All(TlmNodes.ToArray(expected).Zip(TlmNodes.ToArray(actual)), pair => Assert.Equal(pair.First, pair.Second, 4));
    }

    // The add-on puts materials made in Blender into the file with their image
    [AvaloniaFact]
    public void MaterialsMadeInBlenderBecomeTheProjects()
    {
        _project.BuildProjectTree("Global PS2_Test/Material", "Global PS2_Test/Texture");
        var rigidModel = _assets.AddRigidModel("Chair", _assets.AddMaterial("Varnish").URI, _assets.AddMaterial("Paint").URI);
        rigidModel.Serialize(SerializationFlags.SaveData);
        var png = TextureData.CreateSolidColor(_assets.AddTexture("Scratch", 0), 8, 0xFF20C040).GetPngBytes();
        WithBlenderMaterial(rigidModel.FullDataPath, png);
        var assetsBefore = _project.AssetManager.GetAssets().Count(asset => !asset.IsInternal);

        var data = ((IAsset)rigidModel).GetData<RigidModelData>();

        var material = _assets.Get<Material>(data.Materials[1]);
        Assert.False(material.IsInternal);
        Assert.Equal("wood-id", material.Parameters[TlmMaterials.BlenderMaterialParameter]);
        var shader = ((IAsset)material).GetData<MaterialData>().Shaders.Single();
        Assert.Equal(TwinShader.AlphaBlending.ON, shader.ABlending);
        Assert.False(_assets.Get(shader.TextureId).IsInternal);
        Assert.Equal(assetsBefore + 2, _project.AssetManager.GetAssets().Count(asset => !asset.IsInternal));
        Assert.Equal(material.URI.ToString(), TlmFile.Load(rigidModel.FullDataPath).Materials[1]!["uri"]!.GetValue<string>());

        // Blender keeps exporting the material until the model is imported again
        WithBlenderMaterial(rigidModel.FullDataPath, png);
        rigidModel.UnloadData();
        data = ((IAsset)rigidModel).GetData<RigidModelData>();

        Assert.Equal(material.URI, data.Materials[1]);
        Assert.Equal(assetsBefore + 2, _project.AssetManager.GetAssets().Count(asset => !asset.IsInternal));
    }

    // Blender's images come in any size, the game's textures are powers of two of at most 256, with a palette where its tools laid one out
    [AvaloniaFact]
    public void ImagesMadeInBlenderGetTheGamesSizes()
    {
        _project.BuildProjectTree("Global PS2_Test/Material", "Global PS2_Test/Texture");
        var rigidModel = _assets.AddRigidModel("Chair", _assets.AddMaterial("Varnish").URI, _assets.AddMaterial("Paint").URI);
        rigidModel.Serialize(SerializationFlags.SaveData);
        WithBlenderMaterial(rigidModel.FullDataPath, TextureData.CreateSolidColor(_assets.AddTexture("Scratch", 0), 1024, 0xFF20C040).GetPngBytes());

        var data = ((IAsset)rigidModel).GetData<RigidModelData>();

        var texture = _assets.Get<Texture>(((IAsset)_assets.Get<Material>(data.Materials[1])).GetData<MaterialData>().Shaders.Single().TextureId);
        var textureData = ((IAsset)texture).GetData<TextureData>();
        Assert.Equal((256, 256), (textureData.Bitmap!.PixelSize.Width, textureData.Bitmap.PixelSize.Height));
        Assert.Equal(0xFF20C040, textureData.GetPixels()[0]);
        Assert.Equal((ITwinTexture.TexturePixelFormat.PSMCT32, false), (texture.PixelFormat, texture.GenerateMipmaps));
    }

    // Shadows fall on what the game's scenery and objects draw, and the characters casting them keep them off their skins, or their own
    // would darken them (the game's "no FBA" bit of a shader). TT Lab's new shaders take them like nearly every one of the game's
    [AvaloniaFact]
    public void MaterialsMadeInBlenderTakeShadowsLikeTheGamesOfTheirKind()
    {
        _project.BuildProjectTree("Global PS2_Test/Material", "Global PS2_Test/Texture");
        var rigidModel = _assets.AddRigidModel("Chair", _assets.AddMaterial("Varnish").URI, _assets.AddMaterial("Paint").URI);
        rigidModel.Serialize(SerializationFlags.SaveData);
        var skin = _assets.AddSkin(_assets.AddMaterial("Fur").URI);
        skin.Serialize(SerializationFlags.SaveData);
        var png = TextureData.CreateSolidColor(_assets.AddTexture("Scratch", 0), 8, 0xFF20C040).GetPngBytes();
        WithBlenderMaterial(rigidModel.FullDataPath, png);
        WithBlenderMaterial(skin.FullDataPath, png, 0, "fur-id");

        var rigid = ShaderOf(((IAsset)rigidModel).GetData<RigidModelData>().Materials[1]);
        var fur = ShaderOf(((IAsset)skin).GetData<SkinData>().SubSkins[0].Material);

        Assert.Equal((TwinShader.Type.StandardUnlit, true), (rigid.ShaderType, rigid.AlphaCorrectionValue));
        Assert.Equal((TwinShader.Type.LitSkinnedModel, false), (fur.ShaderType, fur.AlphaCorrectionValue));
        Assert.True(new LabShader().AlphaCorrectionValue);

        LabShader ShaderOf(LabURI material) => ((IAsset)_assets.Get<Material>(material)).GetData<MaterialData>().Shaders.Single();
    }

    // The skinned shader is the only one that draws a skin's packets, the game hung drawing a skin with a rigid one: a material made in
    // Blender that a rigid part and the skin share becomes one of each kind in the project, a skin part without one a skinned placeholder
    [AvaloniaFact]
    public void SkinsAreDrawnWithTheSkinnedShaderOnly()
    {
        _project.BuildProjectTree("Global PS2_Test/Material", "Global PS2_Test/Texture");
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        // The fur the mask and the skin share
        var png = TextureData.CreateSolidColor(_assets.AddTexture("Scratch", 0), 8, 0xFF20C040).GetPngBytes();
        file.Materials[0] = new JsonObject { ["name"] = "Fur", ["blender_id"] = "fur-id", ["image"] = new JsonObject { ["png"] = file.Write(png.AsSpan()), ["name"] = "fur.png" } };

        data.ReadTlm(file);
        var rigid = RigidMaterial();
        var skin = SkinMaterial();
        Assert.NotEqual(rigid, skin);
        Assert.Equal(TwinShader.Type.StandardUnlit, ShaderOf(rigid).ShaderType);
        Assert.Equal(TwinShader.Type.LitSkinnedModel, ShaderOf(skin).ShaderType);
        Assert.All([rigid, skin], material => Assert.Equal("fur-id", _assets.Get<Material>(material).Parameters[TlmMaterials.BlenderMaterialParameter]?.ToString()));

        // Read again, both are the ones made the first time
        data.ReadTlm(file);
        Assert.Equal((rigid, skin), (RigidMaterial(), SkinMaterial()));

        foreach (var part in file.Root!.Traverse().Where(node => node[TlmNodes.MeshKey] is JsonObject).SelectMany(node => node[TlmNodes.MeshKey]!["parts"]!.AsArray()))
        {
            part!["material"] = -1;
        }

        data.ReadTlm(file);
        Assert.Equal(TwinShader.Type.LitSkinnedModel, ShaderOf(SkinMaterial()).ShaderType);
        Assert.NotEqual(TwinShader.Type.LitSkinnedModel, ShaderOf(RigidMaterial()).ShaderType);

        LabURI RigidMaterial() => _project.AssetManager.GetAssetData<RigidModelData>(data.RigidModelIds[0]).Materials[0];
        LabURI SkinMaterial() => _project.AssetManager.GetAssetData<SkinData>(data.Skin).SubSkins[0].Material;
        LabShader ShaderOf(LabURI material) => _project.AssetManager.GetAssetData<MaterialData>(material).Shaders[0];
    }

    // NTSC's beach draws a part of Cortex's skin with a StandardLit material, which only gets a warning
    [Fact]
    public void SkinsOfAnotherShaderAreStillBuilt()
    {
        var skin = ((IAsset)_assets.AddSkin(_assets.AddMaterial("Varnish").URI)).GetData<SkinData>();
        var blendSkin = ((IAsset)_assets.AddBlendSkin(_assets.AddMaterial("Lacquer").URI)).GetData<BlendSkinData>();

        Assert.Equal(2, ((ITwinSkin)skin.Export(new PS2ItemFactory())).SubSkins.Count);
        Assert.NotEmpty(((ITwinBlendSkin)blendSkin.Export(new PS2ItemFactory())).SubBlends);
    }

    // Creating a project reads the files of skins two models share back while other writes write their materials and let go of them:
    // the check read a material half let go of, found no shaders and warned about PAL's Aku Aku
    [Fact]
    public void ReadingASkinWhileAProjectIsCreatedLeavesItsMaterialsAlone()
    {
        var data = ((IAsset)_assets.AddOgi()).GetData<OGIData>();
        var file = data.WriteTlm();
        // Let go of without a file to load it back from, looking at its data throws (its hash is worked out, like creation's are)
        var plain = _assets.AddMaterial("Plain");
        ((IAsset)plain).GetDataHash();
        ((IAsset)plain).UnloadData();
        file.Materials[0]!["uri"] = plain.URI.ToString();

        using (_project.AssetManager.HookLookups(_ => { }))
        {
            data.ReadTlm(file);
        }

        Assert.False(((IAsset)plain).IsLoaded);
    }

    private static void WithBlenderMaterial(string path, byte[] png, int index = 1, string id = "wood-id")
    {
        var file = TlmFile.Load(path);
        file.Materials[index] = new JsonObject { ["name"] = "Wood", ["blender_id"] = id, ["alpha"] = "BLEND", ["image"] = new JsonObject { ["png"] = file.Write(png.AsSpan()), ["name"] = "wood.png" } };
        file.Save(path);
    }
}
