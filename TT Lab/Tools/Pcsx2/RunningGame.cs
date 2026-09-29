using System;
using System.Collections.Generic;

namespace TT_Lab.Tools.Pcsx2;

/// <summary>
/// States of the game controller that playing from TT Lab goes through, the same in every release (the functions named are the PAL
/// release's)
/// </summary>
public enum GameState : byte
{
    // 1 loads the startup files, 2 to 5 show the logos and the legal screen
    Logos = 4,
    // FUN_00173d10: drops every chunk and loads G_StartChunk_Path with the chunks it links, then the title
    LoadStartChunk = 6,
    // "Press start" over the start chunk. Its character slot 4 plays the title's cinematic (the beach's "THREE YEARS AGO..."), it isn't the
    // player: a title skipped straight to playing kept it running, and the beach never got past it
    Title = 7,
    // Where the main menu's new game goes (FUN_001744a0): the game's data made again for a new game (FUN_00172fc8), the player in
    // character slot 0, then the intro movie
    NewGame = 10,
    // FUN_001745f8: the player gets control (FUN_0017c248, when it comes from the movie) and the camera its start, then playing
    StartPlaying = 11,
    Playing = 12,
    // A movie (FUN_00172ae8), the new game's intro starts half a second in
    Movie = 14,
    None = 0x18
}

/// <summary>
/// What TT Lab reads and asks of the game while it plays, through PINE (Ghidra Stuff/Engine_RE, PCSX2 section), at the release's
/// addresses
/// </summary>
public sealed class RunningGame(PineClient pine, GameRelease release)
{
    // The game controller's word of states holds the next state in bits 50-55 (None when there's none), the current one in 44-49 and
    // the previous one in 38-43: the dispatcher (FUN_00177278) moves on to the next state before running the current one each frame
    private const Int32 CurrentStateShift = 44 - 32;
    private const Int32 NextStateShift = 50 - 32;
    // The chunk manager's first word counts the chunks it has loaded (the low half), their chunk metas follow it, each starting with its
    // path as a String. Dropping every chunk only sets the count to 0 (UnloadAllChunks_), the old metas' pointers stay
    private const UInt32 MaxChunks = 0x200;

    public PineClient Pine { get; } = pine;

    public GameRelease Release { get; } = release;

    private UInt32 StatesHighWord => Release.StatesOffset + 4;

    /// <summary>
    /// None until the game controller exists
    /// </summary>
    public GameState State
    {
        get
        {
            var controller = Pine.ReadUInt32(Release.GameControllerPointer);
            return controller == 0 ? GameState.None : (GameState)(Pine.ReadUInt32(controller + StatesHighWord) >> CurrentStateShift & 0x3f);
        }
    }

    public void Request(GameState state)
    {
        var controller = Pine.ReadUInt32(Release.GameControllerPointer);
        if (controller == 0)
        {
            throw new InvalidOperationException("The game hasn't started yet");
        }

        var states = Pine.ReadUInt32(controller + StatesHighWord);
        Pine.WriteUInt32(controller + StatesHighWord, states & ~(0x3fu << NextStateShift) | (UInt32)state << NextStateShift);
    }

    // Starting to play sets the game controller's timer to a quarter of a second, when it runs out the level's title card and, without a
    // save file picked, the autosave notice come up (FUN_00174a90)
    public void SkipStartNotices()
    {
        var controller = Pine.ReadUInt32(Release.GameControllerPointer);
        if (controller != 0)
        {
            Pine.WriteUInt32(controller + Release.StartTimerOffset, 0);
        }
    }

    /// <summary>
    /// The game's paths of the chunks it has loaded, lower case (levels\earth\hub\beach)
    /// </summary>
    public IReadOnlyList<string> LoadedChunks()
    {
        var chunks = new List<string>();
        var manager = Pine.ReadUInt32(Release.ChunkManagerPointer);
        if (manager == 0)
        {
            return chunks;
        }

        var count = Math.Min(Pine.ReadUInt32(manager) & 0xffff, MaxChunks);
        for (var slot = 0u; slot < count; slot++)
        {
            var meta = Pine.ReadUInt32(manager + 4 + slot * 4);
            if (meta == 0)
            {
                continue;
            }

            var text = Pine.ReadUInt32(meta);
            var length = (Int32)Pine.ReadUInt32(meta + 4);
            if (text != 0 && length is > 0 and < 256)
            {
                chunks.Add(Pine.ReadString(text, length));
            }
        }

        return chunks;
    }
}
