using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetData.Code
{
    public sealed class SoundEffectData : AbstractAssetData
    {
        public SoundEffectData(IAsset asset) : base(asset)
        {
        }

        public SoundEffectData(IAsset asset, string filepath) : this(asset)
        {
            LoadInternal(filepath);
        }

        public SoundEffectData(IAsset asset, ITwinSound sound) : this(asset)
        {
            SetTwinItem(sound);
        }

        public MemoryStream GetSoundEffectStream()
        {
            return soundEffectStream;
        }

        Byte[] WAVE;
        MemoryStream soundEffectStream;
        Byte[] PCM;
        UInt32 Frequency;
        Int16 Channels;

        protected override void Dispose(Boolean disposing)
        {
            if (!disposing)
            {
                return;
            }
            
            soundEffectStream?.Dispose();
        }

        protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
        {
            using FileStream fs = new(dataPath, FileMode.Create, FileAccess.Write);
            using BinaryWriter writer = new(fs);
            RIFF.SaveRiff(writer, PCM, ref Channels, ref Frequency);
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            using FileStream fs = new(dataPath, FileMode.Open, FileAccess.Read);
            using BinaryReader reader = new(fs);
            WAVE = RIFF.LoadRiff(reader, ref PCM, ref Channels, ref Frequency);
            soundEffectStream = new MemoryStream(WAVE);
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinSound sound = GetTwinItem<ITwinSound>();
            Frequency = sound.GetFreq();
            Channels = 1;
            PCM = sound.ToPCM();
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var sound = factory.GenerateSound();
            sound.SetDataFromPCM(PCM);
            sound.SetFreq((UInt16)Frequency);

            return sound;
        }
    }
}
