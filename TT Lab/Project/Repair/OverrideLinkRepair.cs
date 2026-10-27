using System.Linq;
using Newtonsoft.Json.Linq;
using TT_Lab.Assets;
using TT_Lab.Project.Migration;

namespace TT_Lab.Project.Repair;

/// <summary>
/// C7: 1.1.0's merge of the disc's variants worked out a variant's values before the variants it links were merged away, and a chunk's own
/// value kept the link to one the merge then deleted (a level's extra life crate's slot named the level's own health, and the level didn't
/// build). The project doesn't have what such a value links: it takes the shadow's value at that path, the shared one when the shadow's chunk
/// has none there. Without the shadow they're noted for the user to put the shared values back
/// </summary>
internal sealed class OverrideLinkRepair(RepairFiles? shadow, RepairFiles project, RepairVersion version, MigrationContext context)
{
    public void Run()
    {
        foreach (var chunk in project.OfType(nameof(LevelChunk), version.Packages).ToList())
        {
            if (project.Metadata(chunk)["Overrides"] is not JArray overrides)
            {
                continue;
            }

            foreach (var entry in overrides.OfType<JObject>().ToList())
            {
                if (entry["Values"] is not JObject values)
                {
                    continue;
                }

                var asset = RepairFiles.UriOf(entry["Asset"]);
                foreach (var property in values.Properties().ToList())
                {
                    var missing = RepairFiles.Links(property.Value).Select(RepairFiles.UriOf).FirstOrDefault(uri => !project.Has(uri));
                    if (missing == null)
                    {
                        continue;
                    }

                    if (Fix(chunk, entry, asset, property))
                    {
                        project.Changed(chunk.Path);
                        continue;
                    }

                    context.Note($"{chunk}: its own value {property.Name} of {project.Find(asset)?.ToString() ?? asset} links {missing}, which the project doesn't have " +
                                 "(TT Lab 1.1.0's unpack left it, the chunk doesn't build). Put the shared value back: right click it in the chunk's inspector", Log.LogType.Warning);
                }
            }
        }
    }

    // The shadow's value at the path, none of the chunk's own when that's the asset's
    private bool Fix(RepairFiles.Entry chunk, JObject entry, string asset, JProperty property)
    {
        if (shadow?.Find(chunk.Uri) is not { } shadowChunk || shadow.Find(asset) is not { } shadowAsset || project.Find(asset) is not { } projectAsset)
        {
            return false;
        }

        var shadowEntry = (shadow.Metadata(shadowChunk)["Overrides"] as JArray)?.OfType<JObject>().FirstOrDefault(own => RepairFiles.UriOf(own["Asset"]) == asset);
        var wanted = (shadowEntry?["Values"] as JObject)?[property.Name] ?? shadow.Document(shadowAsset).SelectToken(property.Name);
        if (wanted == null || RepairFiles.Links(wanted).Any(link => !project.Has(RepairFiles.UriOf(link))))
        {
            return false;
        }

        if (project.Document(projectAsset).SelectToken(property.Name) is { } shared && JToken.DeepEquals(shared, wanted))
        {
            ChunkOwnValues.Remove(entry, property.Name);
        }
        else
        {
            property.Value = wanted.DeepClone();
        }

        return true;
    }
}
