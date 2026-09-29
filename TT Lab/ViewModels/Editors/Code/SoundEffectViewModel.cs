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
    private static bool _reportedNoAudio;

    private const int Block = ADPCM.SamplesPerBlock;

    private readonly IAudioService? _audioService;
    private SoundPlayer? _audioPlayer;
    private MemoryStream? _audioStream;
    // The asset's loop, edited through the document so undo covers it and the inspector follows
    private readonly PropertyNode? _loopStartNode;
    private readonly PropertyNode? _loopEndNode;
    private IDisposable? _loopDrag;

    public SoundEffectViewModel(DocumentViewModel document, PropertyNode soundEffectData, params DocumentNodeViewModel[] dependencies) : base(document, soundEffectData, dependencies)
    {
        _audioService = GetAudioService();
        _loopStartNode = FindAssetProperty(soundEffectData, nameof(SoundEffect.LoopStart));
        _loopEndNode = FindAssetProperty(soundEffectData, nameof(SoundEffect.LoopEnd));
        if (_loopStartNode != null)
        {
            _loopStartNode.Changed += OnLoopChanged;
        }

        if (_loopEndNode != null)
        {
            _loopEndNode.Changed += OnLoopChanged;
        }

        BuildDisplaySamples();
        InitAudioPlayer(CurrentValue!);
    }

    // The editor is the data's custom editor node, the loop is the asset's, a node or two up
    private static PropertyNode? FindAssetProperty(PropertyNode node, string name)
    {
        for (var ancestor = node.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor.FindChild($".{name}") is { } property)
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    /// The first channel's samples, what the waveform shows
    /// </summary>
    public short[] DisplaySamples { get; private set; } = [];

    private int SampleRate => (int)Math.Max(1, CurrentValue?.GetFrequency() ?? 1);

    /// <summary>
    /// The loop's first sample, -1 when the sound plays once
    /// </summary>
    public int LoopStart => _loopStartNode?.GetValue() is int start ? start : -1;

    /// <summary>
    /// The sample after the loop's last one, -1 when the sound plays once
    /// </summary>
    public int LoopEnd => _loopEndNode?.GetValue() is int end ? end : -1;

    public bool HasLoop => LoopStart >= 0 && LoopEnd > LoopStart;

    public bool CanEditLoop => _loopStartNode != null && _loopEndNode != null;

    public double PlayheadSample => (_audioPlayer?.Time ?? 0) * SampleRate;

    public string LoopText
    {
        get
        {
            if (!HasLoop)
            {
                return "Plays once";
            }

            var text = $"Loops from {Seconds(LoopStart)} to {Seconds(LoopEnd)} (samples {LoopStart} to {LoopEnd})";
            return LoopEnd < DisplaySamples.Length ? $"{text}, the {Seconds(DisplaySamples.Length - LoopEnd)} after the loop never play" : text;
        }
    }

    private string Seconds(int samples) => $"{samples / (double)SampleRate:0.000} s";

    // A loop can only end on a whole block: the samples of a last block that isn't full never get into it
    private int MaxLoopEnd => Math.Max(Block, DisplaySamples.Length / Block * Block);

    private static int Snap(int sample) => (int)Math.Round(sample / (double)Block) * Block;

    private void BuildDisplaySamples()
    {
        var pcm = CurrentValue?.GetPcm() ?? [];
        var channels = CurrentValue?.IsStereo() == true ? 2 : 1;
        var samples = new short[pcm.Length / (2 * channels)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(pcm, i * 2 * channels);
        }

        DisplaySamples = samples;
        this.RaisePropertyChanged(nameof(DisplaySamples));
        this.RaisePropertyChanged(nameof(LoopText));
    }

    private void OnLoopChanged()
    {
        this.RaisePropertyChanged(nameof(LoopStart));
        this.RaisePropertyChanged(nameof(LoopEnd));
        this.RaisePropertyChanged(nameof(HasLoop));
        this.RaisePropertyChanged(nameof(LoopText));
        ApplyLoopToPlayer();
    }

    // The preview plays the sound the way the PS2 does: from the start, then the loop over and over
    private void ApplyLoopToPlayer()
    {
        if (_audioPlayer == null)
        {
            return;
        }

        if (HasLoop)
        {
            _audioPlayer.SetLoopPoints((float)(LoopStart / (double)SampleRate), (float)(Math.Min(LoopEnd, DisplaySamples.Length) / (double)SampleRate));
        }

        _audioPlayer.IsLooping = HasLoop;
    }

    // Both ends as one step, a loop at least a block long that fits the sound; -1 for no loop
    private void SetLoop(int start, int end, string description)
    {
        if (!CanEditLoop)
        {
            return;
        }

        if (start >= 0)
        {
            end = Math.Clamp(end, Block, MaxLoopEnd);
            start = Math.Clamp(start, 0, end - Block);
        }
        else
        {
            end = -1;
        }

        using (_loopDrag == null ? Document.History.BeginGroup(description) : null)
        {
            _loopStartNode!.SetValue(start);
            _loopEndNode!.SetValue(end);
        }
    }

    public void SetLoopStartHere()
    {
        var start = Snap((int)PlayheadSample);
        SetLoop(start, HasLoop ? Math.Max(LoopEnd, start + Block) : MaxLoopEnd, "Moved the loop's start");
    }

    public void SetLoopEndHere()
    {
        var end = Snap((int)PlayheadSample);
        SetLoop(HasLoop ? Math.Min(LoopStart, end - Block) : 0, end, "Moved the loop's end");
    }

    public void LoopWholeSound()
    {
        SetLoop(0, MaxLoopEnd, "Looped the whole sound");
    }

    public void RemoveLoop()
    {
        SetLoop(-1, -1, "Removed the loop");
    }

    public void BeginLoopDrag()
    {
        _loopDrag?.Dispose();
        _loopDrag = Document.History.BeginGroup("Moved the loop");
    }

    public void DragLoopStart(int sample)
    {
        if (HasLoop)
        {
            SetLoop(Math.Min(Snap(sample), LoopEnd - Block), LoopEnd, "Moved the loop's start");
        }
    }

    public void DragLoopEnd(int sample)
    {
        if (HasLoop)
        {
            SetLoop(LoopStart, Math.Max(Snap(sample), LoopStart + Block), "Moved the loop's end");
        }
    }

    public void EndLoopDrag()
    {
        _loopDrag?.Dispose();
        _loopDrag = null;
    }

    public void Seek(int sample)
    {
        if (_audioPlayer == null)
        {
            return;
        }

        _audioPlayer.Seek((float)(sample / (double)SampleRate));
        this.RaisePropertyChanged(nameof(CurrentTime));
        this.RaisePropertyChanged(nameof(SoundProgress));
        this.RaisePropertyChanged(nameof(PlayheadSample));
    }

    /// <summary>
    /// Whether the sound can be listened to: without a playback device it can still be edited and replaced
    /// </summary>
    public bool CanPlay => _audioPlayer != null;

    // The service opens the playback device when it's first asked for, which fails on machines without one
    private static IAudioService? GetAudioService()
    {
        try
        {
            return Locator.Current.GetService<IAudioService>();
        }
        catch (Exception ex)
        {
            if (!_reportedNoAudio)
            {
                _reportedNoAudio = true;
                Log.WriteLine($"Sounds can't be played: {ex.Message}", Log.LogType.Warning);
            }

            return null;
        }
    }

    protected override void OnClosed(CompositeDisposable disposables)
    {
        _audioPlayer?.DisposeWith(disposables);
        _loopDrag?.Dispose();
        _loopDrag = null;
        if (_loopStartNode != null)
        {
            _loopStartNode.Changed -= OnLoopChanged;
        }

        if (_loopEndNode != null)
        {
            _loopEndNode.Changed -= OnLoopChanged;
        }
    }

    private void InitAudioPlayer(SoundEffectData soundData)
    {
        _audioPlayer = null;
        try
        {
            if (_audioService != null)
            {
                _audioStream = soundData.GetSoundEffectStream();
                var player = _audioService.CreateSoundPlayer(_audioStream);
                player.PlaybackEnded += (_, _) =>
                {
                    if (player.State != SoundFlow.Enums.PlaybackState.Stopped)
                    {
                        return;
                    }

                    SoundProgress = 0;
                };
                _audioPlayer = player;
            }
        }
        catch (Exception ex)
        {
            Log.WriteLine($"The sound can't be played: {ex.Message}", Log.LogType.Warning);
        }

        this.RaisePropertyChanged(nameof(CanPlay));
        ApplyLoopToPlayer();
    }

    public void PlaySound()
    {
        if (_audioPlayer == null)
        {
            return;
        }

        if (_audioPlayer.State is SoundFlow.Enums.PlaybackState.Playing or SoundFlow.Enums.PlaybackState.Stopped)
        {
            StopPlayback();
        }

        _audioPlayer.Play();
    }

    public void PauseSound()
    {
        _audioPlayer?.Pause();
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
        Riff.LoadRiff(reader, ref pcm, ref channels, ref frequency, out var loopStart, out var loopEnd);

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
        
        var looped = HasLoop;
        SetValueCommand.Execute(new SoundEffectData((IAsset)Document.DocumentModel));
        CurrentValue!.Load(file);
        if (Document.DocumentModel is SoundEffect soundEffect)
        {
            soundEffect.Pitch = CurrentValue.GetPitch();
        }

        BuildDisplaySamples();
        InitAudioPlayer(CurrentValue!);
        // The wav's own loop (its sampler chunk), else a sound that looped loops the whole new one
        if (loopStart >= 0 && loopEnd > loopStart)
        {
            SetLoop(Snap(loopStart), Snap(loopEnd), "Took the wav's loop");
        }
        else if (looped)
        {
            SetLoop(0, MaxLoopEnd, "Looped the whole sound");
        }
        else
        {
            OnLoopChanged();
        }

        SoundProgress = 0;
        this.RaisePropertyChanged(nameof(SoundDuration));
        this.RaisePropertyChanged(nameof(TotalTimeLength));
    }

    public void ChangeTrackPosition(RangeBaseValueChangedEventArgs e)
    {
        if (_audioPlayer == null || _audioPlayer.State == SoundFlow.Enums.PlaybackState.Playing)
        {
            return;
        }
        
        _audioPlayer.Pause();
        _audioPlayer.Seek((float)e.NewValue);
        
        this.RaisePropertyChanged(nameof(CurrentTime));
        this.RaisePropertyChanged(nameof(SoundProgress));
        this.RaisePropertyChanged(nameof(PlayheadSample));
    }

    public void UpdateTrackUi()
    {
        if (_audioPlayer?.State != SoundFlow.Enums.PlaybackState.Playing)
        {
            return;
        }
        
        this.RaisePropertyChanged(nameof(CurrentTime));
        this.RaisePropertyChanged(nameof(SoundProgress));
        this.RaisePropertyChanged(nameof(PlayheadSample));
    }

    public float SoundDuration => _audioPlayer?.Duration ?? 0;

    [Reactive]
    public float SoundProgress
    {
        get => _audioPlayer?.Time ?? 0;
        set
        {
            _audioPlayer?.Seek(value);
            Dispatcher.UIThread.Post(() =>
            {
                this.RaisePropertyChanged(nameof(CurrentTime));
                this.RaisePropertyChanged(nameof(SoundProgress));
                this.RaisePropertyChanged(nameof(PlayheadSample));
            });
        }
    }

    public string CurrentTime => TimeSpan.FromSeconds(_audioPlayer?.Time ?? 0).ToString(@"mm\:ss\.ff");
    public string TotalTimeLength => TimeSpan.FromSeconds(_audioPlayer?.Duration ?? 0).ToString(@"mm\:ss\.ff");

    private void StopPlayback()
    {
        _audioPlayer?.Stop();
        SoundProgress = 0;
    }
}