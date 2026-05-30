using TT_Lab.AssetData.Graphics;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Util;

public class BoundingBox
{
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 V1 { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 V2 { get; set; }
}