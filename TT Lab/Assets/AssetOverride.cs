using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.Attributes;

namespace TT_Lab.Assets;

/// <summary>
/// A chunk's own values of an asset it shares with other chunks, the chunk gets built with them
/// </summary>
/// <remarks>
/// Values are kept by their path in the asset as a JSON document, the way the asset and its data serialize, with the data under
/// <see cref="AssetOverrides.DataProperty"/>. See <see cref="AssetOverrides"/>
/// </remarks>
[JsonObject(MemberSerialization.OptIn)]
[ReferencesAssets]
public sealed class AssetOverride
{
    // Overrides of a deleted asset go with it
    [JsonProperty(Required = Required.Always)]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI Asset { get; set; } = LabURI.Empty;

    [JsonProperty(Required = Required.Always)]
    public SortedDictionary<String, JToken> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Assets the values link to
    /// </summary>
    public IEnumerable<LabURI> GetLinkedAssets()
    {
        return Values.Values.SelectMany(value => value is JContainer container ? container.DescendantsAndSelf() : [value]).OfType<JObject>()
            .Where(token => token.Properties().Count() == 1 && token["_uri"] is JValue { Type: JTokenType.String })
            .Select(token => new LabURI((string)token["_uri"]!));
    }
}
