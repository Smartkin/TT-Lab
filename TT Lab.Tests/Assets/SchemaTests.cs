using System.Text.Json.Nodes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Assets;

/// <summary>
/// The Blender add-on edits the data of TT Lab model files' nodes from its schema.json, which has to know what TT Lab writes
/// </summary>
public class SchemaTests
{
    private static readonly Dictionary<string, Type> Enums = new()
    {
        ["LodType"] = typeof(Enums.LodType),
        // The add-on edits materials' shaders like TT Lab does
        ["ShaderType"] = typeof(TwinShader.Type),
        ["AlphaBlendPresets"] = typeof(TwinShader.AlphaBlendPresets),
        ["AlphaTestMethod"] = typeof(TwinShader.AlphaTestMethod),
        ["ProcessAfterAlphaTestFailed"] = typeof(TwinShader.ProcessAfterAlphaTestFailed),
        ["DestinationAlphaTestMode"] = typeof(TwinShader.DestinationAlphaTestMode),
        ["DepthTestMethod"] = typeof(TwinShader.DepthTestMethod),
        ["ShadingMethod"] = typeof(TwinShader.ShadingMethod),
        ["TextureCoordinatesSpecification"] = typeof(TwinShader.TextureCoordinatesSpecification),
        ["Context"] = typeof(TwinShader.Context),
        ["ColorSpecMethod"] = typeof(TwinShader.ColorSpecMethod),
        ["AlphaSpecMethod"] = typeof(TwinShader.AlphaSpecMethod),
        ["TextureFilter"] = typeof(TwinShader.TextureFilter),
        ["ZValueDrawMask"] = typeof(TwinShader.ZValueDrawMask),
        ["XScrollFormula"] = typeof(TwinShader.XScrollFormula),
        ["YScrollFormula"] = typeof(TwinShader.YScrollFormula)
    };

    public static JsonObject Schema { get; } = LoadSchema();

    [Fact]
    public void AddOnKnowsTheGamesEnums()
    {
        var enums = Schema["enums"]!.AsObject();

        Assert.Equal(Enums.Keys.Order(), enums.Select(pair => pair.Key).Order());
        foreach (var (name, type) in Enums)
        {
            var expected = Enum.GetNames(type).Select(item => (item, Convert.ToUInt64(Enum.Parse(type, item)))).ToList();
            var actual = enums[name]!.AsArray().Select(item => (item![0]!.GetValue<string>(), item[1]!.GetValue<ulong>())).ToList();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void AddOnKnowsTheRenderBuckets()
    {
        var buckets = Schema["renderBuckets"]!.AsArray().Select(item => (item![0]!.GetValue<uint>(), item[1]!.GetValue<string>()));

        Assert.Equal(RenderBuckets.All.Select(bucket => (bucket.Id, bucket.Name)), buckets);
    }

    [Fact]
    public void AddOnKnowsTheFogColors()
    {
        var fogs = Schema["fogColors"]!.AsArray();
        var names = fogs.Select(item => item![0]!.GetValue<string>()).ToList();
        var colors = fogs.Select(item => item![1]!.AsArray().Select(value => value!.GetValue<int>()).ToArray()).ToList();

        Assert.Equal(SceneryFog.Tables.Select(table => table.Name), names);
        Assert.Equal(SceneryFog.Colors.Select(color => new[] { (int)color.R, color.G, color.B, color.A }), colors);
    }

    /// <summary>
    /// Keys the add-on knows for a type of the given kinds of elements, null when it doesn't know the type
    /// </summary>
    public static HashSet<string>? KnownKeys(string type, params string[] kinds)
    {
        var types = Schema["types"]!.AsObject();
        foreach (var kind in kinds)
        {
            if (types[kind]![type] is JsonArray keys)
            {
                return keys.Select(key => key!.GetValue<string>()).ToHashSet();
            }
        }

        return null;
    }

    private static JsonObject LoadSchema()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "TT Lab", "BlenderTools", "twin_tech_tools", "schema.json");
            if (File.Exists(path))
            {
                return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            }
        }

        throw new FileNotFoundException("The Blender add-on's schema.json wasn't found above the tests");
    }
}
