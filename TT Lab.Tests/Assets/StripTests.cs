using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets.Factory;
using TT_Lab.MeshProcessor;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Tests.Assets;

public class StripTests
{
    public static TheoryData<StripWinding, string> Meshes()
    {
        var data = new TheoryData<StripWinding, string>();
        foreach (var winding in new[] { StripWinding.EvenFlipped, StripWinding.OddFlipped })
        {
            foreach (var mesh in MeshesByName.Keys)
            {
                data.Add(winding, mesh);
            }
        }

        return data;
    }

    private static readonly Dictionary<string, Func<(int VertexCount, List<IndexedFace> Faces)>> MeshesByName = new()
    {
        ["Triangle"] = () => (3, [new IndexedFace(0, 1, 2)]),
        ["Grid"] = () => Grid(16, 11),
        ["Box"] = Box,
        ["Fan"] = () => (51, Enumerable.Range(0, 50).Select(i => new IndexedFace(0, 1 + i, 1 + (i + 1) % 50)).ToList()),
        ["Soup"] = () => Soup(new Random(9), 120, 300),
        ["Doubled"] = () => (4, [new IndexedFace(0, 1, 2), new IndexedFace(0, 1, 2), new IndexedFace(2, 1, 0), new IndexedFace(1, 3, 2)]),
        // Many triangles on one edge, which only two of them can share in a strip
        ["NonManifold"] = () => (8, Enumerable.Range(2, 6).Select(i => new IndexedFace(0, 1, i)).ToList()),
        ["TwoGrids"] = () =>
        {
            var (count, faces) = Grid(5, 5);
            return (count * 2, faces.Concat(faces.Select(f => new IndexedFace(f.Indexes![0] + count, f.Indexes[1] + count, f.Indexes[2] + count))).ToList());
        }
    };

    [Theory]
    [MemberData(nameof(Meshes))]
    public void StripsDrawEveryTriangleOnceFacingTheSameWay(StripWinding winding, string mesh)
    {
        var (vertexCount, faces) = MeshesByName[mesh]();

        var layout = Stripifier.Stripify(faces, winding);

        Assert.True(layout.Draws(faces, vertexCount, winding));
        Assert.All(layout.Batches, batch => Assert.InRange(batch.Vertexes.Count, 3, TwinVIFCompiler.MaxBatchVertexes));
    }

    [Theory]
    [InlineData(StripWinding.EvenFlipped)]
    [InlineData(StripWinding.OddFlipped)]
    public void TrianglesWithoutAreaAreLeftOut(StripWinding winding)
    {
        List<IndexedFace> faces = [new IndexedFace(0, 1, 2), new IndexedFace(1, 1, 2), new IndexedFace(2, 3, 2)];

        var layout = Stripifier.Stripify(faces, winding);

        Assert.Equal([(0, 1, 2)], layout.GetFaces(winding).Select(Rotated).Where(face => face.Item1 != face.Item2 && face.Item2 != face.Item3 && face.Item1 != face.Item3));
    }

    // A flat grid facing up in two strips, the second starting on an odd vertex of the batch. Skins count which of their triangles are
    // flipped from their strip's start, counted from the batch that strip faced down
    [Fact]
    public void SkinStripsStartingOnAnOddVertexFaceLikeTheOthers()
    {
        var positions = new System.Numerics.Vector3[] { new(0, 0, 0), new(0, 0, 1), new(1, 0, 0), new(1, 0, 1), new(2, 0, 0), new(0, 0, 5), new(0, 0, 6), new(1, 0, 5) };
        var layout = new StripLayout
        {
            Batches = [new StripBatch { Vertexes = [new(0, false), new(1, false), new(2, true), new(3, true), new(4, true), new(5, false), new(6, false), new(7, true)] }]
        };

        var faces = layout.GetFaces(StripParts.SkinWinding);

        Assert.Equal(4, faces.Count);
        Assert.All(faces, face =>
        {
            var (a, b, c) = (positions[face.Indexes![0]], positions[face.Indexes[1]], positions[face.Indexes[2]]);
            Assert.True(System.Numerics.Vector3.Cross(b - a, c - a).Y > 0, $"{face.Indexes[0]}, {face.Indexes[1]}, {face.Indexes[2]} faces down");
        });
    }

    [Fact]
    public void GridsTakeFewVertexesPerTriangle()
    {
        var (vertexCount, faces) = Grid(40, 40);

        var layout = Stripifier.Stripify(faces, StripWinding.EvenFlipped);

        Assert.True(layout.Draws(faces, vertexCount, StripWinding.EvenFlipped));
        Assert.True(layout.VertexCount < faces.Count * 1.3, $"{layout.VertexCount} vertexes for {faces.Count} triangles");
    }

    [Fact]
    public void EveryOtherTriangleOfAStripIsFlipped()
    {
        var layout = StripLayout.FromArrays([-1, -2, 2, 3, 4], [5], TwinVifPadding.QuadWord)!;

        Assert.Equal([(1, 0, 2), (1, 2, 3), (3, 2, 4)], layout.GetFaces(StripWinding.EvenFlipped).Select(Tuple));
        Assert.Equal([(0, 1, 2), (2, 1, 3), (2, 3, 4)], layout.GetFaces(StripWinding.OddFlipped).Select(Tuple));
    }

    [Fact]
    public void LayoutsSurviveTheirArrays()
    {
        var layout = Stripifier.Stripify(Grid(8, 8).Faces, StripWinding.OddFlipped);
        layout.Padding = TwinVifPadding.NopPerByte;

        var (vertexes, batchSizes) = layout.ToArrays();
        var read = StripLayout.FromArrays(vertexes, batchSizes, layout.Padding)!;

        Assert.Equal(TwinVifPadding.NopPerByte, read.Padding);
        Assert.Equal(layout.Batches.Select(b => b.Vertexes), read.Batches.Select(b => b.Vertexes));
        Assert.Null(StripLayout.FromArrays(vertexes, [vertexes.Length + 1], TwinVifPadding.QuadWord));
    }

    [Fact]
    public void DrawingTellsMissingAndTurnedTriangles()
    {
        var (vertexCount, faces) = Grid(3, 3);
        var layout = Stripifier.Stripify(faces, StripWinding.EvenFlipped);

        Assert.False(layout.Draws(faces.Skip(1).ToList(), vertexCount, StripWinding.EvenFlipped));
        var first = faces[0].Indexes!;
        Assert.False(layout.Draws([..faces.Skip(1), new IndexedFace(first[1], first[0], first[2])], vertexCount, StripWinding.EvenFlipped));
        Assert.False(layout.Draws(faces, vertexCount, StripWinding.OddFlipped));
        Assert.False(layout.Draws(faces, vertexCount - 1, StripWinding.EvenFlipped));
    }

    [Fact]
    public void StoredLayoutIsKeptWhileItDrawsTheTriangles()
    {
        var (vertexCount, faces) = Grid(4, 4);
        var vertexes = Enumerable.Range(0, vertexCount).Select(i => new Vertex(new Vector4(i, 0, 0, 1))).ToList();
        var stored = Stripifier.Stripify(faces, StripWinding.EvenFlipped);

        Assert.Same(stored, StripParts.GetValidLayout(stored, vertexes, faces, StripWinding.EvenFlipped));

        var edited = faces.Take(faces.Count - 2).ToList();
        var layout = StripParts.GetValidLayout(stored, vertexes, edited, StripWinding.EvenFlipped);
        Assert.NotSame(stored, layout);
        Assert.True(layout.Draws(edited, vertexCount, StripWinding.EvenFlipped));
    }

    // Game strips go through TT Lab's vertexes and triangles and come out as the same bytes
    [Theory]
    [InlineData(TwinVifPadding.QuadWord)]
    [InlineData(TwinVifPadding.NopPerByte)]
    public void RigidPartsExportToTheSameBytes(TwinVifPadding padding)
    {
        var random = new Random(13);
        var model = new PS2AnyModel { SubModels = [StripSubModel(random, padding, [30, 12]), StripSubModel(random, padding, [7])] };
        model.Compile();
        var original = Serialize(model);
        var read = new PS2AnyModel();
        using (var reader = new BinaryReader(new MemoryStream(original)))
        {
            read.Read(reader, original.Length);
        }

        var parts = read.SubModels.Select(StripParts.FromRigid).ToList();
        var exported = new PS2ItemFactory().GenerateModel(parts.Select(part => new RigidPartExport(part.Vertexes, part.Layout)).ToList());
        exported.Compile();

        Assert.Equal(original, Serialize(exported));
        // The vertex the strip uses twice is the same vertex
        Assert.Equal(41, parts[0].Vertexes.Count);
    }

    // A strip over a row of quads: the first two vertexes start it, every following one draws, one vertex comes back later
    private static PS2SubModel StripSubModel(Random random, TwinVifPadding padding, int[] groupSizes)
    {
        var subModel = new PS2SubModel
        {
            Padding = padding,
            GroupSizes = [..groupSizes],
            Vertexes = [],
            UVW = [],
            Colors = [],
            Connection = [],
            Normals = [],
            EmitColor = [],
            UnusedBlob = []
        };
        foreach (var size in groupSizes)
        {
            for (var i = 0; i < size; i++)
            {
                subModel.Vertexes.Add(new Vector4(i / 2, i % 2, random.NextSingle(), 0));
                var uv = new Vector4(random.NextSingle(), random.NextSingle(), 1.0f, 0);
                uv.SetBinaryX(uv.GetBinaryX() & 0xFFFFFF00);
                uv.SetBinaryY(uv.GetBinaryY() & 0xFFFFFF00);
                subModel.UVW.Add(uv);
                subModel.Colors.Add(Vector4.FromColor(new Color((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 0x80, true)));
                subModel.Connection.Add(i >= 2);
            }
        }

        if (groupSizes[0] > 20)
        {
            // Repeats vertex 5 at the end of the first batch
            var last = groupSizes[0] - 1;
            subModel.Vertexes[last] = new Vector4(subModel.Vertexes[5]);
            subModel.UVW[last] = new Vector4(subModel.UVW[5]);
            subModel.Colors[last] = new Vector4(subModel.Colors[5]) { StoresColorWithAlphaBlend = subModel.Colors[5].StoresColorWithAlphaBlend };
        }

        return subModel;
    }

    private static (int VertexCount, List<IndexedFace> Faces) Grid(int width, int height)
    {
        var faces = new List<IndexedFace>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var corner = y * (width + 1) + x;
                faces.Add(new IndexedFace(corner, corner + width + 1, corner + 1));
                faces.Add(new IndexedFace(corner + 1, corner + width + 1, corner + width + 2));
            }
        }

        return ((width + 1) * (height + 1), faces);
    }

    private static (int, List<IndexedFace>) Box()
    {
        int[][] quads = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];
        return (8, quads.SelectMany(q => new[] { new IndexedFace(q[0], q[1], q[2]), new IndexedFace(q[0], q[2], q[3]) }).ToList());
    }

    private static (int, List<IndexedFace>) Soup(Random random, int vertexCount, int faceCount)
    {
        var faces = new List<IndexedFace>();
        while (faces.Count < faceCount)
        {
            var a = random.Next(vertexCount);
            var b = random.Next(vertexCount);
            var c = random.Next(vertexCount);
            if (a != b && b != c && a != c)
            {
                faces.Add(new IndexedFace(a, b, c));
            }
        }

        return (vertexCount, faces);
    }

    private static (int, int, int) Tuple(IndexedFace face) => (face.Indexes![0], face.Indexes[1], face.Indexes[2]);

    private static (int, int, int) Rotated(IndexedFace face)
    {
        var (a, b, c) = Tuple(face);
        return a <= b && a <= c ? (a, b, c) : b <= c ? (b, c, a) : (c, a, b);
    }

    private static byte[] Serialize(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }
}
