using System.Numerics;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets.Factory;
using TT_Lab.MeshProcessor;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using TwinVector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using Vector3 = System.Numerics.Vector3;
using static TT_Lab.Tests.Support.TestGeometry;

namespace TT_Lab.Tests.Assets;

// The game's models go into TT Lab model files and back into the game's format without a byte changing
public class TlmMeshesTests
{
    [Theory]
    [InlineData(TwinVifPadding.QuadWord)]
    [InlineData(TwinVifPadding.NopPerByte)]
    public void ModelsSurviveTheFile(TwinVifPadding padding)
    {
        var random = new Random(21);
        var original = new PS2AnyModel { SubModels = [RigidSubModel(random, padding, [38, 21], true), RigidSubModel(random, padding, [9], false)] };
        original.Compile();
        var bytes = Serialize(original);
        var parts = Deserialize(new PS2AnyModel(), bytes).SubModels.Select(subModel => StripParts.FromRigid(subModel))
            .Select(part => new ModelPart { Vertexes = part.Vertexes, Faces = part.Faces, Layout = part.Layout });

        var read = RoundTrip(parts, false);

        Assert.All(read, part => Assert.NotNull(part.Layout));
        var rebuilt = new PS2ItemFactory().GenerateModel(read.Select(part => new RigidPartExport(part.Vertexes, part.Layout!)).ToList());
        rebuilt.Compile();
        Assert.Equal(bytes, Serialize(rebuilt));
    }

    [Theory]
    [InlineData(TwinVifPadding.QuadWord)]
    [InlineData(TwinVifPadding.NopPerByte)]
    public void SkinsSurviveTheFile(TwinVifPadding padding)
    {
        var random = new Random(22);
        var original = new PS2AnySkin { SubSkins = [SubSkin(random, padding, 0x10, [38, 38, 11]), SubSkin(random, padding, 0x20, [14])] };
        original.Compile();
        var bytes = Serialize(original);
        var parts = Deserialize(new PS2AnySkin(), bytes).SubSkins.Select(subSkin =>
        {
            var part = StripParts.FromSkin(subSkin);
            return new ModelPart { Vertexes = part.Vertexes, Faces = part.Faces, Layout = part.Layout, Compression = subSkin.Compression };
        });

        var read = RoundTrip(parts, true);

        var rebuilt = new PS2ItemFactory().GenerateSkin(read.Select((part, i) => new SkinPartExport(i == 0 ? 0x10U : 0x20U, part.Vertexes, part.Layout!, part.Compression)).ToList());
        rebuilt.Compile();
        Assert.Equal(bytes, Serialize(rebuilt));
    }

    [Fact]
    public void BlendSkinsSurviveTheFile()
    {
        var random = new Random(23);
        const int shapes = 2;
        var original = new PS2AnyBlendSkin { BlendsAmount = shapes, SubBlends = [SubBlend(random, shapes, 0x30, [20, 16]), SubBlend(random, shapes, 0x40, [12])] };
        original.Compile();
        var bytes = Serialize(original);
        var parts = Deserialize(new PS2AnyBlendSkin(), bytes).SubBlends.Select(subBlend =>
        {
            var part = StripParts.FromBlend(subBlend.Models, shapes, out var shapeOffsets);
            return new ModelPart { Vertexes = part.Vertexes, Faces = part.Faces, Layout = part.Layout, Compression = subBlend.Models[0].Compression, ShapeOffsets = shapeOffsets };
        });

        var read = RoundTrip(parts, true);

        Assert.All(read, part => Assert.Equal(shapes, part.ShapeOffsets.Count));
        var rebuilt = new PS2ItemFactory().GenerateBlendSkin(shapes, read.Select((part, i) => new BlendPartExport(i == 0 ? 0x30U : 0x40U, part.Vertexes, part.ShapeOffsets, part.Layout!, part.Compression)).ToList());
        rebuilt.Compile();
        Assert.Equal(bytes, Serialize(rebuilt));
    }

    [Fact]
    public void EditedNormalsKeepTheFlagsTheGameStoresInThem()
    {
        var part = new ModelPart
        {
            Vertexes = [RigidVertex(new TwinVector4(0, 0, 0, 0), Flagged(new TwinVector4(0, 1, 0, 0), 0x20)), RigidVertex(new TwinVector4(1, 0, 0, 0), Flagged(new TwinVector4(0, 1, 0, 0), 0x00)),
                RigidVertex(new TwinVector4(0, 0, 1, 0), Flagged(new TwinVector4(0, 0.5f, 0, 0), 0x20))],
            Faces = [new IndexedFace(0, 1, 2)]
        };

        // The first normal gets turned in Blender, the others stay
        var read = RoundTrip([part], false, (file, json) => json["normal"] = file.Write(new[] { 1f, 0, 0, 0, 1, 0, 0, 1, 0 }.AsSpan()))[0];

        Assert.Equal(1.0f, read.Vertexes[0].Normal.X, 1e-5f);
        Assert.Equal(0x20U, read.Vertexes[0].Normal.GetBinaryX() & 0xFF);
        Assert.Equal(part.Vertexes[1].Normal.GetBinaryY(), read.Vertexes[1].Normal.GetBinaryY());
        // Normals the game didn't normalize stay as they were while they point the same way
        Assert.Equal(0.5f, read.Vertexes[2].Normal.Y);
    }

    [Fact]
    public void EditedWeightsReplaceTheGameOnes()
    {
        var part = new ModelPart { Vertexes = [SkinVertex(0.6f, 0.2f), SkinVertex(0.6f, 0.2f), SkinVertex(0.6f, 0.2f)], Faces = [new IndexedFace(0, 1, 2)] };

        var edited = RoundTrip([part], true, (file, json) => Groups(file, json, [3, 4], [0.5f, 0.5f]))[0];
        var unedited = RoundTrip([part], true, (file, json) => Groups(file, json, [3, 4], [0.75f, 0.25f]))[0];

        Assert.All(edited.Vertexes, vertex => Assert.Equal((0.5f, 0.5f), (vertex.JointInfo.Weight1, vertex.JointInfo.Weight2)));
        // Weights that weren't edited are the game's, which don't have to add up to 1
        Assert.All(unedited.Vertexes, vertex => Assert.Equal((0.6f, 0.2f), (vertex.JointInfo.Weight1, vertex.JointInfo.Weight2)));
    }

    // Blender scales a vertex's weights to add up to 1 when it deforms it, however much they add up to, the game needs them that way
    [Fact]
    public void BlendersWeightsAddUpToOne()
    {
        var part = new ModelPart { Vertexes = [SkinVertex(0.6f, 0.2f), SkinVertex(0.6f, 0.2f), SkinVertex(0.6f, 0.2f)], Faces = [new IndexedFace(0, 1, 2)] };

        var heavy = RoundTrip([part], true, (file, json) => Groups(file, json, [3, 4], [0.5f, 1.5f]))[0];
        var light = RoundTrip([part], true, (file, json) => Groups(file, json, [3, 4, 5], [0.1f, 0.05f, 0.05f]))[0];

        Assert.All(heavy.Vertexes, vertex => Assert.Equal((3, 0.25f, 4, 0.75f), (vertex.JointInfo.JointIndex1, vertex.JointInfo.Weight1, vertex.JointInfo.JointIndex2, vertex.JointInfo.Weight2)));
        Assert.All(light.Vertexes, vertex => Assert.Equal((0.5f, 0.25f, 0.25f, 3), (vertex.JointInfo.Weight1, vertex.JointInfo.Weight2, vertex.JointInfo.Weight3, vertex.JointInfo.WeightsAmount)));
    }

    // Blender keeps weights by bone, the game's order stays while the weights are the same
    [Fact]
    public void JointsReorderedInBlenderKeepTheGameOrder()
    {
        var part = new ModelPart { Vertexes = [SkinVertex(0.4f, 0.4f), SkinVertex(0.4f, 0.4f), SkinVertex(0.4f, 0.4f)], Faces = [new IndexedFace(0, 1, 2)] };

        var read = RoundTrip([part], true, (file, json) => Groups(file, json, [4, 3], [0.5f, 0.5f]))[0];

        Assert.All(read.Vertexes, vertex => Assert.Equal((3, 4), (vertex.JointInfo.JointIndex1, vertex.JointInfo.JointIndex2)));
    }

    // The game gives some vertexes the same joint twice, Blender's vertex group has both weights together
    [Fact]
    public void JointsTheGameGivesTwiceStay()
    {
        var vertex = SkinVertex(0.5f, 0.3f);
        vertex.JointInfo.JointIndex2 = 3;
        var part = new ModelPart { Vertexes = [vertex, vertex, vertex], Faces = [new IndexedFace(0, 1, 2)] };

        var read = RoundTrip([part], true, (file, json) => Groups(file, json, [3], [1.0f]))[0];

        Assert.All(read.Vertexes, read => Assert.Equal((3, 0.5f, 3, 0.3f), (read.JointInfo.JointIndex1, read.JointInfo.Weight1, read.JointInfo.JointIndex2, read.JointInfo.Weight2)));
    }

    // Blender flips UVs upside down and back, and keeps shapes as positions
    [Fact]
    public void ValuesMovedByRoundingErrorsStayTheGameOnes()
    {
        var part = new ModelPart { Vertexes = [SkinVertex(1, 0), SkinVertex(0.5f, 0.5f), SkinVertex(0.25f, 0.75f)], Faces = [new IndexedFace(0, 1, 2)] };
        part.Vertexes[0].UV = new TwinVector4(0.1f, 0.3f, part.Vertexes[0].UV.Z, part.Vertexes[0].UV.W);
        part.ShapeOffsets = [part.Vertexes.Select(_ => new TwinVector4(0.1f, 0.2f, 0.3f, 1)).ToList()];

        var read = RoundTrip([part], true, (file, json) =>
        {
            json["twin_uv"] = json["uv"]!.DeepClone();
            var uvs = file.Read<float>(json["uv"]);
            uvs[0] += 3e-8f;
            uvs[2] += 0.25f;
            json["uv"] = file.Write(uvs.AsSpan());
            json["twin_shapes"] = json["shapes"]!.DeepClone();
            var offsets = file.Read<float>(json["shapes"]![0]);
            offsets[0] += 1e-7f;
            offsets[3] += 0.5f;
            json["shapes"]![0] = file.Write(offsets.AsSpan());
        })[0];

        Assert.Equal(0.1f, read.Vertexes[0].UV.X);
        Assert.Equal(part.Vertexes[1].UV.X + 0.25f, read.Vertexes[1].UV.X);
        Assert.Equal(0.1f, read.ShapeOffsets[0][0].X);
        Assert.Equal(0.6f, read.ShapeOffsets[0][1].X);
    }

    [Fact]
    public void MovedNodesGetBakedIntoTheVertexes()
    {
        var part = new ModelPart
        {
            Vertexes = [RigidVertex(new TwinVector4(1, 0, 0, 0), new TwinVector4(1, 0, 0, 0)), RigidVertex(new TwinVector4(0, 1, 0, 0), new TwinVector4(1, 0, 0, 0)),
                RigidVertex(new TwinVector4(0, 0, 1, 0), new TwinVector4(1, 0, 0, 0))],
            Faces = [new IndexedFace(0, 1, 2)]
        };
        var transform = Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(10, 0, 0);

        var read = RoundTrip([part], false, transform: transform)[0];

        var moved = Vector3.Transform(Vector3.UnitX, transform);
        Assert.Equal(moved.X, read.Vertexes[0].Position.X, 1e-5f);
        Assert.Equal(moved.Z, read.Vertexes[0].Position.Z, 1e-5f);
        var turned = Vector3.TransformNormal(Vector3.UnitX, transform);
        Assert.Equal(turned.Z, read.Vertexes[0].Normal.Z, 1e-5f);
    }

    [Fact]
    public void EditedTrianglesDropTheStrips()
    {
        var random = new Random(24);
        var subModel = RigidSubModel(random, TwinVifPadding.QuadWord, [12], false);
        subModel.Compile();
        var part = StripParts.FromRigid(Deserialize(new PS2SubModel(), Serialize(subModel)));
        var modelPart = new ModelPart { Vertexes = part.Vertexes, Faces = part.Faces, Layout = part.Layout };
        Assert.NotNull(RoundTrip([modelPart], false)[0].Layout);

        var read = RoundTrip([modelPart], false, (file, json) => json["faces"] = file.Write(file.Read<uint>(json["faces"]).Skip(3).ToArray().AsSpan()))[0];

        Assert.Null(read.Layout);
        Assert.Equal(part.Faces.Count - 1, read.Faces.Count);
        var layout = StripParts.GetValidLayout(read.Layout, read.Vertexes, read.Faces, StripParts.RigidWinding);
        Assert.True(layout.Draws(read.Faces, read.Vertexes.Count, StripParts.RigidWinding));
    }

    // Blender can't have two faces on the same vertexes, which the game draws some triangles from both sides with
    [Fact]
    public void TrianglesDrawnFromBothSidesKeepTheirVertexes()
    {
        var part = new ModelPart
        {
            Vertexes = [RigidVertex(new TwinVector4(0, 0, 0, 0), new TwinVector4(0, 1, 0, 0)), RigidVertex(new TwinVector4(1, 0, 0, 0), new TwinVector4(0, 1, 0, 0)),
                RigidVertex(new TwinVector4(0, 0, 1, 0), new TwinVector4(0, 1, 0, 0))],
            Faces = [new IndexedFace(0, 1, 2), new IndexedFace(0, 2, 1)]
        };
        var file = new TlmFile("Test", "Test");

        var json = TlmMeshes.WriteRigidPart(file, part, -1);
        var read = TlmMeshes.ReadRigidPart(file, json);

        Assert.Equal(6, json.GetInt("vertices"));
        Assert.Equal(3, read.Vertexes.Count);
        Assert.Equal([0, 1, 2, 0, 2, 1], read.Faces.SelectMany(face => face.Indexes!));
    }

    private static void Groups(TlmFile file, JsonObject json, int[] joints, float[] weights)
    {
        var count = json.GetInt("vertices");
        var groupJoints = Enumerable.Range(0, count).SelectMany(_ => joints.Concat(Enumerable.Repeat(-1, 4 - joints.Length))).ToArray();
        var groupWeights = Enumerable.Range(0, count).SelectMany(_ => weights.Concat(Enumerable.Repeat(0f, 4 - weights.Length))).ToArray();
        json["group_joints"] = file.Write(groupJoints.AsSpan());
        json["group_weights"] = file.Write(groupWeights.AsSpan());
    }

    // Written, edited like Blender would, saved, loaded and read back
    private static List<ModelPart> RoundTrip(IEnumerable<ModelPart> parts, bool skinned, Action<TlmFile, JsonObject>? edit = null, Matrix4x4? transform = null)
    {
        var file = new TlmFile("Test", "Test");
        var mesh = TlmMeshes.WriteMesh(file, parts.Select(part => (part, -1)), skinned);
        file.Root = new JsonObject { ["mesh"] = mesh };
        foreach (var part in mesh["parts"]!.AsArray().OfType<JsonObject>())
        {
            edit?.Invoke(file, part);
        }

        using var stream = new MemoryStream();
        file.WriteTo(stream);
        stream.Position = 0;
        var read = TlmFile.Read(stream);
        return TlmMeshes.ReadMesh(read, read.Root!["mesh"] as JsonObject, skinned, transform).Select(part => part.Part).ToList();
    }

    private static Vertex RigidVertex(TwinVector4 position, TwinVector4 normal)
    {
        return new Vertex(position, new TwinVector4(0.5f, 0.5f, 0.5f, 1.0f), new TwinVector4(0, 0, 1, 0)) { Normal = normal };
    }

    private static Vertex SkinVertex(float weight1, float weight2)
    {
        return new Vertex(new TwinVector4(weight1, weight2, 0, 0), new TwinVector4(0.5f, 0.5f, 0.5f, 0.5f), new TwinVector4(0, 0, 1, 0))
        {
            JointInfo = new VertexJointInfo { JointIndex1 = 3, JointIndex2 = 4, Weight1 = weight1, Weight2 = weight2, WeightsAmount = weight2 > 0 ? 2 : 1, Connection = true }
        };
    }
}
