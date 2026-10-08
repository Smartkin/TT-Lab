using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.AssetData.Instance.Particle;

/// <summary>
/// A kind of decal the game stamps onto the ground, with its variants (the retail game has one type with "Ripple" and "FootFall"),
/// see <see cref="TwinDecalType"/>
/// </summary>
public class DecalType : IDocumentModel
{
    public DecalType()
    {
        Variants = Enumerable.Range(0, TwinDecalType.MaxVariants).Select(_ => new DecalVariant()).ToList();
    }

    public DecalType(TwinDecalType twin)
    {
        DmaTag = CloneUtils.CloneArray(twin.DmaTag);
        VariantCount = twin.VariantCount;
        Leftover1 = twin.Leftover1;
        Leftover2 = twin.Leftover2;
        Leftover3 = twin.Leftover3;
        Variants = Enumerable.Range(0, TwinDecalType.MaxVariants).Select(i => new DecalVariant(twin.Variants[i], twin.Names[i])).ToList();
    }

    public TwinDecalType ToTwin()
    {
        var twin = new TwinDecalType
        {
            DmaTag = CloneUtils.CloneArray(DmaTag),
            VariantCount = VariantCount,
            Leftover1 = Leftover1,
            Leftover2 = Leftover2,
            Leftover3 = Leftover3
        };
        for (var i = 0; i < TwinDecalType.MaxVariants; i++)
        {
            var variant = i < Variants.Count ? Variants[i] : new DecalVariant();
            twin.Variants[i] = variant.ToTwin();
            twin.Names[i] = variant.ToTwinName();
        }

        return twin;
    }

    // The game writes its own DMA tag over it before every upload
    [JsonProperty(Required = Required.Always)]
    public Byte[] DmaTag { get; set; } = new Byte[16];

    // Only the first this many variants are defined
    [Editable] public Int32 VariantCount { get; set; }

    [Editable]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<DecalVariant> Variants { get; set; }

    // The tools' memory
    [JsonProperty(Required = Required.Always)] public Int32 Leftover1 { get; set; }
    [JsonProperty(Required = Required.Always)] public Int32 Leftover2 { get; set; }
    [JsonProperty(Required = Required.Always)] public Int32 Leftover3 { get; set; }

    public string DocumentName => "Decal Type";
}

/// <summary>
/// One decal of a type: 4 colour keys (red, green, blue 0 to 255 and the alpha in W) and 4 size keys (the scale along X, Y and Z with
/// the key's time in W, the first key's W being the lifetime in seconds)
/// </summary>
public class DecalVariant : IDocumentModel
{
    public DecalVariant()
    {
    }

    public DecalVariant(TwinDecalVariant twin, TwinDecalVariantName name)
    {
        (Name, NameLeftover) = ParticleNames.Split(name.Name);
        NameLeftover1 = name.Leftover1;
        NameLeftover2 = name.Leftover2;
        Colors = CloneUtils.CloneArray(twin.Colors);
        Sizes = CloneUtils.CloneArray(twin.Sizes);
    }

    public TwinDecalVariant ToTwin()
    {
        return new TwinDecalVariant
        {
            Colors = CloneUtils.CloneArray(Colors),
            Sizes = CloneUtils.CloneArray(Sizes)
        };
    }

    public TwinDecalVariantName ToTwinName()
    {
        var name = new TwinDecalVariantName { Leftover1 = NameLeftover1, Leftover2 = NameLeftover2 };
        name.Name = ParticleNames.Join(Name, NameLeftover, name.Name.Length);
        return name;
    }

    [Editable] [EditorParam(TextFieldViewModel.TextFieldStringLength, 8U)] [EditorParam(TextFieldViewModel.TextFieldAsciiOnly, true)]
    public string Name { get; set; } = "NULL";

    // What the tools left in the name's buffer after its NUL, written back so the file stays the same
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? NameLeftover { get; set; }

    [Editable(Caption = "Colors (r, g, b, alpha)")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector4[] Colors { get; set; } = [new(), new(), new(), new()];

    [Editable(Caption = "Sizes (x, y, z, time; the first's W the lifetime)")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector4[] Sizes { get; set; } = [new(), new(), new(), new()];

    [JsonProperty(Required = Required.Always)] public UInt32 NameLeftover1 { get; set; }
    [JsonProperty(Required = Required.Always)] public UInt32 NameLeftover2 { get; set; }

    public string DocumentName => Name;
}

/// <summary>
/// The packet that sets the decals up for drawing, see <see cref="TwinDecalUvPacket"/>
/// </summary>
public class DecalUvPacket : IDocumentModel
{
    public DecalUvPacket()
    {
        Uvs = Enumerable.Range(0, TwinDecalUvPacket.MaxUvs).Select(_ => new Vector4()).ToArray();
    }

    public DecalUvPacket(TwinDecalUvPacket twin)
    {
        GifTag = CloneUtils.CloneArray(twin.GifTag);
        Uvs = CloneUtils.CloneArray(twin.Uvs);
        UvCount = twin.UvCount;
        Tail = CloneUtils.CloneArray(twin.Tail);
    }

    public TwinDecalUvPacket ToTwin()
    {
        return new TwinDecalUvPacket
        {
            GifTag = CloneUtils.CloneArray(GifTag),
            Uvs = CloneUtils.CloneArray(Uvs),
            UvCount = UvCount,
            Tail = CloneUtils.CloneArray(Tail)
        };
    }

    [JsonProperty(Required = Required.Always)]
    public Byte[] GifTag { get; set; } = new Byte[16];

    // Texture coordinates (u, v, u, v) of the decal images on the decal texture
    [Editable]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector4[] Uvs { get; set; }

    // The game uploads (this + 1) × 2 of the vectors
    [Editable] public Int32 UvCount { get; set; }

    [JsonProperty(Required = Required.Always)]
    public Byte[] Tail { get; set; } = new Byte[12];

    public string DocumentName => "Decal UV Packet";
}
