using System.Text.RegularExpressions;
using GlmSharp;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.UniformDescs;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Rendering;

// The Generic program reads a material's values from the context's material table (MainPass.vert/.frag's TwinMaterial) instead of uniforms
// set for every batch: an entry has to hold what the uniforms held, laid out the way TwinMaterial.glsl's TwinMaterialEntry reads it
public class MaterialTableTests
{
    private static TwinMaterialDesc Material() => new()
    {
        UseTexture = 1.0f,
        AlphaTestMethod = 5,
        AlphaReference = 64,
        Mipmapped = true,
        LodL = 0,
        LodK = -69.0f / 16.0f,
        Fog = TwinMaterialDesc.BothFogs,
    };

    [Fact]
    public void AnEntryIsLaidOutTheWayTheShaderReadsIt()
    {
        var material = new TwinMaterialDesc
        {
            DeformMode = 2,
            DeformSpeed = 0.5f,
            DeformAmplitude = new vec3(1.0f, 2.0f, 3.0f),
            ReflectDist = new vec2(1.0f, 0.25f),
            AlphaTestMethod = -1,
            AlphaReference = 64,
            AlphaFail = TwinShader.ProcessAfterAlphaTestFailed.FB_ONLY,
            AlphaBlend = 1.0f,
            EnvMap = 2.0f,
            UseTexture = 1.0f,
            Mipmapped = true,
            LodL = 2,
            LodK = -4.3125f,
            Fog = TwinMaterialDesc.BothFogs,
            EditorShading = true,
            Lit = true,
        };

        var entry = MaterialTable.EntryOf(material, 0.0);

        Assert.Equal(MaterialTable.FloatsPerEntry, entry.Length);
        Assert.Equal([1.0f, 2.0f, 3.0f, 0.5f], entry[..4]);
        Assert.Equal([0.0f, 0.0f, 1.0f, 0.25f], entry[4..8]);
        // The ints as their bits: written as floats, the shader's fog == 2 and alpha_method >= 0 wouldn't hold
        Assert.Equal(2, BitConverter.SingleToInt32Bits(entry[8]));
        Assert.Equal(-1, BitConverter.SingleToInt32Bits(entry[9]));
        Assert.Equal(TwinMaterialDesc.BothFogs, BitConverter.SingleToInt32Bits(entry[10]));
        Assert.Equal([64.0f, 1.0f, 1.0f, 2.0f], entry[12..16]);
        Assert.Equal([1.0f, 1.0f, 4.0f, -4.3125f], entry[16..20]);
        Assert.Equal([0.0f, 1.0f, 1.0f, 0.0f], entry[20..24]);
    }

    [Fact]
    public void TheShaderReadsTheEntryInTheSameOrder()
    {
        var source = ManifestResourceLoader.LoadTextFile("Media/Shaders/Includes/TwinMaterial.glsl");
        var body = Regex.Match(source, @"struct TwinMaterialEntry \{(.*?)\};", RegexOptions.Singleline).Groups[1].Value;
        var fields = Regex.Matches(body, @"^\s*(\w+) (\w+);", RegexOptions.Multiline).Select(match => $"{match.Groups[1].Value} {match.Groups[2].Value}");

        Assert.Equal(["vec4 deform", "vec4 offsets", "ivec4 modes", "vec4 alpha", "vec4 sampling", "vec4 shading"], fields);
    }

    // The uniforms took 1 and 0 for whatever the switches held
    [Fact]
    public void SwitchesAreOneOrZero()
    {
        var entry = MaterialTable.EntryOf(Material() with { UseTexture = 2.0f, AlphaBlend = 0.5f }, 0.0);

        Assert.Equal(0.0f, entry[14]);
        Assert.Equal(0.0f, entry[16]);
        Assert.True(MaterialTable.SharesEntry(Material() with { UseTexture = 2.0f }, Material() with { UseTexture = 0.0f }));
    }

    // Textures, samplers and blending are the draw's GL state, not the entry's: the batches of every material of the same values share one
    [Fact]
    public void MaterialsOfTheSameValuesShareAnEntry()
    {
        var material = Material();

        Assert.True(MaterialTable.SharesEntry(material, material with
        {
            BlendFunc = TwinShader.AlphaBlendPresets.Add,
            DepthWrite = true,
            DepthTest = TwinShader.DepthTestMethod.ALWAYS,
            MagnifiesLinearly = false,
            ClampsCoordinates = true,
        }));
        Assert.False(MaterialTable.SharesEntry(material, material with { LodK = 0.0f }));
        Assert.False(MaterialTable.SharesEntry(material, material with { Fog = TwinMaterialDesc.NoFog }));
    }

    // A material's scroll values only tell its entry apart when it scrolls, and leftover NaNs don't make every entry a new one
    [Fact]
    public void OnlyScrollingMaterialsAreToldApartByTheirScrolls()
    {
        var still = Material() with { ScrollPhases = new vec2(0.25f, float.NaN), ScrollSpeeds = new vec2(1.0f, 2.0f) };

        Assert.True(MaterialTable.SharesEntry(still, Material()));
        Assert.True(MaterialTable.SharesEntry(Material() with { DeformSpeed = float.NaN }, Material() with { DeformSpeed = float.NaN }));
        var scrolling = Material() with { ScrollU = TwinShader.XScrollFormula.Linear, ScrollSpeeds = new vec2(0.5f, 0.0f) };
        Assert.False(MaterialTable.SharesEntry(scrolling, scrolling with { ScrollSpeeds = new vec2(0.25f, 0.0f) }));
    }

    [Fact]
    public void AScrollingEntryHasTheOffsetOfTheTime()
    {
        var material = Material() with
        {
            ScrollU = TwinShader.XScrollFormula.Linear,
            ScrollV = TwinShader.YScrollFormula.LinearPlus_1,
            ScrollPhases = new vec2(0.1f, 0.2f),
            ScrollSpeeds = new vec2(0.75f, 0.5f),
        };

        var entry = MaterialTable.EntryOf(material, 3.25);

        var offset = ShaderScroll.At(material.ScrollU, material.ScrollV, material.ScrollPhases, material.ScrollSpeeds, 3.25, vec2.Zero);
        Assert.Equal(offset.x, entry[4]);
        Assert.Equal(offset.y, entry[5]);
        Assert.Equal([0.0f, 0.0f], MaterialTable.EntryOf(Material(), 3.25)[4..6]);
    }

    // A draw of every fragment keeps the failing ones only where they write what the passing ones do: a material drawing them apart
    // (RenderBatch, GsAlphaTest.FailDrawOf) leaves them out of it
    [Fact]
    public void FailingFragmentsAreKeptWhereTheyWriteWhatPassingOnesDo()
    {
        Assert.Equal(1.0f, MaterialTable.EntryOf(Material() with { AlphaFail = TwinShader.ProcessAfterAlphaTestFailed.FB_ONLY }, 0.0)[13]);
        Assert.Equal(0.0f, MaterialTable.EntryOf(Material() with { AlphaFail = TwinShader.ProcessAfterAlphaTestFailed.FB_ONLY, DepthWrite = true }, 0.0)[13]);
        Assert.Equal(0.0f, MaterialTable.EntryOf(Material() with { AlphaFail = TwinShader.ProcessAfterAlphaTestFailed.KEEP }, 0.0)[13]);
        // Without a test the fail mode is a leftover
        Assert.True(MaterialTable.SharesEntry(Material() with { AlphaTestMethod = -1, AlphaFail = TwinShader.ProcessAfterAlphaTestFailed.RGB_ONLY },
            Material() with { AlphaTestMethod = -1 }));
    }

    // Editing a material makes an entry of every value it goes through. The table only starts over before a frame (Prepare): started over
    // while a frame was drawn, the indirect draws whose entries were looked up before would read other materials' values
    [Fact]
    public void TheTableStartsOverOnlyBetweenFrames()
    {
        var table = new MaterialTable(null!);
        var first = table.IndexOf(Material());
        for (var value = 1; value <= MaterialTable.MostEntries; value++)
        {
            table.IndexOf(Material() with { LodK = value });
        }

        Assert.Equal(0, table.Generation);
        Assert.Equal(first, table.IndexOf(Material()));
        Assert.Equal(MaterialTable.MostEntries + 2, table.Count);

        table.StartOverWhenFull();

        Assert.Equal(1, table.Generation);
        Assert.Equal(1, table.IndexOf(Material() with { LodK = 5.0f }));
        table.StartOverWhenFull();
        Assert.Equal(1, table.Generation);
    }

    // Draws whose materials set the same GL state are drawn in one indirect draw (IndirectDraws)
    [Fact]
    public void MaterialsOfTheSameStateDrawAlike()
    {
        static TwinMaterial Of(TwinMaterialDesc desc) => new(null!, desc);
        var opaque = Material() with { DepthWrite = true };
        var blended = Material() with { AlphaBlend = 1.0f, BlendFunc = TwinShader.AlphaBlendPresets.Add };

        Assert.True(Of(opaque).DrawsLike(Of(opaque with { UseTexture = 0.0f, LodK = 1.0f, BlendFunc = TwinShader.AlphaBlendPresets.Sub })));
        Assert.False(Of(opaque).DrawsLike(Of(opaque with { DepthWrite = false })));
        Assert.False(Of(opaque).DrawsLike(Of(opaque with { DepthTest = TwinShader.DepthTestMethod.ALWAYS })));
        // A test byte past the four methods draws with the Lequal of GEQUAL
        Assert.True(Of(opaque).DrawsLike(Of(opaque with { DepthTest = (TwinShader.DepthTestMethod)7 })));
        Assert.True(Of(blended).DrawsLike(Of(blended)));
        Assert.False(Of(blended).DrawsLike(Of(blended with { BlendFunc = TwinShader.AlphaBlendPresets.Mix })));
        Assert.False(Of(blended).DrawsLike(Of(blended with { CustomBlend = true })));
        Assert.False(Of(blended).DrawsLike(Of(opaque)));
    }
}
