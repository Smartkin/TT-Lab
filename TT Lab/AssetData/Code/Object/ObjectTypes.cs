using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetData.Code.Object;

/// <summary>
/// What the game makes of an object of each type and needs from the object and its instances (the decomp's instancefactory.cpp:
/// MakeTypeNode, MakeAgentObjectNode, GameFactoryStandIn; properties.cpp: PropertyHolder::CopyFrom, PropertyExtras::Construct)
/// </summary>
public static class ObjectTypes
{
    // What the game puts past the values the class of an object's type keeps: 7 words it allocates with no check (PropertyExtras)
    public const int MaxExtraProperties = 7;
    // Every instance's second integer is how far it's seen before its updates get thinned out (0 always, 255 never), the factory reads
    // it from the instance's list with no check (GameFactoryStandIn)
    public const int NearDistanceProperty = 1;
    // A character's first integer is the playable character it is (CharacterKindProperty)
    public const int CharacterKindProperty = 0;
    public const int CharacterCrash = 0;
    public const int CharacterNone = 4;
    public const int CharacterMecha = 5;

    public const Byte PlainSubType = 1;
    public const Byte CustomPickupWithoutPropertiesSubType = 16;
    public const Byte CustomPickupSubType = 17;
    // The tools gave every projectile its code model's kind (CodeModel::KindProjectile), nothing reads it
    public const Byte ProjectileSubType = 18;

    /// <summary>
    /// What the class of an object's type keeps of an instance's properties (the property holders' counts), whether its object node has
    /// waypoints, and what an object or instance of the type that lacks values gets: the game's most common values for the type
    /// </summary>
    public sealed record TypeRules(int Tagged, int Floats, int Ints, bool HasWaypoints, UInt32[] TaggedValues, Single[] FloatValues, Int32[] IntValues,
        Enums.InstanceState State);

    // Every Crash instance of the game's levels has these
    public static readonly Enums.InstanceState CrashState = (Enums.InstanceState)0x7D2E;
    public static readonly UInt32[] CrashTaggedValues = [65536, 131072, 131072, 364088, 109226, 16384, 262144, 262144, 0];
    public static readonly Single[] CrashFloats =
    [
        1.0f, 50.0f, 5.2f, 15.0f, 50.0f, 0.0f, 2.5f, 9.0f, 0.0f, 10.0f, 0.4f, 0.15f, 0.15f, 0.5f, 1.0f, 8.0f,
        13.0f, 37.556f, 57.874f, 8.0f, 16.0f, 64.0f, 72.951f, 11.0f, 5.0f, 10.0f, 14.938f, 0.05f, 0.4f, 0.05f, 0.05f, 0.4f,
        10.0f, 400.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.75f, 0.1f, 0.1f, 0.1f, 18.0f, 0.15f, 0.2f, 0.1f,
        0.3f, 0.3f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f
    ];
    public static readonly Int32[] CrashIntegers = [CharacterCrash, 255, 2];

    private static readonly Dictionary<ITwinObject.ObjectType, TypeRules> Rules = new()
    {
        // A character made of another object is the playable character none, which no controls drive
        [ITwinObject.ObjectType.Character] = new(9, 56, 3, true, CrashTaggedValues, CrashFloats, [CharacterNone, 255, 2], CrashState),
        [ITwinObject.ObjectType.Pickup] = new(0, 1, 2, false, [], [1.0f], [0, 0], (Enums.InstanceState)0x10E),
        [ITwinObject.ObjectType.Crate] = new(0, 3, 2, false, [], [1.0f, 50.0f, 0.0f], [0, 0], (Enums.InstanceState)0x811E),
        [ITwinObject.ObjectType.Creature] = new(1, 6, 3, true, [0x10000], [1.0f, 25.0f, 3.0f, 15.0f, 100.0f, 0.0f], [0, 255, 10], (Enums.InstanceState)0x832E),
        [ITwinObject.ObjectType.GenericObject] = new(0, 1, 2, true, [], [1.0f], [0, 255], (Enums.InstanceState)0x7D36),
        [ITwinObject.ObjectType.Grabbable] = new(1, 4, 2, true, [0], [1.0f, 1.0f, 0.1f, 0.0f], [1, 255], (Enums.InstanceState)0x8104),
        // No object of the game is a pay gate, it's as solid as a generic object
        [ITwinObject.ObjectType.PayGate] = new(0, 1, 3, false, [], [1.0f], [0, 255, 0], (Enums.InstanceState)0x7D36),
        [ITwinObject.ObjectType.Graple] = new(0, 18, 2, false, [], [1.0f, ..Enumerable.Repeat(0.0f, 17)], [0, 255], (Enums.InstanceState)0x7D00),
        [ITwinObject.ObjectType.Projectile] = new(0, 1, 2, false, [], [1.0f], [0, 255], (Enums.InstanceState)0x306),
    };

    // The game makes no agent for anything past the projectiles and reads past nothing (MakeTypeNode), TT Lab keeps their values
    public static TypeRules? Of(ITwinObject.ObjectType type) => Rules.GetValueOrDefault(type);

    public static IReadOnlyList<NamedChoice> PlayableCharacters { get; } =
    [
        new(CharacterCrash, "Crash", "Played as Crash"),
        new(1, "Cortex", "Played as Cortex"),
        new(2, "Tall Crash", "A Crash 2 units high without probes"),
        new(3, "Nina", "Played as Nina"),
        new(CharacterNone, "None", "No controls, look or procedural joints drive it, the game's front end has one"),
        new(CharacterMecha, "Mecha-Bandicoot", "Played as the Mecha-Bandicoot"),
    ];

    // The exit points and joint IDs the character code reads for each kind with no check: exit points 0 to 10 (the Mecha-Bandicoot's
    // probes up to 16), the look's joints 0 to 4 and the procedural joints' (Crash's up to 28, Cortex's 27, the tall Crash's and Nina's
    // 11, the Mecha-Bandicoot's look 18). Every character of the game has 11 exit points and 29 joint IDs, the Mecha-Bandicoot 18 and 33
    private static readonly (int ExitPoints, int JointIds)[] CharacterMinimums = [(11, 29), (11, 28), (11, 12), (11, 12), (11, 0), (17, 19)];

    public static bool IsPlayableCharacter(int kind) => kind >= 0 && kind < CharacterMinimums.Length;

    public static (int ExitPoints, int JointIds) CharacterNeeds(int kind) => CharacterMinimums[kind];

    // What a character of the kind gets when it's made of another object: the game's characters' counts
    public static (Byte ExitPoints, Byte JointIds) CharacterCounts(int kind) => kind == CharacterMecha ? ((Byte)18, (Byte)33) : ((Byte)11, (Byte)29);

    private static readonly NamedChoice NoSubType = new(PlainSubType, "None", "What the tools gave every object but the custom pickups and the projectiles, only pickups read it");

    private static readonly IReadOnlyList<NamedChoice> PickupSubTypes =
    [
        new(PlainSubType, "Scripted", "Runs its own behaviours like other objects, the second one while the player is close (most of the game's pickups)"),
        new(CustomPickupWithoutPropertiesSubType, "Custom, no properties",
            "Driven like a custom pickup, made without its instance's properties: hidden and without collision until its scripts change that, its custom pickup only set " +
            "when a script spawns it with a subtype. No object of the game has it"),
        new(CustomPickupSubType, "Custom",
            "Driven by the custom pickup its first integer picks instead of behaviours (the red wumpa): spins and bobs, flies to the player within 1.5 units and is " +
            "collected within 0.5. A pickup code model has to have set that pickup up, the game reads an empty one"),
    ];

    private static readonly IReadOnlyList<NamedChoice> ProjectileSubTypes =
    [
        NoSubType,
        new(ProjectileSubType, "Projectile", "What the tools gave every projectile (a projectile code model's kind), nothing reads it"),
    ];

    /// <summary>
    /// The sub types the game gives a meaning to for an object of the type, only pickups read it (16 and 17)
    /// </summary>
    public static IReadOnlyList<NamedChoice> SubTypesOf(ITwinObject.ObjectType type) => type switch
    {
        ITwinObject.ObjectType.Pickup => PickupSubTypes,
        ITwinObject.ObjectType.Projectile => ProjectileSubTypes,
        _ => [NoSubType],
    };

    public static NamedChoice FindSubType(ITwinObject.ObjectType type, int value)
    {
        return SubTypesOf(type).FirstOrDefault(choice => choice.Value == value)
               ?? (IsCustomPickup(value)
                   ? new NamedChoice(value, "Pickups only", "Pins the instance's collision where it's made and writes a pickup's timer into its node, which on a projectile is its state")
                   : new NamedChoice(value, "Not read", "Only pickups read the sub type, 16 and 17"));
    }

    // Only a pickup gets the custom pickups' node for them, any other object (but a character) still gets its collision pinned and the
    // timer written into its node (GameFactoryStandIn): its ObjectNode's movement, a projectile's state
    public static bool AllowsSubType(ITwinObject.ObjectType type, int value) => !IsCustomPickup(value) || type == ITwinObject.ObjectType.Pickup;

    public static Byte DefaultSubTypeOf(ITwinObject.ObjectType type) => type == ITwinObject.ObjectType.Projectile ? ProjectileSubType : PlainSubType;

    private static bool IsCustomPickup(int value) => value is CustomPickupWithoutPropertiesSubType or CustomPickupSubType;

    /// <summary>
    /// What the game can't take of property lists for an instance of the type, null when it takes them. The class keeps its share of
    /// each kind and puts what's past it aside, 7 words with no check, counting each kind's difference in a byte: one kind past its
    /// share while another is short goes round to hundreds of values it copies over its memory
    /// </summary>
    public static string? PropertyProblem(ITwinObject.ObjectType type, int tagged, int floats, int ints)
    {
        if (Of(type) is not { } rules)
        {
            return null;
        }

        var over = tagged > rules.Tagged || floats > rules.Floats || ints > rules.Ints;
        var under = tagged < rules.Tagged || floats < rules.Floats || ints < rules.Ints;
        if (over && under)
        {
            return $"has more of some properties and fewer of others than the {rules.Tagged} tagged values, {rules.Floats} floats and {rules.Ints} integers a {type} keeps, " +
                   "the game counts what it puts aside as the difference of each, which goes round to hundreds for the short ones and is written over its memory";
        }

        var extras = Math.Max(0, tagged - rules.Tagged) + Math.Max(0, floats - rules.Floats) + Math.Max(0, ints - rules.Ints);
        return extras > MaxExtraProperties
            ? $"has {extras} property values past the {rules.Tagged} tagged values, {rules.Floats} floats and {rules.Ints} integers a {type} keeps, " +
              $"the game keeps {MaxExtraProperties} of them and writes the rest over its memory"
            : null;
    }

    /// <summary>
    /// Property lists as an object or instance of the type needs them: every kind filled up to what the type's class keeps with the
    /// source's values (an instance's object's) and else the type's (the game reads what its heap had for the missing ones), and what's
    /// past them dropped when the game couldn't put it aside
    /// </summary>
    public static (List<UInt32> Tagged, List<Single> Floats, List<Int32> Ints) Fit(ITwinObject.ObjectType type, IReadOnlyList<UInt32> tagged,
        IReadOnlyList<Single> floats, IReadOnlyList<Int32> ints, IReadOnlyList<UInt32>? sourceTagged = null, IReadOnlyList<Single>? sourceFloats = null,
        IReadOnlyList<Int32>? sourceInts = null)
    {
        var rules = Of(type);
        if (rules == null)
        {
            return ([..tagged], [..floats], [..ints]);
        }

        var fitTagged = FillUp(tagged, rules.Tagged, sourceTagged, rules.TaggedValues, 0U);
        var fitFloats = FillUp(floats, rules.Floats, sourceFloats, rules.FloatValues, 0.0f);
        var fitInts = FillUp(ints, rules.Ints, sourceInts, rules.IntValues, 0);
        if (PropertyProblem(type, fitTagged.Count, fitFloats.Count, fitInts.Count) != null)
        {
            fitTagged.RemoveRange(rules.Tagged, fitTagged.Count - rules.Tagged);
            fitFloats.RemoveRange(rules.Floats, fitFloats.Count - rules.Floats);
            fitInts.RemoveRange(rules.Ints, fitInts.Count - rules.Ints);
        }

        return (fitTagged, fitFloats, fitInts);
    }

    /// <summary>
    /// The list's node made to hold the values: its elements past them taken out, the others set and the missing ones put in, so the
    /// history puts it back
    /// </summary>
    public static void SetElements<T>(PropertyNode? list, IReadOnlyList<T> values) where T : notnull
    {
        if (list == null)
        {
            return;
        }

        while (list.Children.Count > values.Count)
        {
            list.RemoveElement(list.Children[^1]);
        }

        for (var index = 0; index < values.Count; index++)
        {
            if (index < list.Children.Count)
            {
                list.Children[index].SetValue(values[index]);
            }
            else
            {
                list.InsertElement(index, values[index]);
            }
        }
    }

    private static List<T> FillUp<T>(IReadOnlyList<T> values, int count, IReadOnlyList<T>? source, IReadOnlyList<T> defaults, T fallback)
    {
        var filled = new List<T>(values);
        for (var index = filled.Count; index < count; index++)
        {
            filled.Add(source != null && index < source.Count ? source[index] : index < defaults.Count ? defaults[index] : fallback);
        }

        return filled;
    }
}
