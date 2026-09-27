using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TT_Lab.Attributes;

namespace TT_Lab.Assets;

/// <summary>
/// Walks the objects marked with <see cref="ReferencesAssetsAttribute"/> and fixes up their references to deleted assets
/// </summary>
/// <param name="deletedAssets">Assets that are getting deleted</param>
/// <param name="placeholders">Placeholder to use for each deleted asset, when not given nothing gets changed and the fixer only collects what would be changed</param>
public sealed class DeletedReferenceFixer(IReadOnlySet<LabURI> deletedAssets, IReadOnlyDictionary<LabURI, LabURI>? placeholders = null)
{
    private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Deleted assets whose references need to be replaced with a placeholder
    /// </summary>
    public HashSet<LabURI> RequiredPlaceholders { get; } = [];

    /// <summary>
    /// Whether any reference to a deleted asset was found
    /// </summary>
    public bool FoundReferences { get; private set; }

    /// <summary>
    /// Whether the fixer only collects what would be changed
    /// </summary>
    public bool IsDryRun => placeholders == null;

    public bool IsDeleted(LabURI uri) => deletedAssets.Contains(uri);

    public void FixObject(object? data)
    {
        if (data?.GetType().GetCustomAttribute<ReferencesAssetsAttribute>() is null || !_visited.Add(data))
        {
            return;
        }

        foreach (var property in data.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            FixProperty(data, property);
        }
    }

    public void FixProperties(object owner, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            FixProperty(owner, owner.GetType().GetProperty(propertyName)!);
        }
    }

    private void FixProperty(object owner, PropertyInfo property)
    {
        if (!property.CanRead || property.GetIndexParameters().Length > 0)
        {
            return;
        }

        var action = property.GetCustomAttribute<OnReferenceDeletedAttribute>()?.Action ?? DeletedReferenceAction.ReplaceWithPlaceholder;
        switch (property.GetValue(owner))
        {
            case LabURI uri:
                if (!deletedAssets.Contains(uri))
                {
                    return;
                }

                var replacement = GetReplacement(uri, action == DeletedReferenceAction.Remove ? DeletedReferenceAction.Clear : action);
                if (!IsDryRun && property.CanWrite)
                {
                    property.SetValue(owner, replacement);
                }

                break;
            case IList<LabURI> uris:
                FixUris(uris, action);
                break;
            case IList items:
                FixItems(items, action);
                break;
            case var value:
                FixObject(value);
                break;
        }
    }

    private void FixUris(IList<LabURI> uris, DeletedReferenceAction action)
    {
        var canRemove = action == DeletedReferenceAction.Remove && !uris.IsReadOnly;
        for (var i = uris.Count - 1; i >= 0; i--)
        {
            if (!deletedAssets.Contains(uris[i]))
            {
                continue;
            }

            var replacement = GetReplacement(uris[i], canRemove ? DeletedReferenceAction.Clear : action);
            if (IsDryRun)
            {
                continue;
            }

            if (canRemove)
            {
                uris.RemoveAt(i);
            }
            else
            {
                uris[i] = replacement!;
            }
        }
    }

    private void FixItems(IList items, DeletedReferenceAction action)
    {
        var canRemove = action == DeletedReferenceAction.Remove && !items.IsFixedSize && !items.IsReadOnly;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (canRemove && ReferencesDeletedAssets(item))
            {
                FoundReferences = true;
                if (!IsDryRun)
                {
                    items.RemoveAt(i);
                }

                continue;
            }

            FixObject(item);
        }
    }

    private bool ReferencesDeletedAssets(object? item)
    {
        var finder = new DeletedReferenceFixer(deletedAssets);
        finder.FixObject(item);
        return finder.FoundReferences;
    }

    private LabURI? GetReplacement(LabURI deletedAsset, DeletedReferenceAction action)
    {
        FoundReferences = true;
        if (action != DeletedReferenceAction.ReplaceWithPlaceholder)
        {
            return LabURI.Empty;
        }

        if (IsDryRun)
        {
            RequiredPlaceholders.Add(deletedAsset);
            return null;
        }

        return placeholders![deletedAsset];
    }
}
