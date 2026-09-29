using Newtonsoft.Json;
using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Assets.Code;

public class SoundEffect : SerializableAsset
{
    protected override String DataExt => ".wav";
    public override UInt32 Section => Constants.CODE_SOUND_EFFECTS_SECTION;
    public override String IconPath => "SFX.png";

    [JsonProperty(Required = Required.Always)]
    public UInt32 Header { get; set; }
    
    /// <summary>
    /// The SPU2 pitch the PS2 version plays at, the sample rate times 4096 over 48000. Replacing the wav sets it from the wav's rate
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Sample rate", EditorDescType = typeof(SampleRateEditorDesc), Hint = "The rate the PS2 plays the sound at, kept as its pitch: the rate times 4096 over 48000 (682 for 8000 Hz, 1881 for 22050 Hz, 2730 for 32000 Hz). Replacing the wav sets it from the wav's rate, the Xbox version rounds its rate to it")]
    public UInt16 Pitch { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Handed to the sound driver with every play, 32 on every sound of the game")]
    public UInt16 Param1 { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Never read by the game, 16 on every sound of the game")]
    public UInt16 Param2 { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Never read by the game, 8192 on every sound of the game")]
    public UInt16 Param3 { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Never read by the game, 8192 on every sound of the game")]
    public UInt16 Param4 { get; set; }

    /// <summary>
    /// The sample the PS2 plays the sound again from once it played the loop's last one, -1 when it plays once. The PS2 keeps loops in
    /// the ADPCM blocks' flags, in blocks of 28 samples: builds round both points down to them
    /// </summary>
    [JsonProperty]
    [Editable]
    [EditorHidden]
    public Int32 LoopStart { get; set; } = -1;

    /// <summary>
    /// The sample after the loop's last one, -1 when the sound plays once. A looping sound stops there: the samples after it are never
    /// played and builds leave them out
    /// </summary>
    [JsonProperty]
    [Editable]
    [EditorHidden]
    public Int32 LoopEnd { get; set; } = -1;

    public SoundEffect()
    {
        Header = 3;
        Pitch = ITwinSound.PitchOf(22050);
        Param1 = 32;
        Param2 = 16;
        Param3 = 8192;
        Param4 = Param3;
        Raw = false;
    }

    public SoundEffect(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinSound sound) : base(id, name, package, needVariant, variant)
    {
        AssetData = new SoundEffectData(this, sound);
        Header = sound.Header;
        Pitch = sound.Pitch;
        Param1 = sound.Param1;
        Param2 = sound.Param2;
        Param3 = sound.Param3;
        Param4 = sound.Param4;
        LoopStart = sound.LoopStart;
        LoopEnd = sound.LoopEnd;
        Raw = false;
    }

    public override Type GetEditorType()
    {
        return typeof(SoundEffectViewModel);
    }

    public override void ResolveChunkResources(ITwinItemFactory factory, ITwinSection section)
    {
        if (OverrideViewOf(factory) is { } view)
        {
            view.ResolveChunkResources(factory, section);
            return;
        }

        if (ChunkVersionOf(factory) is { } version)
        {
            version.ResolveChunkResources(factory, section);
            return;
        }

        if (!factory.Resolution.Begin(this, section))
        {
            return;
        }

        try
        {
            var soundData = (SoundEffectData)GetData();
            var item = soundData.ResolveChunkResources(factory, section, ID) as ITwinSound;
            item?.SetID(ID);
            item?.Compile();
            if (item != null)
            {
                item.Header = Header;
                item.SetFreq((UInt16)soundData.GetFrequency());
                item.Pitch = Pitch;
                item.Param1 = Param1;
                item.Param2 = Param2;
                item.Param3 = Param3;
                item.Param4 = Param4;
                item.SetDataFromPCM(soundData.GetPcm(), LoopStart, LoopEnd);
            }

            DisposeData();
        }
        finally
        {
            factory.Resolution.End(this, section);
        }
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