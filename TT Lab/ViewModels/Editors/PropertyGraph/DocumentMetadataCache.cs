using System;
using System.Collections.Concurrent;
using TT_Lab.AssetData.Instance.Scenery;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Common.Lights;

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
        Register<TwinCollisionHull>(true);
        Register<TwinChunkLinkHull>(true);
        Register<BossCamera>(true);
        Register<CameraLine>(true);
        Register<CameraLine2>(true);
        Register<CameraPath>(true);
        Register<CameraPoint>(true);
        Register<CameraPoint2>(true);
        Register<CameraSpline>(true);
        Register<CameraZone>(true);
        // A light put into a scenery's list starts as the ones new chunks get, a black light of no intensity does nothing
        DocumentMetadata.RegisterFactory(DefaultLights.Ambient);
        DocumentMetadata.RegisterFactory(DefaultLights.Directional);
        DocumentMetadata.RegisterFactory(DefaultLights.Point);
        DocumentMetadata.RegisterFactory(DefaultLights.Spot);
        Register<AmbientLight>(true);
        Register<DirectionalLight>(true);
        Register<PointLight>(true);
        Register<SpotLight>(true);
    }
}