using Newtonsoft.Json;
using System;
using System.Diagnostics;
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
            Debug.Assert(_soundEffectStream != null, $"Attempting to get disposed sound! {Owner.Name}");
            return _soundEffectStream;
        }

        private Byte[] _wave;
        private MemoryStream? _soundEffectStream;
        private Byte[] _pcm;
        private UInt32 _frequency;
        private Int16 _channels;

        protected override void Dispose(Boolean disposing)
        {
            if (!disposing)
            {
                return;
            }
            
            _soundEffectStream?.Dispose();
        }

        protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
        {
            using FileStream fs = new(dataPath, FileMode.Create, FileAccess.Write);
            using BinaryWriter writer = new(fs);
            Riff.SaveRiff(writer, _pcm, ref _channels, ref _frequency);
            fs.Flush(true);
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            using FileStream fs = new(dataPath, FileMode.Open, FileAccess.Read);
            using BinaryReader reader = new(fs);
            _wave = Riff.LoadRiff(reader, ref _pcm, ref _channels, ref _frequency);
            _soundEffectStream = new MemoryStream(_wave);
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinSound sound = GetTwinItem<ITwinSound>();
            _frequency = sound.GetFreq();
            _channels = 1;
            _pcm = sound.ToPCM();
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var sound = factory.GenerateSound();
            sound.SetDataFromPCM(_pcm);
            sound.SetFreq((UInt16)_frequency);

            return sound;
        }
    }
}
