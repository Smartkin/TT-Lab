using GlmSharp;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Graphics.TlModel;
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
using Matrix4x4 = System.Numerics.Matrix4x4;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Quaternion = System.Numerics.Quaternion;
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

    /// <param name="joints">The joints the made-up parts' joints 0, 1 and 2 stand for</param>
    public Skin AddSkin(LabURI material, string name = "Crash Skin", int[]? joints = null)
    {
        var skin = project.Add(new Skin(), name);
        var data = new SkinData(skin);
        data.SubSkins.AddRange(new[] { SubSkin(_random, TwinVifPadding.NopPerByte, 0, [38, 20]), SubSkin(_random, TwinVifPadding.QuadWord, 0, [9]) }.Select(subSkin =>
        {
            if (joints != null)
            {
                foreach (var info in subSkin.SkinJoints)
                {
                    info.JointIndex1 = joints[info.JointIndex1];
                    info.JointIndex2 = info.WeightsAmount > 1 ? joints[info.JointIndex2] : info.JointIndex2;
                    info.JointIndex3 = info.WeightsAmount > 2 ? joints[info.JointIndex3] : info.JointIndex3;
                }
            }

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

    /// <summary>
    /// A character skeleton of 15 joints aimed the way the game's tools aimed them, every joint with children pointing one of its axes at
    /// its child: the arms behavior-mirrored (the right joint's frame the left's mirror image with every axis turned around, so the right
    /// arm aims along -X), the legs plainly mirrored (only X turned around), the chest at the spine's place, the head and its nose looking a
    /// hair off straight ahead on the middle line, and two plainly mirrored eyes looking a hair off straight ahead to the side, short and
    /// far from the origin, which Blender keeps with their roll a few thousandths of a radian off. A skin weighted to the upper arms and an
    /// eye, rigid models on the left upper arm and the right hand, exit points on the right hand, the head and the right eye, a hull on the
    /// left hand, and two animations turning, moving and unevenly scaling the joints
    /// </summary>
    /// <remarks>
    /// The joints: 0 hips, 1 spine, 2 chest, 3 head, 4 nose, 5 and 6 the eyes (left, right), 7 and 9 the upper arms, 8 and 10 the hands, 11
    /// and 13 the thighs, 12 and 14 the shins. The game's left is its -X
    /// </remarks>
    public OGI AddMirroredOgi(string name = "Mirror")
    {
        var joints = MirroredSkeleton();
        var ogi = project.Add(new OGI(), name, 0x6);
        var material = AddSkinMaterial($"{name} Fur");
        var skin = AddSkin(material.URI, $"{name} Skin", [7, 9, 5]);
        var arm = AddRigidModel($"{name} Arm", material.URI, material.URI);
        var glove = AddRigidModel($"{name} Glove", material.URI, material.URI);
        var worlds = joints.Select(joint => joint.Frame.At(joint.Position)).ToArray();
        var data = new OGIData(ogi)
        {
            BoundingBox = [new Vector4(-1, 0, -1, 1), new Vector4(28, 2, 1.5f, 1)],
            Joints = joints.Select((joint, index) =>
            {
                // Rest poses that agree with the bind poses, like the game's
                var parentInverse = Matrix4x4.Identity;
                if (joint.Parent >= 0)
                {
                    Matrix4x4.Invert(worlds[joint.Parent], out parentInverse);
                }

                Matrix4x4.Decompose(worlds[index] * parentInverse, out _, out var rotation, out var translation);
                rotation = Quaternion.Normalize(rotation);
                return new TwinJoint
                {
                    Index = index, ParentIndex = joint.Parent < 0 ? 0xFF : joint.Parent, Id = joint.Id, ChildCount = joints.Count(other => other.Parent == index),
                    Detail = index == 0 ? 1 : 0, LocalTranslation = new Vector4(translation.X, translation.Y, translation.Z, 1),
                    LocalRotation = new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W), WorldTranslation = new Vector4(joint.Position.X, joint.Position.Y, joint.Position.Z, 1),
                    UnusedRotation = new Vector4(0, 0, 0, 1),
                    // The right upper arm's animations turn it by 20 degrees about its Y first
                    AdditionalAnimationRotation = index == 9 ? new Vector4(0, MathF.Sin(MathF.PI / 18), 0, MathF.Cos(MathF.PI / 18)) : new Vector4(0, 0, 0, 1)
                };
            }).ToList(),
            JointIdCount = (Byte)joints.Count(joint => joint.Id != 0xFF),
            SkinInverseMatrices = worlds.Select(world => (Matrix4x4.Invert(world, out var inverse) ? inverse : Matrix4x4.Identity).ToTwin()).ToList(),
            ExitPoints =
            [
                new TwinExitPoint { ID = 0, ParentJointIndex = 10, Matrix = (Matrix4x4.CreateFromYawPitchRoll(0.3f, -0.2f, 0.5f) * Matrix4x4.CreateTranslation(0.04f, -0.01f, 0.02f)).ToTwin() },
                new TwinExitPoint { ID = 1, ParentJointIndex = 3, Matrix = (Matrix4x4.CreateRotationX(0.35f) * Matrix4x4.CreateTranslation(0, 0.1f, 0.06f)).ToTwin() },
                new TwinExitPoint { ID = 2, ParentJointIndex = 6, Matrix = (Matrix4x4.CreateRotationY(-0.6f) * Matrix4x4.CreateTranslation(0.03f, 0.015f, -0.01f)).ToTwin() }
            ],
            RigidModelIds = [arm.URI, glove.URI],
            RigidModelJointIndices = [7, 10],
            CollisionHulls = [TwinCollisionHull.CreateBox(new Vector4(-0.05f, -0.03f, -0.04f, 1), new Vector4(0.05f, 0.03f, 0.04f, 1))],
            CollisionHullJoints = [8],
            Skin = skin.URI
        };

        // Every joint's rest pose, the keys move, turn and scale from
        var rests = data.Joints.Select(joint => (joint.LocalTranslation, Angles: new quat(joint.LocalRotation.X, joint.LocalRotation.Y, joint.LocalRotation.Z, joint.LocalRotation.W).ToEulerAngles())).ToList();
        short[] Key(int joint, int frame, int variant)
        {
            var (translation, angles) = rests[joint];
            var direction = joint % 2 == 0 ? 1 : -1;
            return
            [
                (short)(TlmAnimations.ToRawValue(translation.X) + (joint switch { 0 => frame * 120, 7 or 9 => frame * 25 * direction, _ => 0 }) + variant * 9),
                (short)(TlmAnimations.ToRawValue(translation.Y) + (joint == 0 ? frame * 60 : 0)),
                (short)(TlmAnimations.ToRawValue(translation.Z) + (joint is 11 or 13 ? frame * 30 : 0)),
                (short)(TlmAnimations.ToRawRotation(angles.x) + frame * 41 * direction + variant * 5),
                (short)(TlmAnimations.ToRawRotation(angles.y) + frame * 17 * direction),
                (short)(TlmAnimations.ToRawRotation(angles.z) - frame * 29 + joint * 3 * variant),
                (short)(joint switch { 7 => 4096 + frame * 200, 13 => 4096 - frame * 120, _ => 4096 }),
                (short)(joint switch { 1 => 4096 + frame * 300, 13 => 4096 + frame * 80, _ => 4096 }),
                (short)(joint switch { 7 => 4096 - frame * 150, 10 => 4096 + frame * 400, _ => 4096 })
            ];
        }

        data.Animations =
        [
            new AnimationData { ID = 0x20, Name = "Run", TotalFrames = 5, DefaultFPS = 25, MainAnimation = TestAnimations.CreateTwinAnimation(5, joints.Count, rawValues: (joint, frame) => Key(joint, frame, 0)) },
            new AnimationData { ID = 0x21, Name = "Reach", TotalFrames = 3, DefaultFPS = 30, MainAnimation = TestAnimations.CreateTwinAnimation(3, joints.Count, independentScaling: joint => joint == 10,
                additionalRotation: joint => joint == 9, rawValues: (joint, frame) => Key(joint, frame, 1)) }
        ];
        ogi.SetData(data);
        return ogi;
    }

    private readonly record struct SkeletonJoint(int Parent, int Id, Vector3 Position, JointFrame Frame);

    // A joint's frame: its axes in the model's space, Z = X x Y
    private readonly record struct JointFrame(Vector3 X, Vector3 Y, Vector3 Z)
    {
        public static JointFrame Of(Vector3 x, Vector3 y)
        {
            x = Vector3.Normalize(x);
            y = Vector3.Normalize(y - Vector3.Dot(y, x) * x);
            return new JointFrame(x, y, Vector3.Cross(x, y));
        }

        public static JointFrame OfZ(Vector3 z, Vector3 x)
        {
            z = Vector3.Normalize(z);
            x = Vector3.Normalize(x - Vector3.Dot(x, z) * z);
            return new JointFrame(x, Vector3.Cross(z, x), z);
        }

        public JointFrame TurnedAboutX(float angle) => new(X, MathF.Cos(angle) * Y + MathF.Sin(angle) * Z, MathF.Cos(angle) * Z - MathF.Sin(angle) * Y);

        public JointFrame TurnedAboutZ(float angle) => new(MathF.Cos(angle) * X + MathF.Sin(angle) * Y, MathF.Cos(angle) * Y - MathF.Sin(angle) * X, Z);

        // The mirror image in the model's X: the tools turned every axis of a behavior-mirrored joint around, only X of a plain mirror's
        public JointFrame Mirrored(bool behavior) => behavior ? new(-Mirror(X), -Mirror(Y), -Mirror(Z)) : new(-Mirror(X), Mirror(Y), Mirror(Z));

        // Its bind pose, transforming row vectors like System.Numerics
        public Matrix4x4 At(Vector3 position) => new(X.X, X.Y, X.Z, 0, Y.X, Y.Y, Y.Z, 0, Z.X, Z.Y, Z.Z, 0, position.X, position.Y, position.Z, 1);

        public static Vector3 Mirror(Vector3 value) => value with { X = -value.X };
    }

    private static List<SkeletonJoint> MirroredSkeleton()
    {
        var hips = new Vector3(0, 1, 0);
        var spine = new Vector3(0, 1.25f, 0);
        var head = new Vector3(0, 1.6f, 0.02f);
        var shoulder = new Vector3(-0.25f, 1.5f, 0);
        var hand = new Vector3(-0.55f, 1.35f, 0.05f);
        var thigh = new Vector3(-0.12f, 0.95f, 0);
        var knee = new Vector3(-0.14f, 0.5f, 0.02f);
        // X up the spine at the head, Z across; the chest half a turn about its X from it
        var spineAim = Vector3.Normalize(head - spine);
        var spineFrame = new JointFrame(spineAim, new Vector3(0, spineAim.Z, -spineAim.Y), -Vector3.UnitX);
        var chestFrame = new JointFrame(spineFrame.X, -spineFrame.Y, -spineFrame.Z);
        // X ahead a hair down at the nose, close enough to make the nose shorter than a fortieth of the skeleton, the nose's a hair up, Z
        // across: Blender's -Y, where it keeps bones pointing to the side a little imprecisely
        var headFrame = Pitched(1.2e-3f);
        var noseFrame = Pitched(-4e-4f);
        var nose = head + headFrame.X * 0.02f;
        // The left eye's X ahead a hair to the left and up, Y up
        var eye = head + new Vector3(-0.03f, 0.025f, 0.04f);
        const float eyeOff = 4e-4f;
        var eyeAzimuth = MathF.PI * 140 / 180;
        var eyeFrame = JointFrame.Of(new Vector3(MathF.Sin(eyeOff) * MathF.Cos(eyeAzimuth), MathF.Sin(eyeOff) * MathF.Sin(eyeAzimuth), MathF.Cos(eyeOff)), Vector3.UnitY);
        // X along the arm at the hand, twisted; the hand bent from it
        var armFrame = JointFrame.Of(hand - shoulder, Vector3.UnitY).TurnedAboutX(MathF.PI * 25 / 180);
        var handFrame = armFrame.TurnedAboutZ(MathF.PI / 6);
        // Z down the leg at the knee, X across turned forward; the shin bent from it
        var legFrame = JointFrame.OfZ(knee - thigh, Vector3.UnitX).TurnedAboutZ(MathF.PI / 12);
        var shinFrame = legFrame.TurnedAboutX(-MathF.PI / 15);
        return
        [
            new(-1, 0, hips, JointFrame.Of(Vector3.UnitX, Vector3.UnitY)),
            new(0, 0xFF, spine, spineFrame),
            new(1, 0xFF, spine, chestFrame),
            new(2, 1, head, headFrame),
            new(3, 0xFF, nose, noseFrame),
            new(3, 0xFF, eye, eyeFrame),
            new(3, 0xFF, JointFrame.Mirror(eye), eyeFrame.Mirrored(false)),
            new(2, 0xFF, shoulder, armFrame),
            new(7, 2, hand, handFrame),
            new(2, 0xFF, JointFrame.Mirror(shoulder), armFrame.Mirrored(true)),
            new(9, 3, JointFrame.Mirror(hand), handFrame.Mirrored(true)),
            new(0, 0xFF, thigh, legFrame),
            new(11, 0xFF, knee, shinFrame),
            new(0, 0xFF, JointFrame.Mirror(thigh), legFrame.Mirrored(false)),
            new(13, 0xFF, JointFrame.Mirror(knee), shinFrame.Mirrored(false))
        ];

        // X ahead turned down by the angle about the model's X, Z across
        static JointFrame Pitched(float angle) => JointFrame.Of(new Vector3(0, -MathF.Sin(angle), MathF.Cos(angle)), new Vector3(0, MathF.Cos(angle), MathF.Sin(angle)));
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
