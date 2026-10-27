using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project.Migration;

namespace TT_Lab.Project.Repair;

/// <summary>
/// A5: the game reuses texture IDs for other pictures in other chunks, under parts other chunks have the same of. 1.1.0's unpack only read the
/// textures of what a chunk had first, and only linked the materials of the chunk a kept variant is named after to it: the chunks built with
/// another chunk's picture. The shadow's chunks link their materials to their own pictures: the project's variant of the same picture and
/// settings where it has one (1.1.0 named its variants after other chunks), else the shadow's copied in, and a chunk gets the link while it
/// has no value of its own there and the material and the picture it shares are as they were made
/// </summary>
internal sealed class TextureRepair(RepairFiles shadow, RepairFiles project, RepairVersion version, MigrationContext context, RepairFingerprints fingerprints)
{
    private static readonly string[] Settings = [nameof(Texture.TextureFunction), nameof(Texture.PixelFormat), nameof(Texture.GenerateMipmaps), nameof(Texture.ReservesMemory)];

    private readonly List<RepairFiles.Entry> _shadowTextures = shadow.OfType(nameof(Texture), version.Packages).ToList();

    // The chunks that got their own texture links of a material, with the shadow's chunk
    private readonly HashSet<(RepairFiles.Entry Chunk, RepairFiles.Entry ShadowChunk, string Material)> _linked = [];
    // The shadow's pictures the project has none of the same of
    private readonly HashSet<string> _toCopy = new(StringComparer.Ordinal);

    public void Run()
    {
        var variants = _shadowTextures.Where(texture => texture.IsVariant).ToDictionary(texture => texture.Uri, StringComparer.Ordinal);
        if (variants.Count == 0)
        {
            return;
        }

        var projectTextures = project.OfType(nameof(Texture), version.Packages).ToList();
        var bases = new Dictionary<string, RepairFiles.Entry>(StringComparer.Ordinal);
        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variant in variants.Values)
        {
            if (BaseOf(variant) is not { } @base || project.Find(@base.Uri) == null)
            {
                continue;
            }

            bases[variant.Uri] = @base;
            var same = projectTextures.Where(texture => texture.IsVariant && texture.Id == variant.Id && texture.Package == variant.Package)
                .OrderByDescending(texture => texture.Uri == variant.Uri).ThenBy(texture => texture.Uri, StringComparer.Ordinal)
                .FirstOrDefault(texture => IsSamePicture(texture, variant));
            if (same != null)
            {
                mapped[variant.Uri] = same.Uri;
                continue;
            }

            if (project.Find(variant.Uri) != null)
            {
                context.Note($"The project's texture {variant.Name} is another picture than the disc's, its chunks keep drawing {@base.Name}", Log.LogType.Warning);
                continue;
            }

            // Copied in once a chunk gets linked to it
            mapped[variant.Uri] = variant.Uri;
            _toCopy.Add(variant.Uri);
        }

        LinkChunks(variants, bases, mapped);
        foreach (var (chunk, shadowChunk, material) in _linked)
        {
            AddRetailFingerprints(chunk, shadowChunk, material, mapped);
        }
    }

    // The shared texture of the ID the variant is a chunk's own picture of, the merge's base: its package's, else the version's other one
    private RepairFiles.Entry? BaseOf(RepairFiles.Entry variant)
    {
        return _shadowTextures.Where(texture => !texture.IsVariant && texture.Id == variant.Id)
            .OrderByDescending(texture => texture.Package == variant.Package)
            .ThenByDescending(texture => string.IsNullOrEmpty((string?)shadow.Metadata(texture)["AdditionalPath"]))
            .ThenBy(texture => texture.Uri, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private bool IsSamePicture(RepairFiles.Entry texture, RepairFiles.Entry variant)
    {
        var metadata = project.Metadata(texture);
        var wanted = shadow.Metadata(variant);
        if (Settings.Any(setting => !JToken.DeepEquals(metadata[setting], wanted[setting])))
        {
            return false;
        }

        var picture = texture.DataFile(".png");
        var wantedPicture = variant.DataFile(".png");
        return File.Exists(picture) && File.Exists(wantedPicture) && File.ReadAllBytes(picture).AsSpan().SequenceEqual(File.ReadAllBytes(wantedPicture));
    }

    private void LinkChunks(Dictionary<string, RepairFiles.Entry> variants, Dictionary<string, RepairFiles.Entry> bases, Dictionary<string, string> mapped)
    {
        var noted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shadowChunk in shadow.OfType(nameof(LevelChunk), version.Packages))
        {
            if (project.Find(shadowChunk.Uri) is not { } chunk || shadow.Metadata(shadowChunk)["Overrides"] is not JArray overrides)
            {
                continue;
            }

            foreach (var entry in overrides.OfType<JObject>())
            {
                var material = RepairFiles.UriOf(entry["Asset"]);
                if (entry["Values"] is not JObject values)
                {
                    continue;
                }

                foreach (var property in values.Properties())
                {
                    var linked = RepairFiles.Links(property.Value).Select(RepairFiles.UriOf).Where(variants.ContainsKey).Distinct().ToList();
                    if (linked.Count == 0 || linked.Any(uri => !mapped.ContainsKey(uri)))
                    {
                        continue;
                    }

                    var value = property.Value.DeepClone();
                    foreach (var link in RepairFiles.Links(value))
                    {
                        if (mapped.TryGetValue(RepairFiles.UriOf(link), out var texture))
                        {
                            link["_uri"] = texture;
                        }
                    }

                    var variant = variants[linked[0]];
                    if (project.Find(material) is not { } materialEntry ||
                        RepairFiles.Links(value).Select(RepairFiles.UriOf).Any(uri => !project.Has(uri) && !_toCopy.Contains(uri)))
                    {
                        continue;
                    }

                    // The user's picture or material stays what the chunk draws
                    var changed = linked.Select(uri => bases[uri].Uri).Prepend(material).FirstOrDefault(uri => !fingerprints.IsUnchanged(uri));
                    if (changed != null)
                    {
                        if (noted.Add($"{chunk.Uri}|{material}"))
                        {
                            context.Note($"{chunk}: {project.Find(changed)} was changed after creation, the chunk keeps drawing {materialEntry} with it instead of its own picture " +
                                         $"{variant.Name}");
                        }

                        continue;
                    }

                    var metadata = project.Metadata(chunk);
                    var projectEntry = (metadata["Overrides"] as JArray)?.OfType<JObject>().FirstOrDefault(own => RepairFiles.UriOf(own["Asset"]) == material);
                    var existing = projectEntry?["Values"] is JObject projectValues ? ChunkOwnValues.Covering(projectValues, property.Name) : null;
                    // 1.1.0 linked the chunk a variant is named after to it: without the link now, the user put the shared picture back
                    var variation = RepairVersion.VariationOf(project, chunk);
                    if (existing == null && linked.Select(uri => mapped[uri]).FirstOrDefault(uri => !_toCopy.Contains(uri) && project.Find(uri)?.Variation == variation) is { } taken)
                    {
                        context.Note($"{chunk}'s own picture {project.Find(taken)!.Name} of {materialEntry} was taken out after creation, it stays out");
                        continue;
                    }

                    if (existing == null)
                    {
                        foreach (var uri in linked.Select(uri => mapped[uri]).Where(_toCopy.Remove))
                        {
                            project.Copy(shadow, variants[uri], ".png");
                        }

                        ChunkOwnValues.Set(projectEntry ?? ChunkOwnValues.EntryFor(metadata, material), property.Name, value);
                        project.Changed(chunk.Path);
                        _linked.Add((chunk, shadowChunk, material));
                        continue;
                    }

                    // The chunk's own link to a picture of the ID is 1.1.0's variant of its picture, or the user's choice
                    if ((existing.Name == property.Name && JToken.DeepEquals(existing.Value, value)) ||
                        RepairFiles.Links(existing.Value).Any(link => project.Find(RepairFiles.UriOf(link)) is { Type: nameof(Texture) } texture && texture.Id == variant.Id && texture.IsVariant))
                    {
                        continue;
                    }

                    context.Note($"{chunk}'s own value {existing.Name} of {materialEntry} was changed after creation, it stays: the chunk's own picture is {variant.Name}");
                }
            }
        }
    }

    // The chunk's view of the material is made of what the disc's chunk had while its own values of it are the shadow's (its links to the pictures
    // the project's), and gets built with the disc's ID then (RetailIds): what it's made of is recorded like creation records it
    private void AddRetailFingerprints(RepairFiles.Entry chunk, RepairFiles.Entry shadowChunk, string material, Dictionary<string, string> mapped)
    {
        if (project.Find(material) is not { } entry || OwnValuesOf(project, chunk, material) is not { } values || OwnValuesOf(shadow, shadowChunk, material) is not { } wanted)
        {
            return;
        }

        var made = (JObject)wanted.DeepClone();
        foreach (var link in RepairFiles.Links(made))
        {
            if (mapped.TryGetValue(RepairFiles.UriOf(link), out var texture))
            {
                link["_uri"] = texture;
            }
        }

        var current = RepairFingerprints.Recorded(project, entry).ToList();
        if (!JToken.DeepEquals(values, made) || fingerprints.ViewOf(material, values) is not { } fingerprint || current.Contains(fingerprint))
        {
            return;
        }

        // A project 1.0.0 made recorded nothing, the material is as the disc's was (it was checked before the chunk got the link)
        if (current.Count == 0 && fingerprints.Current(material) is { } own)
        {
            current.Add(own);
        }

        var template = shadow.Find(material) is { } shadowMaterial ? shadow.Metadata(shadowMaterial) : project.Metadata(entry);
        RepairFiles.SetLike(project.Metadata(entry), nameof(IAsset.RetailFingerprints), new JArray(current.Append(fingerprint).Cast<object>().ToArray()), template);
        project.Changed(entry.Path);
    }

    private static JObject? OwnValuesOf(RepairFiles files, RepairFiles.Entry chunk, string asset)
    {
        return (files.Metadata(chunk)["Overrides"] as JArray)?.OfType<JObject>().FirstOrDefault(entry => RepairFiles.UriOf(entry["Asset"]) == asset)?["Values"] as JObject;
    }
}
