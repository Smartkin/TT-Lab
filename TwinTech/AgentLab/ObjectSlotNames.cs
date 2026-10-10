using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab;

// The names [UseObjectSlot(...)] takes: the slots' own, and the ones slots went by before what starts them was known, which old scripts
// still compile with (completion offers the slots' own only)
internal static class ObjectSlotNames
{
    public static readonly Dictionary<string, ITwinBehaviourState.ObjectBehaviourSlots> OldNames = new()
    {
        ["OnPhysicsCollision"] = ITwinBehaviourState.ObjectBehaviourSlots.Slot_9,
        ["OnUnknownCollision"] = ITwinBehaviourState.ObjectBehaviourSlots.OnGettingThrownAttacked,
    };

    // What starts the slots every type has (the decomp's AgentBehaviourSlot)
    private static readonly string[] AgentEvents =
    {
        "the instance started without a spawn script of its own: made, reset, spawned or restarted",
        "another instance's script triggered the instances linked to it (TriggerLinkedObjects), and a pickup the player came near",
        "a contact message, and some types' hard physics hits",
        "walked into by a playable character, and some types' physics hits",
        "hit from below by a playable character",
        "landed on by a playable character",
        "spun into by a playable character",
        "body slammed by a playable character, or touched by the second of two tied characters",
        "slid into by a playable character",
        "nothing in the game starts it",
        "touched by a playable character the other one threw",
    };

    public static ITwinBehaviourState.ObjectBehaviourSlots Parse(string name) =>
        OldNames.TryGetValue(name, out var slot) ? slot : Enum.Parse<ITwinBehaviourState.ObjectBehaviourSlots>(name);

    // What the slot of the name is, none for a name of no slot
    public static string Describe(string name)
    {
        var isOld = OldNames.TryGetValue(name, out var slot);
        if (!isOld && !(Enum.IsDefined(typeof(ITwinBehaviourState.ObjectBehaviourSlots), name) && Enum.TryParse(name, out slot)))
        {
            return null;
        }

        var index = (int)slot;
        var what = index < AgentEvents.Length
            ? AgentEvents[index]
            : "the type's own event (the playable characters', crates' and the graple's) or one only scripts start, see the object's Behaviour Slots";
        return isOld ? $"The old name of {slot}, object slot {index}: {what}" : $"Object slot {index}: {what}";
    }
}
