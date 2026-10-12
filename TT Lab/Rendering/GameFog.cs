using System;
using GlmSharp;

namespace TT_Lab.Rendering;

/// <summary>
/// The fog the game mixes into a chunk's picture: one of the executable's 8 tables of 256 colors (the scenery's <c>FogColor</c>, see
/// <c>SceneryFog</c>, the same in every PS2 release), whose alpha says how much of the color goes in.
/// <para>
/// The PS2 version draws the frame over itself through the table once the buckets 0 to 3 are drawn (the decomp's <c>FilterColours</c>,
/// <c>FUN_001aee28</c>, in bucket 4 before its materials): a pixel's depth picks the entry by its bits 8 to 15 and the alpha blends it,
/// <c>(Cd - Cs)·As + Cs</c>, only where the depth is below 65536 (its sprites' depth and a GREATER test). The depth is the game lens's
/// (near 0.1, far 1500) 2^23 - 1 times one minus the clip depth, so nothing nearer than about 25.6 units is fogged, then up to 40% of the
/// color at a thousand units, and 30% on the sky and the cleared background, whose depth is 0. What draws after it (the blended buckets,
/// the decals, the particles) isn't fogged.
/// </para>
/// <para>
/// The Xbox version blends a 256 texel ramp made of the same tables (in the PS2's palette order, the GS's CLUT, which swaps every 8
/// entries of the second and third quarter of every 32, and with entry 0's alpha 77) over every fragment of the 3D materials, in texture
/// stage 1 (<c>DrawToTexture</c> 0x103f50, <c>FUN_00103f10</c>): the clip depth times 0.02 picks the texel (clamped, point sampled), its
/// alpha a running mean of 16 entries' (0.5 - a/256), which makes it half as strong, all of it from 50 units on, the sky at the ramp's end
/// </para>
/// </summary>
public sealed record GameFog(int Table, bool Xbox)
{
    public const int Entries = 256;
    // The lens every camera of the game has (CameraLensNode::Construct)
    public const float GameNear = 0.1f;
    public const float GameFar = 1500.0f;
    // The PS2's matrix to the screen takes the clip depth -1 to 1 to 2^24 - 2 to 0 (SetUpRenderTarget)
    private const float DepthRange = 8388607.0f;
    // The colour filter's sprites only pass in front of depths below theirs
    public const float Ps2FilteredDepth = 65536.0f;
    // What the Xbox's level shaders scale the clip depth by for the fog stage (c[15].z)
    public const float XboxDepthScale = 0.02f;
    // The PS2's filter draws in bucket 4, before its materials
    public const uint LastFoggedBucket = 3;
    // Alpha 128 everywhere, the picture as it is
    private const int NoFogTable = 6;

    /// <summary>
    /// The color of one of the tables' entries by index (the PS2's), red, green, blue and alpha as bytes, 128 alpha leaving the picture
    /// as it is
    /// </summary>
    public static (Byte R, Byte G, Byte B, Byte A) Entry(int table, int index)
    {
        if (table is < 0 or > 7 || table == NoFogTable)
        {
            return (0, 0, 0, 128);
        }

        var alpha = Alphas[index];
        return table switch
        {
            0 => (VioletColors[index * 3], VioletColors[index * 3 + 1], VioletColors[index * 3 + 2], alpha),
            7 => (BlueToRedColors[index * 3], BlueToRedColors[index * 3 + 1], BlueToRedColors[index * 3 + 2], alpha),
            1 => (0, 0, 0, alpha),
            2 => (165, 235, 255, alpha),
            3 => (0, 255, 18, alpha),
            4 => (255, 255, 255, alpha),
            _ => (255, 200, 120, alpha),
        };
    }

    /// <summary>
    /// The texels of the fog the shaders read (<c>FogTable</c>, red, green, blue and how much of it goes in as bytes): the PS2's every entry
    /// by index with the share of the color, one minus its alpha over 128, the Xbox's ramp
    /// </summary>
    public Byte[] Texels()
    {
        var texels = new Byte[Entries * 4];
        if (!Xbox)
        {
            for (var index = 0; index < Entries; index++)
            {
                var (r, g, b, a) = Entry(Table, index);
                texels[index * 4] = r;
                texels[index * 4 + 1] = g;
                texels[index * 4 + 2] = b;
                texels[index * 4 + 3] = (Byte)Math.Round(Math.Clamp(1.0f - a / 128.0f, 0.0f, 1.0f) * 255.0f);
            }

            return texels;
        }

        // DrawToTexture reads the table from its end, keeping the last 16 entries' (0.5 - a/256) and truncating what it makes, red and blue
        // scaled by 255/256
        Span<float> ring = stackalloc float[16];
        for (var texel = 0; texel < Entries; texel++)
        {
            var (r, g, b, a) = XboxEntry(Table, Entries - 1 - texel);
            ring[texel & 15] = 0.5f - a * 0.00390625f;
            var total = 0.0f;
            foreach (var value in ring)
            {
                total += value;
            }

            texels[texel * 4] = (Byte)(int)(r * 0.00390625f * 255.0f);
            texels[texel * 4 + 1] = g;
            texels[texel * 4 + 2] = (Byte)(int)(b * 0.99609375f);
            texels[texel * 4 + 3] = (Byte)(int)(total * 0.0625f * 255.0f);
        }

        return texels;
    }

    /// <summary>
    /// What the Xbox version's tables hold at an index: the PS2's table in the order the GS's palette keeps it, entry 0 with an alpha of 77
    /// </summary>
    public static (Byte R, Byte G, Byte B, Byte A) XboxEntry(int table, int index)
    {
        var (r, g, b, a) = Entry(table, PaletteSlot(index));
        return index == 0 && a != 128 ? (r, g, b, Alphas[1]) : (r, g, b, a);
    }

    // Bits 3 and 4 of the index swapped (CSM1)
    private static int PaletteSlot(int index) => (index & 0xE7) | (index & 0x8) << 1 | (index & 0x10) >> 1;

    /// <summary>
    /// The PS2's depth of something that far from the camera (along the view, the clip w), 2^24 - 2 at the near plane and 0 at the far
    /// </summary>
    public static float Ps2Depth(float viewDepth)
    {
        return DepthRange * 2.0f * GameNear * (GameFar - viewDepth) / (viewDepth * (GameFar - GameNear));
    }

    /// <summary>
    /// Which entry the PS2's colour filter gives a pixel that far, -1 where it leaves it alone. Past the far plane (which the game doesn't
    /// draw) the first
    /// </summary>
    public static int Ps2Index(float viewDepth)
    {
        var depth = Ps2Depth(Math.Max(viewDepth, GameNear));
        if (depth >= Ps2FilteredDepth)
        {
            return -1;
        }

        return Math.Clamp((int)(depth / 256.0f), 0, Entries - 1);
    }

    /// <summary>
    /// Which texel of its ramp the Xbox's fog stage reads that far: the clip depth (the lens's, like the PS2's projection) times 0.02 as a
    /// coordinate on the 256 texels, clamped
    /// </summary>
    public static int XboxTexel(float viewDepth)
    {
        var clipDepth = viewDepth * (GameFar + GameNear) / (GameFar - GameNear) - 2.0f * GameFar * GameNear / (GameFar - GameNear);
        return Math.Clamp((int)Math.Floor(clipDepth * XboxDepthScale * Entries), 0, Entries - 1);
    }

    /// <summary>
    /// The fog at a fragment that far: its color and how much of it goes in (what <c>MainPass.frag</c> does with the texels)
    /// </summary>
    public vec4 At(float viewDepth)
    {
        var index = Xbox ? XboxTexel(viewDepth) : Ps2Index(viewDepth);
        return index < 0 ? vec4.Zero : TexelAt(index);
    }

    /// <summary>
    /// The fog the sky and the background get: the PS2's depth there is 0, the Xbox's sky reads the ramp's end
    /// </summary>
    public vec4 Sky => TexelAt(Xbox ? Entries - 1 : 0);

    private vec4 TexelAt(int index)
    {
        var texels = Texels();
        return new vec4(texels[index * 4], texels[index * 4 + 1], texels[index * 4 + 2], texels[index * 4 + 3]) / 255.0f;
    }

    public static GameFog Of(uint table, bool xbox) => new(table > 7 ? NoFogTable : (int)table, xbox);

    // Every table's alphas but the sixth's (128 everywhere), by index
    private static readonly Byte[] Alphas = Convert.FromHexString(
        "5A4D4D4E4E4F4F4F50505151525252535354545455555656565757575858585959595A5A5A5B5B5B5C5C5C5D5D5D5E5E" +
        "5E5F5F5F5F606060616161616262626363636364646464656565656666666666676767676868686869696969696A6A6A" +
        "6A6A6B6B6B6B6B6C6C6C6C6C6D6D6D6D6D6E6E6E6E6E6E6F6F6F6F6F7070707070707171717171717272727272727273" +
        "737373737374747474747474757575757575757676767676767676777777777777777778787878787878787979797979" +
        "7979797A7A7A7A7A7A7A7A7A7B7B7B7B7B7B7B7B7B7C7C7C7C7C7C7C7C7C7C7D7D7D7D7D7D7D7D7D7D7E7E7E7E7E7E7E" +
        "7E7E7E7E7F7F7F7F7F7F7F7F7F7F7F80");
    // The colors of table 0, red, green and blue by index
    private static readonly Byte[] VioletColors = Convert.FromHexString(
        "3400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F7" +
        "3400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F73400F7" +
        "3500F73500F73600F73600F73700F73700F73700F73800F73900F73A00F73A00F73A00F73B00F73C00F73D00F73D00F7" +
        "3D00F73E00F73F00F73F00F73F00F64000F64100F64200F54300F54300F44400F34400F34600F24600F14600F04700EF" +
        "4700EF4900EE4900ED4A00EC4B00EB4C00EA4C00E94D00E94D00E74E00E64F00E64F00E45000E35100E25200E15200E0" +
        "5300DE5300DD5400DC5600DB5600DA5600D85700D75800D65800D45900D35A00D25A00D15B00CF5C00CD5C00CC5D00CB" +
        "5E00CA5F00C85F00C66000C56000C46100C26200C06200C06300BE6300BC6400BB6500B96500B76600B76700B56700B3" +
        "6700B26800B06900AF6900AD6A00AB6A00AA6A00A96B00A76B00A56C00A46D00A26D00A16E009F6D009E6E009D6E009B" +
        "6E009A6F00986F009770009570009470009271009170008F71008E71008C71008B72008A720089720087720086720085" +
        "72008472008272008172008072007E72007D72007C72007B720079720079720078720076720075720074720074720072" +
        "72007272007072006F71006E70006C70006A6F00696F00686F00676E00666D00646C00636C00616B006069005F68005E" +
        "68005C67005A6600596500586400566300556100546000525F00515E004F5C004E5C004D5A004B59004A570049560046" +
        "5500455400445100435000424F00404D003F4C003D4A003C49003B4700394600374500364200354100344000323E0031" +
        "3C00303B002E3A002D38002C37002B3500293300283200273000252E00252D00232B002129002128002027001E25001D" +
        "23001C22001B20001A1E00191D00171C00171B001619001418001317001215001114001013000F11000E10000D0F000C" +
        "0E000B0C000A0B00090A0009090008080007070006060006050005040004040003020002020001020001010001000000");
    // The colors of table 7, red, green and blue by index
    private static readonly Byte[] BlueToRedColors = Convert.FromHexString(
        "8393CA8393CA8393CA8392C98492C88492C88492C88592C78592C78591C78591C68591C58691C58691C58690C48690C3" +
        "878FC3878FC2878FC2888EC1888EC0888EC0888DC0898EBF898DBE898CBE8A8CBE8A8CBD8A8BBC8A8BBB8B8BBB8B8ABB" +
        "8C8ABA8C89B98D89B98D8AB78D89B78E89B78E88B68E88B58E87B48F87B38F87B39087B29186B19085B19185B09285AF" +
        "9185AE9284AD9284AC9383AC9382AB9482AA9482AA9481A99581A89580A79680A7967FA59780A4977FA4977FA4987EA2" +
        "987EA2997DA1997DA09A7C9F9B7B9E9B7B9D9B7B9C9C7B9B9C799B9C7A9A9D79999D78989E78989F78969F77959F7694" +
        "A07694A07693A17592A27491A27490A3748FA3738EA3738DA4728DA5728BA5718AA57189A67088A77088A76F86A76F85" +
        "A86E85A86D84A96D83AA6D82AA6C81AA6B80AB6B7FAB6A7EAC6A7DAC697CAD697BAD697AAE687AAE6778B06777AF6676" +
        "B06675B06574B26573B16472B36472B26370B36270B4636FB4626DB5616DB5616CB6606AB66069B75F68B85F67B85E66" +
        "B85D65B95D65BA5D63BA5C63BA5B61BB5B61BB5A60BC5A5EBD595EBD585CBD585BBF585ABF575AC05759BF5758C05557" +
        "C15556C15555C25454C25352C35352C35251C45250C5524FC5514EC5504DC6504CC6504BC74F4BC84F49C84E48C94D48" +
        "C94D47CA4C45CA4C44CA4C43CB4B42CC4A41CC4A41CC4940CD483ECD483ECE483DCE473CCF473CD0463AD0463AD04539" +
        "D14538D14537D14536D34435D34334D44333D44232D44232D54131D54130D5402FD6402ED7402DD73F2CD73F2CD83E2B" +
        "D83D2AD83D29DA3C28D93D27DA3C26DB3C26DB3B25DB3B24DC3A23DC3923DC3A21DD3921DE3820DE381FDE381EDE371E" +
        "DF371DE0371CE0361CE0361BE1351BE13519E23419E23418E33417E23417E33316E43316E43215E43214E43213E53213" +
        "E53112E53012E63011E63011E62F10E6300FE72F0EE72F0EE72F0DE82E0CE82E0CE92E0BE92E0BE92D0BE92D0AE92D0A" +
        "EA2C09EA2C09EA2B08EB2B07EB2B07EB2A07EB2A06EB2B05EC2A05EC2A04ED2905ED2A03ED2A03ED2903ED2903ED2903");
}
