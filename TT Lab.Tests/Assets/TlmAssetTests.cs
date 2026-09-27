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
        var skin = _assets.AddSkin(_assets.AddMaterial("Fur").URI);
        var before = _assets.Export(skin);

        _assets.Reload<SkinData>(skin);

        Assert.Equal(before, _assets.Export(skin));
    }

    [Fact]
    public void BlendSkinsComeBackTheSame()
    {
        var blendSkin = _assets.AddBlendSkin(_assets.AddMaterial("Fur").URI);
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
        Assert.Equal([OGIData.ArmatureKind, SkinData.TlmKind, BlendSkinData.TlmKind, OGIData.RigidBodiesKind, OGIData.ExitPointsKind], root.GetChildren().Select(child => child.GetKind()));
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
        Assert.Equal(1, data.Joints[2].ChildrenAmt1);
        Assert.Equal(4, data.SkinInverseMatrices.Count);
        Assert.Equal(-2.5f, data.SkinInverseMatrices[3].ToSystem().M42, 4);
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
        Assert.Equal((7U, 2U), (data.ExitPoints[0].ID, data.ExitPoints[0].ParentJointIndex));

        var exitPoint = file.Root!.FindChild(OGIData.ExitPointsKind)!.GetChildren().Single();
        exitPoint["translation"] = new JsonArray(0.5f, 0, 0);
        exitPoint[TlmNodes.JointKey] = 1;
        data.ReadTlm(file);

        Assert.Equal(1U, data.ExitPoints[0].ParentJointIndex);
        Assert.Equal(0.5f, data.ExitPoints[0].Matrix.ToSystem().M41);
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

    private static void WithBlenderMaterial(string path, byte[] png)
    {
        var file = TlmFile.Load(path);
        file.Materials[1] = new JsonObject { ["name"] = "Wood", ["blender_id"] = "wood-id", ["alpha"] = "BLEND", ["image"] = new JsonObject { ["png"] = file.Write(png.AsSpan()), ["name"] = "wood.png" } };
        file.Save(path);
    }
}
