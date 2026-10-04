using System;
using System.Collections.Generic;
using System.Diagnostics;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Services;
using TT_Lab.Services.Implementations;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ServiceProviders;

public static class TwinIdGeneratorServiceProvider
{
    private static Dictionary<Type, ITwinIdGeneratorService> _idGeneratorServices = new();
    
    static TwinIdGeneratorServiceProvider()
    {
        RegisterGeneratorService<BehaviourCommandsSequence>();
        RegisterGeneratorService<BehaviourGraph>(new TwinIdGeneratorServiceBehaviour());
        RegisterGeneratorService<GameObject>();
        RegisterGeneratorService<OGI>();
        RegisterGeneratorService<SoundEffect>();
        RegisterGeneratorService<SoundEffectEN>(GetGenerator<SoundEffect>());
        RegisterGeneratorService<SoundEffectFR>(GetGenerator<SoundEffect>());
        RegisterGeneratorService<SoundEffectGR>(GetGenerator<SoundEffect>());
        RegisterGeneratorService<SoundEffectIT>(GetGenerator<SoundEffect>());
        RegisterGeneratorService<SoundEffectJP>(GetGenerator<SoundEffect>());
        RegisterGeneratorService<SoundEffectSP>(GetGenerator<SoundEffect>());
        RegisterGeneratorService<BlendSkin>();
        RegisterGeneratorService<LodModel>();
        RegisterGeneratorService<Material>();
        RegisterGeneratorService<Mesh>();
        RegisterGeneratorService<RigidModel>();
        RegisterGeneratorService<Skin>();
        RegisterGeneratorService<Skydome>();
        RegisterGeneratorService<Texture>();
        RegisterGeneratorService<Folder>(new TwinIdGeneratorServiceFolder());
        RegisterGeneratorService<Package>(GetGenerator<Folder>());
        RegisterGeneratorService<LevelChunk>(GetGenerator<Folder>());
    }

    public static void RegisterGeneratorService<T>() where T : IAsset
    {
        _idGeneratorServices.Add(typeof(T), new TwinIdGeneratorService<T>());
    }
    
    public static void RegisterGeneratorService<T>(ITwinIdGeneratorService gen) where T : IAsset
    {
        _idGeneratorServices.Add(typeof(T), gen);
    }

    public static ITwinIdGeneratorService GetGenerator<T>() where T : IAsset
    {
        return _idGeneratorServices[typeof(T)];
    }

    // A chunk's elements go by its path and version, a chunk without a tab (an instance duplicated in the project tree) gets them too
    public static ITwinIdGeneratorService GetGeneratorForChunk<T>(string chunk, LabURI package, Enums.Layouts layout) where T : SerializableInstance
    {
        return GetGeneratorForChunk(typeof(T), chunk, package, layout);
    }
    
    public static ITwinIdGeneratorService GetGeneratorForChunk(Type type, string chunk, LabURI package, Enums.Layouts layout)
    {
        Debug.Assert(type.IsAssignableTo(typeof(SerializableInstance)), $"{type} does not implement SerializableInstance");
        return new TwinIdGeneratorServiceInstance(type, layout, chunk, package);
    }

    public static ITwinIdGeneratorService GetGenerator(Type type)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"{type} does not implement IAsset");
        return _idGeneratorServices[type];
    }
}