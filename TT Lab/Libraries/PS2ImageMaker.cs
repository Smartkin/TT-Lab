using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace TT_Lab.Libraries;

public static partial class Ps2ImageMaker
{
    public static Progress StartPacking(string twinsPath, string imagePathName)
    {
        var ptr = start_packing(twinsPath, imagePathName);
        // PS2ImageMaker before its fix started its progress over but not the copy it hands out, which polls gave until the packing's thread
        // first reported: the last packing's, finished. Only polls write the copy, the packing's thread never does
        Marshal.StructureToPtr(new ProgressC { file_name = "" }, ptr, false);
        var progress = (ProgressC)Marshal.PtrToStructure(ptr, typeof(ProgressC))!;
        var prog = new Progress
        {
            Finished = progress.finished != 0,
            NewFile = progress.new_file != 0,
            NewState = progress.new_state != 0,
            ProgressS = progress.state,
            ProgressPercentage = progress.progress,
            File = progress.file_name
        };
        return prog;
    }

    public static Progress PollProgress()
    {
        var ptr = poll_progress();
        var progress = (ProgressC)Marshal.PtrToStructure(ptr, typeof(ProgressC))!;
        var prog = new Progress
        {
            Finished = progress.finished != 0,
            NewFile = progress.new_file != 0,
            NewState = progress.new_state != 0,
            ProgressS = progress.state,
            ProgressPercentage = progress.progress,
            File = progress.file_name
        };
        return prog;
    }

    public enum ProgressState
    {
        Failed = -1,
        EnumFiles,
        WriteSectors,
        WriteFiles,
        WriteEnd,
        Finished,
    }

    public class Progress {
        public string File;
        public ProgressState ProgressS;
        public float ProgressPercentage;
        public bool Finished;
        public bool NewState;
        public bool NewFile;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct ProgressC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string file_name;
        public int size;
        public ProgressState state;
        public float progress;
        public byte finished;
        public byte new_state;
        public byte new_file;
    }

    [LibraryImport("PS2ImageMaker", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static unsafe partial IntPtr start_packing([MarshalAs(UnmanagedType.LPStr)] string game_path, [MarshalAs(UnmanagedType.LPStr)] string dest_path);
    
    [LibraryImport("PS2ImageMaker")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static unsafe partial IntPtr poll_progress();
}

// PS2ImageMaker's packing of a folder, polled until it's done, one at a time: the library has one packing and one progress for the process.
// An image growing past what the folder's files make is the packer running away (PS2ImageMaker before its fix wrote zero sectors past the
// image's end until the memory ran out when a count of them was left unset), which stops with an error. Its thread can't be stopped and keeps
// the library's progress, so no other packing starts until TT Lab is closed, which the error says to do right away
internal sealed class Ps2ImagePacker
{
    private const long SectorSize = 2048;
    private const string ProgramName = "TT Lab";

    // TT Lab's packings, through the native library
    public static readonly Ps2ImagePacker Native = new(Ps2ImageMaker.StartPacking, Ps2ImageMaker.PollProgress);

    private readonly Func<string, string, Ps2ImageMaker.Progress> _start;
    private readonly Func<Ps2ImageMaker.Progress> _poll;
    private readonly object _packing = new();
    private string? _ranAway;

    public Ps2ImagePacker(Func<string, string, Ps2ImageMaker.Progress> start, Func<Ps2ImageMaker.Progress> poll)
    {
        _start = start;
        _poll = poll;
    }

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(0.5);

    // Packs the folder into the image, its progress (0 to 1) reported after every poll. Its FAILED state is an IOException naming the file it
    // stopped at, a packing whose thread goes on writing a Ps2ImageRunawayException
    public void Pack(string folder, string imagePath, Action<float>? report = null)
    {
        lock (_packing)
        {
            if (_ranAway != null)
            {
                throw new Ps2ImageRunawayException($"PS2ImageMaker is still writing {_ranAway}, the image it ran away with: close {ProgramName} now, it makes " +
                                                   "no other image until then");
            }

            var most = MostImageBytes(folder);
            // Its progress starts over: whatever finished is this packing's, however late its thread starts or quickly it's done
            var progress = _start(folder, imagePath);
            while (!progress.Finished && progress.ProgressS != Ps2ImageMaker.ProgressState.Failed)
            {
                Thread.Sleep(PollInterval);
                progress = _poll();
                report?.Invoke(progress.ProgressPercentage);
                var written = SizeOf(imagePath);
                if (written <= most)
                {
                    continue;
                }

                var stopped = progress.Finished || progress.ProgressS == Ps2ImageMaker.ProgressState.Failed;
                if (stopped)
                {
                    throw new IOException($"PS2ImageMaker's image of {folder} is {written} bytes, more than its files make ({most} at most): it wrote past the image's end");
                }

                // Its thread fills the memory and the disk while the program runs
                _ranAway = imagePath;
                throw new Ps2ImageRunawayException($"PS2ImageMaker ran away with the image of {folder}: {written} bytes, more than its files make ({most} at most), and " +
                                                   $"it goes on writing {imagePath} and taking more memory until {ProgramName} is closed. Close {ProgramName} now: the " +
                                                   "image wasn't made, the last one is left as it was");
            }

            if (progress.ProgressS == Ps2ImageMaker.ProgressState.Failed)
            {
                throw new IOException($"PS2ImageMaker couldn't make the image of {folder}" + (string.IsNullOrEmpty(progress.File) ? "" : $", it stopped at {progress.File}"));
            }
        }
    }

    // The most bytes an image of the folder takes: every file from a sector of its own, and the disc's own sectors with room to spare (261
    // before the directories, the path tables, then the records, identifiers and entries of every folder and file, a sector or two each)
    internal static long MostImageBytes(string folder)
    {
        var sectors = 512L;
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            sectors += 4 + (entry is FileInfo file ? (file.Length + SectorSize - 1) / SectorSize : 0);
        }

        return sectors * SectorSize;
    }

    private static long SizeOf(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? file.Length : 0;
    }
}

// The packer's thread goes on writing the image until TT Lab is closed: what it writes is left alone
internal sealed class Ps2ImageRunawayException(string message) : IOException(message);
