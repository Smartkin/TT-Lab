using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.MeshProcessor;
using TT_Lab.Util;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.AgentLab.AgentLabObjectDescs.Xbox;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SMX;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Code;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.Assets.Factory;

/// <summary>
/// Creates the Xbox version's items. Most of them are written like the PS2 ones, only the graphics, sounds and scripts differ
/// </summary>
public class XboxItemFactory : PS2ItemFactory
{
    // Joints a strip's palette can hold, the most the game's skins and blend skins use
    private const Int32 SkinPaletteLimit = 16;
    private const Int32 BlendPaletteLimit = 8;

    private static readonly Dictionary<Type, Type> XboxTypes = new()
    {
        [typeof(PS2AnyAIPath)] = typeof(XboxAnyAIPath),
        [typeof(PS2AnyAIPosition)] = typeof(XboxAnyAIPosition),
        [typeof(PS2AnyAnimation)] = typeof(XboxAnyAnimation),
        [typeof(PS2AnyCamera)] = typeof(XboxAnyCamera),
        [typeof(PS2AnyCollisionData)] = typeof(XboxAnyCollisionData),
        [typeof(PS2AnyDynamicScenery)] = typeof(XboxAnyDynamicScenery),
        [typeof(PS2AnyInstance)] = typeof(XboxAnyInstance),
        [typeof(PS2AnyLink)] = typeof(XboxAnyLink),
        [typeof(PS2AnyLOD)] = typeof(XboxAnyLOD),
        [typeof(PS2AnyMaterial)] = typeof(XboxAnyMaterial),
        [typeof(PS2AnyMesh)] = typeof(XboxAnyMesh),
        [typeof(PS2AnyObject)] = typeof(XboxAnyObject),
        [typeof(PS2AnyOGI)] = typeof(XboxAnyOGI),
        [typeof(PS2AnyParticleData)] = typeof(XboxAnyParticleData),
        [typeof(PS2DefaultParticleData)] = typeof(XboxDefaultParticleData),
        [typeof(PS2AnyPath)] = typeof(XboxAnyPath),
        [typeof(PS2AnyPosition)] = typeof(XboxAnyPosition),
        [typeof(PS2AnyRigidModel)] = typeof(XboxAnyRigidModel),
        [typeof(PS2AnyScenery)] = typeof(XboxAnyScenery),
        [typeof(PS2AnySkydome)] = typeof(XboxAnySkydome),
        [typeof(PS2AnyCollisionSurface)] = typeof(XboxAnyCollisionSurface),
        [typeof(PS2AnyTemplate)] = typeof(XboxAnyTemplate),
        [typeof(PS2AnyTrigger)] = typeof(XboxAnyTrigger)
    };

    private static readonly Dictionary<Type, Type> XboxSections = new()
    {
        [typeof(PS2Default)] = typeof(XboxDefault),
        [typeof(PS2AnyTwinsanityRM2)] = typeof(XboxAnyTwinsanityRMX),
        [typeof(PS2AnyTwinsanitySM2)] = typeof(XboxAnyTwinsanitySMX),
        [typeof(PS2AnyGraphicsSection)] = typeof(XboxAnyGraphicsSection),
        [typeof(PS2AnyCodeSection)] = typeof(XboxAnyCodeSection),
        [typeof(PS2AnyLayoutSection)] = typeof(XboxAnyLayoutSection),
        [typeof(PS2AnyTemplatesSection)] = typeof(XboxAnyTemplatesSection),
        [typeof(PS2AnyAIPositionsSection)] = typeof(XboxAnyAIPositionsSection),
        [typeof(PS2AnyAIPathsSection)] = typeof(XboxAnyAIPathsSection),
        [typeof(PS2AnyPositionsSection)] = typeof(XboxAnyPositionsSection),
        [typeof(PS2AnyPathsSection)] = typeof(XboxAnyPathsSection),
        [typeof(PS2AnySurfacesSection)] = typeof(XboxAnySurfacesSection),
        [typeof(PS2AnyInstancesSection)] = typeof(XboxAnyInstancesSection),
        [typeof(PS2AnyTriggersSection)] = typeof(XboxAnyTriggersSection),
        [typeof(PS2AnyCamerasSection)] = typeof(XboxAnyCamerasSection),
        [typeof(PS2AnyTexturesSection)] = typeof(XboxAnyTexturesSection),
        [typeof(PS2AnyMaterialsSection)] = typeof(XboxAnyMaterialsSection),
        [typeof(PS2AnyModelsSection)] = typeof(XboxAnyModelsSection),
        [typeof(PS2AnyRigidModelsSection)] = typeof(XboxAnyRigidModelsSection),
        [typeof(PS2AnySkinsSection)] = typeof(XboxAnySkinsSection),
        [typeof(PS2AnyBlendSkinsSection)] = typeof(XboxAnyBlendSkinsSection),
        [typeof(PS2AnyMeshesSection)] = typeof(XboxAnyMeshesSection),
        [typeof(PS2AnyLODsSection)] = typeof(XboxAnyLODsSection),
        [typeof(PS2AnySkydomesSection)] = typeof(XboxAnySkydomesSection),
        [typeof(PS2AnyGameObjectsSection)] = typeof(XboxAnyGameObjectsSection),
        [typeof(PS2AnyBehavioursSection)] = typeof(XboxAnyBehavioursSection),
        [typeof(PS2AnyAnimationsSection)] = typeof(XboxAnyAnimationsSection),
        [typeof(PS2AnyOGIsSection)] = typeof(XboxAnyOGIsSection),
        [typeof(PS2AnyBehaviourCommandsSequencesSection)] = typeof(XboxAnyBehaviourCommandsSequencesSection),
        [typeof(PS2AnySoundsSection)] = typeof(XboxAnySoundsSection)
    };

    protected override String ActionDefinitionsFile => "ActionDefinitionsXbox.lab";

    public override ITwinItemFactory ForChunk()
    {
        return new XboxItemFactory { GlobalPackage = GlobalPackage, CompiledBehaviours = CompiledBehaviours };
    }

    protected override T Create<T>()
    {
        return XboxTypes.TryGetValue(typeof(T), out var type) ? (T)Activator.CreateInstance(type)! : base.Create<T>();
    }

    protected override BaseTwinSection CreateSection<T>()
    {
        return XboxSections.TryGetValue(typeof(T), out var type) ? (BaseTwinSection)Activator.CreateInstance(type)! : base.CreateSection<T>();
    }

    protected override ITwinBehaviourCommandPack CreateCommandPack() => new XboxBehaviourCommandPack();
    protected override CommandDesc NewCommandDesc() => new XboxCommandDesc();
    protected override CommandPackDesc NewCommandPackDesc() => new XboxCommandPackDesc();
    protected override CommandsSequenceDesc NewCommandsSequenceDesc() => new XboxCommandsSequenceDesc();
    protected override StateDesc NewStateDesc() => new XboxStateDesc();
    protected override StateBodyDesc NewStateBodyDesc() => new XboxStateBodyDesc();
    protected override GraphDesc NewGraphDesc() => new XboxGraphDesc();

    public override ITwinModel GenerateModel(List<RigidPartExport> parts)
    {
        var model = new XboxAnyModel();
        foreach (var part in parts)
        {
            var normals = GetNormals(part.Vertexes, part.Layout, StripParts.RigidWinding, vertex => vertex.HasNormals ? vertex.Normal : null);
            var subModel = new XboxSubModel();
            foreach (var strip in ToStrips(part.Layout, StripParts.RigidWinding))
            {
                subModel.GroupSizes.Add(strip.Count);
                foreach (var index in strip)
                {
                    var vertex = part.Vertexes[index];
                    subModel.Vertexes.Add(new Vector4(vertex.Position));
                    subModel.Normals.Add(normals[index]);
                    subModel.Colors.Add(new Vector4(vertex.Color));
                    subModel.UVW.Add(new Vector4(vertex.UV));
                }
            }

            subModel.CalculateData();
            model.SubModels.Add(subModel);
        }

        return model;
    }

    public override ITwinSkin GenerateSkin(List<SkinPartExport> parts)
    {
        var skin = new XboxAnySkin();
        foreach (var part in parts)
        {
            var subSkin = new XboxSubSkin { Material = part.Material };
            FillSkinnedGeometry(subSkin.Geometry, part.Vertexes, part.Layout, SkinPaletteLimit);
            skin.SubSkins.Add(subSkin);
        }

        return skin;
    }

    public override ITwinBlendSkin GenerateBlendSkin(Int32 blendsAmount, List<BlendPartExport> parts)
    {
        var blendSkin = new XboxAnyBlendSkin { BlendsAmount = blendsAmount };
        foreach (var part in parts)
        {
            var subBlend = new XboxSubBlendSkin(blendsAmount) { Material = part.Material };
            var model = new XboxBlendSkinModel(blendsAmount);
            var indexes = FillSkinnedGeometry(model.Geometry, part.Vertexes, part.Layout, BlendPaletteLimit);
            for (var shape = 0; shape < blendsAmount; shape++)
            {
                var offsets = shape < part.ShapeOffsets.Count ? part.ShapeOffsets[shape] : null;
                model.Faces.Add(new XboxBlendSkinFace((UInt32)indexes.Count)
                {
                    Vertices = indexes.Select(index => new VertexBlendShape
                    {
                        BlendShape = new Vector3(),
                        Offset = offsets != null ? new Vector4(offsets[index]) : new Vector4()
                    }).ToList()
                });
            }

            subBlend.Models.Add(model);
            blendSkin.SubBlends.Add(subBlend);
        }

        return blendSkin;
    }

    public override ITwinSound GenerateSound()
    {
        return new XboxAnySound();
    }

    public override ITwinTexture GenerateTexture()
    {
        return new XboxAnyTexture();
    }

    public override ITwinSection GenerateFrontend(List<ITwinSound> sounds)
    {
        return new XboxFrontend();
    }

    public override ITwinPSF GenerateFont(List<ITwinPTC> pages, List<VectorCharacterData> characterData, Int32 spaceIdentifier)
    {
        var font = new XboxPSF();
        font.FontPages.AddRange(pages);
        font.CharacterData = CloneUtils.CloneList(characterData);
        font.SpaceIdentifier = spaceIdentifier;
        return font;
    }

    public override ITwinPTC GeneratePTC(UInt32 texID, UInt32 matID, ITwinTexture texture, ITwinMaterial material)
    {
        return new XboxPTC
        {
            TexID = texID,
            MatID = matID,
            Texture = texture,
            Material = material
        };
    }

    public override ITwinPSM GeneratePSM(List<ITwinPTC> ptcs)
    {
        var psm = new XboxPSM();
        psm.PTCs.AddRange(ptcs);
        return psm;
    }

    // Fills the strips of skinned vertexes, returning which of the part's vertexes every strip vertex is
    private static List<Int32> FillSkinnedGeometry(XboxSkinnedGeometry geometry, List<Vertex> vertexes, StripLayout layout, Int32 paletteLimit)
    {
        var normals = GetNormals(vertexes, layout, StripParts.SkinWinding, StripParts.SkinNormal);
        var indexes = new List<Int32>();
        var batchStrips = layout.IgnoresFacing
            ? layout.Batches.Select(b => (Strip: b.Vertexes.Select(v => v.Index).ToList(), Palette: b.Joints)).ToList()
            : ToStrips(layout, StripParts.SkinWinding).Select(strip => (Strip: strip, Palette: (List<Int32>?)null)).ToList();
        foreach (var (batchStrip, storedPalette) in batchStrips)
        {
            foreach (var (strip, palette) in SplitByPalette(batchStrip, storedPalette, vertexes, paletteLimit))
            {
                geometry.GroupSizes.Add(strip.Count);
                geometry.JointPalettes.Add(palette);
                for (var i = 0; i < strip.Count; i++)
                {
                    var index = strip[i];
                    var vertex = vertexes[index];
                    indexes.Add(index);
                    var normal = normals[index];
                    geometry.Vertexes.Add(new Vector4(vertex.Position.X, vertex.Position.Y, vertex.Position.Z, normal.X));
                    geometry.Colors.Add(new Vector4(vertex.Color));
                    geometry.UVW.Add(new Vector4(vertex.UV.X, vertex.UV.Y, normal.Y, normal.Z));
                    geometry.SkinJoints.Add(new VertexJointInfo
                    {
                        JointIndex1 = vertex.JointInfo.JointIndex1,
                        JointIndex2 = vertex.JointInfo.JointIndex2,
                        JointIndex3 = vertex.JointInfo.JointIndex3,
                        Weight1 = vertex.JointInfo.Weight1,
                        Weight2 = vertex.JointInfo.Weight2,
                        Weight3 = vertex.JointInfo.Weight3,
                        WeightsAmount = vertex.JointInfo.WeightsAmount,
                        Connection = i >= 2
                    });
                }
            }
        }

        return indexes;
    }

    // A strip keeps the palette it had while it still holds every joint its vertexes use, strips using more joints than a palette holds
    // are split into strips that each fit one
    private static IEnumerable<(List<Int32> Strip, List<Int32> Palette)> SplitByPalette(List<Int32> strip, List<Int32>? storedPalette, List<Vertex> vertexes,
        Int32 limit)
    {
        var used = strip.SelectMany(index => GetJoints(vertexes[index])).Distinct().ToList();
        if (storedPalette != null && used.All(storedPalette.Contains))
        {
            yield return (strip, storedPalette);
            yield break;
        }

        if (used.Count <= limit)
        {
            yield return (strip, used);
            yield break;
        }

        var start = 0;
        while (start < strip.Count - 2)
        {
            var palette = new List<Int32>();
            var end = start;
            // Every triangle of the piece has to fit, which needs its 3 vertexes' joints
            while (end < strip.Count)
            {
                var joints = GetJoints(vertexes[strip[end]]).Where(joint => !palette.Contains(joint)).ToList();
                if (palette.Count + joints.Count > limit && end - start >= 3)
                {
                    break;
                }

                palette.AddRange(joints);
                end++;
            }

            var piece = strip.GetRange(start, end - start);
            // A piece starting on an odd position would flip its triangles, a repeated vertex keeps them facing the same way
            if (start % 2 == 1)
            {
                piece.Insert(0, piece[0]);
            }

            yield return (piece, palette);
            if (end >= strip.Count)
            {
                yield break;
            }

            start = end - 2;
        }
    }

    private static IEnumerable<Int32> GetJoints(Vertex vertex)
    {
        var joint = vertex.JointInfo;
        var amount = joint.GetJointConnectionsAmount();
        return new[] { joint.JointIndex1, joint.JointIndex2, joint.JointIndex3 }.Take(Math.Max(1, amount));
    }

    /// <summary>
    /// The layout's strips as the Xbox version draws them, every triangle of a strip gets drawn
    /// </summary>
    /// <remarks>
    /// Strips read from the Xbox version stay as they are. Others get split where they don't draw a triangle, a strip starting on an
    /// odd position gets a repeated vertex to keep its triangles facing the same way
    /// </remarks>
    internal static List<List<Int32>> ToStrips(StripLayout layout, StripWinding winding)
    {
        if (layout.IgnoresFacing)
        {
            return layout.Batches.Select(b => b.Vertexes.Select(v => v.Index).ToList()).ToList();
        }

        var strips = new List<List<Int32>>();
        foreach (var batch in layout.Batches)
        {
            var vertexes = batch.Vertexes;
            var k = 2;
            var stripStart = 0;
            while (k < vertexes.Count)
            {
                if (!vertexes[k].Draws)
                {
                    k++;
                    continue;
                }

                stripStart = StripLayout.GetStripStart(vertexes, k, stripStart);

                var end = k;
                while (end + 1 < vertexes.Count && vertexes[end + 1].Draws)
                {
                    end++;
                }

                var strip = vertexes.Skip(k - 2).Take(end - k + 3).Select(v => v.Index).ToList();
                // The strip's first triangle has to be flipped the way the game flips the triangle ending at k
                if (StripLayout.IsFlipped(2, 0, winding) != StripLayout.IsFlipped(k, stripStart, winding))
                {
                    strip.Insert(0, strip[0]);
                }

                strips.Add(strip);
                k = end + 1;
            }
        }

        return strips;
    }

    // The vertexes' normals, the ones without any get the average of the triangles around them. Skins always have one
    private static List<Vector4> GetNormals(List<Vertex> vertexes, StripLayout layout, StripWinding winding, Func<Vertex, Vector4?> getNormal)
    {
        var sums = new System.Numerics.Vector3[vertexes.Count];
        var stored = vertexes.Select(getNormal).ToList();
        if (stored.Any(normal => normal == null))
        {
            foreach (var face in layout.GetFaces(winding))
            {
                var indexes = face.Indexes!;
                var a = ToVector(vertexes[indexes[0]].Position);
                var b = ToVector(vertexes[indexes[1]].Position);
                var c = ToVector(vertexes[indexes[2]].Position);
                var normal = System.Numerics.Vector3.Cross(b - a, c - a);
                foreach (var index in indexes)
                {
                    sums[index] += normal;
                }
            }
        }

        return vertexes.Select((vertex, i) =>
        {
            if (stored[i] != null)
            {
                return new Vector4(stored[i]!);
            }

            var sum = sums[i];
            return sum.LengthSquared() > 1e-12f ? new Vector4(System.Numerics.Vector3.Normalize(sum).X, System.Numerics.Vector3.Normalize(sum).Y, System.Numerics.Vector3.Normalize(sum).Z, 0) : new Vector4(0, 1, 0, 0);
        }).ToList();
    }

    private static System.Numerics.Vector3 ToVector(Vector4 vector) => new(vector.X, vector.Y, vector.Z);
}
