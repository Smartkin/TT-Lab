using Avalonia.Headless.XUnit;
using System.Numerics;
using System.Text.Json.Nodes;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Extensions;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Path = System.IO.Path;
using TwinShader = Twinsanity.TwinsanityInterchange.Common.TwinShader;

namespace TT_Lab.Tests.Assets;

// The Blender add-on's tests work with a model TT Lab wrote, and TT Lab reads the add-on's export of it back. Setting
// TTL_UPDATE_FIXTURES=1 writes the model again after changing the format, blender_roundtrip.py makes the add-on's export of it
[Collection(ProjectCollection.Name)]
public sealed class TlmFixtureTests : IDisposable
{
    private const string FixtureName = "ogi.tlm";
    private const string BlenderExportName = "ogi_from_blender.tlm";
    private const string BlenderEditName = "ogi_edited_in_blender.tlm";
    private const string BlenderCustomizedName = "ogi_customized_in_blender.tlm";
    private const string BlenderSwappedName = "ogi_swapped_in_blender.tlm";
    private const string BlenderRetargetedName = "ogi_retargeted_in_blender.tlm";
    private const string SceneryFixtureName = "scenery.tlm";
    private const string SceneryBlenderExportName = "scenery_from_blender.tlm";
    private const string SceneryCollisionName = "scenery_collision_from_blender.tlm";
    private const string SceneryAddedName = "scenery_added_in_blender.tlm";
    private const string OgiTemplateName = "ogi_template.tlm";
    private const string SceneryTemplateName = "scenery_template.tlm";
    private const string SaveIconTemplateName = "save_icon_template.tlm";
    private const string SaveIconFixtureName = "save_icon.tlm";
    private const string SaveIconBlenderExportName = "save_icon_from_blender.tlm";
    private const string SaveIconBlenderEditName = "save_icon_edited_in_blender.tlm";
    private const string MaterialsName = "ogi_materials_from_blender.tlm";

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

    [Fact]
    public void SaveIconFixtureIsWhatTtLabWrites()
    {
        CheckFixture(SaveIconFixtureName, Bytes(SaveIconTlm.Write("Crash", TestAssets.MakeSaveIcon())));
    }

    // Nothing was edited in Blender, the icon comes back as the game has it, and the same from what Blender shows of it
    [Fact]
    public void BlendersSaveIconExportIsTheSameIcon()
    {
        var expected = SaveIconTlm.ToBytes(TestAssets.MakeSaveIcon());
        var file = TlmFile.Load(FixturePath(SaveIconBlenderExportName));

        Assert.Equal(expected, SaveIconTlm.ToBytes(SaveIconTlm.Read(file)));
        file.Root!.Remove("exact");
        Assert.Equal(expected, SaveIconTlm.ToBytes(SaveIconTlm.Read(file)));
    }

    // blender_roundtrip.py --edit moved the icon's first vertex half a unit along X and halved the second shape's middle key, the first
    // shape's weight is what the second's leaves over
    [Fact]
    public void SaveIconEditedInBlenderHasTheEdits()
    {
        var expected = TestAssets.MakeSaveIcon();
        foreach (var corner in new[] { 0, 3 })
        {
            expected.Vertexes[corner].Positions[0] += 2048;
            expected.Vertexes[corner].Positions[4] += 2048;
        }

        expected.Frames[1].Keys[1] = new Twinsanity.TwinsanityInterchange.Implementations.PS2.SaveIconKey(30, 0.5f);
        expected.Frames[0].Keys[1] = new Twinsanity.TwinsanityInterchange.Implementations.PS2.SaveIconKey(30, 0.5f);

        var icon = SaveIconTlm.Read(TlmFile.Load(FixturePath(SaveIconBlenderEditName)));

        Assert.Equal(SaveIconTlm.ToBytes(expected), SaveIconTlm.ToBytes(icon));
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

    // blender_roundtrip.py --add put a mesh made in Blender into the scenery's Meshes and an empty with two meshes into its LODs: they're a
    // placed mesh and a LOD of two levels where Blender put them, after the scenery's own
    [Fact]
    public void MeshesAndLodsMadeInBlendersGroupsArePlaced()
    {
        var scenery = _assets.AddScenery();
        var before = ((IAsset)scenery).GetData<SceneryData>().Placements.Count;
        var expected = Expectations(SceneryAddedName);
        var read = new SceneryData(scenery);
        read.ReadTlm(TlmFile.Load(FixturePath(SceneryAddedName)));
        scenery.SetData(read);

        Assert.Equal(expected["Meshes"]!.GetValue<int>() + expected["Lods"]!.GetValue<int>(), read.Placements.Count);
        Assert.Equal(before + 2, read.Placements.Count);
        var added = read.Placements.Skip(before).ToList();
        var mesh = Assert.Single(added, placement => !placement.IsLod);
        var lod = Assert.Single(added, placement => placement.IsLod);
        Assert.Equal(Vector(expected, "AddedMesh"), mesh.Matrix.ToSystem().Translation);
        Assert.Equal(Vector(expected, "AddedLod"), lod.Matrix.ToSystem().Translation);
        Assert.Equal(expected["AddedLevels"]!.GetValue<int>(), _assets.Get(lod.Model).GetData<LodModelData>().Meshes.Count);
        Assert.NotEmpty(_assets.Export(scenery));
    }

    // blender_roundtrip.py --collision made the scenery's collision anew out of its meshes with the Generate Collision button: the
    // triangles and vertexes Blender made, all on one surface, with nothing of the game's order the build makes a tree for again
    [Fact]
    public void CollisionGeneratedInBlenderIsTheScenerysCollision()
    {
        var scenery = _assets.AddScenery();
        var expected = Expectations(SceneryCollisionName);
        var read = new SceneryData(scenery);
        read.ReadTlm(TlmFile.Load(FixturePath(SceneryCollisionName)));
        scenery.SetData(read);

        var collision = _assets.Get(read.Collision);
        var data = collision.GetData<CollisionData>();
        Assert.Equal(expected.GetInt("Triangles"), data.Triangles.Count);
        Assert.Equal(expected.GetInt("Vertexes"), data.Vertexes.Count);
        Assert.Equal(expected.GetString("Surface"), _assets.Get(Assert.Single(data.Triangles.Select(triangle => triangle.Surface).Distinct())).Alias);
        Assert.NotEmpty(_assets.Export(collision));
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

    // The add-on's export of the model after its material was edited in Blender (tests/blender_materials.py, against a project of its own
    // whose fur material has a shader of an animated U track over a lit one): the shader animation keyed, a picture painted on the first
    // shader and a new name, which the project's material takes
    [AvaloniaFact]
    public void MaterialSettingsChangedInBlenderAreTheProjects()
    {
        _project.BuildProjectTree("Global PS2_Test/Material", "Global PS2_Test/Texture");
        var fur = _project.AssetManager.GetAllAssetsOf<Material>().Single(material => material.Alias == "Crash Fur");

        new OGIData(_ogi).ReadTlm(TlmFile.Load(FixturePath(MaterialsName)));

        var data = ((IAsset)fur).GetData<MaterialData>();
        Assert.Equal("FUR", data.Name);
        Assert.Equal([TwinShader.Type.StandardLit, TwinShader.Type.UnlitEnvironmentMap], data.Shaders.Select(shader => shader.ShaderType));
        var animation = data.Shaders[1].Animation!;
        Assert.Equal((10, 4), (animation.FramesPerSecond, ShaderAnimationTracks.FrameCount(animation)));
        Assert.Equal([0.0f, 0.25f, 0.75f, 0.75f], Enumerable.Range(0, 4).Select(frame => ShaderAnimationTracks.ValueAt(animation, 0, frame)));
        Assert.False(ShaderAnimationTracks.IsAnimated(animation, 2));
        var texture = _assets.Get<Texture>(data.Shaders[0].TextureId);
        Assert.NotNull(texture.Parameters[TlmMaterials.BlenderImageParameter]);
        Assert.Equal(0xFFFF0000, ((IAsset)texture).GetData<TextureData>().GetPixels()[0]);
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

    private static JsonObject Expectations(string fixture)
    {
        return JsonNode.Parse(File.ReadAllText(Path.ChangeExtension(FixturePath(fixture), ".json")))!.AsObject();
    }

    private static Vector3 Vector(JsonObject expectations, string key)
    {
        var values = expectations.GetFloats(key);
        return new Vector3(values[0], values[1], values[2]);
    }

    private static List<Vector3> Positions(TestAssets assets, LabURI rigidModel)
    {
        var model = assets.Get(assets.Get(rigidModel).GetData<RigidModelData>().Model).GetData<ModelData>();
        return model.Vertexes.SelectMany(vertexes => vertexes).Select(vertex => new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z)).ToList();
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"Expected {expected}, got {actual}");
    }

    // What a modder does to a model in Blender: the skin's object put elsewhere under the root, the shape's weights left unnormalized,
    // a shape key added, a body and an exit point following bones through constraints, a body parented to a bone, a bone added and an
    // animation made longer, all exported while an animation is posed (see blender_roundtrip.py --customize)
    [Fact]
    public void CustomizationsMadeInBlenderComeThrough()
    {
        var data = ((IAsset)_ogi).GetData<OGIData>();
        var expectations = Expectations(BlenderCustomizedName);
        var skinBefore = _assets.Get(data.Skin).GetData<SkinData>().SubSkins.Select(subSkin => subSkin.Vertexes.Select(vertex => new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z)).ToList()).ToList();
        var blendSkinBefore = _assets.Get(data.BlendSkin).GetData<BlendSkinData>();
        var shapesBefore = blendSkinBefore.Blends.Select(blend => blend.ShapeOffsets.Select(shape => shape.Select(TestGeometry.Serialize).ToList()).ToList()).ToList();
        var weightsBefore = blendSkinBefore.Blends.Select(blend => blend.Vertexes.Select(vertex => TestGeometry.Serialize(vertex.JointInfo)).ToList()).ToList();
        var bodyBefore = _assets.Export(_assets.Get(data.RigidModelIds[0]));
        var walkBefore = data.Animations[0];
        var waveBefore = data.Animations[1];
        var hullBefore = data.CollisionHulls[0];

        var read = new OGIData(_ogi);
        var changed = read.ReadTlm(TlmFile.Load(FixturePath(BlenderCustomizedName)));
        _ogi.SetData(read);

        Assert.True(changed);
        // The moved hull is where its object is, with planes worked out for it
        var hull = read.CollisionHulls.Single();
        Assert.Equal(2U, read.CollisionHullJoints[0]);
        foreach (var (before, after) in hullBefore.Vertexes.Zip(hull.Vertexes))
        {
            AssertClose(new Vector3(before.X, before.Y, before.Z) + Vector(expectations, "hull_offset"), new Vector3(after.X, after.Y, after.Z));
        }

        Assert.Equal(hullBefore.Faces, hull.Faces);
        Assert.True(hull.DescribesFaces());
        Assert.NotEqual(hullBefore.Planes.Select(TestGeometry.Serialize), hull.Planes.Select(TestGeometry.Serialize));
        // Blender's matrices transform column vectors, TT Lab's row vectors
        var placement = Matrix4x4.Transpose(TlmNodes.ToMatrix(expectations.GetFloats("skin_placement")));
        var skin = _assets.Get(read.Skin).GetData<SkinData>();
        Assert.Equal(skinBefore.Select(part => part.Count), skin.SubSkins.Select(subSkin => subSkin.Vertexes.Count));
        foreach (var (before, after) in skinBefore.Zip(skin.SubSkins))
        {
            foreach (var (position, vertex) in before.Zip(after.Vertexes))
            {
                AssertClose(Vector3.Transform(position, placement), new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
            }
        }

        // The moved skin went past the model's box, which the add-on wrote around the meshes
        AssertHolds(read.BoundingBox, skin.SubSkins.SelectMany(subSkin => subSkin.Vertexes));

        // Halved weights are the same shares, the game's stay. The new shape moves the first vertex of the shape's mesh
        var blendSkin = _assets.Get(read.BlendSkin).GetData<BlendSkinData>();
        Assert.Equal(3, blendSkin.BlendsAmount);
        Assert.Equal(weightsBefore, blendSkin.Blends.Select(blend => blend.Vertexes.Select(vertex => TestGeometry.Serialize(vertex.JointInfo)).ToList()));
        Assert.Equal(shapesBefore, blendSkin.Blends.Select(blend => blend.ShapeOffsets.Take(2).Select(shape => shape.Select(TestGeometry.Serialize).ToList()).ToList()));
        var newShape = blendSkin.Blends.SelectMany(blend => blend.ShapeOffsets[2]).Select(offset => new Vector3(offset.X, offset.Y, offset.Z)).ToList();
        AssertClose(Vector(expectations, "new_shape_offset"), newShape[0]);
        Assert.All(newShape.Skip(1), offset => AssertClose(Vector3.Zero, offset));

        // The bodies are where they were put relative to their joint's rest, whatever pose was on
        Assert.Equal([1, 2, 1], read.RigidModelJointIndices.Select(joint => (int)joint));
        Assert.Equal(bodyBefore, _assets.Export(_assets.Get(read.RigidModelIds[0])));
        var hat = Positions(_assets, read.RigidModelIds[1]);
        Assert.Equal(8, hat.Count);
        AssertClose(Vector(expectations, "hat_offset"), hat.Aggregate(Vector3.Add) / hat.Count);
        Assert.Equal(1.0f, hat.Max(position => position.X) - hat.Min(position => position.X), 4);
        var badge = Positions(_assets, read.RigidModelIds[2]);
        AssertClose(Vector(expectations, "badge_offset"), badge.Aggregate(Vector3.Add) / badge.Count);

        // The new exit point's ID 9 became its place
        Assert.Equal([0U, 1U], read.ExitPoints.Select(exitPoint => exitPoint.ID));
        Assert.Equal(1U, read.ExitPoints[1].ParentJointIndex);
        var hand = read.ExitPoints[1].Matrix.ToSystem();
        AssertClose(Vector(expectations, "hand_offset"), hand.Translation);
        Assert.True(TlmNodes.IsIdentity(hand with { M41 = 0, M42 = 0, M43 = 0 }, 1e-5f));

        // Every joint reads its settings from the animation playing, the animations got the added bone's. The facial animation got the
        // new shape, and Walk two frames more of its last pose and weights: the game reads the face at the main animation's frame
        Assert.Equal(expectations.GetInt("joints"), read.Joints.Count);
        Assert.Equal((3, 2), (read.Joints[3].Index, read.Joints[3].ParentIndex));
        Assert.All(read.Animations, animation => Assert.Equal(read.Joints.Count, animation.MainAnimation.JointSettings.Count));
        var walk = read.Animations[0];
        Assert.Equal(expectations.GetInt("walk_frames"), walk.TotalFrames);
        Assert.Equal(walk.TotalFrames, walk.FacialAnimation.TotalFrames);
        Assert.Equal(3, walk.FacialAnimation.JointSettings[0].FacialShapesAmount);
        for (var joint = 0; joint < walkBefore.MainAnimation.JointSettings.Count; joint++)
        {
            for (var frame = 0; frame < walk.TotalFrames; frame++)
            {
                Assert.Equal(TlmAnimations.GetRawValues(walkBefore, joint, Math.Min(frame, walkBefore.TotalFrames - 1)), TlmAnimations.GetRawValues(walk, joint, frame));
            }
        }

        for (var frame = 0; frame < walk.TotalFrames; frame++)
        {
            Assert.Equal(TlmAnimations.GetFacialRawValues(walkBefore.FacialAnimation, Math.Min(frame, walkBefore.TotalFrames - 1)).Append((short)0),
                TlmAnimations.GetFacialRawValues(walk.FacialAnimation, frame));
        }

        // The animation without a face keeps the game's values of the joints it had, with the bone it turns now in Euler angles
        var wave = read.Animations[1];
        for (var joint = 0; joint < waveBefore.MainAnimation.JointSettings.Count; joint++)
        {
            for (var frame = 0; frame < waveBefore.TotalFrames; frame++)
            {
                Assert.Equal(TlmAnimations.GetRawValues(waveBefore, joint, frame), TlmAnimations.GetRawValues(wave, joint, frame));
            }
        }

        // An animation keyed in Blender from frame 1 at 60 frames a second has every other frame of the action's at 30, the bone in
        // Euler angles turned by its keys
        var nod = read.Animations[2];
        var angles = expectations.GetFloats("nod_angles");
        Assert.Equal((angles.Length, expectations.GetInt("nod_fps"), true), (nod.TotalFrames, nod.DefaultFPS, nod.ID >= 0x8000));
        var rest = JointRotation(nod, 2, 0);
        for (var frame = 0; frame < angles.Length; frame++)
        {
            var dot = Math.Min(1f, Math.Abs(glm.Dot(rest, JointRotation(nod, 2, frame))));
            Assert.Equal(angles[frame], 2 * MathF.Acos(dot), 2);
        }
    }

    // The game takes the OGI's box for its instances' collision without hulls, shadows and physics
    private static void AssertHolds(Twinsanity.TwinsanityInterchange.Common.Vector4[] box, IEnumerable<TT_Lab.AssetData.Graphics.SubModels.Vertex> vertexes)
    {
        Assert.Equal((1f, 1f), (box[0].W, box[1].W));
        Assert.All(vertexes, vertex => Assert.True(vertex.Position.X >= box[0].X - 1e-3f && vertex.Position.Y >= box[0].Y - 1e-3f && vertex.Position.Z >= box[0].Z - 1e-3f &&
                                                   vertex.Position.X <= box[1].X + 1e-3f && vertex.Position.Y <= box[1].Y + 1e-3f && vertex.Position.Z <= box[1].Z + 1e-3f,
            $"({vertex.Position.X}, {vertex.Position.Y}, {vertex.Position.Z}) is outside the box"));
    }

    // The game builds a joint's rotation from its angles in 4096ths of a turn
    private static quat JointRotation(AnimationData animation, int joint, int frame)
    {
        var values = TlmAnimations.GetRawValues(animation, joint, frame);
        return new quat(new vec3(values[3], values[4], values[5]) * (MathF.PI * 2 / 4096));
    }

    // The skin and the shape swapped for meshes made in Blender: a cube weighted to two bones without normalizing and a triangle
    // with two shape keys of its own, neither with a material (see blender_roundtrip.py --swap)
    [Fact]
    public void ModelsSwappedInBlenderComeThrough()
    {
        var data = ((IAsset)_ogi).GetData<OGIData>();
        var expectations = Expectations(BlenderSwappedName);
        var walkBefore = TlmAnimations.GetExactBytes(data.Animations[0]);
        var bodyBefore = _assets.Export(_assets.Get(data.RigidModelIds[0]));

        var read = new OGIData(_ogi);
        read.ReadTlm(TlmFile.Load(FixturePath(BlenderSwappedName)));
        _ogi.SetData(read);

        var skin = _assets.Get(read.Skin).GetData<SkinData>();
        var part = skin.SubSkins.Single();
        // The cube went past the model's box, which the add-on wrote around the meshes
        AssertHolds(read.BoundingBox, part.Vertexes);
        Assert.Equal(8, part.Vertexes.Count);
        Assert.Equal(12, part.Faces.Count);
        var weights = expectations.GetFloats("skin_weights");
        Assert.All(part.Vertexes, vertex =>
        {
            Assert.Equal((1, 2, 2), (vertex.JointInfo.JointIndex1, vertex.JointInfo.JointIndex2, vertex.JointInfo.WeightsAmount));
            Assert.Equal(weights[0], vertex.JointInfo.Weight1, 5);
            Assert.Equal(weights[1], vertex.JointInfo.Weight2, 5);
        });
        Assert.True(_assets.Get(part.Material).IsInternal);
        Assert.NotEmpty(_assets.Export(_assets.Get(read.Skin)));

        var blendSkin = _assets.Get(read.BlendSkin).GetData<BlendSkinData>();
        var blend = blendSkin.Blends.Single();
        Assert.Equal((2, 3, 2), (blendSkin.BlendsAmount, blend.Vertexes.Count, blend.ShapeOffsets.Count));
        Assert.All(blend.Vertexes, vertex => Assert.Equal((0, 1.0f, 1), (vertex.JointInfo.JointIndex1, vertex.JointInfo.Weight1, vertex.JointInfo.WeightsAmount)));
        AssertClose(Vector(expectations, "smile_offset"), new Vector3(blend.ShapeOffsets[0][0].X, blend.ShapeOffsets[0][0].Y, blend.ShapeOffsets[0][0].Z));
        AssertClose(Vector(expectations, "frown_offset"), new Vector3(blend.ShapeOffsets[1][1].X, blend.ShapeOffsets[1][1].Y, blend.ShapeOffsets[1][1].Z));
        Assert.NotEmpty(_assets.Export(_assets.Get(read.BlendSkin)));

        // The facial animation plays on the new shape keys in the order it had them
        Assert.Equal(walkBefore, TlmAnimations.GetExactBytes(read.Animations[0]));
        Assert.Equal(bodyBefore, _assets.Export(_assets.Get(read.RigidModelIds[0])));
    }

    // The animations retargeted to a copy of the model whose bone "Joint 1" was moved up and "Joint 2" named "Tail", keeping the
    // joints where the animations put them: the copy's joints rest elsewhere, its animations are the game's (see blender_roundtrip.py
    // --retarget)
    [Fact]
    public void AnimationsRetargetedInBlenderAnimateTheMovedJointFromWhereItRests()
    {
        var data = ((IAsset)_ogi).GetData<OGIData>();
        var expectations = Expectations(BlenderRetargetedName);
        var animationsBefore = data.Animations.Select(animation => (animation.ID, animation.Name, animation.TotalFrames, TlmAnimations.GetExactBytes(animation))).ToList();
        var offset = Vector(expectations, "offset");
        // The test skeleton's bind poses don't agree with its rest poses like the game's do, bones rest at the bind poses
        var binds = data.SkinInverseMatrices.Select(inverse => Matrix4x4.Invert(inverse.ToSystem(), out var bind) ? bind : Matrix4x4.Identity).ToList();
        var inverseBindsBefore = data.SkinInverseMatrices.Select(TestGeometry.Serialize).ToList();

        var read = new OGIData(_ogi);
        read.ReadTlm(TlmFile.Load(FixturePath(BlenderRetargetedName)));

        Assert.Equal(2, expectations.GetInt("retargeted"));
        Assert.Equal(1.0f, expectations["scale"]!.GetValue<float>(), 4);
        // The copies keep Blender's names while the armature isn't in the model's place, putting it there gives them the originals'
        Assert.Equal(animationsBefore.Select(animation => (animation.ID, animation.Name + ".001", animation.TotalFrames)), read.Animations.Select(animation => (animation.ID, animation.Name, animation.TotalFrames)));
        Assert.Equal(binds[1].Translation.Y + offset.Y, read.Joints[1].WorldTranslation.Y, 4);
        Assert.Equal(binds[2].Translation.Y - binds[1].Translation.Y - offset.Y, read.Joints[2].LocalTranslation.Y, 4);
        Assert.Equal(inverseBindsBefore[0], TestGeometry.Serialize(read.SkinInverseMatrices[0]));
        Assert.Equal(inverseBindsBefore[2], TestGeometry.Serialize(read.SkinInverseMatrices[2]));
        Assert.Equal(-(binds[1].Translation.Y + offset.Y), read.SkinInverseMatrices[1].ToSystem().M42, 4);

        // Every joint turns like before, the moved joint keeps its offset from its parent's frame on every frame and its child
        // rests that much closer to it: the animation's translations are in the parent's frame, the offset in the model's
        var movedInParent = Vector3.Transform(offset, Quaternion.Inverse(RotationOf(binds[0])));
        var childInParent = Vector3.Transform(offset, Quaternion.Inverse(RotationOf(binds[1])));
        for (var index = 0; index < data.Animations.Count; index++)
        {
            var before = data.Animations[index];
            var after = read.Animations[index];
            for (var joint = 0; joint < before.MainAnimation.JointSettings.Count; joint++)
            {
                for (var frame = 0; frame < before.TotalFrames; frame++)
                {
                    var expected = TlmAnimations.GetRawValues(before, joint, frame);
                    var actual = TlmAnimations.GetRawValues(after, joint, frame);
                    Assert.Equal(expected[3..], actual[3..]);
                    var shift = joint switch { 1 => movedInParent, 2 => -childInParent, _ => Vector3.Zero };
                    for (var axis = 0; axis < 3; axis++)
                    {
                        Assert.Equal(expected[axis] / 4096.0f + shift[axis], actual[axis] / 4096.0f, 3);
                    }
                }
            }
        }
    }

    private static Quaternion RotationOf(Matrix4x4 matrix)
    {
        Assert.True(Matrix4x4.Decompose(matrix, out _, out var rotation, out _));
        return rotation;
    }

    // The add-on's OGI template (Add > Twin Tech > OGI Model, blender_templates.py exports it as it comes) is a model: two joints (one joint
    // and no exit points and the game would draw no skin), its box a rigid body on the second, and a skin and a blend skin left empty to
    // fill, which the model doesn't have until they are
    [Fact]
    public void OgiTemplateMadeInBlenderIsAModel()
    {
        var read = new OGIData(_ogi);
        read.ReadTlm(TlmFile.Load(FixturePath(OgiTemplateName)));
        _ogi.SetData(read);

        // The game marks a root joint's parent with 0xFF
        Assert.Equal([(0, 0xFF), (1, 0)], read.Joints.Select(joint => (joint.Index, joint.ParentIndex)));
        Assert.Equal(LabURI.Empty, read.Skin);
        Assert.Equal(LabURI.Empty, read.BlendSkin);
        Assert.Empty(read.Animations);
        var body = Assert.Single(read.RigidModelIds);
        Assert.Equal((Byte)1, Assert.Single(read.RigidModelJointIndices));
        Assert.Empty(read.ExitPoints);
        Assert.Empty(read.CollisionHulls);
        Assert.Equal((-0.5f, 0f, -0.5f, 1f), (read.BoundingBox[0].X, read.BoundingBox[0].Y, read.BoundingBox[0].Z, read.BoundingBox[0].W));
        Assert.Equal((0.5f, 1f, 0.5f, 1f), (read.BoundingBox[1].X, read.BoundingBox[1].Y, read.BoundingBox[1].Z, read.BoundingBox[1].W));
        Assert.NotEmpty(_assets.Export(_assets.Get(body)));
        Assert.NotEmpty(_assets.Export(_ogi));
    }

    // The add-on's save icon template (Add > Twin Tech > PS2 Save Icon) is a save icon: a box of the console's full vertex colors, the game's
    // icon's header, and the material's picture as its texture
    [Fact]
    public void SaveIconTemplateMadeInBlenderIsASaveIcon()
    {
        var icon = SaveIconTlm.Read(TlmFile.Load(FixturePath(SaveIconTemplateName)));

        Assert.Equal(1, icon.ShapeCount);
        // A box: 12 triangles, every corner of every one a vertex of the icon
        Assert.Equal(36, icon.Vertexes.Count);
        Assert.All(icon.Vertexes, vertex => Assert.Equal(0xFF808080u, vertex.Color));
        Assert.Equal((0x10000u, 6u, 1u), (icon.FileId, icon.TextureType, icon.FrameLength));
        // The checker's two greys
        Assert.Equal(2, icon.Texture.Distinct().Count());
        Assert.NotEmpty(SaveIconTlm.ToBytes(icon));
    }

    // The add-on's scenery template is a scenery: the ground placed in it, an ambient and a directional light, the
    // ground as the collision on the surface its material is named after, and an empty dynamic scenery
    [Fact]
    public void SceneryTemplateMadeInBlenderIsAScenery()
    {
        var defaultSurface = _project.Add(new CollisionSurface { Chunk = "default" }, "SURF_DEFAULT_0", 0);
        _project.Add(new CollisionSurface { Chunk = "default" }, "SURF_ICE_1", 1);
        var scenery = _project.Add(new Scenery { Chunk = "levels/test" }, "Scenery 0");
        var read = new SceneryData(scenery);
        read.ReadTlm(TlmFile.Load(FixturePath(SceneryTemplateName)));
        scenery.SetData(read);

        Assert.False(Assert.Single(read.Placements).IsLod);
        Assert.True(read.HasLighting);
        // The lights new chunks get, a third grey at 4.5 and 3
        var ambient = Assert.Single(read.AmbientLights);
        Assert.Equal((DefaultLights.ThirdGrey, DefaultLights.AmbientIntensity), (ambient.Color.X, ambient.Intensity));
        var sun = Assert.Single(read.DirectionalLights);
        Assert.True(sun.Direction.Y > 0.9f);
        Assert.Equal((DefaultLights.ThirdGrey, DefaultLights.Intensity), (sun.Color.Y, sun.Intensity));
        Assert.Empty(read.PointLights);
        Assert.Empty(read.SpotLights);
        var collision = _assets.Get(read.Collision).GetData<CollisionData>();
        Assert.Equal(2, collision.Triangles.Count);
        Assert.All(collision.Triangles, triangle => Assert.Equal(defaultSurface.URI, triangle.Surface));
        Assert.NotEqual(LabURI.Empty, read.DynamicScenery);
        Assert.Empty(_assets.Get(read.DynamicScenery).GetData<DynamicSceneryData>().DynamicModels);
        Assert.NotEmpty(_assets.Export(scenery));
    }
}
