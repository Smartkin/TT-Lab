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

    // TwinTech's types the inspector shows every property of
    internal static readonly Type[] ForeignTypes =
    [
        typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(VectorCharacterData), typeof(Matrix4), typeof(TwinCollisionHull), typeof(TwinChunkLinkHull),
        typeof(BossCamera), typeof(CameraLine), typeof(CameraLine2), typeof(CameraPath), typeof(CameraPoint), typeof(CameraPoint2), typeof(CameraSpline),
        typeof(CameraZone), typeof(AmbientLight), typeof(DirectionalLight), typeof(PointLight), typeof(SpotLight),
    ];

    private static void RegisterForeignTypes()
    {
        // A light put into a scenery's list starts as the ones new chunks get, a black light of no intensity does nothing
        DocumentMetadata.RegisterFactory(DefaultLights.Ambient);
        DocumentMetadata.RegisterFactory(DefaultLights.Directional);
        DocumentMetadata.RegisterFactory(DefaultLights.Point);
        DocumentMetadata.RegisterFactory(DefaultLights.Spot);
        foreach (var type in ForeignTypes)
        {
            Register(type, true);
        }
    }
}