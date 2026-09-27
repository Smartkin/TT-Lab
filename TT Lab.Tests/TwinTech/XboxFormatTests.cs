using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.Tests.TwinTech;

public class XboxFormatTests
{
    private static byte[] Write(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static T Read<T>(byte[] bytes, int length = -1) where T : ITwinSerializable, new()
    {
        var item = new T();
        using var reader = new BinaryReader(new MemoryStream(bytes));
        item.Read(reader, length < 0 ? bytes.Length : length);
        Assert.Equal(bytes.Length, reader.BaseStream.Position);
        return item;
    }

    private static byte[] SubModelBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(5);
        writer.Write(5 * 0x1C);
        writer.Write(2);
        writer.Write(3);
        writer.Write(2);
        for (var i = 0; i < 5; i++)
        {
            writer.Write(i * 1.5f);
            writer.Write(-i * 0.25f);
            writer.Write(2.0f);
            // X is the smallest 11 bit value, Y the largest, Z a negative 10 bit value
            writer.Write((uint)(0x400 | 0x3FF << 11 | (0x3FF - i) << 22));
            writer.Write(new byte[] { 0x7F, (byte)(i * 20), 0x10, 0xFF });
            writer.Write(i * 0.5f);
            writer.Write(1 - i * 0.5f);
        }

        writer.Write(0);
        writer.Flush();
        return stream.ToArray();
    }

    [Fact]
    public void ModelsWriteBackTheSameBytes()
    {
        var bytes = SubModelBytes();

        var subModel = Read<XboxSubModel>(bytes);

        Assert.Equal(bytes, Write(subModel));
        Assert.Equal([3, 2], subModel.GroupSizes);
        Assert.Equal([false, false, true, false, false], subModel.Connection);
    }

    // The normals are packed as signed fractions of 11, 11 and 10 bits
    [Fact]
    public void NormalsAreSignedFractions()
    {
        var subModel = Read<XboxSubModel>(SubModelBytes());

        var normal = subModel.Normals[0];
        Assert.Equal(-1024 / 1023f, normal.X, 5);
        Assert.Equal(1.0f, normal.Y, 5);
        Assert.Equal(-1 / 511f, normal.Z, 5);
        Assert.Equal(0x7F / 255f, subModel.Colors[0].X, 5);
        Assert.Equal(1.0f, subModel.Colors[0].W, 5);
    }

    private static XboxSkinnedGeometry SkinGeometry()
    {
        var geometry = new XboxSkinnedGeometry
        {
            GroupSizes = [3],
            JointPalettes = [[7, 2]],
            Vertexes = [new Vector4(0, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(0, 1, 0, 1)],
            Normals = [new Vector4(0, 0, 1, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 0, 1, 0)],
            Colors = [new Vector4(1, 1, 1, 1), new Vector4(1, 1, 1, 1), new Vector4(1, 1, 1, 1)],
            UVW = [new Vector4(), new Vector4(), new Vector4()],
            SkinJoints =
            [
                new VertexJointInfo { JointIndex1 = 7, Weight1 = 1, WeightsAmount = 1 },
                new VertexJointInfo { JointIndex1 = 2, JointIndex2 = 7, Weight1 = 0.75f, Weight2 = 0.25f, WeightsAmount = 2 },
                new VertexJointInfo { JointIndex1 = 2, Weight1 = 1, WeightsAmount = 1 }
            ]
        };
        return geometry;
    }

    // Vertexes refer to their strip's joints by the shader register of the joint's matrix
    [Fact]
    public void SkinnedVertexesReferToTheirStripsJoints()
    {
        var subSkin = new XboxSubSkin { Material = 0x1234, Geometry = SkinGeometry() };

        var bytes = Write(subSkin);
        var read = Read<XboxSubSkin>(bytes);

        Assert.Equal(bytes, Write(read));
        Assert.Equal([[7, 2]], read.JointPalettes);
        Assert.Equal((7, 0, 1), (read.SkinJoints[0].JointIndex1, read.SkinJoints[0].JointIndex2, read.SkinJoints[0].WeightsAmount));
        Assert.Equal((2, 7, 2), (read.SkinJoints[1].JointIndex1, read.SkinJoints[1].JointIndex2, read.SkinJoints[1].WeightsAmount));
        // The palette's first joint is at register 16, every one takes 4
        var firstVertex = 4 + 16 + 8 + 8;
        Assert.Equal(16, BitConverter.ToUInt16(bytes, firstVertex + 24));
        Assert.Equal(0xFFFF, BitConverter.ToUInt16(bytes, firstVertex + 26));
        Assert.Equal(20, BitConverter.ToUInt16(bytes, firstVertex + XboxSkinnedGeometry.VertexLength + 24));
    }

    // The game leaves registers past the palette and weights close to 0 in the slots it doesn't use, the PS2 version refers to joint 0 there
    [Fact]
    public void LeftoversInUnusedJointSlotsAreKept()
    {
        var bytes = Write(new XboxSubSkin { Geometry = SkinGeometry() });
        var firstVertex = 4 + 16 + 8 + 8;
        BitConverter.TryWriteBytes(bytes.AsSpan(firstVertex + 16), 1e-7f);
        BitConverter.TryWriteBytes(bytes.AsSpan(firstVertex + 26), (ushort)40);

        var read = Read<XboxSubSkin>(bytes);

        Assert.Equal(1, read.SkinJoints[0].WeightsAmount);
        Assert.Equal(0, read.SkinJoints[0].JointIndex2);
        Assert.Equal(bytes, Write(read));
    }

    [Fact]
    public void BlendSkinModelsKeepEveryShapesOffsets()
    {
        var model = new XboxBlendSkinModel(2) { Geometry = SkinGeometry() };
        model.Faces = Enumerable.Range(0, 2).Select(shape => (ITwinBlendSkinFace)new XboxBlendSkinFace(3)
        {
            Vertices = Enumerable.Range(0, 3).Select(i => new VertexBlendShape { Offset = new Vector4(shape, i, 0.5f, 1) }).ToList()
        }).ToList();
        var blendSkin = new XboxAnyBlendSkin { BlendsAmount = 2 };
        var subBlend = new XboxSubBlendSkin(2) { Material = 3 };
        subBlend.Models.Add(model);
        blendSkin.SubBlends.Add(subBlend);

        var bytes = Write(blendSkin);
        var read = Read<XboxAnyBlendSkin>(bytes);

        Assert.Equal(bytes, Write(read));
        Assert.Equal(bytes.Length, read.GetLength());
        var readModel = read.SubBlends[0].Models[0];
        Assert.Equal(3, readModel.VertexesAmount);
        Assert.Equal(2.0f, readModel.Faces[1].Vertices[2].Offset.Y);
    }

    [Fact]
    public void CompressedTexturesKeepTheirHeader()
    {
        // A block's colors lie on a line, a gradient along one direction fits it
        var image = Enumerable.Range(0, 16 * 16).Select(i => new Color((byte)(i % 16 * 16), (byte)(i % 16 * 8), 40, (byte)(i < 128 ? 255 : 0))).ToList();
        var texture = new XboxAnyTexture();
        texture.FromBitmap(image, 16, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8);

        var bytes = Write(texture);
        var read = Read<XboxAnyTexture>(bytes);

        Assert.Equal(bytes, Write(read));
        Assert.Equal(0x88 + 16 * 16, bytes.Length);
        Assert.Equal(ITwinTexture.TexturePixelFormat.DXT5, read.TextureFormat);
        read.CalculateData();
        Assert.All(read.Colors.Zip(image), pair => Assert.InRange(Math.Abs(pair.First.A - pair.Second.A), 0, 0));
        Assert.True(read.Colors.Zip(image).Average(pair => Math.Abs(pair.First.R - pair.Second.R) + Math.Abs(pair.First.G - pair.Second.G)) < 8);
    }

    // Textures inside fonts and menus aren't compressed and don't know their own length
    [Fact]
    public void UncompressedTexturesInsidePtcsReadTheirPixels()
    {
        var image = Enumerable.Range(0, 8 * 4).Select(i => new Color((byte)i, 2, 3, (byte)(i * 8))).ToList();
        var texture = new XboxAnyTexture();
        texture.FromBitmap(image, 8, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.Raw);
        var ptc = new XboxPTC { TexID = 1, MatID = 2, Texture = texture, Material = new XboxAnyMaterial { Name = "font", Shaders = [] } };

        var bytes = Write(ptc);
        var read = Read<XboxPTC>(bytes);

        Assert.Equal(bytes, Write(read));
        read.Texture.CalculateData();
        Assert.Equal(image.Select(c => c.ToARGB()), read.Texture.Colors.Select(c => c.ToARGB()));
    }

    [Fact]
    public void Dxt5KeepsFlatBlocksExact()
    {
        var image = Enumerable.Range(0, 8 * 8).Select(i => i < 32 ? new Color(255, 0, 0, 255) : new Color(0, 0, 255, 128)).ToList();

        var decoded = Dxt5.Decode(Dxt5.Encode(image, 8, 8), 8, 8);

        Assert.Equal(image.Select(c => c.ToARGB()), decoded.Select(c => c.ToARGB()));
    }

    // Characters store their box as fractions of their page
    [Fact]
    public void FontCharactersKeepTheirBoxes()
    {
        var page = new XboxAnyTexture();
        page.FromBitmap(Enumerable.Repeat(new Color(), 128 * 256).ToList(), 128, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.Raw);
        var font = new XboxPSF { SpaceIdentifier = 32 };
        font.FontPages.Add(new XboxPTC { Texture = page, Material = new XboxAnyMaterial { Name = "page", Shaders = [] } });
        font.CharacterData.Add(new VectorCharacterData { PageUv = new Vector2 { X = 0.5f, Y = 254.5f }, Size = new Vector2 { X = 16, Y = 39 } });

        var bytes = Write(font);
        var read = Read<XboxPSF>(bytes);

        Assert.Equal(bytes, Write(read));
        var character = read.CharacterData[0];
        Assert.Equal((0.5f, 254.5f, 16f, 39f), (character.PageUv.X, character.PageUv.Y, character.Size.X, character.Size.Y));
        var record = bytes.AsSpan(bytes.Length - 28).ToArray();
        Assert.Equal(0.5f / 128, BitConverter.ToSingle(record, 8));
        Assert.Equal(215.5f / 256, BitConverter.ToSingle(record, 20));
    }

    // The sound is 16 bit PCM right after its length
    [Fact]
    public void SoundsKeepEverySample()
    {
        var pcm = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var sound = new XboxAnySound();
        sound.SetDataFromPCM(pcm);
        sound.SetFreq(22050);

        var bytes = Write(sound);
        var read = Read<XboxAnySound>(bytes);

        Assert.Equal(pcm, read.ToPCM());
        Assert.Equal(bytes, Write(read));
        Assert.Equal(bytes.Length, read.GetLength());
    }

    // Both versions set every bit above the caller index of an object's trigger behaviours
    [Fact]
    public void TriggerBehavioursKeepTheirUpperBits()
    {
        var triggerBehaviour = new TwinObjectTriggerBehaviour(0xFE00_0C21);

        Assert.Equal(0x7F, triggerBehaviour.UpperBits);
        Assert.Equal(0xFE00_0C21u, triggerBehaviour.Compress());
    }

    // The Xbox version's scenery can list its lights in any order, which is kept while it still lists every light once
    [Fact]
    public void SceneryKeepsItsLightOrder()
    {
        var scenery = new PS2AnyScenery { Name = "scenery", HasLighting = true, LightOrder = [0, 2, 0, 0, 1, 0] };
        scenery.AmbientLights.Add(new AmbientLight());
        scenery.AmbientLights.Add(new AmbientLight());
        scenery.PointLights.Add(new PointLight());

        var read = Read<PS2AnyScenery>(Write(scenery));

        Assert.Equal([0, 2, 0, 0, 1, 0], read.LightOrder);
        read.PointLights.Clear();
        var fallback = Read<PS2AnyScenery>(Write(read));
        Assert.Empty(fallback.LightOrder);
    }

    // Some commands of the Xbox version take more arguments
    [Fact]
    public void XboxCommandsReadTheirOwnArguments()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(54u);
        for (var i = 0; i < 5; i++)
        {
            writer.Write(i + 1);
        }

        writer.Flush();
        var bytes = stream.ToArray();

        var xbox = new XboxBehaviourCommand();
        using (var reader = new BinaryReader(new MemoryStream(bytes)))
        {
            xbox.Read(reader, bytes.Length);
        }

        var ps2 = new PS2BehaviourCommand();
        using (var reader = new BinaryReader(new MemoryStream(bytes)))
        {
            ps2.Read(reader, bytes.Length);
        }

        Assert.Equal(5, xbox.Arguments.Count);
        Assert.Equal(4, ps2.Arguments.Count);
        Assert.Equal(AgentLabVersion.Xbox, xbox.Version);
    }
}
