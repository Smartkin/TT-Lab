using System;
using Newtonsoft.Json;

namespace TT_Lab.AssetData.Instance;

/// <summary>
/// One of an instance's, a template's or an object's tagged values: the word the game keeps, an int, an angle or a float, or an
/// instance property's index (<see cref="Twinsanity.TwinsanityInterchange.Common.AgentLab.TaggedValue"/>). Stored as the word
/// </summary>
[JsonConverter(typeof(TaggedPropertyConverter))]
public readonly record struct TaggedProperty(UInt32 Bits)
{
    public override string ToString() => Twinsanity.TwinsanityInterchange.Common.AgentLab.TaggedValue.Describe(Bits);
}

public class TaggedPropertyConverter : JsonConverter<TaggedProperty>
{
    public override void WriteJson(JsonWriter writer, TaggedProperty value, JsonSerializer serializer)
    {
        writer.WriteValue(value.Bits);
    }

    public override TaggedProperty ReadJson(JsonReader reader, Type objectType, TaggedProperty existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        return new TaggedProperty(Convert.ToUInt32(reader.Value));
    }
}
