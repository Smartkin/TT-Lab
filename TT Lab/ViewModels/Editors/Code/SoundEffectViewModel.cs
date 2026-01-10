using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using SoundFlow.Components;
using SoundFlow.Interfaces;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using TT_Lab.Services;
using TT_Lab.Util;
using Twinsanity.Libraries;

namespace TT_Lab.ViewModels.Editors.Code;

public class SoundEffectViewModel : ResourceEditorViewModel
{
    private readonly IAudioService _audioService;
    private bool _soundReplaced;
    private UInt32 _header;
    private Byte _unkFlag;
    private UInt16 _param1;
    private UInt16 _param2;
    private UInt16 _param3;
    private UInt16 _param4;
    
    private SoundPlayer _audioPlayer;
    private MemoryStream _audioStream;

    public SoundEffectViewModel(IAudioService audioService)
    {
        _audioService = audioService;
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close)
        {
            _audioPlayer.Dispose();
        }
        else
        {
            StopPlayback();
        }

        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public override void LoadData()
    {
        var soundData = AssetManager.Get().GetAssetData<SoundEffectData>(EditableResource);
        if (_audioPlayer != null)
        {
            _audioPlayer.Dispose();
            soundData.Dispose();
            soundData = AssetManager.Get().GetAssetData<SoundEffectData>(EditableResource);
        }
        
        _audioStream = soundData.GetSoundEffectStream();
        _audioPlayer = _audioService.CreateSoundPlayer(_audioStream);
        _audioPlayer.PlaybackEnded += (s, e) =>
        {
            NotifyOfPropertyChange(nameof(SoundProgress));
            NotifyOfPropertyChange(nameof(CurrentTime));
            if (_audioPlayer.State != SoundFlow.Enums.PlaybackState.Stopped)
            {
                return;
            }
            
            SoundProgress = 0;
        };

        var sound = AssetManager.Get().GetAsset<SoundEffect>(EditableResource);
        _header = sound.Header;
        _unkFlag = sound.UnkFlag;
        _param1 = sound.Param1;
        _param2 = sound.Param2;
        _param3 = sound.Param3;
        _param4 = sound.Param4;
    }

    protected override void Save()
    {
        var sound = AssetManager.Get().GetAsset<SoundEffect>(EditableResource);
        sound.Header = _header;
        sound.UnkFlag = _unkFlag;
        sound.Param1 = _param1;
        sound.Param2 = _param2;
        sound.Param3 = _param3;
        sound.Param4 = _param4;
        sound.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);

        base.Save();
    }

    public void PlaySound()
    {
        if (_audioPlayer.State is SoundFlow.Enums.PlaybackState.Playing or SoundFlow.Enums.PlaybackState.Stopped)
        {
            StopPlayback();
        }

        _audioPlayer.Play();
    }

    public void PauseSound()
    {
        _audioPlayer.Pause();
        NotifyOfPropertyChange(nameof(SoundProgress));
        NotifyOfPropertyChange(nameof(CurrentTime));
    }

    public async Task ReplaceSound()
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Choose a wave file...", "Sound files", ["*.wav"]);
        if (string.IsNullOrEmpty(file))
        {
            return;
        }
        
        await using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(fs);
        var pcm = Array.Empty<byte>();
        short channels = 0;
        uint frequency = 0;
        RIFF.LoadRiff(reader, ref pcm, ref channels, ref frequency);
        if (channels != 1)
        {
            Log.WriteLine("ERROR: Stereo sound effects are not supported. Sound wasn't replaced.");
            return;
        }

        if (frequency > 48000)
        {
            Log.WriteLine("ERROR: Sounds over 48000 Hz are not supported. Sound wasn't replaced.");
            return;
        }
        
        fs.Flush();
        fs.Close();
        reader.Close();
        
        var soundData = AssetManager.Get().GetAssetData<SoundEffectData>(EditableResource);
        soundData.Load(file);
        LoadData();
        
        _soundReplaced = true;
        SoundProgress = 0;
        NotifyOfPropertyChange(nameof(TotalTimeLength));
        NotifyOfPropertyChange(nameof(ReplacedAudioMark));
    }

    public void ChangeTrackPosition(RangeBaseValueChangedEventArgs e)
    {
        if (_audioPlayer.State == SoundFlow.Enums.PlaybackState.Playing)
        {
            return;
        }
        
        _audioPlayer.Pause();
        _audioPlayer.Seek((float)e.NewValue);
        NotifyOfPropertyChange(nameof(CurrentTime));
    }

    public void UpdateTrackUi()
    {
        if (_audioPlayer.State != SoundFlow.Enums.PlaybackState.Playing)
        {
            return;
        }
        
        NotifyOfPropertyChange(nameof(SoundProgress));
        NotifyOfPropertyChange(nameof(CurrentTime));
    }

    public float SoundDuration => _audioPlayer.Duration;

    public float SoundProgress
    {
        get => _audioPlayer.Time;
        set
        {
            _audioPlayer.Seek(value);
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CurrentTime));
        }
    }

    [MarkDirty]
    public bool ReplacedAudioMark => _soundReplaced;

    [MarkDirty]
    public UInt32 Header
    {
        get => _header;
        set
        {
            if (value != _header)
            {
                _header = value;
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public Byte UnkFlag
    {
        get => _unkFlag;
        set
        {
            if (value != _unkFlag)
            {
                _unkFlag = value;
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public UInt16 Param1
    {
        get => _param1;
        set
        {
            if (value != _param1)
            {
                _param1 = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    [MarkDirty]
    public UInt16 Param2
    {
        get => _param2;
        set
        {
            if (value != _param2)
            {
                _param2 = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    [MarkDirty]
    public UInt16 Param3
    {
        get => _param3;
        set
        {
            if (value != _param3)
            {
                _param3 = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    [MarkDirty]
    public UInt16 Param4
    {
        get => _param4;
        set
        {
            if (value != _param4)
            {
                _param4 = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public string CurrentTime => TimeSpan.FromSeconds(_audioPlayer.Time).ToString(@"mm\:ss\.ff");
    public string TotalTimeLength => TimeSpan.FromSeconds(_audioPlayer.Duration).ToString(@"mm\:ss\.ff");

    private void StopPlayback()
    {
        _audioPlayer.Stop();
        SoundProgress = 0;
    }
}