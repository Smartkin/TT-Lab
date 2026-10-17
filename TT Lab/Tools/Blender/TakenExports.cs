using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Project;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// The model files the add-on wrote without TT Lab (exported_by) that TT Lab took and wrote again as it read them: the Blender that wrote one
/// knows the model by what it wrote (its "exported:" revision, LINK.md), which is TT Lab's model while the file is still what TT Lab wrote of
/// it. Kept while TT Lab runs
/// </summary>
public static class TakenExports
{
    private static readonly byte[] ExportedBy = "\"exported_by\""u8.ToArray();
    private static readonly Dictionary<string, (string Written, string Taken)> Files = new(AssetFileStamps.PathComparer);

    /// <summary>
    /// Before the asset's data file is read: the SHA-256 of what the add-on wrote into it, none for another file (what Blender writes through
    /// the link is read from a file of its own) and while what's read isn't taken (a build, creating the project, a dry read)
    /// </summary>
    internal static string? Before(string path, IAsset? owner)
    {
        var assetManager = AssetManager.Get();
        if (!AssetLinks.IsModelFile(path) || AssetDataScope.IsReadingDry || assetManager.IsCreating || assetManager.IsBuilding || owner?.URI == null || owner.IsInternal
            || !AssetFileStamps.PathComparer.Equals(AssetFileStamps.Normalize(path), AssetFileStamps.Normalize(owner.FullDataPath)) || !File.Exists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // The header: the magic, the version, the JSON's length, then the JSON
        if (bytes.Length < 12)
        {
            return null;
        }

        var length = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4)), (uint)(bytes.Length - 12));
        return bytes.AsSpan(12, length).IndexOf(ExportedBy) >= 0 ? BlenderLinkAssets.Sha256(bytes) : null;
    }

    /// <summary>
    /// Once it's read: an export TT Lab wrote again is its model as long as the file stays what TT Lab wrote
    /// </summary>
    internal static void After(string path, string? written)
    {
        if (written == null)
        {
            return;
        }

        var taken = BlenderLinkAssets.FileRevision(path);
        if (taken == written)
        {
            return;
        }

        lock (Files)
        {
            Files[AssetFileStamps.Normalize(path)] = (written, taken);
        }
    }

    /// <summary>
    /// Whether the file is what TT Lab wrote of the export of the SHA-256
    /// </summary>
    public static bool Took(string path, string written, string file)
    {
        lock (Files)
        {
            return Files.TryGetValue(AssetFileStamps.Normalize(path), out var taken) && taken.Written == written && taken.Taken == file;
        }
    }
}
