using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.TlModel;
using Twinsanity.PS2Hardware;

namespace TT_Lab.Tests.Assets;

public class TlmJsonTests
{
    // Values made in code only convert to the type they were made from, parsed ones to any number
    [Fact]
    public void NumbersReadTheSameMadeInCodeAndParsed()
    {
        var made = new JsonObject
        {
            ["Int"] = 5,
            ["UInt"] = TlmJson.ToJson(0xFFFFFFF0U),
            ["Float"] = 1.5f,
            ["Double"] = 2.25,
            ["Long"] = 7L,
            ["Byte"] = (byte)3,
            ["Bool"] = true,
            ["Floats"] = TlmJson.ToJson(new[] { 1.5f, -0.0f }),
            ["Ints"] = TlmJson.ToJson(new[] { 1, -2 })
        };
        var parsed = JsonNode.Parse(made.ToJsonString())!.AsObject();

        Assert.All(new[] { made, parsed }, json =>
        {
            Assert.Equal(5, json.GetInt("Int"));
            Assert.Equal(0xFFFFFFF0U, json.GetUInt("UInt"));
            Assert.Equal(1.5f, json.GetFloat("Float"));
            Assert.Equal(2.25f, json.GetFloat("Double"));
            Assert.Equal(7, json.GetInt("Long"));
            Assert.Equal(3, json.GetInt("Byte"));
            Assert.True(json.GetBool("Bool"));
            Assert.Equal([BitConverter.SingleToUInt32Bits(1.5f), BitConverter.SingleToUInt32Bits(-0.0f)], json.GetFloats("Floats").Select(BitConverter.SingleToUInt32Bits));
            Assert.Equal([1, -2], json.GetInts("Ints"));
        });
    }

    // Blender keeps booleans as integers and lists of objects as objects keyed by the index
    [Fact]
    public void ValuesAsBlenderKeepsThemAreRead()
    {
        var json = JsonNode.Parse("""
            {"Bool": 1, "Enum": "NopPerByte", "EnumNumber": 1, "Hex": "0x1F", "Text": "12",
             "Items": {"10": {"V": 10}, "2": {"V": 2}, "0": {"V": 0}}, "List": [{"V": 1}]}
            """)!.AsObject();

        Assert.True(json.GetBool("Bool"));
        Assert.Equal(TwinVifPadding.NopPerByte, json.GetEnum("Enum", TwinVifPadding.QuadWord));
        Assert.Equal(TwinVifPadding.NopPerByte, json.GetEnum("EnumNumber", TwinVifPadding.QuadWord));
        Assert.Equal(0x1F, json.GetInt("Hex"));
        Assert.Equal(12, json.GetInt("Text"));
        Assert.Equal([0, 2, 10], json.GetIndexed("Items").Select(item => item.GetInt("V")));
        Assert.Equal([1], json.GetIndexed("List").Select(item => item.GetInt("V")));
    }

    [Fact]
    public void MissingValuesFallBack()
    {
        var json = new JsonObject { ["Text"] = "words", ["Object"] = new JsonObject() };

        Assert.Equal(4, json.GetInt("Missing", 4));
        Assert.Equal(4, json.GetInt("Text", 4));
        Assert.Equal(0.5f, json.GetFloat("Object", 0.5f));
        Assert.Empty(json.GetFloats("Missing"));
        Assert.Empty(json.GetIndexed("Text"));
        Assert.Null(json.GetString("Missing"));
    }
}
