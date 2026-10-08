using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Data.Converters;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

/// <summary>
/// Captions of enum members whose names say something else than what the game does with them. The names stay until a version of
/// TT Lab that renames them
/// </summary>
public static class EnumCaptions
{
    private static readonly Dictionary<(Type Type, string Member), string> Captions = new()
    {
        // Nina clings to walls of it and skid trails are laid on it (CharacterAgent::ClingToWall, LaySkidMark), no footprints
        [(typeof(Enums.SurfaceCollisionFlags), nameof(Enums.SurfaceCollisionFlags.LeavesFootprints))] = "Soft",
        // Only sets a context bit nothing reads (BaseFactoryStandIn), scripts can test it like any bit
        [(typeof(Enums.InstanceState), nameof(Enums.InstanceState.PlayableCharacterCanMoveAlong))] = "SoftFlag4",
        // Neither the path finder nor a condition reads them
        [(typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.Flag0))] = "Unused0",
        [(typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.Flag1))] = "Unused1",
        // What EdgeNeedsFlying tests
        [(typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.Flag5))] = "NeedsFlying",
        // The scroll settings move the coordinate on from its phase and wrap it, or sway it by the sine or cosine of it (UpdateShader)
        [(typeof(Twinsanity.TwinsanityInterchange.Common.TwinShader.XScrollFormula), nameof(Twinsanity.TwinsanityInterchange.Common.TwinShader.XScrollFormula.Linear))] = "Scroll",
        [(typeof(Twinsanity.TwinsanityInterchange.Common.TwinShader.XScrollFormula), nameof(Twinsanity.TwinsanityInterchange.Common.TwinShader.XScrollFormula.LinearPlus_1))] = "SineSway",
        [(typeof(Twinsanity.TwinsanityInterchange.Common.TwinShader.XScrollFormula), nameof(Twinsanity.TwinsanityInterchange.Common.TwinShader.XScrollFormula.LinearPlus_2))] = "CosineSway",
        [(typeof(Twinsanity.TwinsanityInterchange.Common.TwinShader.YScrollFormula), nameof(Twinsanity.TwinsanityInterchange.Common.TwinShader.YScrollFormula.Linear))] = "Scroll",
        [(typeof(Twinsanity.TwinsanityInterchange.Common.TwinShader.YScrollFormula), nameof(Twinsanity.TwinsanityInterchange.Common.TwinShader.YScrollFormula.LinearPlus_1))] = "SineSway",
        [(typeof(Twinsanity.TwinsanityInterchange.Common.TwinShader.YScrollFormula), nameof(Twinsanity.TwinsanityInterchange.Common.TwinShader.YScrollFormula.LinearPlus_2))] = "CosineSway",
    };

    // Bits the game never reads, left out of the flags' check boxes: they keep what they have
    private static readonly HashSet<(Type Type, string Member)> NeverRead =
    [
        // Bit 1 only lets a switch back blend that nothing starts, bit 14 nothing reads (FollowCamera taking a camera)
        (typeof(Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera.CameraFlags), nameof(Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera.CameraFlags.Unused1)),
        (typeof(Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera.CameraFlags), nameof(Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera.CameraFlags.Unused14)),
        (typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.Flag0)),
        (typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.Flag1)),
    ];

    static EnumCaptions()
    {
        // Set on every surface and never read (the decomp's collision.cpp)
        for (var bit = 12; bit < 20; bit++)
        {
            NeverRead.Add((typeof(Enums.SurfaceCollisionFlags), Enum.GetName((Enums.SurfaceCollisionFlags)(1U << bit))!));
        }

        // Bits 19-31 are the scripts' own, which they test with SoftFlagSet(n) (no engine code reads them); their names are numbered
        // from 4
        for (var bit = 19; bit < 32; bit++)
        {
            Captions[(typeof(Enums.InstanceState), Enum.GetName((Enums.InstanceState)(1U << bit))!)] = $"SoftFlag{bit}";
        }

        // Kinds of hit nothing in retail sends and no condition tests
        foreach (var name in Enum.GetNames<Enums.ContactKinds>().Where(name => name.StartsWith("Unused")))
        {
            NeverRead.Add((typeof(Enums.ContactKinds), name));
        }
    }

    public static string Of(Type type, string member)
    {
        return Captions.TryGetValue((type, member), out var caption) ? caption : member;
    }

    /// <summary>
    /// What the game does with an enum member, its <see cref="DescriptionAttribute"/>
    /// </summary>
    public static string? HintOf(Type type, string member) => type.GetField(member)?.GetCustomAttribute<DescriptionAttribute>()?.Description;

    /// <summary>
    /// Whether the game never reads the flag, its check box is left out
    /// </summary>
    public static bool IsNeverRead(Type type, string member) => NeverRead.Contains((type, member));

    /// <summary>
    /// Shows an enum value by its caption
    /// </summary>
    public static IValueConverter Converter { get; } = new FuncValueConverter<object?, string?>(value => value is Enum member ? Of(member.GetType(), member.ToString()) : value?.ToString());

    /// <summary>
    /// What the game does with an enum value, for its choice's tooltip
    /// </summary>
    public static IValueConverter HintConverter { get; } = new FuncValueConverter<object?, string?>(value => value is Enum member ? HintOf(member.GetType(), member.ToString()) : null);
}
