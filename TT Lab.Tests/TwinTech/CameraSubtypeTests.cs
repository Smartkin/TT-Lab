using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Tests.TwinTech;

// The camera subtypes as the PAL executable reads them: what each kind stores and how a zone's boxes become a matrix and back
public class CameraSubtypeTests
{
    [Fact]
    public void EveryKindComesBackFromItsBytes()
    {
        var cameras = new CameraSubBase[]
        {
            new CameraPoint { Point = new Vector4(1, 2, 3, 1), Offset = 2 },
            new CameraPoint2 { Point = new Vector4(4, 5, 6, 1), Distance = 0.7f, Mode = 1 },
            new CameraLine { LineStart = new Vector4(0, 0, 0, 1), LineEnd = new Vector4(10, 0, 0, 1), Offset = -3 },
            new CameraLine2 { LineStart = new Vector4(0, 0, 0, 1), LineEnd = new Vector4(10, 0, 0, 1), NearDistance = 2, FarDistance = 15 },
            new CameraPath { PathPoints = [new Vector4(0, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(2, 0, 0, 1), new Vector4(3, 0, 0, 1)], Parameters = [new Vector2 { X = 1, Y = 0.2f }], Offset = -9 },
            new CameraSpline
            {
                StepLength = 1, PathPoints = [new Vector4(0, 0, 0, 0), new Vector4(1, 0, 0, 0)], Tangents = [new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1)],
                Parameters = [new Vector2 { X = 1, Y = 0.2f }], SplineFlags = 15, Offset = 6
            },
            new CameraZone(),
            new BossCamera { Orbit = new Vector4(35, 65, 2, 0), UsesDistanceCurves = true, RadiusBlend = 0.3f, NearHeightOffset = 6, FarHeightOffset = 4, MaxTurnRate = 1, DistanceIncludesHeight = true },
        };

        foreach (var camera in cameras)
        {
            var bytes = Write(camera);
            var read = (CameraSubBase)Activator.CreateInstance(camera.GetType())!;
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                read.Read(reader, bytes.Length);
                Assert.Equal(bytes.Length, reader.BaseStream.Position);
            }

            Assert.Equal(camera.GetLength(), bytes.Length);
            Assert.Equal(bytes, Write(read));
            Assert.Equal(camera.GetCameraType(), read.GetCameraType());
        }

        var boss = (BossCamera)cameras[^1];
        Assert.Equal((35f, 65f, 2f), (boss.Orbit.X, boss.Orbit.Y, boss.Orbit.Z));
        Assert.Equal(1, BitConverter.ToInt32(Write(cameras[0]), 0));
    }

    // A zone box is its axes, corner and sizes; as a matrix the axes scaled by the sizes are the columns and the corner the translation
    [Fact]
    public void ZoneBoxesBecomeMatricesAndBack()
    {
        var box = new[] { new Vector4(0, 0, 1, -12.8f), new Vector4(0, 1, 0, 1), new Vector4(-1, 0, 0, 1), new Vector4(5, 6, 7, 1), new Vector4(20, 10, 4, 1) };

        var transform = CameraZone.ToTransform(box);

        Assert.Equal((0f, 0f, 20f), (transform.Column1.X, transform.Column1.Y, transform.Column1.Z));
        Assert.Equal((0f, 4f, 0f), (transform.Column2.X, transform.Column2.Y, transform.Column2.Z));
        Assert.Equal((-10f, 0f, 0f), (transform.Column3.X, transform.Column3.Y, transform.Column3.Z));
        Assert.Equal((5f, 6f, 7f, 1f), (transform.Column4.X, transform.Column4.Y, transform.Column4.Z, transform.Column4.W));

        transform.Column1 = new Vector4(0, 0, 40, 0);
        transform.Column4 = new Vector4(1, 1, 1, 1);
        CameraZone.FromTransform(box, transform);

        Assert.Equal((0f, 0f, 1f, -12.8f), (box[CameraZone.AxisX].X, box[CameraZone.AxisX].Y, box[CameraZone.AxisX].Z, box[CameraZone.AxisX].W));
        Assert.Equal((40f, 10f, 4f), (box[CameraZone.Size].X, box[CameraZone.Size].Y, box[CameraZone.Size].Z));
        Assert.Equal((1f, 1f, 1f), (box[CameraZone.Origin].X, box[CameraZone.Origin].Y, box[CameraZone.Origin].Z));
    }

    private static byte[] Write(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }
}
