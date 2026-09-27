using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets.Factory;
using TT_Lab.MeshProcessor;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems;

namespace TT_Lab.Tests.Assets;

public class XboxGeometryTests
{
    // A 3 by 2 grid of quads facing up, as the Xbox's tools stripped it: the second row's strip got joined turned around
    private static XboxSubModel GridSubModel()
    {
        Vector4 At(int x, int z) => new(x, 0, z, 1);
        var first = new[] { At(0, 0), At(0, 1), At(1, 0), At(1, 1), At(2, 0), At(2, 1), At(3, 0), At(3, 1) };
        var second = new[] { At(0, 2), At(0, 1), At(1, 2), At(1, 1), At(2, 2), At(2, 1), At(3, 2), At(3, 1) };
        var subModel = new XboxSubModel { GroupSizes = [first.Length, second.Length + 1] };
        // A repeated vertex starts the second strip, which flips which of its triangles the strip order turns around
        foreach (var position in first.Concat(new[] { second[0] }).Concat(second))
        {
            subModel.Vertexes.Add(position);
            subModel.Normals.Add(new Vector4(0, 1, 0, 0));
            subModel.Colors.Add(new Vector4(1, 1, 1, 1));
            subModel.UVW.Add(new Vector4(position.X / 3, position.Z / 2, 1, 0));
        }

        subModel.CalculateData();
        return subModel;
    }

    private static float FacingUp(StripPart part, IndexedFace face)
    {
        var a = part.Vertexes[face.Indexes![0]].Position;
        var b = part.Vertexes[face.Indexes[1]].Position;
        var c = part.Vertexes[face.Indexes[2]].Position;
        // Y of the cross product of the triangle's edges
        return (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);
    }

    // The Xbox's strips don't keep their triangles facing one way, the vertexes' normals tell where they face
    [Fact]
    public void XboxTrianglesFaceTheirNormals()
    {
        var part = StripParts.FromRigid(GridSubModel());

        Assert.True(part.Layout.IgnoresFacing);
        Assert.Equal(12, part.Faces.Count(face => Math.Abs(FacingUp(part, face)) > 0));
        Assert.All(part.Faces.Where(face => Math.Abs(FacingUp(part, face)) > 0), face => Assert.True(FacingUp(part, face) > 0));
        Assert.Equal(12, part.Vertexes.Count);
    }

    [Fact]
    public void XboxModelsExportTheirStrips()
    {
        var subModel = GridSubModel();
        var part = StripParts.FromRigid(subModel);

        var model = (XboxAnyModel)new XboxItemFactory().GenerateModel([new RigidPartExport(part.Vertexes, part.Layout)]);

        var exported = (XboxSubModel)model.SubModels[0];
        Assert.Equal(subModel.GroupSizes, exported.GroupSizes);
        Assert.Equal(subModel.Vertexes.Select(v => (v.X, v.Y, v.Z)), exported.Vertexes.Select(v => (v.X, v.Y, v.Z)));
    }

    // PS2 strips skip triangles, the Xbox draws every triangle of a strip so they get split there, a repeated vertex keeps the facing
    [Theory]
    [InlineData(StripWinding.EvenFlipped)]
    [InlineData(StripWinding.OddFlipped)]
    public void OtherStripsAreSplitWhereTheySkipTriangles(StripWinding winding)
    {
        var (vertexCount, faces) = (9, Enumerable.Range(0, 3).SelectMany(row => new[]
        {
            new IndexedFace(row * 3, row * 3 + 1, row * 3 + 2)
        }).ToList());
        var layout = Stripifier.Stripify(faces, winding);

        var strips = XboxItemFactory.ToStrips(layout, winding);

        var drawn = new List<(int, int, int)>();
        foreach (var strip in strips)
        {
            for (var k = 2; k < strip.Count; k++)
            {
                var (a, b, c) = StripLayout.IsFlipped(k, 0, winding) ? (strip[k - 1], strip[k - 2], strip[k]) : (strip[k - 2], strip[k - 1], strip[k]);
                if (a != b && b != c && a != c)
                {
                    drawn.Add(Rotated(a, b, c));
                }
            }
        }

        Assert.Equal(faces.Select(f => Rotated(f.Indexes![0], f.Indexes[1], f.Indexes[2])).Order(), drawn.Order());
        Assert.True(vertexCount >= 9);
    }

    private static (int, int, int) Rotated(int a, int b, int c)
    {
        if (a < b && a < c)
        {
            return (a, b, c);
        }

        return b < c ? (b, c, a) : (c, a, b);
    }

    // Skins keep the palettes of their strips, strips made anew get the joints their vertexes use
    [Fact]
    public void SkinStripsGetTheJointsTheirVertexesUse()
    {
        var vertexes = Enumerable.Range(0, 4).Select(i => new Vertex(new Vector4(i % 2, i / 2, 0, 1), new Vector4(1, 1, 1, 1), new Vector4())
        {
            JointInfo = new VertexJointInfo { JointIndex1 = 10 + i, Weight1 = 1, WeightsAmount = 1 }
        }).ToList();
        List<IndexedFace> faces = [new IndexedFace(0, 1, 2), new IndexedFace(2, 1, 3)];
        var layout = Stripifier.Stripify(faces, StripParts.SkinWinding);

        var skin = new XboxItemFactory().GenerateSkin([new SkinPartExport(7, vertexes, layout, null)]);

        var subSkin = (XboxSubSkin)skin.SubSkins[0];
        Assert.Equal(7u, subSkin.Material);
        Assert.All(subSkin.JointPalettes, palette => Assert.Subset(new HashSet<int> { 10, 11, 12, 13 }, palette.ToHashSet()));
        var part = StripParts.FromSkin(subSkin);
        Assert.Equal(faces.Count, part.Faces.Count);
        Assert.Equal(vertexes.Select(v => v.JointInfo.JointIndex1).Order(), part.Vertexes.Select(v => v.JointInfo.JointIndex1).Order());
    }

    [Fact]
    public void XboxLayoutsOnlyNeedToDrawTheTriangles()
    {
        var layout = StripLayout.FromArrays([-1, -2, 2, 3], [4], default)!;
        layout.IgnoresFacing = true;

        Assert.True(layout.Draws([new IndexedFace(1, 0, 2), new IndexedFace(3, 2, 1)], 4, StripWinding.EvenFlipped));
        Assert.True(layout.Draws([new IndexedFace(0, 1, 2), new IndexedFace(1, 2, 3)], 4, StripWinding.EvenFlipped));
        Assert.False(layout.Draws([new IndexedFace(0, 1, 2)], 4, StripWinding.EvenFlipped));
    }
}
