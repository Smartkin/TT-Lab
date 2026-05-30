using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SoundFlow.Components;
using SoundFlow.Interfaces;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using TT_Lab.Services;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.Libraries;

namespace TT_Lab.ViewModels.Editors.Code;

public partial class SoundEffectViewModel : DocumentDataViewModel<SoundEffectData>
{
    private readonly IAudioService _audioService;
    
    private SoundPlayer _audioPlayer;
    private MemoryStream _audioStream;

    public SoundEffectViewModel(DocumentViewModel document, PropertyNode soundEffectData, params DocumentNodeViewModel[] dependencies) : base(document, soundEffectData, dependencies)
    {
        _audioService = Locator.Current.GetService<IAudioService>()!;
        
        InitAudioPlayer(CurrentValue!);
    }

    protected override void OnClosed(CompositeDisposable disposables)
    {
        _audioPlayer.DisposeWith(disposables);
    }

    [MemberNotNull(nameof(_audioPlayer))]
    [MemberNotNull(nameof(_audioStream))]
    private void InitAudioPlayer(SoundEffectData soundData)
    {
        _audioStream = soundData.GetSoundEffectStream();
        _audioPlayer = _audioService.CreateSoundPlayer(_audioStream);
        
        _audioPlayer.PlaybackEnded += (s, e) =>
        {
            if (_audioPlayer.State != SoundFlow.Enums.PlaybackState.Stopped)
            {
                return;
            }
        
            SoundProgress = 0;
        };
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
        Riff.LoadRiff(reader, ref pcm, ref channels, ref frequency);

        if (channels > 2)
        {
            Log.WriteLine("Buddy, what kind of audio are you trying to use here? Either mono or stereo. Sound wasn't replaced.", Log.LogType.Error);
            return;
        }
        
        if (channels == 2 && frequency > 22050)
        {
            Log.WriteLine("Stereo sounds can not be over 22050 Hz. Sound wasn't replaced", Log.LogType.Error);
            return;
        }

        if (frequency > 48000)
        {
            Log.WriteLine("Sounds over 48000 Hz are not supported. Sound wasn't replaced.", Log.LogType.Error);
            return;
        }
        
        fs.Flush();
        fs.Close();
        reader.Close();
        
        SetValueCommand.Execute(new SoundEffectData((IAsset)Document.DocumentModel));
        CurrentValue!.Load(file);
        InitAudioPlayer(CurrentValue!);
        
        SoundProgress = 0;
        this.RaisePropertyChanged(nameof(SoundDuration));
        this.RaisePropertyChanged(nameof(TotalTimeLength));
    }

    public void ChangeTrackPosition(RangeBaseValueChangedEventArgs e)
    {
        if (_audioPlayer.State == SoundFlow.Enums.PlaybackState.Playing)
        {
            return;
        }
        
        _audioPlayer.Pause();
        _audioPlayer.Seek((float)e.NewValue);
        
        this.RaisePropertyChanged(nameof(CurrentTime));
        this.RaisePropertyChanged(nameof(SoundProgress));
    }

    public void UpdateTrackUi()
    {
        if (_audioPlayer.State != SoundFlow.Enums.PlaybackState.Playing)
        {
            return;
        }
        
        this.RaisePropertyChanged(nameof(CurrentTime));
        this.RaisePropertyChanged(nameof(SoundProgress));
    }

    public float SoundDuration => _audioPlayer.Duration;

    [Reactive]
    public float SoundProgress
    {
        get => _audioPlayer.Time;
        set
        {
            _audioPlayer.Seek(value);
            Dispatcher.UIThread.Post(() =>
            {
                this.RaisePropertyChanged(nameof(CurrentTime));
                this.RaisePropertyChanged(nameof(SoundProgress));
            });
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