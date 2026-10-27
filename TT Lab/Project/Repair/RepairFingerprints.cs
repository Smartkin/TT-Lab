using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;

namespace TT_Lab.Project.Repair;

/// <summary>
/// Whether a project's texture or material is still made of what the disc's item was (RetailIds): its fingerprint now is one recorded when it
/// was made. A project 1.0.0 made recorded none, the shadow's of the same asset are the disc's then
/// </summary>
internal sealed class RepairFingerprints(RepairFiles project, RepairFiles shadow)
{
    private readonly Dictionary<string, UInt32?> _current = new(StringComparer.Ordinal);

    public bool IsUnchanged(string uri)
    {
        if (project.Find(uri) is not { } entry)
        {
            return false;
        }

        var recorded = Recorded(project, entry).Concat(shadow.Find(uri) is { } made ? Recorded(shadow, made) : []).ToHashSet();
        return Current(entry) is { } fingerprint && recorded.Contains(fingerprint);
    }

    public static IEnumerable<UInt32> Recorded(RepairFiles files, RepairFiles.Entry entry)
    {
        return files.Metadata(entry)[nameof(IAsset.RetailFingerprints)] is JArray fingerprints ? fingerprints.Where(value => value.Type == JTokenType.Integer).Select(value => (UInt32)value) : [];
    }

    // What the project's asset is made of now
    public UInt32? Current(string uri) => project.Find(uri) is { } entry ? Current(entry) : null;

    private UInt32? Current(RepairFiles.Entry entry)
    {
        if (_current.TryGetValue(entry.Uri, out var known))
        {
            return known;
        }

        var fingerprint = Of(entry, null);
        _current[entry.Uri] = fingerprint;
        return fingerprint;
    }

    // What a chunk's view of the asset with the chunk's own values is made of, the way creation records it (RetailIds.Record)
    public UInt32? ViewOf(string uri, JObject values) => project.Find(uri) is { } entry ? Of(entry, values) : null;

    private static UInt32? Of(RepairFiles.Entry entry, JObject? values)
    {
        try
        {
            var json = File.ReadAllText(entry.Path);
            if (JObject.Parse(json)["Type"]?.ToString() is not { } typeName || Type.GetType(typeName) is not { } type || Activator.CreateInstance(type) is not SerializableAsset asset)
            {
                return null;
            }

            asset.Deserialize(json);
            AbstractAssetData? data = asset switch
            {
                Texture => new TextureData(asset),
                Material => new MaterialData(asset),
                _ => null
            };
            if (data == null)
            {
                return null;
            }

            // Read from the project's files without opening it, the data goes with the scope
            using (new AssetDataScope())
            {
                asset.AssetData = data;
                data.LoadFrom(entry.DataFile(asset is Texture ? ".png" : ".data"));
                return values == null
                    ? asset.Fingerprint()
                    : AssetOverrides.CreateView(asset, values.Properties().ToDictionary(property => property.Name, property => property.Value)).Fingerprint();
            }
        }
        catch (Exception exception)
        {
            Log.WriteLine($"What {entry.Path} is made of couldn't be worked out, it's taken for changed: {exception.Message}", Log.LogType.Debug);
            return null;
        }
    }
}
