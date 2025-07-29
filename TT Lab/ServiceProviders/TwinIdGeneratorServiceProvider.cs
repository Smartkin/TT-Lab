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
    private static Dictionary<string, Dictionary<(Enums.Layouts, Type), ITwinIdGeneratorService>> _chunkIdGeneratorServices = new();
    
    static TwinIdGeneratorServiceProvider()
    {
        RegisterGeneratorService<Animation>();
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
    }

    public static void RegisterGeneratorService<T>() where T : IAsset
    {
        _idGeneratorServices.Add(typeof(T), new TwinIdGeneratorService<T>());
    }
    
    public static void RegisterGeneratorService<T>(ITwinIdGeneratorService gen) where T : IAsset
    {
        _idGeneratorServices.Add(typeof(T), gen);
    }

    public static void RegisterGeneratorServiceForChunk(ChunkFolder chunk)
    {
        var chunkGenerators = new Dictionary<(Enums.Layouts, Type), ITwinIdGeneratorService>();
        for (var i = 0; i < (int)Enums.Layouts.LAYER_8 + 1; ++i)
        {
            RegisterChunkGenerator<AiPath>((Enums.Layouts)i, chunk, chunkGenerators);
            RegisterChunkGenerator<AiPosition>((Enums.Layouts)i, chunk, chunkGenerators);
            RegisterChunkGenerator<Camera>((Enums.Layouts)i, chunk, chunkGenerators);
            RegisterChunkGenerator<InstanceTemplate>((Enums.Layouts)i, chunk, chunkGenerators);
            RegisterChunkGenerator<ObjectInstance>((Enums.Layouts)i, chunk, chunkGenerators);
            RegisterChunkGenerator<Path>((Enums.Layouts)i, chunk, chunkGenerators);
            RegisterChunkGenerator<Position>((Enums.Layouts)i, chunk, chunkGenerators);
            RegisterChunkGenerator<Trigger>((Enums.Layouts)i, chunk, chunkGenerators);
        }
        _chunkIdGeneratorServices.Add(chunk.Variation, chunkGenerators);
    }

    public static void DeregisterGeneratorServiceForChunk(string chunk)
    {
        _chunkIdGeneratorServices.Remove(chunk);
    }

    private static void RegisterChunkGenerator<T>(Enums.Layouts layout, ChunkFolder folder, Dictionary<(Enums.Layouts, Type), ITwinIdGeneratorService> chunkGenerators) where T : SerializableInstance
    {
        chunkGenerators.Add((layout, typeof(T)), new TwinIdGeneratorServiceInstance<T>(layout, folder));
    }

    public static ITwinIdGeneratorService GetGenerator<T>() where T : IAsset
    {
        return _idGeneratorServices[typeof(T)];
    }

    public static ITwinIdGeneratorService GetGeneratorForChunk<T>(string chunk, Enums.Layouts layout) where T : SerializableInstance
    {
        return _chunkIdGeneratorServices[chunk][(layout, typeof(T))];
    }
    
    public static ITwinIdGeneratorService GetGeneratorForChunk(Type type, string chunk, Enums.Layouts layout)
    {
        Debug.Assert(type.IsAssignableTo(typeof(SerializableInstance)), $"{type} does not implement SerializableInstance");
        return _chunkIdGeneratorServices[chunk][(layout, type)];
    }

    public static ITwinIdGeneratorService GetGenerator(Type type)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"{type} does not implement IAsset");
        return _idGeneratorServices[type];
    }
}