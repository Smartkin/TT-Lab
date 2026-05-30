using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Code;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetData.Code;

[Editable(Caption = "SFX Editor", EditorDescType = typeof(SoundEffectEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
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
        return new MemoryStream(_wave);
    }

    public UInt32 GetFrequency()
    {
        return _frequency;
    }

    public bool IsStereo()
    {
        return _channels == 2;
    }

    public Byte[] GetPcm()
    {
        return _pcm;
    }

    private Byte[] _wave;
    private Byte[] _pcm;
    private UInt32 _frequency;
    private Int16 _channels;

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        using FileStream fs = new(dataPath, FileMode.Create, FileAccess.Write);
        using BinaryWriter writer = new(fs);
        Riff.SaveRiff(writer, _pcm, ref _channels, ref _frequency);
        fs.Flush(true);
    }

    protected override void Dispose(bool disposing)
    {
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using FileStream fs = new(dataPath, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(fs);
        _wave = Riff.LoadRiff(reader, ref _pcm, ref _channels, ref _frequency);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var sound = GetTwinItem<ITwinSound>();
        _frequency = sound.GetFreq();
        _channels = (sound.Header & 1) == 0 ? (short)2 : (short)1;

        _pcm = sound.ToPCM();
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var sound = factory.GenerateSound();
        sound.SetFreq((UInt16)_frequency);

        return sound;
    }
}