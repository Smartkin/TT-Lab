using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Project;
using TT_Lab.Util;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Factory;

public enum AssetCreationStatus
{
    Success,
    Failed,
}

public static class AssetDataFactory
{
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
            Log.WriteLine("ERROR: No sound file provided.");
            return AssetCreationStatus.Failed;
        }
        
        await using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(fs);
        Byte[] pcm = Array.Empty<byte>();
        short channels = 0;
        uint frequency = 0;
        Riff.LoadRiff(reader, ref pcm, ref channels, ref frequency);
        if (channels != 1)
        {
            Log.WriteLine("ERROR: Stereo sound effects are not supported. Sound wasn't added.");
            return AssetCreationStatus.Failed;
        }

        if (frequency > 48000)
        {
            Log.WriteLine("ERROR: Sounds over 48000 Hz are not supported. Sound wasn't added.");
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
        asset.SetData(new SkydomeData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateGameObjectData(IAsset asset)
    {
        var gameObjectData = new GameObjectData(asset);
        gameObjectData.Name = asset.Name.Trim();
        asset.SetData(gameObjectData);
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateChunkData(IAsset asset)
    {
        throw new System.NotImplementedException();
    }

    public static AssetCreationStatus CreateBehaviourData(IAsset asset)
    {
        asset.SetData(new BehaviourGraphData(asset));
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