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
using TT_Lab.AssetData.Instance.Scenery;
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
        // Like the root joint of nearly every model of the game: at the origin, no ID
        var rootJoint = new TwinJoint
        {
            Id = 0xFF,
            Index = 0,
            LocalRotation = new Vector4(0, 0, 0, 1),
            LocalTranslation = new Vector4(0, 0, 0, 1),
            WorldTranslation = new Vector4(0, 0, 0, 1),
            AdditionalAnimationRotation = new Vector4(0, 0, 0, 1),
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
        CollisionHulls = [];
        CollisionHullJoints = [];
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
    private const string JointIdCountKey = "JointIdCount";
    public const string ExitPointKind = "exit_point";
    public const string CollisionHullsKind = "collision_hulls";
    private const Int32 NoParent = 0xFF;
    /// <summary>
    /// The joint of a hull that isn't on any, in the model's space
    /// </summary>
    public const Byte NoJoint = 0xFF;

    // A new model's cube is a unit across, the checker's squares half a unit so its faces show four
    private const Single PlaceholderSize = 1.0f;
    private const Single PlaceholderSquare = 0.5f;

    /// <summary>
    /// Makes the model what a new one starts as: a cube standing on the ground, the rigid body of its one joint, on the checker material
    /// of its version of the game (the scenery placeholders', made the first time)
    /// </summary>
    internal void MakePlaceholder()
    {
        var half = PlaceholderSize / 2.0f;
        BoundingBox = [new Vector4(-half, 0, -half, 1), new Vector4(half, PlaceholderSize, half, 1)];
        RigidModelIds = [LabURI.Empty];
        RigidModelJointIndices = [0];
        var file = WriteTlm();
        var material = SceneryPlaceholders.UseCheckerMaterial(file, Owner.Package);
        var cube = PlaceholderShapes.Make(PlaceholderShape.Cube, PlaceholderSize);
        var body = file.Root.FindChild(RigidBodiesKind).GetChildren(BodyKind).Single();
        body[TlmNodes.MeshKey] = new JsonObject { ["parts"] = new JsonArray(SceneryPlaceholders.WritePart(file, cube, material, PlaceholderSquare)) };
        ReadTlm(file);
    }

    internal TlmFile WriteTlm()
    {
        var assetManager = AssetManager.Get();
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var materials = new TlmMaterials(file);
        var root = TlmNodes.Create(TlmKind, Owner.Name, new JsonObject
        {
            ["BoundingBoxMin"] = TlmJson.ToJson(BoundingBox[0]),
            ["BoundingBoxMax"] = TlmJson.ToJson(BoundingBox[1])
        });
        // Only kept where it isn't what reading works out, the game's models whose joint IDs have gaps
        if (JointIdCount != WorkOutJointIdCount(-1))
        {
            root.GetData()[JointIdCountKey] = (Int32)JointIdCount;
        }

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
                    ["Id"] = joint.Id,
                    ["Detail"] = joint.Detail,
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
            var shape = root.AddChild(TlmNodes.Create(BlendSkinData.TlmKind, "Blend Skin"));
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
            var node = exitPoints.AddChild(TlmNodes.Create(ExitPointKind, $"Exit Point {exitPoint.ID}", new JsonObject { ["Id"] = TlmJson.ToJson(exitPoint.ID) }));
            node[TlmNodes.JointKey] = (Int32)exitPoint.ParentJointIndex;
            var matrix = exitPoint.Matrix.ToSystem();
            node.SetTransform(matrix);
            // The game's matrix as it is: a few of the game's exit points are scaled or sheared, and a transform loses the last bits of the rest
            node[TlmNodes.MatrixKey] = TlmJson.ToJson(TlmNodes.ToArray(matrix));
        }

        var hulls = root.AddChild(TlmNodes.Create(CollisionHullsKind, "Collision Hulls"));
        for (var i = 0; i < CollisionHulls.Count; i++)
        {
            var node = hulls.AddChild(TlmHulls.Write(file, CollisionHulls[i], $"Hull {i}"));
            node[TlmNodes.JointKey] = (Int32)(i < CollisionHullJoints.Count ? CollisionHullJoints[i] : NoJoint);
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
        ReadJoints(armature, IsSkinned(root));
        JointIdCount = WorkOutJointIdCount(root.GetData().GetInt(JointIdCountKey, -1));

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

        var renumbered = ReadExitPoints(root);
        ReadHulls(file, root);
        ReadSkins(file, root, materials);
        // ModelNode::SetOgi gives a model of one joint and no exit points no animator, the game draws only its rigid models then
        if ((Skin != LabURI.Empty || BlendSkin != LabURI.Empty) && Joints.Count < 2 && ExitPoints.Count == 0)
        {
            Log.WriteLine($"{Owner.Name} has one joint and no exit points: the game draws such a model's rigid models only, never its skin. Add a bone under the root " +
                          "in Blender and weight the skin to it", Log.LogType.Warning);
        }

        var animationsChanged = ReadAnimations(file, armature);
        return animationsChanged || materials.AddedToProject || renumbered;
    }

    // The game finds an exit point by its place (BindExitPoints, a character's hand is 0 and its head 1) and never reads its ID: the exit
    // points go in the order of their IDs, then the file's, and the IDs that aren't their places become them
    private bool ReadExitPoints(JsonObject root)
    {
        ExitPoints = root.FindChild(ExitPointsKind).Traverse().Where(node => node.GetKind() == ExitPointKind).Select(node => new TwinExitPoint
        {
            ID = node.GetData().GetUInt("Id"),
            ParentJointIndex = ToJoint(node),
            Matrix = TlmNodes.KeepStored(node.GetFloats(TlmNodes.MatrixKey), node.GetTransform(), out _).ToTwin()
        }).OrderBy(exitPoint => exitPoint.ID).ToList();
        if (ExitPoints.Select(exitPoint => exitPoint.ID).SequenceEqual(Enumerable.Range(0, ExitPoints.Count).Select(place => (UInt32)place)))
        {
            return false;
        }

        Log.WriteLine($"{Owner.Name}'s exit points have the IDs {string.Join(", ", ExitPoints.Select(exitPoint => exitPoint.ID))}: the game finds an exit point by its place, " +
                      $"they got their places as IDs in that order", Log.LogType.Warning);
        for (var place = 0; place < ExitPoints.Count; place++)
        {
            ExitPoints[place].ID = (UInt32)place;
        }

        return true;
    }

    private Byte ToJoint(JsonObject node)
    {
        var joint = node.GetInt(TlmNodes.JointKey);
        return (Byte)(joint >= 0 && joint < Joints.Count ? joint : 0);
    }

    // The game binds the joint IDs below the count (SetAnimatorOgi looks each up among the joints). The game's models have the number of
    // their joints with an ID, which leaves the higher IDs of the 4 whose IDs have gaps unbound: that's kept while it still is the number,
    // else every ID gets bound
    private Byte WorkOutJointIdCount(Int32 stored)
    {
        var ids = Joints.Where(joint => joint.Id < NoJoint).Select(joint => joint.Id).ToList();
        return (Byte)(stored == ids.Count ? stored : ids.Select(id => id + 1).DefaultIfEmpty(0).Max());
    }

    private void ReadRootData(JsonObject data)
    {
        BoundingBox = [data.GetVector4("BoundingBoxMin", BoundingBox[0]), data.GetVector4("BoundingBoxMax", BoundingBox[1])];
        CollisionHulls.Clear();
        CollisionHullJoints.Clear();
    }

    // A hull on no joint is in the model's space, the game marks those with a joint of 0xFF
    private void ReadHulls(TlmFile file, JsonObject root)
    {
        foreach (var node in root.FindChild(CollisionHullsKind).Traverse().Where(node => node.GetKind() == TlmHulls.Kind))
        {
            var joint = node.GetInt(TlmNodes.JointKey, NoJoint);
            CollisionHulls.Add(TlmHulls.Read(file, node));
            CollisionHullJoints.Add((Byte)(joint >= 0 && joint < Joints.Count ? joint : NoJoint));
        }
    }

    // Bones are placed where the joints' bind poses are. Joints whose bone is still there keep the game's values, the ones moved
    // in Blender get their rest pose and inverse bind matrix from where the bones are now
    private void ReadJoints(JsonObject? armature, Boolean skinned)
    {
        Joints.Clear();
        SkinInverseMatrices.Clear();
        var jointJsons = (armature?["joints"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (jointJsons.Count == 0)
        {
            Joints.Add(new TwinJoint { Index = 0, ParentIndex = NoParent, Id = 0xFF, LocalRotation = new Vector4(0, 0, 0, 1), LocalTranslation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) });
            SkinInverseMatrices.Add(Matrix4x4.Identity.ToTwin());
            return;
        }

        var (byIndex, parents) = IndexJoints(jointJsons);
        if (SkeletonProblem(byIndex, parents, skinned) is { } problem)
        {
            throw new InvalidDataException(problem);
        }

        var jointsAmount = parents.Length;
        var binds = new Matrix4x4[jointsAmount];
        var storedBinds = new Matrix4x4?[jointsAmount];
        var kept = new Boolean[jointsAmount];
        for (var index = 0; index < jointsAmount; index++)
        {
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
        }

        for (var index = 0; index < jointsAmount; index++)
        {
            if (!byIndex.TryGetValue(index, out var json))
            {
                Joints.Add(new TwinJoint { Index = index, ParentIndex = 0, Id = 0xFF, LocalRotation = new Vector4(0, 0, 0, 1), LocalTranslation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) });
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
                Id = data.GetInt("Id", 0xFF),
                ChildCount = parents.Count(p => p == index),
                Detail = data.GetInt("Detail"),
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

    // The joints the game's skeletons give a joint at most (GetJointAnimationFromParentJoint has none for a 13th child, whose own children
    // it then reads from nothing), and the joints a skin is drawn with at most (WriteJoints puts their count in 6 bits of a VIF UNPACK)
    private const Int32 MaxChildJoints = 12;
    private const Int32 MaxSkinnedJoints = 63;

    private static Boolean IsSkinned(JsonObject root) => root.FindChild(SkinData.TlmKind) != null || root.FindChild(BlendSkinData.TlmKind) != null;

    // The file's joints by their index, bones added in Blender without one after the others, and every joint's parent (NoParent for none)
    private static (Dictionary<Int32, JsonObject> ByIndex, Int32[] Parents) IndexJoints(List<JsonObject> jointJsons)
    {
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

        var parents = new Int32[byIndex.Keys.Max() + 1];
        Array.Fill(parents, NoParent);
        foreach (var (index, json) in byIndex)
        {
            // Bones are identified by their index, a parent found by its name in Blender comes as its index too
            var parent = json.GetInt("parent", -1);
            parents[index] = parent >= 0 && parent < parents.Length && parent != index && byIndex.ContainsKey(parent) ? parent : NoParent;
        }

        return (byIndex, parents);
    }

    /// <summary>
    /// Why the game can't have the skeleton of a model file's armature, null when it can
    /// </summary>
    internal static String? SkeletonProblem(JsonObject root)
    {
        var jointJsons = (root.FindChild(ArmatureKind)?["joints"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (jointJsons.Count == 0)
        {
            return null;
        }

        var (byIndex, parents) = IndexJoints(jointJsons);
        return SkeletonProblem(byIndex, parents, IsSkinned(root));
    }

    // The game builds a skeleton in joint order, every joint under its parent's, and moves every joint without a parent as its root
    // (SetJointAnimations). It works out the joints' matrices walking the skeleton from the root down, a joint's children in the order of
    // their indexes, each with everything under it (TransformJoints), and draws the joint of an index with the matrix of that place in the
    // walk (DrawOgi): the indexes have to be the walk's. A model of four root bones made in Blender broke the viewer
    private static String? SkeletonProblem(Dictionary<Int32, JsonObject> byIndex, Int32[] parents, Boolean skinned)
    {
        const String exportAgain = "export the model again with the current add-on, which numbers the joints that way";
        String NameOf(Int32 index) => byIndex.TryGetValue(index, out var json) ? json.GetString("name") ?? $"Joint {index}" : $"Joint {index}";
        var roots = byIndex.Keys.Where(index => parents[index] == NoParent).Order().ToList();
        if (roots.Count > 1)
        {
            return $"The armature has {roots.Count} root bones ({String.Join(", ", roots.Select(NameOf))}) and the game's skeletons have one, joint 0, which every " +
                   "other bone is under: parent the others to the bone that should be the root in Blender";
        }

        if (roots.Count == 1 && roots[0] != 0)
        {
            return $"The root bone {NameOf(roots[0])} is joint {roots[0]}, the game's skeletons start with their root: {exportAgain}";
        }

        var missing = Enumerable.Range(0, parents.Length).Where(index => !byIndex.ContainsKey(index)).ToList();
        if (missing.Count > 0)
        {
            return $"The armature's joints leave out the index{(missing.Count > 1 ? "es" : string.Empty)} {String.Join(", ", missing)}, the game's skeletons number theirs " +
                   $"one after the other: {exportAgain}";
        }

        var children = Enumerable.Range(0, parents.Length).Select(_ => new List<Int32>()).ToArray();
        for (var index = 0; index < parents.Length; index++)
        {
            if (parents[index] != NoParent)
            {
                children[parents[index]].Add(index);
            }
        }

        var crowded = Array.FindIndex(children, list => list.Count > MaxChildJoints);
        if (crowded >= 0)
        {
            return $"{NameOf(crowded)} has {children[crowded].Count} child bones and the game gives a joint {MaxChildJoints} at most";
        }

        if (skinned && parents.Length > MaxSkinnedJoints)
        {
            return $"The armature has {parents.Length} bones and the game draws a skin with {MaxSkinnedJoints} at most";
        }

        var walk = new List<Int32>();
        var pending = new Stack<Int32>([0]);
        while (pending.Count > 0)
        {
            var joint = pending.Pop();
            walk.Add(joint);
            foreach (var child in Enumerable.Reverse(children[joint]))
            {
                pending.Push(child);
            }
        }

        var misplaced = Enumerable.Range(0, walk.Count).FirstOrDefault(place => walk[place] != place, -1);
        return misplaced < 0
            ? null
            : $"{NameOf(walk[misplaced])} is joint {walk[misplaced]}, but the game walks a skeleton from the root down, a joint's children in the order of their " +
              $"indexes and each with everything under it, and gets to it as joint {misplaced}: {exportAgain}";
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
            var (json, data, edited) = animations[i];
            var fps = json.GetInt("fps", data.DefaultFPS);
            if (fps != data.DefaultFPS)
            {
                // The add-on writes faster actions at a rate the game has, sampling every few frames
                Log.WriteLine($"{data.Name} of {Owner.Name} is {fps} frames a second and the game's animations have 1 to {TlmAnimations.MaxFps}: it plays at {data.DefaultFPS}" +
                              (fps > TlmAnimations.MaxFps ? ", export it from Blender again to keep its speed" : string.Empty), Log.LogType.Warning);
                changed = true;
            }

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
    /// <summary>
    /// The convex hulls the game collides the model with
    /// </summary>
    public List<TwinCollisionHull> CollisionHulls { get; set; }
    /// <summary>
    /// The joint every hull is on, <see cref="NoJoint"/> for one in the model's space
    /// </summary>
    public List<Byte> CollisionHullJoints { get; set; }
    public List<TwinJoint> Joints { get; set; }
    /// <summary>
    /// The joint IDs the game binds, every ID below it (<see cref="ITwinOGI.JointIdCount"/>)
    /// </summary>
    public Byte JointIdCount { get; set; }
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
        CollisionHulls.Clear();
        CollisionHullJoints.Clear();
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
        CollisionHullJoints = CloneUtils.CloneList(ogi.CollisionJointIndices);
        Joints = CloneUtils.DeepClone(ogi.Joints);
        JointIdCount = ogi.JointIdCount;
        ExitPoints = CloneUtils.DeepClone(ogi.ExitPoints);
        CollisionHulls = CloneUtils.DeepClone(ogi.CollisionHulls);
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

        writer.Write(CollisionHulls.Count);
        foreach (var hull in CollisionHulls)
        {
            hull.Write(writer);
        }

        writer.Write(CollisionHullJoints.Count);
        foreach (var idx in CollisionHullJoints)
        {
            writer.Write(idx);
        }

        writer.Write(Skin == LabURI.Empty ? 0U : assetManager.GetAsset(Skin).ExportTwinID);
        writer.Write(BlendSkin == LabURI.Empty ? 0U : assetManager.GetAsset(BlendSkin).ExportTwinID);
        writer.Write(WorkOutJointIdCount(JointIdCount));

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
