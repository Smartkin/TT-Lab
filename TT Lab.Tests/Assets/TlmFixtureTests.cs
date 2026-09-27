using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common.Animation;

namespace TT_Lab.Tests.Assets;

// The Blender add-on's tests work with a model TT Lab wrote, and TT Lab reads the add-on's export of it back. Setting
// TTL_UPDATE_FIXTURES=1 writes the model again after changing the format, blender_roundtrip.py makes the add-on's export of it
[Collection(ProjectCollection.Name)]
public sealed class TlmFixtureTests : IDisposable
{
    private const string FixtureName = "ogi.tlm";
    private const string BlenderExportName = "ogi_from_blender.tlm";
    private const string BlenderEditName = "ogi_edited_in_blender.tlm";
    private const string SceneryFixtureName = "scenery.tlm";
    private const string SceneryBlenderExportName = "scenery_from_blender.tlm";

    private readonly TestProject _project = new();
    private readonly TestAssets _assets;
    private readonly OGI _ogi;

    public TlmFixtureTests()
    {
        _assets = new TestAssets(_project);
        _ogi = _assets.AddOgi();
        var data = ((IAsset)_ogi).GetData<OGIData>();
        var facial = new TwinMorphAnimation { TotalFrames = 4 };
        var settings = new MorphJointSettings { FacialShapesAmount = 2 };
        settings.AnimationMorph[0] = Enums.TransformType.Animated;
        settings.AnimationMorph[1] = Enums.TransformType.Static;
        facial.JointSettings.Add(settings);
        facial.StaticTransformations.Add(new Transformation { PureValue = 512 });
        for (var frame = 0; frame < 4; frame++)
        {
            var transformation = new AnimatedTransformation(1);
            transformation.Transforms.Add(new Transformation { PureValue = (short)(frame * 1024) });
            facial.AnimatedTransformations.Add(transformation);
        }

        data.Animations =
        [
            new AnimationData { ID = 0x10, Name = "Walk", TotalFrames = 4, DefaultFPS = 25, MainAnimation = TestAnimations.CreateTwinAnimation(4, 3, 0, joint => joint == 1), FacialAnimation = facial },
            new AnimationData { ID = 0x11, Name = "Wave", TotalFrames = 6, DefaultFPS = 25, MainAnimation = TestAnimations.CreateTwinAnimation(6, 3, 1, additionalRotation: joint => joint == 1) }
        ];
    }

    public void Dispose() => _project.Dispose();

    private static string FixturePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TT Lab.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "TT Lab", "BlenderTools", "tests", "fixtures", name);
    }

    private static byte[] Bytes(TlmFile file)
    {
        using var stream = new MemoryStream();
        file.WriteTo(stream);
        return stream.ToArray();
    }

    [Fact]
    public void FixtureIsWhatTtLabWrites()
    {
        CheckFixture(FixtureName, Bytes(((IAsset)_ogi).GetData<OGIData>().WriteTlm()));
    }

    [Fact]
    public void SceneryFixtureIsWhatTtLabWrites()
    {
        CheckFixture(SceneryFixtureName, Bytes(((IAsset)_assets.AddScenery()).GetData<SceneryData>().WriteTlm()));
    }

    // Nothing was edited in Blender, the tree, the placed meshes, the lights, the collision and the dynamic scenery come back the same
    [Fact]
    public void BlendersSceneryExportIsTheSameScenery()
    {
        var scenery = _assets.AddScenery();
        var data = ((IAsset)scenery).GetData<SceneryData>();
        var before = _assets.Export(scenery);
        var collisionBefore = _assets.Export(_assets.Get(data.Collision));
        var dynamicSceneryBefore = _assets.Export(_assets.Get(data.DynamicScenery));

        var read = new SceneryData(scenery);
        var changed = read.ReadTlm(TlmFile.Load(FixturePath(SceneryBlenderExportName)));
        scenery.SetData(read);

        Assert.False(changed);
        Assert.Equal(collisionBefore, _assets.Export(_assets.Get(read.Collision)));
        Assert.Equal(dynamicSceneryBefore, _assets.Export(_assets.Get(read.DynamicScenery)));
        Assert.Equal(before, _assets.Export(scenery));
    }

    private static void CheckFixture(string name, byte[] written)
    {
        var path = FixturePath(name);
        if (Environment.GetEnvironmentVariable("TTL_UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, written);
        }

        Assert.Equal(File.ReadAllBytes(path), written);
    }

    // Nothing was edited in Blender, the model comes back as the game has it
    [Fact]
    public void BlendersExportIsTheSameModel()
    {
        var data = ((IAsset)_ogi).GetData<OGIData>();
        var before = _assets.Export(_ogi);
        var skinBefore = _assets.Export(_assets.Get(data.Skin));
        var blendSkinBefore = _assets.Export(_assets.Get(data.BlendSkin));
        var rigidModelBefore = _assets.Export(_assets.Get(data.RigidModelIds[0]));
        var animationsBefore = data.Animations.Select(animation => (animation.ID, animation.Name, TlmAnimations.GetExactBytes(animation))).ToList();
        var joints = data.Joints.Select(TestGeometry.Serialize).ToList();

        var read = new OGIData(_ogi);
        var changed = read.ReadTlm(TlmFile.Load(FixturePath(BlenderExportName)));
        _ogi.SetData(read);

        Assert.False(changed);
        Assert.Equal(joints, read.Joints.Select(TestGeometry.Serialize));
        Assert.Equal(animationsBefore, read.Animations.Select(animation => (animation.ID, animation.Name, TlmAnimations.GetExactBytes(animation))));
        Assert.Equal(rigidModelBefore, _assets.Export(_assets.Get(read.RigidModelIds[0])));
        Assert.Equal(skinBefore, _assets.Export(_assets.Get(read.Skin)));
        Assert.Equal(blendSkinBefore, _assets.Export(_assets.Get(read.BlendSkin)));
        // Skins and models get their IDs from what they hold
        Assert.Equal(before, _assets.Export(_ogi));
    }

    // A bone turned on one frame and a vertex moved in Blender change only those, everything else stays as the game has it
    [Fact]
    public void EditsMadeInBlenderChangeOnlyWhatWasEdited()
    {
        var data = ((IAsset)_ogi).GetData<OGIData>();
        var walk = data.Animations[0];
        var wave = TlmAnimations.GetExactBytes(data.Animations[1]);
        var skinBefore = _assets.Export(_assets.Get(data.Skin));
        var modelBefore = _assets.Get(_assets.Get(data.RigidModelIds[0]).GetData<TT_Lab.AssetData.Graphics.RigidModelData>().Model).GetData<TT_Lab.AssetData.Graphics.ModelData>();
        var positions = modelBefore.Vertexes.Select(part => part.Select(vertex => (vertex.Position.X, vertex.Position.Y, vertex.Position.Z)).ToList()).ToList();

        var read = new OGIData(_ogi);
        var changed = read.ReadTlm(TlmFile.Load(FixturePath(BlenderEditName)));

        Assert.True(changed);
        Assert.Equal(wave, TlmAnimations.GetExactBytes(read.Animations[1]));
        for (var joint = 0; joint < walk.MainAnimation.JointSettings.Count; joint++)
        {
            for (var frame = 0; frame < walk.TotalFrames; frame++)
            {
                var before = TlmAnimations.GetRawValues(walk, joint, frame);
                var after = TlmAnimations.GetRawValues(read.Animations[0], joint, frame);
                if (joint == 1 && frame == 2)
                {
                    Assert.Equal(before[..3].Concat(before[6..]), after[..3].Concat(after[6..]));
                    Assert.NotEqual(before[3..6], after[3..6]);
                    continue;
                }

                Assert.Equal(before, after);
            }
        }

        Assert.Equal(skinBefore, _assets.Export(_assets.Get(read.Skin)));
        var model = _assets.Get(_assets.Get(read.RigidModelIds[0]).GetData<TT_Lab.AssetData.Graphics.RigidModelData>().Model).GetData<TT_Lab.AssetData.Graphics.ModelData>();
        var moved = model.Vertexes.Select(part => part.Select(vertex => (vertex.Position.X, vertex.Position.Y, vertex.Position.Z)).ToList()).ToList();
        Assert.Equal(positions[0][0].X + 0.1f, moved[0][0].X, 5);
        Assert.Equal(positions[0].Skip(1), moved[0].Skip(1));
        Assert.Equal(positions[1], moved[1]);
        Assert.All(model.Layouts, Assert.NotNull);
    }
}
