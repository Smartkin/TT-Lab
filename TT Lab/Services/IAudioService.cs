using System.IO;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace TT_Lab.Services;

public interface IAudioService
{
    ISoundPlayer CreateSoundPlayer(MemoryStream audio);
}