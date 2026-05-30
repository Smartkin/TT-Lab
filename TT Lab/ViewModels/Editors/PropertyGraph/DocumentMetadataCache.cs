using System;
using System.Collections.Concurrent;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public static class DocumentMetadataCache
{
    private static readonly ConcurrentDictionary<Type, DocumentMetadata> Cache = new();

    static DocumentMetadataCache()
    {
        RegisterForeignTypes();
    }

    public static void Register<T>(bool searchAllProperties = false) => Register(typeof(T), searchAllProperties);
    
    public static void Register(Type type, bool searchAllProperties) => Cache.AddOrUpdate(type, static (t, arg) => new DocumentMetadata(t, arg), static (type, metadata, arg3) => metadata,
        searchAllProperties);

    public static DocumentMetadata Get<T>(bool searchAllProperties = false) => Get(typeof(T), searchAllProperties);

    public static DocumentMetadata Get(Type type, bool searchAllProperties = false)
        => Cache.GetOrAdd(type, static (t, arg) => new DocumentMetadata(t, arg), searchAllProperties);

    private static void RegisterForeignTypes()
    {
        Register<Vector2>(true);
        Register<Vector3>(true);
        Register<Vector4>(true);
        Register<VectorCharacterData>(true);
        Register<Matrix4>(true);
        Register<TwinBoundingBoxBuilder>(true);
        Register<TwinChunkLinkBoundingBoxBuilder>(true);
        Register<BossCamera>(true);
        Register<CameraLine>(true);
        Register<CameraLine2>(true);
        Register<CameraPath>(true);
        Register<CameraPoint>(true);
        Register<CameraPoint2>(true);
        Register<CameraSpline>(true);
        Register<CameraZone>(true);
    }
}