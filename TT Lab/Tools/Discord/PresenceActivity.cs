using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;

namespace TT_Lab.Tools.Discord;

// What Discord shows: the first line, the second (none leaves it out), when the timer started counting (none hides it) and the small
// picture of the editor the second line is about (none without one)
public sealed record PresenceActivity(string Details, string? State, DateTime? Start, string? SmallImage = null)
{
    public const string Deciding = "Deciding on what mod to start creating...";
    public const string Making = "In the process of making the greatest mod!";
    public const string OnBreak = "On a creative break...";
    public const string Testing = "Testing the mod...";

    internal static PresenceActivity For(ProjectPhase phase, bool onBreak, bool gameActive, IAsset? activeAsset, DateTime? timerStart) => phase switch
    {
        ProjectPhase.None => new PresenceActivity(Deciding, null, null),
        ProjectPhase.Loading => new PresenceActivity(Making, null, null),
        _ when gameActive => new PresenceActivity(Making, Testing, timerStart),
        _ when onBreak => new PresenceActivity(OnBreak, null, null),
        _ => new PresenceActivity(Making, LineFor(activeAsset), timerStart, LineFor(activeAsset) == null ? null : IconKeyOf(activeAsset!))
    };

    // The Discord application's small pictures are the editors' icons (Media/LabIcons), keyed by their file names in lower case
    internal static string IconKeyOf(IAsset asset) => System.IO.Path.GetFileNameWithoutExtension(asset.IconPath).ToLowerInvariant();

    // A level chunk's resources never get a tab of their own, a package's settings have nothing to say
    internal static string? LineFor(IAsset? asset) => asset switch
    {
        LevelChunk => "Editing chunk...",
        OGI or SaveIcon => "Looking at a model...",
        BehaviourGraph or BehaviourCommandsSequence => "Labbing new behavior...",
        Texture => "Swapping textures...",
        Skydome => "Admiring the Skydome...",
        SoundEffect => "Listening to nice sounds...",
        Font => "Making good fonts...",
        TextFile => "Changing texts...",
        Material => "Changing materials...",
        GameObject => "Editing game objects...",
        PSM or PTC => "Tinkering with pictures...",
        Mesh or InstanceTemplate or CollisionSurface or DefaultParticles => "Changing the game globally...",
        UiSoundLibrary => "Messing with UI sounds...",
        _ => null
    };
}

internal enum ProjectPhase
{
    None,
    // Being created or opened, until its tree is shown
    Loading,
    Ready
}

// What the presence is worked out from, read from TT Lab every tick
internal readonly record struct PresenceInputs(ProjectPhase Phase, DateTime? ReadyAt, IAsset? ActiveAsset, bool GameActive, bool HasFocus);
