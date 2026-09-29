using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Splat;
using TT_Lab.ViewModels;

namespace TT_Lab.Assets;

public static class AssetDeletion
{
    /// <summary>
    /// Deletes the asset along with everything it contains, and fixes up the references other assets had to them
    /// </summary>
    /// <returns>Whether the asset got deleted</returns>
    /// <summary>
    /// Takes an asset that never got saved out of the project, like an instance made for a chunk that the chunk doesn't list anymore
    /// (its placing undone, the chunk closed without saving): nothing refers to it and it has no files. It stayed in the project tree,
    /// and once its data got released it couldn't be opened
    /// </summary>
    public static void ForgetUnsaved(SerializableAsset asset)
    {
        var assetManager = AssetManager.Get();
        foreach (var folder in assetManager.GetAllAssetsOf<Folder>())
        {
            folder.Children.Remove(asset.URI);
        }

        asset.RemoveFromTree();
        assetManager.RemoveAsset(asset);
    }

    public static async Task<bool> DeleteAsync(IAsset asset)
    {
        var assetManager = AssetManager.Get();
        var assetsToDelete = CollectAssetsToDelete(asset);
        if (assetsToDelete.OfType<LevelChunk>().Any(chunk => chunk.IsGlobalDefaultChunk))
        {
            Log.WriteLine($"Can't delete {asset.Alias}, the game can't boot without the default chunk.", Log.LogType.Error);
            return false;
        }

        var deletedAssets = assetsToDelete.Select(a => a.URI).ToHashSet();
        var dependents = assetManager.GetAssets()
            .Where(a => !a.IsInternal && !deletedAssets.Contains(a.URI) && a.IsReferencingAny(deletedAssets))
            .ToList();

        var requiredPlaceholders = new HashSet<LabURI>();
        var canDelete = true;
        foreach (var dependent in dependents)
        {
            var finder = new DeletedReferenceFixer(deletedAssets);
            dependent.FixDeletedReferences(finder);
            requiredPlaceholders.UnionWith(finder.RequiredPlaceholders);
            foreach (var missingPlaceholder in finder.RequiredPlaceholders.Select(assetManager.GetAsset)
                         .Where(deleted => !PlaceholderAssets.CanReplace(deleted, deletedAssets)))
            {
                Log.WriteLine($"Can't delete {asset.Alias}, {dependent.Alias} requires {missingPlaceholder.Alias} and there is no placeholder {missingPlaceholder.Type.Name} that could replace it.", Log.LogType.Error);
                canDelete = false;
            }
        }

        if (!canDelete)
        {
            return false;
        }

        // There are no editors to close when running without the UI
        var editors = Locator.Current.GetService<EditorsViewModel>();
        var affectedAssets = deletedAssets.Concat(dependents.Select(d => d.URI)).ToHashSet();
        if (editors != null && !await editors.CloseEditorsReferencing(affectedAssets))
        {
            Log.WriteLine($"Deleting {asset.Alias} was cancelled.");
            return false;
        }

        var folders = assetManager.GetAllAssetsOf<Folder>();
        foreach (var deletedAsset in assetsToDelete)
        {
            foreach (var folder in folders)
            {
                folder.Children.Remove(deletedAsset.URI);
            }

            deletedAsset.Delete();
        }

        var deletedAssetsSet = assetsToDelete.ToHashSet();
        foreach (var internalAsset in assetManager.GetAssets().Where(a => a.IsInternal && a.InternalOwner != null && deletedAssetsSet.Contains(a.InternalOwner)).ToList())
        {
            assetManager.RemoveAsset(internalAsset);
        }

        // Placeholders are made only after the deletion since they could otherwise end up in a deleted folder
        var deletedByUri = assetsToDelete.ToDictionary(a => a.URI);
        var placeholders = requiredPlaceholders.ToDictionary(uri => uri, uri => PlaceholderAssets.GetOrCreate(deletedByUri[uri]));
        foreach (var dependent in dependents)
        {
            dependent.FixDeletedReferences(new DeletedReferenceFixer(deletedAssets, placeholders));
            dependent.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData | SerializationFlags.FixReferences);
        }

        editors?.ForgetDeletedAssets(deletedAssets);
        Global.LevelSelect.RemoveChunks(assetsToDelete.OfType<LevelChunk>());
        Log.WriteLine($"Deleted {assetsToDelete.Count} assets and fixed references in {dependents.Count} assets");
        if (placeholders.Count > 0)
        {
            Log.WriteLine($"Replaced references to {placeholders.Count} deleted assets with placeholders");
        }

        return true;
    }

    // Contained assets come before their containers so folders get deleted last
    private static List<IAsset> CollectAssetsToDelete(IAsset asset)
    {
        var assetManager = AssetManager.Get();
        var result = new List<IAsset>();
        var collected = new HashSet<LabURI>();
        Collect(asset);
        return result;

        void Collect(IAsset current)
        {
            if (!collected.Add(current.URI))
            {
                return;
            }

            var containedAssets = current switch
            {
                Folder folder => folder.Children,
                LevelChunk chunk => chunk.ChunkResources,
                _ => []
            };
            foreach (var contained in containedAssets.Where(assetManager.DoesAssetExist).ToList())
            {
                Collect(assetManager.GetAsset(contained));
            }

            result.Add(current);
        }
    }
}
