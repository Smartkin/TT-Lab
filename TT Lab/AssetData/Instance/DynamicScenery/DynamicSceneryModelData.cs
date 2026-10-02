using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using GlmSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using AnimatedTransformation = Twinsanity.TwinsanityInterchange.Common.DynamicScenery.AnimatedTransformation;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Transformation = Twinsanity.TwinsanityInterchange.Common.DynamicScenery.Transformation;

namespace TT_Lab.AssetData.Instance.DynamicScenery;

public struct DynamicModelAnimationSample
{
    public (float, System.Numerics.Vector3) Translation;
    public (float, System.Numerics.Quaternion) Rotation;
}

[ReferencesAssets]
public class DynamicSceneryModelData
{
    /// <summary>
    /// The convex hulls the game collides the model with, in its space
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<TwinCollisionHull> CollisionHulls { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public Int32 AnimatedFrames { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public TwinDynamicSceneryAnimation Animation { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public Boolean UsesLod { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public LabURI Mesh { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonListConverter<Vector4>))]
    public List<Vector4> BoundingBox { get; set; }

    public DynamicSceneryModelData()
    {
        CollisionHulls = [];
        Animation = new TwinDynamicSceneryAnimation();
        Mesh = LabURI.Empty;
        BoundingBox = [new Vector4(0, 0, 0, 1), new Vector4(10, 10, 10, 1)];
    }

    public DynamicSceneryModelData(IAsset owner, TwinDynamicSceneryModel model)
    {
        CollisionHulls = CloneUtils.DeepClone(model.CollisionHulls);
        AnimatedFrames = model.AnimatedFrames;
        Animation = CloneUtils.DeepClone(model.Animation);
        UsesLod = model.UsesLod;
        Mesh = AssetManager.Get().GetUriByTwinId<Mesh>(owner, model.MeshID);
        BoundingBox = [new Vector4(), new Vector4()];
        for (Int32 i = 0; i < BoundingBox.Count; i++)
        {
            BoundingBox[i] = CloneUtils.Clone(model.BoundingBox[i]);
        }
    }

    public const string TlmKind = "dynamic_model";

    /// <summary>
    /// The model as a node, its movement as the game has it next to every frame's translation and Euler angles, which Blender shows
    /// </summary>
    public JsonObject WriteTlmNode(TlmFile file, TlmMaterials materials, Int32 order)
    {
        var node = TlmNodes.Create(TlmKind, $"Dynamic Model {order}", new JsonObject
        {
            ["Order"] = order,
            ["UsesLod"] = UsesLod,
            ["BoundingBoxMin"] = TlmJson.ToJson(BoundingBox[0]),
            ["BoundingBoxMax"] = TlmJson.ToJson(BoundingBox[1])
        });
        for (var i = 0; i < CollisionHulls.Count; i++)
        {
            node.AddChild(TlmHulls.Write(file, CollisionHulls[i], $"Hull {i}"));
        }

        var assetManager = AssetManager.Get();
        if (Mesh != LabURI.Empty && assetManager.DoesAssetExist(Mesh))
        {
            node[TlmNodes.MeshKey] = assetManager.GetAssetData<MeshData>(Mesh).WriteTlmMesh(file, materials);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Animation.Write(writer);
        writer.Flush();
        var translations = new List<Single>();
        var rotations = new List<Single>();
        for (var frame = 0; frame < AnimatedFrames; frame++)
        {
            var (translation, rotation) = GetValues(frame);
            translations.AddRange([translation.X, translation.Y, translation.Z]);
            rotations.AddRange([rotation.X, rotation.Y, rotation.Z]);
        }

        node["animation"] = new JsonObject
        {
            ["frames"] = AnimatedFrames,
            ["exact"] = file.Write(stream.ToArray().AsSpan()),
            ["translation"] = file.Write(translations),
            ["rotation"] = file.Write(rotations)
        };
        return node;
    }

    /// <summary>
    /// Reads a dynamic model from its node, its movement stays as the game had it while the node's keys are its values
    /// </summary>
    public static DynamicSceneryModelData FromTlm(TlmFile file, TlmTreeNode node)
    {
        var data = node.Data;
        var result = new DynamicSceneryModelData
        {
            UsesLod = data.GetBool("UsesLod"),
            BoundingBox = [data.GetVector4("BoundingBoxMin", new Vector4(0, 0, 0, 1)), data.GetVector4("BoundingBoxMax", new Vector4(10, 10, 10, 1))],
            CollisionHulls = node.Children.Where(child => child.Kind == TlmHulls.Kind).Select(child => TlmHulls.Read(file, child.Json)).ToList()
        };

        if (node.Json["animation"] is not JsonObject animation)
        {
            return result;
        }

        var frames = animation.GetInt("frames");
        var translations = file.Read<Single>(animation["translation"]);
        var rotations = file.Read<Single>(animation["rotation"]);
        var exact = file.Read<Byte>(animation["exact"]);
        if (exact.Length > 0)
        {
            using var stream = new MemoryStream(exact);
            using var reader = new BinaryReader(stream);
            result.Animation = new TwinDynamicSceneryAnimation();
            result.Animation.Read(reader, exact.Length);
            result.AnimatedFrames = frames;
            if (translations.Length == frames * 3 && rotations.Length == frames * 3 && result.HasKeys(translations, rotations))
            {
                return result;
            }
        }

        if (translations.Length >= frames * 3 && rotations.Length >= frames * 3)
        {
            result.ReadAnimationFromKeys(frames, translations, rotations);
        }

        return result;
    }

    // Whether the keys are the values of every frame of the movement, Blender keeps them exactly
    private Boolean HasKeys(Single[] translations, Single[] rotations)
    {
        for (var frame = 0; frame < AnimatedFrames; frame++)
        {
            var (translation, rotation) = GetValues(frame);
            if (translation != new System.Numerics.Vector3(translations[frame * 3], translations[frame * 3 + 1], translations[frame * 3 + 2]) ||
                rotation != new System.Numerics.Vector3(rotations[frame * 3], rotations[frame * 3 + 1], rotations[frame * 3 + 2]))
            {
                return false;
            }
        }

        return true;
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

    public List<DynamicModelAnimationSample> GetAnimationSamples()
    {
        var result = new List<DynamicModelAnimationSample>();

        for (var i = 0; i < AnimatedFrames; i++)
        {
            result.Add(GetTransformFromAnimation(i));
        }
        
        return result;
    }

    private static Enums.TransformType IsPropertyAnimated(List<Single> values)
    {
        return values.All(value => Math.Abs(value - values[0]) < 0.000001) ? Enums.TransformType.Static : Enums.TransformType.Animated;
    }

    /// <summary>
    /// Makes the movement out of the translation and Euler angles of every frame, the ones that never change are stored once
    /// </summary>
    public void ReadAnimationFromKeys(Int32 frames, Single[] translations, Single[] rotations)
    {
        Animation = new TwinDynamicSceneryAnimation();
        var settings = new DynamicModelSettings
        {
            LeftoverByte = 22,
            ChannelCount = 7,
            StaticTransformationIndex = 0,
            AnimationTransformationIndex = 0
        };
        Animation.ModelSettings.Add(settings);
        AnimatedFrames = frames;
        Animation.TotalFrames = (UInt16)frames;
        var channels = Enumerable.Range(0, 6).Select(channel => Enumerable.Range(0, frames)
            .Select(frame => channel < 3 ? translations[frame * 3 + channel] : rotations[frame * 3 + channel - 3]).ToList()).ToList();
        var choices = channels.Select(values => values.Count == 0 ? Enums.TransformType.Static : IsPropertyAnimated(values)).ToArray();
        settings.TranslateX = choices[0];
        settings.TranslateY = choices[1];
        settings.TranslateZ = choices[2];
        settings.RotateX = choices[3];
        settings.RotateY = choices[4];
        settings.RotateZ = choices[5];
        settings.RotateW = Enums.TransformType.Static;
        for (var channel = 0; channel < channels.Count; channel++)
        {
            if (choices[channel] == Enums.TransformType.Static)
            {
                Animation.StaticTransformations.Add(new Transformation { Value = channels[channel].FirstOrDefault() });
            }
        }

        Animation.StaticTransformations.Add(new Transformation { Value = 1.0f });
        var animated = Enumerable.Range(0, channels.Count).Where(channel => choices[channel] == Enums.TransformType.Animated).ToList();
        for (var frame = 0; frame < frames; frame++)
        {
            var transformation = new AnimatedTransformation((UInt16)animated.Count);
            transformation.TransformationValues.AddRange(animated.Select(channel => channels[channel][frame]));
            Animation.AnimatedTransformations.Add(transformation);
        }
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(TwinDynamicSceneryModel.GameLeftover);
        writer.Write(CollisionHulls.Count);
        foreach (var hull in CollisionHulls)
        {
            hull.Write(writer);
        }
        writer.Write(AnimatedFrames);
        Animation.Write(writer);
        writer.Write((Byte)(UsesLod ? 1 : 0));
        writer.Write(AssetManager.Get().GetAsset(Mesh).ExportTwinID);
        foreach (var v in BoundingBox)
        {
            v.Write(writer);
        }
    }
    
    private DynamicModelAnimationSample GetTransformFromAnimation(int frame)
    {
        var sampleTime = frame / 25.0f;
        var (translation, rotation) = GetValues(frame);
        var quat = new quat(new vec3(rotation.X, rotation.Y, rotation.Z));
        return new DynamicModelAnimationSample
        {
            Translation = (sampleTime, translation),
            Rotation = (sampleTime, new System.Numerics.Quaternion(quat.x, quat.y, quat.z, quat.w)),
        };
    }

    /// <summary>
    /// Translation and Euler angles of a frame
    /// </summary>
    private (System.Numerics.Vector3 Translation, System.Numerics.Vector3 Rotation) GetValues(Int32 frame)
    {
        if (Animation.ModelSettings.Count <= 0)
        {
            return (System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero);
        }

        var model = Animation.ModelSettings[0];
        var staticIndex = model.StaticTransformationIndex;
        var animatedIndex = model.AnimationTransformationIndex;
        var choices = new[] { model.TranslateX, model.TranslateY, model.TranslateZ, model.RotateX, model.RotateY, model.RotateZ };
        var values = new Single[choices.Length];
        for (var channel = 0; channel < choices.Length; channel++)
        {
            values[channel] = choices[channel] == Enums.TransformType.Animated
                ? Animation.AnimatedTransformations[frame].TransformationValues[animatedIndex++]
                : Animation.StaticTransformations[staticIndex++].Value;
        }

        return (new System.Numerics.Vector3(values[0], values[1], values[2]), new System.Numerics.Vector3(values[3], values[4], values[5]));
    }
}
