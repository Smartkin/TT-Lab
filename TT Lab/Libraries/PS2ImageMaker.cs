using System;
using System.Runtime.InteropServices;

namespace TT_Lab.Libraries;

public static partial class Ps2ImageMaker
{
    public static Progress StartPacking(string twinsPath, string imagePathName)
    {
        var ptr = start_packing(twinsPath, imagePathName);
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