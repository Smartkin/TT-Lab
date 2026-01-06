using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace TT_Lab.Services.Implementations;

public class AudioService : IAudioService, IDisposable
{
    private readonly MiniAudioEngine _audioEngine;
    private readonly AudioPlaybackDevice _audioDevice;

    public AudioService()
    {
        _audioEngine = new MiniAudioEngine();
        var playbackDevice = _audioEngine.PlaybackDevices.FirstOrDefault(d => d.IsDefault);
        Debug.Assert(playbackDevice.Id != IntPtr.Zero, "No default playback device found!");
        
        var audioFormat = new AudioFormat
        {
            Format = SampleFormat.F32,
            SampleRate = 48000,
            Channels = 2,
        };
        
        _audioDevice = _audioEngine.InitializePlaybackDevice(playbackDevice, audioFormat);
        _audioDevice.Start();
    }
    
    public ISoundPlayer CreateSoundPlayer(MemoryStream audio)
    {
        var dataProvider = new StreamDataProvider(_audioEngine, _audioDevice.Format, audio);
        var player = new SoundPlayer(_audioEngine, _audioDevice.Format, dataProvider);
        _audioDevice.MasterMixer.AddComponent(player);
        
        return player;
    }


    public void Dispose()
    {
        _audioEngine.Dispose();
        _audioDevice.Dispose();
    }
}