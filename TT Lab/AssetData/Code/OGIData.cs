using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using GlmSharp;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Skin = TT_Lab.Assets.Graphics.Skin;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Code;

[ReferencesAssets]
public class OGIData : AbstractAssetData
{
    public OGIData(IAsset asset) : base(asset)
    {
        BoundingBox = [new Vector4(0, 0, 0, 1), new Vector4(10, 10, 10, 1)];
        var rootJoint = new TwinJoint
        {
            Index = 0,
            LocalRotation = new Vector4(0, 0, 0, 1),
            LocalTranslation = new Vector4(0, 0, 0, 1),
            ParentIndex = 255
        };
        Joints = new List<TwinJoint>
        {
            rootJoint
        };
        ExitPoints = [];
        RigidModelJointIndices = [0];
        RigidModelIds = [LabURI.Empty];
        SkinInverseMatrices = [mat4.Identity.ToTwin()];
        Skin = LabURI.Empty;
        BlendSkin = LabURI.Empty;
        BoundingBoxBuilders = [];
        BoundingBoxBuilderToJointIndex = [];
        Animations = [];
    }

    public OGIData(IAsset asset, ITwinOGI ogi) : this(asset)
    {
        SetTwinItem(ogi);
    }

    public void SetAnimations(List<AnimationData> animations)
    {
        Animations = animations;
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        WriteTlm().Save(dataPath);
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        // Chunks building in parallel load the same OGIs, and loading one can write its file
        lock (Owner)
        {
            if (ReadTlm(TlmFile.Load(dataPath)))
            {
                // Animations edited in Blender are kept as the game stores them from now on, new ones keep the IDs they got and materials
                // made in Blender are referred to
                SaveInternal(dataPath, settings);
            }

            DisposedValue = false;
        }
    }

    public const string TlmAssetType = "Ogi";
    public const string TlmKind = "ogi";
    public const string ArmatureKind = "armature";
    public const string RigidBodiesKind = "rigid_bodies";
    public const string BodyKind = "body";
    public const string ExitPointsKind = "exit_points";
    public const string ExitPointKind = "exit_point";
    private const Int32 NoParent = 0xFF;

    internal TlmFile WriteTlm()
    {
        var assetManager = AssetManager.Get();
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var materials = new TlmMaterials(file);
        var root = TlmNodes.Create(TlmKind, Owner.Name, new JsonObject
        {
            ["BoundingBoxMin"] = TlmJson.ToJson(BoundingBox[0]),
            ["BoundingBoxMax"] = TlmJson.ToJson(BoundingBox[1]),
            ["Collisions"] = new JsonArray(BoundingBoxBuilders.Select((builder, i) => (JsonNode)new JsonObject
            {
                ["Joint"] = (Int32)BoundingBoxBuilderToJointIndex.ElementAtOrDefault(i),
                ["Points"] = TlmJson.ToJson(builder.BoundingBoxPoints.SelectMany(v => new[] { v.X, v.Y, v.Z, v.W })),
                ["UnkVectors1"] = TlmJson.ToJson(builder.UnkVectors1.SelectMany(v => new[] { v.X, v.Y, v.Z, v.W })),
                ["UnkVectors2"] = TlmJson.ToJson(builder.UnkVectors2.SelectMany(v => new[] { v.X, v.Y, v.Z, v.W })),
                ["UnkVectors3"] = TlmJson.ToJson(builder.UnkVectors3.SelectMany(v => new[] { v.X, v.Y, v.Z, v.W })),
                ["UnkShorts"] = TlmJson.ToJson(builder.UnkShorts.Select(v => (Int32)v)),
                ["UnkBytes1"] = TlmJson.ToJson(builder.UnkBytes1.Select(v => (Int32)v)),
                ["UnkBytes2"] = TlmJson.ToJson(builder.UnkBytes2.Select(v => (Int32)v))
            }).ToArray())
        });

        var armature = root.AddChild(TlmNodes.Create(ArmatureKind, "Armature"));
        var restWorlds = GetRestWorldMatrices();
        armature["joints"] = new JsonArray(Joints.Select((joint, i) =>
        {
            var inverse = i < SkinInverseMatrices.Count ? SkinInverseMatrices[i].ToSystem() : Matrix4x4.Invert(restWorlds[i], out var restInverse) ? restInverse : Matrix4x4.Identity;
            var bind = Matrix4x4.Invert(inverse, out var inverted) ? inverted : restWorlds[i];
            return (JsonNode)new JsonObject
            {
                ["index"] = joint.Index,
                ["parent"] = joint.ParentIndex == NoParent ? -1 : joint.ParentIndex,
                ["name"] = $"Joint {joint.Index}",
                ["bind"] = TlmNodes.ToColumnVectorJson(bind),
                [TlmNodes.DataKey] = new JsonObject
                {
                    ["ReactId"] = joint.ReactId,
                    ["ChildrenAmt2"] = joint.ChildrenAmt2,
                    ["AdditionalAnimationRotation"] = TlmJson.ToJson(joint.AdditionalAnimationRotation),
                    ["LocalTranslation"] = TlmJson.ToJson(joint.LocalTranslation),
                    ["LocalRotation"] = TlmJson.ToJson(joint.LocalRotation),
                    ["WorldTranslation"] = TlmJson.ToJson(joint.WorldTranslation),
                    ["InverseBindMatrix"] = TlmJson.ToJson(TlmNodes.ToArray(inverse)),
                    ["UnusedRotation"] = TlmJson.ToJson(joint.UnusedRotation)
                }
            };
        }).ToArray());
        armature["animations"] = new JsonArray(Animations.Select(animation => (JsonNode)TlmAnimations.Write(file, animation, Joints)).ToArray());

        if (Skin != LabURI.Empty)
        {
            var skin = root.AddChild(TlmNodes.Create(SkinData.TlmKind, "Skin"));
            skin[TlmNodes.MeshKey] = assetManager.GetAssetData<SkinData>(Skin).WriteTlmMesh(file, materials);
        }

        if (BlendSkin != LabURI.Empty)
        {
            var blendSkinData = assetManager.GetAssetData<BlendSkinData>(BlendSkin);
            var shape = root.AddChild(TlmNodes.Create(BlendSkinData.TlmKind, "Blend Skin", blendSkinData.WriteTlmData()));
            shape[TlmNodes.MeshKey] = blendSkinData.WriteTlmMesh(file, materials);
        }

        var bodies = root.AddChild(TlmNodes.Create(RigidBodiesKind, "Rigid Bodies"));
        for (var i = 0; i < RigidModelIds.Count; i++)
        {
            var body = bodies.AddChild(TlmNodes.Create(BodyKind, $"Rigid Model {i}", new JsonObject { ["Order"] = i }));
            body[TlmNodes.JointKey] = (Int32)RigidModelJointIndices.ElementAtOrDefault(i);
            if (RigidModelIds[i] != LabURI.Empty && assetManager.DoesAssetExist(RigidModelIds[i]))
            {
                body[TlmNodes.MeshKey] = assetManager.GetAssetData<RigidModelData>(RigidModelIds[i]).WriteTlmMesh(file, materials);
            }
        }

        var exitPoints = root.AddChild(TlmNodes.Create(ExitPointsKind, "Exit Points"));
        foreach (var exitPoint in ExitPoints)
        {
            var node = exitPoints.AddChild(TlmNodes.Create(ExitPointKind, $"Exit Point {exitPoint.ID}", new JsonObject
            {
                ["Id"] = TlmJson.ToJson(exitPoint.ID),
                ["Matrix"] = TlmJson.ToJson(TlmNodes.ToArray(exitPoint.Matrix.ToSystem()))
            }));
            node[TlmNodes.JointKey] = (Int32)exitPoint.ParentJointIndex;
            node.SetTransform(exitPoint.Matrix.ToSystem());
        }

        file.Root = root;
        return file;
    }

    // Where every joint's rest pose puts it in the model, for joints without an inverse bind matrix
    private Matrix4x4[] GetRestWorldMatrices()
    {
        var result = new Matrix4x4[Joints.Count];
        for (var i = 0; i < Joints.Count; i++)
        {
            var joint = Joints[i];
            var local = Matrix4x4.CreateFromQuaternion(new Quaternion(joint.LocalRotation.X, joint.LocalRotation.Y, joint.LocalRotation.Z, joint.LocalRotation.W)) *
                        Matrix4x4.CreateTranslation(joint.LocalTranslation.X, joint.LocalTranslation.Y, joint.LocalTranslation.Z);
            result[i] = joint.ParentIndex < i ? local * result[joint.ParentIndex] : local;
        }

        return result;
    }

    /// <summary>
    /// Reads the model back from its file
    /// </summary>
    /// <returns>Whether the file has to be written again: animations were edited in Blender or got new IDs, materials made in Blender
    /// became the project's, or its skins' faces were brought up to date</returns>
    internal bool ReadTlm(TlmFile file)
    {
        var root = file.Root ?? new JsonObject();
        var materials = new TlmMaterials(file, Owner);
        ReadRootData(root.GetData());
        var armature = root.FindChild(ArmatureKind);
        ReadJoints(armature);

        var rigidModels = new List<(Int32 Order, Byte Joint, LabURI Model)>();
        foreach (var body in root.FindChild(RigidBodiesKind).Traverse().Where(node => node.GetKind() == BodyKind))
        {
            var mesh = body[TlmNodes.MeshKey] as JsonObject;
            var rigidModel = mesh == null
                ? LabURI.Empty
                : RigidModelData.ReadTlm<RigidModel>(Owner, file, mesh, materials, body.GetTransform(), $"RigidModel_{rigidModels.Count}").URI;
            rigidModels.Add((body.GetData().GetInt("Order", Int32.MaxValue), ToJoint(body), rigidModel));
        }

        RigidModelJointIndices.Clear();
        RigidModelIds.Clear();
        foreach (var (_, joint, rigidModel) in rigidModels.OrderBy(r => r.Order))
        {
            RigidModelJointIndices.Add(joint);
            RigidModelIds.Add(rigidModel);
        }

        ExitPoints.Clear();
        foreach (var node in root.FindChild(ExitPointsKind).Traverse().Where(node => node.GetKind() == ExitPointKind))
        {
            var data = node.GetData();
            ExitPoints.Add(new TwinExitPoint
            {
                ID = data.GetUInt("Id"),
                ParentJointIndex = ToJoint(node),
                Matrix = TlmNodes.KeepStored(data.GetFloats("Matrix"), node.GetTransform(), out _).ToTwin()
            });
        }

        ExitPoints.Sort((e1, e2) => e1.ID.CompareTo(e2.ID));
        ReadSkins(file, root, materials);
        var animationsChanged = ReadAnimations(file, armature);
        return animationsChanged || materials.AddedToProject || file.IsOutdated;
    }

    private Byte ToJoint(JsonObject node)
    {
        var joint = node.GetInt(TlmNodes.JointKey);
        return (Byte)(joint >= 0 && joint < Joints.Count ? joint : 0);
    }

    private void ReadRootData(JsonObject data)
    {
        BoundingBox = [data.GetVector4("BoundingBoxMin", BoundingBox[0]), data.GetVector4("BoundingBoxMax", BoundingBox[1])];
        BoundingBoxBuilders.Clear();
        BoundingBoxBuilderToJointIndex.Clear();
        foreach (var collision in data.GetIndexed("Collisions"))
        {
            BoundingBoxBuilders.Add(new TwinBoundingBoxBuilder
            {
                BoundingBoxPoints = ToVectors(collision.GetFloats("Points")),
                UnkVectors1 = ToVectors(collision.GetFloats("UnkVectors1")),
                UnkVectors2 = ToVectors(collision.GetFloats("UnkVectors2")),
                UnkVectors3 = ToVectors(collision.GetFloats("UnkVectors3")),
                UnkShorts = collision.GetInts("UnkShorts").Select(v => (UInt16)v).ToList(),
                UnkBytes1 = collision.GetInts("UnkBytes1").Select(v => (Byte)v).ToList(),
                UnkBytes2 = collision.GetInts("UnkBytes2").Select(v => (Byte)v).ToList()
            });
            BoundingBoxBuilderToJointIndex.Add((Byte)collision.GetInt("Joint"));
        }
    }

    private static List<Vector4> ToVectors(Single[] values)
    {
        var result = new List<Vector4>();
        for (var i = 0; i + 3 < values.Length; i += 4)
        {
            result.Add(new Vector4(values[i], values[i + 1], values[i + 2], values[i + 3]));
        }

        return result;
    }

    // Bones are placed where the joints' bind poses are. Joints whose bone is still there keep the game's values, the ones moved
    // in Blender get their rest pose and inverse bind matrix from where the bones are now
    private void ReadJoints(JsonObject? armature)
    {
        Joints.Clear();
        SkinInverseMatrices.Clear();
        var jointJsons = (armature?["joints"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (jointJsons.Count == 0)
        {
            Joints.Add(new TwinJoint { Index = 0, ParentIndex = NoParent, ReactId = 0xFF, LocalRotation = new Vector4(0, 0, 0, 1), LocalTranslation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) });
            SkinInverseMatrices.Add(Matrix4x4.Identity.ToTwin());
            return;
        }

        // Bones added in Blender have no index yet
        var byIndex = new Dictionary<Int32, JsonObject>();
        foreach (var json in jointJsons.Where(json => json["index"] != null))
        {
            byIndex.TryAdd(json.GetInt("index"), json);
        }

        var nextIndex = byIndex.Keys.DefaultIfEmpty(-1).Max() + 1;
        foreach (var json in jointJsons.Where(json => json["index"] == null))
        {
            byIndex.Add(nextIndex++, json);
        }

        var jointsAmount = byIndex.Keys.Max() + 1;
        var binds = new Matrix4x4[jointsAmount];
        var storedBinds = new Matrix4x4?[jointsAmount];
        var kept = new Boolean[jointsAmount];
        var parents = new Int32[jointsAmount];
        for (var index = 0; index < jointsAmount; index++)
        {
            parents[index] = NoParent;
            if (!byIndex.TryGetValue(index, out var json))
            {
                binds[index] = Matrix4x4.Identity;
                continue;
            }

            binds[index] = TlmNodes.FromColumnVectorJson(json.GetFloats("bind")) ?? Matrix4x4.Identity;
            var storedInverse = json.GetData().GetFloats("InverseBindMatrix");
            if (storedInverse.Length == 16 && Matrix4x4.Invert(TlmNodes.ToMatrix(storedInverse), out var storedBind))
            {
                storedBinds[index] = storedBind;
                kept[index] = IsSamePose(binds[index], storedBind);
            }

            // Bones are identified by their index, a parent found by its name in Blender comes as its index too
            var parent = json.GetInt("parent", -1);
            parents[index] = parent >= 0 && parent < jointsAmount && parent != index && byIndex.ContainsKey(parent) ? parent : NoParent;
        }

        for (var index = 0; index < jointsAmount; index++)
        {
            if (!byIndex.TryGetValue(index, out var json))
            {
                Joints.Add(new TwinJoint { Index = index, ParentIndex = 0, ReactId = 0xFF, LocalRotation = new Vector4(0, 0, 0, 1), LocalTranslation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) });
                SkinInverseMatrices.Add(Matrix4x4.Identity.ToTwin());
                continue;
            }

            var data = json.GetData();
            var parent = parents[index];
            var bind = kept[index] ? storedBinds[index]!.Value : WithScaleOf(binds[index], storedBinds[index]);
            var storedTranslation = data.GetFloats("LocalTranslation");
            var storedRotation = data.GetFloats("LocalRotation");
            var storedWorld = data.GetFloats("WorldTranslation");
            var parentKept = parent == NoParent || kept[parent];
            Vector4 localTranslation;
            Vector4 localRotation;
            if (kept[index] && parentKept && storedTranslation.Length == 4 && storedRotation.Length == 4)
            {
                localTranslation = new Vector4(storedTranslation[0], storedTranslation[1], storedTranslation[2], storedTranslation[3]);
                localRotation = new Vector4(storedRotation[0], storedRotation[1], storedRotation[2], storedRotation[3]);
            }
            else
            {
                var parentBind = parent == NoParent ? Matrix4x4.Identity : kept[parent] ? storedBinds[parent]!.Value : WithScaleOf(binds[parent], storedBinds[parent]);
                var local = Matrix4x4.Invert(parentBind, out var parentInverse) ? bind * parentInverse : bind;
                Matrix4x4.Decompose(local, out _, out var rotation, out var translation);
                rotation = Quaternion.Normalize(rotation);
                // Both of a rotation's quaternions are the same rotation, the one closer to the game's stays
                if (storedRotation.Length == 4 && Quaternion.Dot(rotation, new Quaternion(storedRotation[0], storedRotation[1], storedRotation[2], storedRotation[3])) < 0)
                {
                    rotation = Quaternion.Negate(rotation);
                }

                localTranslation = new Vector4(translation.X, translation.Y, translation.Z, storedTranslation.Length == 4 ? storedTranslation[3] : 1.0f);
                localRotation = new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W);
            }

            Joints.Add(new TwinJoint
            {
                Index = index,
                ParentIndex = parent,
                ReactId = data.GetInt("ReactId", 0xFF),
                ChildrenAmt1 = parents.Count(p => p == index),
                ChildrenAmt2 = data.GetInt("ChildrenAmt2"),
                LocalTranslation = localTranslation,
                LocalRotation = localRotation,
                WorldTranslation = kept[index] && storedWorld.Length == 4
                    ? new Vector4(storedWorld[0], storedWorld[1], storedWorld[2], storedWorld[3])
                    : new Vector4(bind.Translation.X, bind.Translation.Y, bind.Translation.Z, storedWorld.Length == 4 ? storedWorld[3] : 1.0f),
                UnusedRotation = data.GetVector4("UnusedRotation"),
                AdditionalAnimationRotation = data.GetVector4("AdditionalAnimationRotation", new Vector4(0, 0, 0, 1))
            });
            SkinInverseMatrices.Add((kept[index] ? TlmNodes.ToMatrix(data.GetFloats("InverseBindMatrix")) : Matrix4x4.Invert(bind, out var inverse) ? inverse : Matrix4x4.Identity).ToTwin());
        }
    }

    // Blender's bones can't be scaled, a bone's pose is the same while it's where the bind pose puts it and turned the same way. The
    // game scales some joints to nothing in their bind pose, which has no turn to compare
    private static Boolean IsSamePose(Matrix4x4 bone, Matrix4x4 bind)
    {
        if (!Matrix4x4.Decompose(bone, out _, out var boneRotation, out var boneTranslation))
        {
            return false;
        }

        if (!Matrix4x4.Decompose(bind, out var bindScale, out var bindRotation, out var bindTranslation) ||
            Math.Min(Math.Abs(bindScale.X), Math.Min(Math.Abs(bindScale.Y), Math.Abs(bindScale.Z))) < 1e-4f)
        {
            return System.Numerics.Vector3.Distance(boneTranslation, bind.Translation) < TranslationTolerance;
        }

        boneRotation = Quaternion.Normalize(boneRotation);
        bindRotation = Quaternion.Normalize(bindRotation);
        var sign = Quaternion.Dot(boneRotation, bindRotation) < 0 ? -1.0f : 1.0f;
        var difference = bindRotation - boneRotation * sign;
        return System.Numerics.Vector3.Distance(boneTranslation, bindTranslation) < TranslationTolerance &&
               Math.Max(Math.Max(Math.Abs(difference.X), Math.Abs(difference.Y)), Math.Max(Math.Abs(difference.Z), Math.Abs(difference.W))) < RotationTolerance;
    }

    // A bone moved in Blender keeps the scale its bind pose had
    private static Matrix4x4 WithScaleOf(Matrix4x4 bone, Matrix4x4? bind)
    {
        if (bind == null || !Matrix4x4.Decompose(bind.Value, out var scale, out _, out _) || !Matrix4x4.Decompose(bone, out _, out var rotation, out var translation))
        {
            return bone;
        }

        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation)) * Matrix4x4.CreateTranslation(translation);
    }

    // Blender moves bones by rounding errors when it reads them
    private const Single TranslationTolerance = 2e-3f;
    private const Single RotationTolerance = 1e-3f;

    private void ReadSkins(TlmFile file, JsonObject root, TlmMaterials materials)
    {
        Skin = LabURI.Empty;
        BlendSkin = LabURI.Empty;
        var assetManager = AssetManager.Get();
        var skinNodes = root.GetChildren(SkinData.TlmKind).ToList();
        if (skinNodes.Count > 0)
        {
            var skin = new Skin
            {
                Package = Owner.Package,
                InvariantName = $"Skin_{Owner.Name}",
                Alias = $"Skin_{Owner.Name}",
                IsInternal = true,
                InternalOwner = Owner
            };
            var skinData = new SkinData(skin);
            foreach (var node in skinNodes)
            {
                skinData.ReadTlmMesh(file, node[TlmNodes.MeshKey] as JsonObject, materials);
            }

            skin.SetData(skinData);
            assetManager.TryAddAsset(skin);
            Skin = skin.URI;
        }

        var shapeNodes = root.GetChildren(BlendSkinData.TlmKind).ToList();
        if (shapeNodes.Count > 0)
        {
            var blendSkin = new BlendSkin
            {
                Package = Owner.Package,
                InvariantName = $"BlendSkin_{Owner.Name}",
                Alias = $"BlendSkin_{Owner.Name}",
                IsInternal = true,
                InternalOwner = Owner
            };
            var blendSkinData = new BlendSkinData(blendSkin);
            foreach (var node in shapeNodes)
            {
                blendSkinData.ReadTlmData(node.GetData());
                blendSkinData.ReadTlmMesh(file, node[TlmNodes.MeshKey] as JsonObject, materials);
            }

            blendSkin.SetData(blendSkinData);
            assetManager.TryAddAsset(blendSkin);
            BlendSkin = blendSkin.URI;
        }
    }

    // Animations made in Blender have no ID, copies of an animation made there carry the ID of the one they were copied from
    private bool ReadAnimations(TlmFile file, JsonObject? armature)
    {
        var jsons = (armature?["animations"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var animations = jsons.Select(json => (Json: json, Data: TlmAnimations.Read(file, json, Joints, out var edited), Edited: edited)).ToList();
        var claimedIds = new HashSet<UInt32>();
        var needsId = animations.Select(animation => animation.Json["id"] == null || !claimedIds.Add(animation.Data.ID)).ToList();
        var nextId = Math.Max(0x8000U, claimedIds.Select(id => id + 1).DefaultIfEmpty(0U).Max());
        var changed = false;
        Animations = [];
        for (var i = 0; i < animations.Count; i++)
        {
            var (_, data, edited) = animations[i];
            if (needsId[i])
            {
                data.ID = Math.Min(nextId++, 0xFFFEU);
                Log.WriteLine($"Added animation {data.Name} to {Owner.Name} as {data.ID:X}");
                changed = true;
            }
            else if (edited)
            {
                Log.WriteLine($"Imported animation {data.Name} of {Owner.Name}");
                changed = true;
            }

            Animations.Add(data);
        }

        return changed;
    }

    /// <summary>
    /// Adds the animation to the chunk's section and gives the ID it has there
    /// </summary>
    /// <remarks>
    /// Every OGI keeps its own copy of the animations it plays, copies can differ once one gets edited. The copy that isn't the one the
    /// chunk already has with its ID gets an ID of its own
    /// </remarks>
    public UInt32? ResolveAnimation(ITwinItemFactory factory, ITwinSection section, UInt32 id)
    {
        var animation = Animations.FirstOrDefault(animation => animation.ID == id);
        if (animation == null)
        {
            Log.WriteLine($"{Owner.Name} has no animation {id:X}", Log.LogType.Warning);
            return null;
        }

        var item = animation.Export(factory);
        var bytes = GetBytes(item);
        var exportId = id;
        while (section.ContainsItem(exportId))
        {
            if (GetBytes(section.GetItem<ITwinItem>(exportId)).AsSpan().SequenceEqual(bytes))
            {
                return exportId;
            }

            exportId = exportId < 0x8000 ? 0x8000 : exportId + 1;
        }

        item.SetID(exportId);
        section.AddItem(item);
        return exportId;

        static Byte[] GetBytes(ITwinItem twinItem)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            twinItem.Write(writer);
            writer.Flush();
            return stream.ToArray();
        }
    }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Animations", EditorDescType = typeof(OgiAnimationsEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public List<AnimationData> Animations { get; set; }

    public Vector4[] BoundingBox { get; set; }
    public List<TwinExitPoint> ExitPoints { get; set; }
    public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
    public List<Byte> BoundingBoxBuilderToJointIndex { get; set; }
    public List<TwinJoint> Joints { get; set; }
    public List<Byte> RigidModelJointIndices { get; set; }
    public List<LabURI> RigidModelIds { get; set; }
    public List<Matrix4> SkinInverseMatrices { get; set; }
    public LabURI Skin { get; set; }
    public LabURI BlendSkin { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Joints.Clear();
        ExitPoints.Clear();
        RigidModelJointIndices.Clear();
        RigidModelIds.Clear();
        SkinInverseMatrices.Clear();
        BoundingBoxBuilders.Clear();
        BoundingBoxBuilderToJointIndex.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinOGI ogi = GetTwinItem<ITwinOGI>();
        BoundingBox = CloneUtils.CloneArray(ogi.BoundingBox);
        RigidModelJointIndices = CloneUtils.CloneList(ogi.JointIndices);
        RigidModelIds = new List<LabURI>();
        foreach (var model in ogi.RigidModelIds)
        {
            RigidModelIds.Add(AssetManager.Get().GetUriByTwinId<RigidModel>(Owner, model));
        }
        BoundingBoxBuilderToJointIndex = CloneUtils.CloneList(ogi.CollisionJointIndices);
        Joints = CloneUtils.DeepClone(ogi.Joints);
        ExitPoints = CloneUtils.DeepClone(ogi.ExitPoints);
        BoundingBoxBuilders = CloneUtils.DeepClone(ogi.Collisions);
        SkinInverseMatrices = CloneUtils.CloneListUnsafe(ogi.SkinInverseBindMatrices);
        Skin = ogi.SkinID != 0 ? AssetManager.Get().GetUriByTwinId<Skin>(Owner, ogi.SkinID) : LabURI.Empty;
        BlendSkin = ogi.BlendSkinID != 0 ? AssetManager.Get().GetUriByTwinId<BlendSkin>(Owner, ogi.BlendSkinID) : LabURI.Empty;
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        foreach (var vec in BoundingBox)
        {
            vec.Write(writer);
        }

        writer.Write(RigidModelJointIndices.Count);
        foreach (var jointIndex in RigidModelJointIndices)
        {
            writer.Write(jointIndex);
        }

        writer.Write(Joints.Count);
        foreach (var joint in Joints)
        {
            joint.Write(writer);
        }

        writer.Write(RigidModelIds.Count);
        foreach (var rigidModel in RigidModelIds)
        {
            writer.Write(assetManager.GetAsset(rigidModel).ExportTwinID);
        }

        writer.Write(ExitPoints.Count);
        foreach (var exitPoint in ExitPoints)
        {
            exitPoint.Write(writer);
        }

        writer.Write(SkinInverseMatrices.Count);
        foreach (var matrix in SkinInverseMatrices)
        {
            matrix.Write(writer);
        }

        writer.Write(BoundingBoxBuilders.Count);
        foreach (var builder in BoundingBoxBuilders)
        {
            builder.Write(writer);
        }

        writer.Write(BoundingBoxBuilderToJointIndex.Count);
        foreach (var idx in BoundingBoxBuilderToJointIndex)
        {
            writer.Write(idx);
        }

        writer.Write(Skin == LabURI.Empty ? 0U : assetManager.GetAsset(Skin).ExportTwinID);
        writer.Write(BlendSkin == LabURI.Empty ? 0U : assetManager.GetAsset(BlendSkin).ExportTwinID);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateOGI(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var rigidModelSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_RIGID_MODELS_SECTION);
        foreach (var rigidModel in RigidModelIds.Where(uri => uri != LabURI.Empty))
        {
            assetManager.GetAsset(rigidModel).ResolveChunkResources(factory, rigidModelSection);
        }

        if (Skin != LabURI.Empty)
        {
            assetManager.GetAsset(Skin).ResolveChunkResources(factory, graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKINS_SECTION));
        }

        if (BlendSkin != LabURI.Empty)
        {
            assetManager.GetAsset(BlendSkin).ResolveChunkResources(factory, graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_BLEND_SKINS_SECTION));
        }

        return base.ResolveChunkResources(factory, section, id, layoutId);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var context = viewportContext.RenderContext;
        var ogiRender = new OGI(context, context.SkeletonManager, context.MeshService, this);
        var (offset, size) = GetBounds();
        var viewportObject = new ViewportObject(new EditableObject(context, ogiRender, "OGIRender", offset, size), property.Name, property, ogiRender);
        return [viewportObject];
    }

    /// <summary>
    /// Corner and size of the box the game keeps around the model
    /// </summary>
    public (vec3 Offset, vec3 Size) GetBounds()
    {
        var min = new vec3(BoundingBox[0].X, BoundingBox[0].Y, BoundingBox[0].Z);
        var max = new vec3(BoundingBox[1].X, BoundingBox[1].Y, BoundingBox[1].Z);
        return (min, max - min);
    }
}
