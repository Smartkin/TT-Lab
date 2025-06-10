using System;
using System.Runtime.InteropServices;

namespace TT_Lab.Libraries;

public static class Ps2ImageMaker
{
    public static Progress StartPacking(string twinsPath, string imagePathName)
    {
        var ptr = start_packing(twinsPath, imagePathName);
        ProgressC progress = new ProgressC();
        progress = (ProgressC)Marshal.PtrToStructure(ptr, typeof(ProgressC));
        Progress prog = new Progress
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
        ProgressC progress = new ProgressC();
        progress = (ProgressC)Marshal.PtrToStructure(ptr, typeof(ProgressC));
        Progress prog = new Progress
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

    [DllImport("PS2ImageMaker", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern unsafe IntPtr start_packing([MarshalAs(UnmanagedType.LPStr)] string game_path, [MarshalAs(UnmanagedType.LPStr)] string dest_path);
    [DllImport("PS2ImageMaker", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe IntPtr poll_progress();
}