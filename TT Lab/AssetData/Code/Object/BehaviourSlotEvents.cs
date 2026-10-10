using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetData.Code.Object;

/// <summary>
/// What the game starts an object's behaviour slots with (the decomp's callers of RunAgentEvent, checked against the agents' vtables
/// and every object's slots on the NTSC disc). Slots 0 to 10 are every agent's events (agents.h's AgentBehaviourSlot), how they come
/// depends on the type's agent, and nothing starts slot 9. Past them only the playable characters (characters.h's CharacterEvent, the
/// claw's, the gun's and the vehicles'), crates and the graple have events of their own. Every other slot only runs when a script
/// starts it (RunScriptSlot, RunSlotBehaviourOnLinked, a state's UseObjectSlot), the object's scripts decide what it's for
/// </summary>
public static class BehaviourSlotEvents
{
    public sealed record SlotEvent(string Name, string Hint);

    // An attack reaches the scripts while the instance takes hits and its part isn't invulnerable (its state's bits), and while the part
    // takes the attack's kind: all of them when it's made, scripts switch the walk into, landing on, from below, spin, slam and slide
    // (SetAttacksTaken). Only the playable characters attack, never a graple
    private const string Hits = ", while the instance's state has ReceiveOnTriggerSignals and not CanAlwaysDamageCharacter";
    private const string Switchable = " and its scripts haven't turned the attack off (SetObjectFlags587)";
    private const string Contacts = "a surface or water it touches as a physics body, or a script's CreateDamage, DamageOriginator or HitInstancesInBoxes";

    public static SlotEvent? Of(ITwinObject.ObjectType type, int slot)
    {
        if (slot <= 10)
        {
            return AgentEvent(type, slot);
        }

        return type switch
        {
            ITwinObject.ObjectType.Character => CharacterEvents.GetValueOrDefault(slot),
            ITwinObject.ObjectType.Crate => CrateEvents.GetValueOrDefault(slot),
            ITwinObject.ObjectType.Graple => GrapleEvents.GetValueOrDefault(slot),
            _ => null
        };
    }

    private static SlotEvent? AgentEvent(ITwinObject.ObjectType type, int slot)
    {
        var attacked = type != ITwinObject.ObjectType.Graple;
        var crate = type == ITwinObject.ObjectType.Crate;
        return slot switch
        {
            0 => new SlotEvent("Spawned", "Runs whenever an instance of the object starts: made, reset, spawned by a script or restarted by " +
                                          "RestartDefaultBehaviour. An instance with a spawn script runs that instead (scripts: OnSpawn)"),
            1 => new SlotEvent("Triggered", Triggered(type) + " (scripts: OnTrigger)"),
            2 => Damaged(type),
            3 when attacked => new SlotEvent("Touched", "Walked into by a playable character" + Hits + Switchable + TouchedByPhysics(type) + " (scripts: OnTouch)"),
            4 when attacked => new SlotEvent("Headbutted", "Hit from below by a playable character, also by a spinning one (with Spin Attacked)" + Hits + Switchable +
                                                           (crate ? ". Also a physics hit of an impulse over about 2.2 pushing it up" : "") + " (scripts: OnHeadbutt)"),
            5 when attacked => new SlotEvent("Landed On", "Landed on by a playable character, also by a spinning one (with Spin Attacked)" + Hits + Switchable +
                                                          (crate ? ". Also a physics hit of an impulse over about 2.2 pushing it down, and a falling crate landing on it while its " +
                                                                   "state has SolidToBodySlam" : "") + " (scripts: OnLand)"),
            6 when attacked => new SlotEvent("Spin Attacked", "Spun into by a playable character, or landed on or hit from below by a spinning one" + Hits + Switchable +
                                                              " (scripts: OnGettingSpinAttacked)"),
            7 when attacked => new SlotEvent("Body Slammed", "Body slammed by a playable character, or touched by the second of two tied characters (Crash and Cortex tied " +
                                                             "together)" + Hits + Switchable + (crate ? ". Also a falling crate landing on it while its state doesn't have " +
                                                                                                        "SolidToBodySlam" : "") + " (scripts: OnGettingBodyslamAttacked)"),
            8 when attacked => new SlotEvent("Slide Attacked", "Slid into by a playable character" + Hits + Switchable + " (scripts: OnGettingSlideAttacked)"),
            10 when attacked => new SlotEvent("Hit by a Thrown Character", "Touched by a playable character the other one threw, from a spin or a jump" + Hits +
                                                                           " (scripts: OnGettingThrownAttacked)"),
            _ => null
        };
    }

    private static string Triggered(ITwinObject.ObjectType type) => type switch
    {
        ITwinObject.ObjectType.GenericObject => "Started on the instances linked to another one whose script runs TriggerLinkedObjects, OpenAllLinkedFurniture or CloseAllLinkedFurniture",
        ITwinObject.ObjectType.Pickup => "Started on the instances linked to another one whose script runs TriggerLinkedObjects, and while the player is within 1.5 units of " +
                                         "the pickup unless it flies to the player (sub types 16 and 17, a pickup code model)",
        _ => "Started on the instances linked to another one whose script runs TriggerLinkedObjects"
    };

    private static SlotEvent? Damaged(ITwinObject.ObjectType type)
    {
        var hint = type switch
        {
            ITwinObject.ObjectType.Character => "Only a physics hit of an impulse over about 14 (after Touched): contact messages take the character's hit points instead and play " +
                                                "its knock-back or death events (slots 80 to 83 and 67)",
            ITwinObject.ObjectType.Creature => $"A contact message, whatever its damage, while the instance's state doesn't have CanAlwaysDamageCharacter: {Contacts}. Also a " +
                                               "physics hit of an impulse over about 14 (after Touched)",
            ITwinObject.ObjectType.Pickup => $"A contact message, whatever its damage: {Contacts}",
            ITwinObject.ObjectType.Crate => $"A contact message that does damage: {Contacts}. Also a physics hit of an impulse over 5, which breaks the crate unless its state has " +
                                            "SolidToSlide",
            // Projectiles' agents drop contact messages and have no physics hits
            ITwinObject.ObjectType.Projectile => null,
            _ => $"A contact message that does damage: {Contacts}"
        };
        return hint == null ? null : new SlotEvent("Damaged", hint + ". The contact is kept for the scripts' conditions (scripts: OnDamage)");
    }

    private static string TouchedByPhysics(ITwinObject.ObjectType type) => type switch
    {
        ITwinObject.ObjectType.Crate => ". Also a physics hit of an impulse up to 5 that isn't a hard one (over about 2.2) from above or below",
        ITwinObject.ObjectType.Creature or ITwinObject.ObjectType.Character => ". Also a physics hit of an impulse over about 3.2",
        ITwinObject.ObjectType.GenericObject => ". Also any physics hit while walking into it reaches it",
        _ => ""
    };

    private static readonly Dictionary<int, SlotEvent> CrateEvents = new()
    {
        [11] = new SlotEvent("Falling", "Told to fall: dropped off an instance's exit point, TriggerBalancedCrateFalling while it neither rests on the ground nor is in the air, " +
                                        "or its support taken away (UnsupportOverFocus)"),
        [12] = new SlotEvent("Landed", "Landed after it was launched or fell"),
        [13] = new SlotEvent("Nitro Triggered", "A script in its chunk ran TriggerAllNitroCrates, which tells every crate of the chunk (128 at most)"),
        [15] = new SlotEvent("Checkpoint Released", "It was the checkpoint (a checkpoint crate) and isn't any more: another checkpoint took over or the checkpoint was cleared"),
    };

    // Told by Nina's claw (claw.cpp's TellGraple)
    private static readonly Dictionary<int, SlotEvent> GrapleEvents = new()
    {
        [11] = new SlotEvent("Claw Thrown", "Nina's claw left her hand toward its target"),
        [12] = new SlotEvent("Claw Hit", "The claw hit a hook or a point to leap to"),
        [13] = new SlotEvent("Leap", "Nina leapt toward the point the claw hit"),
        [14] = new SlotEvent("Claw Back", "Nina hangs from the hook, or the claw came back"),
        [15] = new SlotEvent("Swipe Out", "A swipe (nothing to grab) going out"),
        [16] = new SlotEvent("Swipe Back", "The swipe coming back"),
        [17] = new SlotEvent("Swipe Done", "The swipe ended"),
    };

    private static readonly Dictionary<int, SlotEvent> CharacterEvents = new()
    {
        [11] = new SlotEvent("Long Drop", "Fell 20 units below where its fall started (in the Rollerbrawl and the wrestle a fast fall, or squashed for 3 seconds): the game's " +
                                          "characters die"),
        [12] = new SlotEvent("Idle", "Stands still, tied characters too, and when a script's MakeCharactersIdle makes it (forced)"),
        [13] = new SlotEvent("Shuffle Feet", "Stands with the stick pointing well off its facing, turning on the spot"),
        [14] = new SlotEvent("Walk", "The stick pushed past the walk"),
        [15] = new SlotEvent("Run", "The stick pushed past the run, for a character with a run speed"),
        [16] = new SlotEvent("Strafe Left", "Started strafing to the left"),
        [17] = new SlotEvent("Strafe Right", "Started strafing to the right"),
        [18] = new SlotEvent("Spin", "Started spinning, tied characters both"),
        [19] = new SlotEvent("Spin Recovery", "A spin ended"),
        [22] = new SlotEvent("Knee Slide Jump", "Jumped out of a knee slide"),
        [23] = new SlotEvent("Standing Jump", "Jumped standing, or launched (thrown by the other character, a script's launch)"),
        [24] = new SlotEvent("Running Jump", "Jumped while running"),
        [25] = new SlotEvent("Double Jump", "Jumped again in the air"),
        [26] = new SlotEvent("Maxed Double Jump", "Jumped again close to the top of the jump"),
        [27] = new SlotEvent("Knee Drop Hang", "Hangs in the air before a knee drop"),
        [28] = new SlotEvent("Knee Drop", "Dropping onto the ground knee first (a body slam)"),
        [29] = new SlotEvent("Flying Kick", "Kicked in the air"),
        [31] = new SlotEvent("Radial Blast Hang", "Hangs in the air before a radial blast"),
        [32] = new SlotEvent("Radial Blast", "Jumped with a radial blast"),
        [33] = new SlotEvent("Short Fall", "Started falling"),
        [34] = new SlotEvent("Fell Far", "Fell 10 units below where its fall started, and every frame Nina clings to a wall, sliding down it"),
        [37] = new SlotEvent("Flying Kick Fall", "Falling after a flying kick"),
        [39] = new SlotEvent("Land", "Landed standing still"),
        [40] = new SlotEvent("Land Moving", "Landed while moving"),
        [41] = new SlotEvent("Land From Far", "Landed after falling far, and turned back by a chunk link whose chunk isn't loaded"),
        [42] = new SlotEvent("Knee Drop Land", "Landed from a knee drop (a body slam)"),
        [46] = new SlotEvent("Stand to Crouch", "Crouched"),
        [47] = new SlotEvent("Crouch to Crawl", "Started crawling"),
        [48] = new SlotEvent("Crawl to Crouch", "Stopped crawling"),
        [49] = new SlotEvent("Crouch to Stand", "Stood up from a crouch"),
        [50] = new SlotEvent("Crawl to Stand", "Stood up from crawling"),
        [51] = new SlotEvent("Run to Knee Slide", "Started a knee slide from a run"),
        [53] = new SlotEvent("Knee Slide to Crouch", "A knee slide ended crouching"),
        [54] = new SlotEvent("Knee Slide to Stand", "A knee slide ended standing"),
        [55] = new SlotEvent("Claw Grab", "Nina threw her claw at a target"),
        [56] = new SlotEvent("Claw Target Lost", "The claw's target got lost while it reached"),
        [57] = new SlotEvent("Claw Hook Hit", "The claw hit a hook"),
        [58] = new SlotEvent("Claw Point Hit", "The claw hit a point to leap to"),
        [60] = new SlotEvent("Claw Swipe", "Nina swiped with the claw, with nothing to grab"),
        [61] = new SlotEvent("Claw Leap", "Nina leapt toward the claw's point"),
        [62] = new SlotEvent("Claw Hang", "Nina hangs from a hook"),
        [63] = new SlotEvent("Claw Drop", "Nina dropped off the hook"),
        [64] = new SlotEvent("Claw Jump Off", "Nina jumped off the hook"),
        [65] = new SlotEvent("Tied", "Crash and Cortex got tied together, both"),
        [66] = new SlotEvent("Untied", "Crash and Cortex got untied, both"),
        [67] = new SlotEvent("Died", "Lost its last hit point"),
        [68] = new SlotEvent("Tied Body Slam", "The tied characters' body slam, both"),
        [69] = new SlotEvent("Thrown From Spin", "One character threw the other out of a spin, both"),
        [70] = new SlotEvent("Thrown From Jump", "One character threw the other out of a jump, both"),
        [71] = new SlotEvent("Gun Drawn", "Drew the gun"),
        [72] = new SlotEvent("Gun Put Away", "Put the gun away"),
        [73] = new SlotEvent("Gun Shot", "Fired the gun with ammo"),
        [74] = new SlotEvent("Vehicle Taken", "Got into a vehicle (forced)"),
        [75] = new SlotEvent("Vehicle Left", "Got out of a vehicle"),
        [76] = new SlotEvent("Rollerbrawl Stopped", "The Rollerbrawl's ball stopped, both characters"),
        [77] = new SlotEvent("Rollerbrawl Rolling", "The Rollerbrawl's ball rolls again, both characters"),
        [80] = new SlotEvent("Knocked Forward", "Knocked back by a hit pushing within 45 degrees of its facing"),
        [81] = new SlotEvent("Knocked Back", "Knocked back by a hit pushing more than 135 degrees off its facing"),
        [82] = new SlotEvent("Knocked Aside -", "Knocked back by a hit pushing 45 to 135 degrees off its facing, to the negative side"),
        [83] = new SlotEvent("Knocked Aside +", "Knocked back by a hit pushing 45 to 135 degrees off its facing, to the positive side"),
        [86] = new SlotEvent("Skate Riding", "Riding the Humiliskate straight, the rider and the board"),
        [87] = new SlotEvent("Skate Riding Left", "Riding the Humiliskate leaning left, the rider and the board"),
        [88] = new SlotEvent("Skate Riding Right", "Riding the Humiliskate leaning right, the rider and the board"),
        [89] = new SlotEvent("Skate Riding Backwards", "Riding the Humiliskate backwards, the rider and the board"),
        [90] = new SlotEvent("Skate Backwards Left", "Riding the Humiliskate backwards leaning left, the rider and the board"),
        [91] = new SlotEvent("Skate Backwards Right", "Riding the Humiliskate backwards leaning right, the rider and the board"),
        [92] = new SlotEvent("Skate Crouched", "The Humiliskate's rider crouches"),
        [93] = new SlotEvent("Skate Crouched Right", "The Humiliskate's rider crouches leaning right"),
        [94] = new SlotEvent("Skate Crouched Left", "The Humiliskate's rider crouches leaning left"),
        [95] = new SlotEvent("Skate Trick Landed", "The Humiliskate's rider landed a trick"),
        [96] = new SlotEvent("Skate Half Spin", "A half spin trick started, the rider and the board"),
        [97] = new SlotEvent("Skate Flip", "A flip trick started, the rider and the board"),
        [98] = new SlotEvent("Skate Grinding", "Grinding a rail, the rider and the board"),
        [99] = new SlotEvent("Skate Grinding Backwards", "Grinding a rail backwards, the rider and the board"),
        [100] = new SlotEvent("Tied Standing Jump", "The second of the tied characters, when the first jumps standing"),
        [101] = new SlotEvent("Tied Running Jump", "The second of the tied characters, when the first jumps running"),
        [103] = new SlotEvent("Wrestle Pins", "Pinned the creature it wrestles"),
        [105] = new SlotEvent("Wrestle Pinned", "Got pinned by the creature it wrestles"),
        [106] = new SlotEvent("Gun Charged", "Charged the gun"),
        [107] = new SlotEvent("Gun Out of Ammo", "Fired the gun without ammo"),
        [108] = new SlotEvent("Radial Blast Failed", "Tried a radial blast without the ammo it costs"),
        [109] = new SlotEvent("Skate Jump", "Jumped on the Humiliskate, the rider and the board"),
        [110] = new SlotEvent("Skate Crash", "The Humiliskate crashed into something, the rider and the board"),
    };
}
