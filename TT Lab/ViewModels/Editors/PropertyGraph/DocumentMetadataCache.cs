using System;
using System.Collections.Concurrent;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public static class DocumentMetadataCache
{
    private static readonly ConcurrentDictionary<Type, DocumentMetadata> Cache = new();

    public static DocumentMetadata Get(Type type, bool searchAllProperties = false)
        => Cache.GetOrAdd(type, static (t, arg) => new DocumentMetadata(t, arg), searchAllProperties);
}