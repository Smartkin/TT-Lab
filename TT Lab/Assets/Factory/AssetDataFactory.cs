using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Util;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using ChunkLinks = TT_Lab.Assets.Instance.ChunkLinks;
using Collision = TT_Lab.Assets.Instance.Collision;
using ObjectInstance = TT_Lab.Assets.Instance.ObjectInstance;
using Particles = TT_Lab.Assets.Instance.Particles;
using Path = System.IO.Path;
using Scenery = TT_Lab.Assets.Instance.Scenery;

namespace TT_Lab.Assets.Factory;

public enum AssetCreationStatus
{
    Success,
    Failed,
}

public static class AssetDataFactory
{
    private const UInt32 CrashObjectId = 0x0;
    private const Single DefaultSkydomeRadius = 120.0f;
    private const Int32 DefaultSkydomeSegments = 16;
    private const Int32 DefaultSkydomeRings = 8;
    private const Single DefaultChunkFloorHalfSize = 5.0f;

    // Parameters that every Crash instance in the retail levels is placed with
    private static readonly Enums.InstanceState CrashInstanceState = (Enums.InstanceState)0x7D2E;
    private static readonly UInt32[] CrashInstanceFlags = [65536, 131072, 131072, 364088, 109226, 16384, 262144, 262144, 0];
    private static readonly Single[] CrashInstanceFloats =
    [
        1.0f, 50.0f, 5.2f, 15.0f, 50.0f, 0.0f, 2.5f, 9.0f, 0.0f, 10.0f, 0.4f, 0.15f, 0.15f, 0.5f, 1.0f, 8.0f,
        13.0f, 37.556f, 57.874f, 8.0f, 16.0f, 64.0f, 72.951f, 11.0f, 5.0f, 10.0f, 14.938f, 0.05f, 0.4f, 0.05f, 0.05f, 0.4f,
        10.0f, 400.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.75f, 0.1f, 0.1f, 0.1f, 18.0f, 0.15f, 0.2f, 0.1f,
        0.3f, 0.3f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f
    ];
    private static readonly UInt32[] CrashInstanceIntegers = [0, 255, 2];

    public static AssetCreationStatus CreateFolderData(IAsset parent, IAsset asset)
    {
        var projectPath = Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath;
        Directory.SetCurrentDirectory(projectPath);
        var parentFolder = (Folder)parent;
        var folder = (Folder)asset;
        folder.Parent = parentFolder.URI;
        
        Directory.SetCurrentDirectory($".{Path.DirectorySeparatorChar}{parentFolder.GetPath()}");
        Directory.CreateDirectory(asset.Alias);
        Directory.SetCurrentDirectory(projectPath);
        return AssetCreationStatus.Success;
    }
    
    public static async Task<AssetCreationStatus> CreateSoundEffectData(IAsset asset)
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Choose a wave file...", "Sound files", ["*.wav"]);
        if (string.IsNullOrEmpty(file))
        {
            Log.WriteLine("No sound file provided.");
            return AssetCreationStatus.Failed;
        }
        
        await using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(fs);
        Byte[] pcm = Array.Empty<byte>();
        short channels = 0;
        uint frequency = 0;
        Riff.LoadRiff(reader, ref pcm, ref channels, ref frequency);
        if (channels > 2)
        {
            Log.WriteLine("Buddy what kind of audio are you trying to use here? Either mono or stereo. Sound wasn't created.", Log.LogType.Error);
            return AssetCreationStatus.Failed;
        }
        
        if (channels == 2 && frequency > 22050)
        {
            Log.WriteLine("Stereo sounds can not be over 22050 Hz. Sound wasn't created.", Log.LogType.Error);
            return AssetCreationStatus.Failed;
        }

        if (frequency > 48000)
        {
            Log.WriteLine("Sounds over 48000 Hz are not supported. Sound wasn't created.", Log.LogType.Error);
            return AssetCreationStatus.Failed;
        }
        
        fs.Flush();
        fs.Close();
        reader.Close();

        var newSoundData = new SoundEffectData(asset, file);
        asset.SetData(newSoundData);
        
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateSkydomeData(IAsset asset)
    {
        var skydomeData = new SkydomeData(asset);
        skydomeData.Meshes.Add(CreateDefaultSkydomeMesh(asset).URI);
        asset.SetData(skydomeData);
        return AssetCreationStatus.Success;
    }

    private static Mesh CreateDefaultSkydomeMesh(IAsset skydome)
    {
        var assetManager = AssetManager.Get();
        var model = new Model
        {
            Package = skydome.Package,
            InvariantName = $"{skydome.Name}_DEFAULT_MODEL",
            Alias = $"{skydome.Name}_DEFAULT_MODEL",
            IsInternal = true,
            InternalOwner = skydome
        };
        var modelData = new ModelData(model);
        var (vertexes, faces) = GenerateSkydomeSphere();
        modelData.Vertexes.Add(vertexes);
        modelData.Faces.Add(faces);
        model.SetData(modelData);
        assetManager.TryAddAsset(model);

        var material = new Material
        {
            Package = skydome.Package,
            InvariantName = $"{skydome.Name}_DEFAULT_MATERIAL",
            Alias = $"{skydome.Name}_DEFAULT_MATERIAL",
            IsInternal = true,
            InternalOwner = skydome
        };
        // Material's name ends up in its internal asset's URI once the skydome gets loaded back so keep it unique
        var materialData = new MaterialData(material)
        {
            Name = $"{skydome.InvariantName}_Sky",
            ActivatedShaders = Enums.AppliedShaders.UnlitSkydome
        };
        materialData.Shaders[0].ShaderType = TwinShader.Type.UnlitSkydome;
        materialData.Shaders[0].DepthTest = TwinShader.DepthTestMethod.ALWAYS;
        material.SetData(materialData);
        assetManager.TryAddAsset(material);

        var mesh = new Mesh
        {
            Package = skydome.Package,
            InvariantName = $"{skydome.Name}_DEFAULT_MESH",
            Alias = $"{skydome.Name}_DEFAULT_MESH",
            IsInternal = true,
            InternalOwner = skydome
        };
        var meshData = new MeshData(mesh)
        {
            Model = model.URI,
            Materials = [material.URI]
        };
        mesh.SetData(meshData);
        assetManager.TryAddAsset(mesh);

        return mesh;
    }

    private static (List<Vertex>, List<IndexedFace>) GenerateSkydomeSphere()
    {
        var zenithColor = new Vector4(0.25f, 0.45f, 0.85f, 1.0f);
        var horizonColor = new Vector4(0.7f, 0.8f, 0.95f, 1.0f);
        var vertexes = new List<Vertex>();
        for (var ring = 0; ring <= DefaultSkydomeRings; ring++)
        {
            var theta = MathF.PI * ring / DefaultSkydomeRings;
            var height = MathF.Cos(theta);
            var ringRadius = MathF.Sin(theta) * DefaultSkydomeRadius;
            var skyFactor = MathF.Max(height, 0.0f);
            var color = new Vector4(
                horizonColor.X + (zenithColor.X - horizonColor.X) * skyFactor,
                horizonColor.Y + (zenithColor.Y - horizonColor.Y) * skyFactor,
                horizonColor.Z + (zenithColor.Z - horizonColor.Z) * skyFactor,
                1.0f);
            for (var segment = 0; segment <= DefaultSkydomeSegments; segment++)
            {
                var phi = 2.0f * MathF.PI * segment / DefaultSkydomeSegments;
                var position = new Vector4(MathF.Cos(phi) * ringRadius, height * DefaultSkydomeRadius, MathF.Sin(phi) * ringRadius, 1.0f);
                var uv = new Vector4((Single)segment / DefaultSkydomeSegments, (Single)ring / DefaultSkydomeRings, 1.0f, 0.0f);
                vertexes.Add(new Vertex(position, color, uv));
            }
        }

        // Faces are wound to point towards the centre since the dome is only ever seen from inside
        var faces = new List<IndexedFace>();
        const Int32 ringSize = DefaultSkydomeSegments + 1;
        for (var ring = 0; ring < DefaultSkydomeRings; ring++)
        {
            for (var segment = 0; segment < DefaultSkydomeSegments; segment++)
            {
                var topLeft = ring * ringSize + segment;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + ringSize;
                var bottomRight = bottomLeft + 1;
                if (ring != 0)
                {
                    faces.Add(new IndexedFace(topLeft, bottomLeft, topRight));
                }

                if (ring != DefaultSkydomeRings - 1)
                {
                    faces.Add(new IndexedFace(topRight, bottomLeft, bottomRight));
                }
            }
        }

        return (vertexes, faces);
    }

    public static AssetCreationStatus CreateGameObjectData(IAsset asset)
    {
        var gameObjectData = new GameObjectData(asset);
        gameObjectData.Name = asset.Name.Trim();
        asset.SetData(gameObjectData);
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateChunkData(IAsset parent, IAsset asset)
    {
        var chunkPath = GetNewChunkPath((Folder)parent, asset.InvariantName);
        if (chunkPath == null)
        {
            Log.WriteLine("Chunks can only be created inside of a package's levels folder. Chunk wasn't created.", Log.LogType.Error);
            return AssetCreationStatus.Failed;
        }

        var crashObject = FindCrashObject(asset.Package);
        if (crashObject == LabURI.Empty)
        {
            Log.WriteLine($"No Crash game object (ID {CrashObjectId:X}) was found. Chunk wasn't created.", Log.LogType.Error);
            return AssetCreationStatus.Failed;
        }

        var collisionSurfaces = AssetManager.Get().GetRelatedAssetsOf<CollisionSurface>(asset.Package);
        if (collisionSurfaces.Count == 0)
        {
            Log.WriteLine("No collision surfaces were found. Chunk wasn't created.", Log.LogType.Error);
            return AssetCreationStatus.Failed;
        }

        var floorSurface = collisionSurfaces.FirstOrDefault(s => s.ID == 0x0, collisionSurfaces[0]).URI;
        var chunk = (LevelChunk)asset;
        // Chunk's name is used for the level's archive files so it can't be varied
        chunk.Variation = string.Empty;
        chunk.AdditionalPath = chunkPath;
        chunk.RegenerateLinks();

        AddChunkResource(chunk, new Scenery { InvariantName = "Scenery 0", ID = Constants.SCENERY_SECENERY_ITEM },
            scenery => CreateDefaultSceneryData(scenery, floorSurface));
        AddChunkResource(chunk, new ChunkLinks { InvariantName = "Chunk Links", ID = Constants.SCENERY_LINK_ITEM },
            chunkLinks => new ChunkLinksData(chunkLinks));
        AddChunkResource(chunk, new Particles { InvariantName = "Particles", ID = Constants.LEVEL_PARTICLES_ITEM },
            particles => new ParticleData(particles));
        AddChunkResource(chunk, new ObjectInstance { InvariantName = "Instance 0", ID = 0x0, LayoutID = (Int32)Enums.Layouts.LAYER_1 },
            instance => new ObjectInstanceData(instance)
            {
                ObjectId = crashObject,
                RefListIndex = -1,
                StateFlags = CrashInstanceState,
                ParamList1 = [..CrashInstanceFlags],
                ParamList2 = [..CrashInstanceFloats],
                ParamList3 = [..CrashInstanceIntegers]
            });

        return AssetCreationStatus.Success;
    }

    // Builds only pick up chunks from the levels folder and the path is relative to the package
    private static string? GetNewChunkPath(Folder parentFolder, string chunkName)
    {
        var assetManager = AssetManager.Get();
        var pathTokens = new List<string> { chunkName };
        var folder = parentFolder;
        while (!folder.Mark.HasFlag(FolderMark.IsPackage))
        {
            pathTokens.Insert(0, folder.Alias);
            if (folder.Parent == LabURI.Empty)
            {
                return null;
            }

            folder = assetManager.GetAsset<Folder>(folder.Parent);
        }

        if (pathTokens.Count < 2 || pathTokens[0] != "levels")
        {
            return null;
        }

        return string.Join(Path.DirectorySeparatorChar, pathTokens);
    }

    private static LabURI FindCrashObject(LabURI package)
    {
        var crashObjects = AssetManager.Get().GetRelatedAssetsOf<GameObject>(package).Where(o => o.ID == CrashObjectId).ToList();
        var crashObject = crashObjects.FirstOrDefault(o => o.Package == package) ?? crashObjects.FirstOrDefault();
        return crashObject?.URI ?? LabURI.Empty;
    }

    private static void AddChunkResource<T>(LevelChunk chunk, T resource, Func<T, AbstractAssetData> dataCreator) where T : SerializableInstance
    {
        resource.Package = chunk.Package;
        resource.Chunk = chunk.AdditionalPath!;
        resource.Alias = resource.InvariantName;
        resource.Variation = string.Empty;
        resource.SetData(dataCreator(resource));

        AssetManager.Get().AddAsset(resource);
        resource.Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);
        chunk.ChunkResources.Add(resource.URI);
    }

    private static SceneryData CreateDefaultSceneryData(Scenery scenery, LabURI floorSurface)
    {
        var collision = new Collision
        {
            Package = scenery.Package,
            Chunk = scenery.Chunk,
            InvariantName = $"{scenery.Chunk}_COLLISION",
            Alias = "Collision",
            Variation = string.Empty,
            IsInternal = true,
            InternalOwner = scenery
        };
        const Single halfSize = DefaultChunkFloorHalfSize;
        var collisionData = new CollisionData(collision);
        collisionData.Vectors.AddRange([
            new Vector4(-halfSize, 0.0f, -halfSize, 1.0f),
            new Vector4(halfSize, 0.0f, -halfSize, 1.0f),
            new Vector4(halfSize, 0.0f, halfSize, 1.0f),
            new Vector4(-halfSize, 0.0f, halfSize, 1.0f)
        ]);
        // Game's walkable floors are wound so their right-handed normal points downwards
        collisionData.Triangles.Add(new CollisionTriangle { Face = new IndexedFace(0, 1, 2), Surface = floorSurface });
        collisionData.Triangles.Add(new CollisionTriangle { Face = new IndexedFace(0, 2, 3), Surface = floorSurface });
        collision.SetData(collisionData);
        AssetManager.Get().AddAsset(collision);

        var sceneryRoot = new SceneryRootData
        {
            UnkUInt = 1,
            SceneryTypes = Enumerable.Repeat(ITwinScenery.SceneryType.None, 8).ToArray(),
            BoundingBoxes = [],
            MeshModelMatrices = [],
            LodModelMatrices = [],
            UnkVec1 = new Vector4(0.0f, 0.0f, 0.0f, halfSize * MathF.Sqrt(2.0f)),
            UnkVec2 = new Vector4(-halfSize, 0.0f, -halfSize, 1.0f),
            UnkVec3 = new Vector4(halfSize, 0.0f, halfSize, 1.0f),
            UnkVec4 = new Vector4(halfSize, 0.0f, halfSize, 1.0f),
            LightsEnabler = new Boolean[128]
        };

        return new SceneryData(scenery)
        {
            Collision = collision.URI,
            Sceneries = [sceneryRoot]
        };
    }

    public static AssetCreationStatus CreateBehaviourData(IAsset asset)
    {
        asset.SetData(new BehaviourGraphData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateBehaviourSequenceData(IAsset asset)
    {
        asset.SetData(new BehaviourCommandsSequenceData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateOgiData(IAsset asset)
    {
        asset.SetData(new OGIData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateAiPathData(IAsset asset)
    {
        asset.SetData(new AiPathData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateAiPositionData(IAsset asset)
    {
        asset.SetData(new AiPositionData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateCameraData(IAsset asset)
    {
        asset.SetData(new CameraData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateCollisionSurfaceData(IAsset asset)
    {
        asset.SetData(new CollisionSurfaceData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateInstanceTemplateData(IAsset asset)
    {
        asset.SetData(new InstanceTemplateData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateObjectInstanceData(IAsset asset)
    {
        asset.SetData(new ObjectInstanceData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreatePathData(IAsset asset)
    {
        asset.SetData(new PathData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreatePositionData(IAsset asset)
    {
        asset.SetData(new PositionData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateTriggerData(IAsset asset)
    {
        asset.SetData(new TriggerData(asset));
        return AssetCreationStatus.Success;
    }
}