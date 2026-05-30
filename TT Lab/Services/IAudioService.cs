using System.IO;
using SoundFlow.Components;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace TT_Lab.Services;

public interface IAudioService
{
    SoundPlayer CreateSoundPlayer(MemoryStream audio);
}