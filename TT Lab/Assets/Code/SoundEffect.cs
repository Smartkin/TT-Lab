using Newtonsoft.Json;
using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.ViewModels.Editors.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Assets.Code
{
    public class SoundEffect : SerializableAsset
    {
        protected override String DataExt => ".wav";
        public override UInt32 Section => Constants.CODE_SOUND_EFFECTS_SECTION;
        public override String IconPath => "SFX.png";

        [JsonProperty(Required = Required.Always)]
        public UInt32 Header { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 SampleRate { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Byte UnkFlag { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 Param1 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 Param2 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 Param3 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 Param4 { get; set; }

        public SoundEffect()
        {
            Header = 3;
            UnkFlag = 0;
            Param1 = 32;
            Param2 = 16;
            Param3 = 8192;
            Param4 = Param3;
            SampleRate = 44100;
            Raw = false;
        }

        public SoundEffect(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinSound sound) : base(id, name, package, needVariant, variant)
        {
            AssetData = new SoundEffectData(this, sound);
            Header = sound.Header;
            UnkFlag = sound.UnkFlag;
            Param1 = sound.Param1;
            Param2 = sound.Param2;
            Param3 = sound.Param3;
            Param4 = sound.Param4;
            SampleRate = sound.GetFreq();
            Raw = false;
        }

        public override Type GetEditorType()
        {
            return typeof(SoundEffectViewModel);
        }

        public override void ResolveChunkResources(ITwinItemFactory factory, ITwinSection section)
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = GetData();
            }

            var item = AssetData.ResolveChunkResources(factory, section, ID) as ITwinSound;
            item?.SetID(ID);
            item?.Compile();
            if (item != null)
            {
                item.Header = Header;
                item.UnkFlag = UnkFlag;
                item.SetFreq(SampleRate);
                item.Param1 = Param1;
                item.Param2 = Param2;
                item.Param3 = Param3;
                item.Param4 = Param4;
                item.SetDataFromPCM(((SoundEffectData)AssetData).GetPcm());
            }

            AssetData.Dispose();
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new SoundEffectData(this);
                AssetData.Load(DataLoadPath);
            }
            return AssetData;
        }
    }
}
