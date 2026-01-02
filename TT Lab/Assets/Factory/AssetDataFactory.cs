using System;
using System.Drawing;
using System.IO;
using Avalonia.Media.Imaging;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
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
        return AssetCreationStatus.Success;
    }
    
    public static AssetCreationStatus CreateSoundEffectData(IAsset asset)
    {
        var file = MiscUtils.GetFileFromDialogue("Wave File|*.wav");
        if (string.IsNullOrEmpty(file))
        {
            Log.WriteLine("ERROR: No sound file provided.");
            return AssetCreationStatus.Failed;
        }
        
        using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(fs);
        Byte[] pcm = Array.Empty<byte>();
        short channels = 0;
        uint frequency = 0;
        RIFF.LoadRiff(reader, ref pcm, ref channels, ref frequency);
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

    public static AssetCreationStatus CreateTextureData(IAsset asset)
    {
        var file = MiscUtils.GetFileFromDialogue("Image file|*.jpg;*.png;*.bmp");
        if (string.IsNullOrEmpty(file))
        {
            Log.WriteLine("ERROR: No texture file provided.");
            return AssetCreationStatus.Failed;
        }
        
        Bitmap image = new(file);
        var creationStatusResult = AssetCreationStatus.Success;
        if (image.Size.Width > 256)
        {
            Log.WriteLine("ERROR: Texture's width can't exceed 256 pixels.");
            creationStatusResult = AssetCreationStatus.Failed;
        }

        if (image.Size.Height > 256)
        {
            Log.WriteLine("ERROR: Texture's height can't exceed 256 pixels.");
            creationStatusResult = AssetCreationStatus.Failed;
        }

        if (!MathExtension.IsPowerOfTwo((long)image.Size.Width))
        {
            Log.WriteLine("ERROR: Texture's width must be a power of two.");
            creationStatusResult = AssetCreationStatus.Failed;
        }

        if (!MathExtension.IsPowerOfTwo((long)image.Size.Height))
        {
            Log.WriteLine("ERROR: Texture's height must be a power of two.");
            creationStatusResult = AssetCreationStatus.Failed;
        }

        if (image.Size.Width < 8)
        {
            Log.WriteLine("ERROR: Texture's width can't be smaller than 8 pixels.");
            creationStatusResult = AssetCreationStatus.Failed;
        }

        if (image.Size.Height < 8)
        {
            Log.WriteLine("ERROR: Texture's height can't be smaller than 8 pixels.");
            creationStatusResult = AssetCreationStatus.Failed;
        }

        if (creationStatusResult == AssetCreationStatus.Failed)
        {
            Log.WriteLine("Please fix the issues above and try again.");
            image.Dispose();

            return creationStatusResult;
        }

        var newTexture = new TextureData(asset);
        newTexture.Bitmap = image;
        newTexture.TextureFunction = ITwinTexture.TextureFunction.MODULATE;
        newTexture.TexturePixelFormat = ITwinTexture.TexturePixelFormat.PSMT8;
        asset.SetData(newTexture);

        return creationStatusResult;
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

    public static AssetCreationStatus CreateLodModelData(IAsset asset)
    {
        asset.SetData(new LodModelData(asset));
        return AssetCreationStatus.Success;
    }

    public static AssetCreationStatus CreateMaterialData(IAsset asset)
    {
        asset.SetData(new MaterialData(asset));
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