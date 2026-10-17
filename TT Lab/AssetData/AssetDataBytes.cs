using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Project;

namespace TT_Lab.AssetData;

/// <summary>
/// An asset's data as the bytes of the file it writes and back, what a step of the history keeps of data replaced whole (a picture painted in
/// Blender, a model Blender wrote): the data objects go with the replacing, the bytes read back as they were
/// </summary>
internal static class AssetDataBytes
{
    public static byte[] Of(AbstractAssetData data, IAsset asset)
    {
        var path = TemporaryPath(asset);
        try
        {
            data.SaveTo(path);
            return File.ReadAllBytes(path);
        }
        finally
        {
            Delete(path);
        }
    }

    public static AbstractAssetData Read(IAsset asset, Type type, byte[] bytes)
    {
        var path = TemporaryPath(asset);
        try
        {
            File.WriteAllBytes(path, bytes);
            var data = (AbstractAssetData)Activator.CreateInstance(type, asset)!;
            data.Load(path);
            return data;
        }
        finally
        {
            Delete(path);
        }
    }

    // Of the data file's extension, what reads it may go by it
    private static string TemporaryPath(IAsset asset)
    {
        var folder = Path.Combine(Path.GetTempPath(), "TT Lab", "Data");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{Guid.NewGuid():N}{Path.GetExtension(asset.Data)}");
    }

    private static void Delete(string path)
    {
        AssetFileStamps.Forget(path);
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
