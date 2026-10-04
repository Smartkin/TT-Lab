using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TT_Lab.Project;

namespace TT_Lab.Assets.Factory;

public static class AssetDeserializerFactory
{
    public static async Task<Dictionary<LabURI, IAsset>> GetAssets(string[] jsonAssets)
    {
        var read = new ConcurrentBag<(string File, IAsset Asset)>();
        var tasks = new Task[jsonAssets.Length];
        var index = 0;
        foreach (var str in jsonAssets)
        {
            tasks[index++] = Task.Factory.StartNew(() =>
            {
                try
                {
                    using System.IO.FileStream fs = new(str, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                    using System.IO.StreamReader reader = new(fs);
                    var json = reader.ReadToEnd();
                    var baseAss = JsonConvert.DeserializeObject<BaseAsset>(json)!;
                    var newAsset = (IAsset)Activator.CreateInstance(baseAss.Type)!;
                    newAsset.Deserialize(json);
                    read.Add((str, newAsset));
                }
                catch (Exception ex)
                {
                    throw new ProjectException($"{str} couldn't be read: {ex.Message}", ex);
                }
            });
        }
        await Task.WhenAll(tasks);
        foreach (var task in tasks)
        {
            task.Dispose();
        }

        // A copy of an asset's file (a file manager's "Copy of") has its URI: the first file by path is kept, whichever got read first.
        // Adding the copy threw while holding the lock every other file waited for, and opening the project never ended
        var assets = new Dictionary<LabURI, (string File, IAsset Asset)>();
        foreach (var (file, asset) in read)
        {
            if (!assets.TryGetValue(asset.URI, out var existing))
            {
                assets.Add(asset.URI, (file, asset));
                continue;
            }

            var keepsExisting = string.CompareOrdinal(existing.File, file) <= 0;
            assets[asset.URI] = keepsExisting ? existing : (file, asset);
            Log.WriteLine($"{(keepsExisting ? file : existing.File)} is another file of {asset.URI} ({(keepsExisting ? existing.File : file)}), it's left out", Log.LogType.Warning);
        }

        return assets.ToDictionary(pair => pair.Key, pair => pair.Value.Asset);
    }

    [JsonObject]
    private class BaseAsset
    {
        public Type Type { get; set; }
    }
}