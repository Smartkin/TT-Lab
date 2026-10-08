using System;
using System.Collections.Generic;
using System.ComponentModel;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code
{
    public interface ITwinObject : ITwinItem
    {
        /// <summary>
        /// Flags determining which resources are present in the object
        /// </summary>
        [Flags]
        enum ResourcesBitfield
        {
            OBJECTS = 1 << 0,
            OGIS = 1 << 1,
            ANIMATIONS = 1 << 2,
            CODE_MODELS = 1 << 3,
            SCRIPTS = 1 << 4,
            /// <summary>
            /// A kind of resource the game never loads or lets go of
            /// </summary>
            UNUSED = 1 << 5,
            SOUNDS = 1 << 6,
        }

        /// <summary>
        /// What the game makes of an object's instances (the decomp's instancefactory.cpp, MakeTypeNode): each type's class keeps its own
        /// share of the instances' properties, and only characters, creatures, generic objects and grabbables follow positions and paths
        /// </summary>
        enum ObjectType
        {
            /// <summary>
            /// A playable character, its instances' first integer picks which (0 Crash to 5 the Mecha-Bandicoot)
            /// </summary>
            [Description("A playable character: its instances' first integer picks which (0 Crash, 1 Cortex, 2 Crash without probes, 3 Nina, 4 none, 5 the Mecha-Bandicoot), a second one of a kind stands in for the first. Needs a model the game animates and the exit points and joint IDs the character code reads (the game's characters have 11 and 29)")]
            Character,
            /// <summary>
            /// Collected by the player, scripted or a custom pickup a pickup code model drives (its sub type)
            /// </summary>
            [Description("Collected by the player: scripted, or a custom pickup a pickup code model drives (its sub type). Follows no positions or paths")]
            Pickup,
            /// <summary>
            /// A crate, its scripts hear the characters' attacks and what lands on it hard
            /// </summary>
            [Description("A crate: its scripts hear the characters' attacks and what lands on it hard, it falls with a gravity of its own. Follows no positions or paths")]
            Crate,
            /// <summary>
            /// Enemies and other characters walking routes, with hit points, falling and snapping to the ground
            /// </summary>
            [Description("Enemies and other characters walking routes: hit points (third integer), falling and snapping to the ground, hit by attacks and the gun")]
            Creature,
            /// <summary>
            /// A scripted prop that stops physics bodies, the game's furniture
            /// </summary>
            [Description("A scripted prop: solid to physics bodies, told when something walks into it, reads none of its own properties")]
            GenericObject,
            /// <summary>
            /// What Nina's claw locks onto: a hook to hang from or a point to leap to
            /// </summary>
            [Description("What Nina's claw locks onto: its first integer 1 makes it a hook to hang from, else it's the count of positions to land on. Needs the target lock's state bit")]
            Grabbable,
            /// <summary>
            /// A wumpa fruit toll gate, no object of the game is one
            /// </summary>
            [Description("A wumpa fruit toll gate, its third integer the fruit it takes. No object of the game is one. Follows no positions or paths")]
            PayGate,
            /// <summary>
            /// Nina's claw rope, spawned on her by her scripts
            /// </summary>
            [Description("Nina's claw rope, spawned on her by her scripts: it moves its model's joints 0 and 1 between her claw and its target. Follows no positions or paths")]
            Graple,
            /// <summary>
            /// Shot by scripts, driven by the projectile code model its first integer picks
            /// </summary>
            [Description("Shot by scripts, driven by the projectile code model its first integer picks instead of behaviours. Follows no positions or paths")]
            Projectile
        }
        /// <summary>
        /// Object's type
        /// </summary>
        ObjectType Type { get; set; }
        /// <summary>
        /// Bits 12-19 of the header, only pickups read it: 16 and 17 make a custom pickup a pickup code model drives, 16 without its instance's
        /// properties (MakeTypeNode, MakeAgentObjectNode). The tools wrote the kind of the code model custom objects had, 17 (a pickup's) on
        /// the red wumpa and 18 (a projectile's) on the projectiles, and 1 on everything else
        /// </summary>
        Byte SubType { get; set; }
        /// <summary>
        /// The amount of react joints that react to camera's movements
        /// </summary>
        Byte ReactJointAmount { get; set; }
        /// <summary>
        /// Amount of exit points(Points of interest)
        /// </summary>
        Byte ExitPointAmount { get; set; }
        /// <summary>
        /// Object's name
        /// </summary>
        String Name { get; set; }
        /// <summary>
        /// Scripts/behaviours that trigger when a certain condition is met
        /// </summary>
        List<TwinObjectTriggerBehaviour> TriggerBehaviours { get; set; }
        /// <summary>
        /// Slotted OGIs/Skeletons
        /// </summary>
        List<UInt16> OGISlots { get; set; }
        /// <summary>
        /// Slotted Animations
        /// </summary>
        List<UInt16> AnimationSlots { get; set; }
        /// <summary>
        /// Slotted scripts/behaviours
        /// </summary>
        List<UInt16> BehaviourSlots { get; set; }
        /// <summary>
        /// Slotted objects
        /// </summary>
        List<UInt16> ObjectSlots { get; set; }
        /// <summary>
        /// Slotted sound effects
        /// </summary>
        List<UInt16> SoundSlots { get; set; }
        /// <summary>
        /// The default state flags for the instance of this object <seealso cref="Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState"/>
        /// </summary>
        Enums.InstanceState InstanceStateFlags { get; set; }
        /// <summary>
        /// The tagged values instances of the object get when the instance factory takes the object's properties instead of the
        /// instance's own (see <see cref="Layout.ITwinInstance.TaggedProperties"/>)
        /// </summary>
        List<UInt32> TaggedProperties { get; set; }
        List<Single> FloatProperties { get; set; }
        List<Int32> IntProperties { get; set; }
        /// <summary>
        /// Objects referenced by this object
        /// </summary>
        List<UInt16> RefObjects { get; set; }
        /// <summary>
        /// OGIs/Skeletons referenced by this object
        /// </summary>
        List<UInt16> RefOGIs { get; set; }
        /// <summary>
        /// Animations referenced by this object
        /// </summary>
        List<UInt16> RefAnimations { get; set; }
        /// <summary>
        /// CodeModels/Command sequences referenced by this object
        /// </summary>
        List<UInt16> RefCodeModels { get; set; }
        /// <summary>
        /// Scripts/Behaviours referenced by this object
        /// </summary>
        List<UInt16> RefBehaviours { get; set; }
        /// <summary>
        /// IDs of the resource kind the game never loads or lets go of (its sixth list, ResourceUnused)
        /// </summary>
        List<UInt16> RefUnused { get; set; }
        /// <summary>
        /// Sound effects referenced by this object
        /// </summary>
        List<UInt16> RefSounds { get; set; }
        /// <summary>
        /// Script/Behaviour pack for this object. Command chain executed when creating an instance of this object
        /// </summary>
        ITwinBehaviourCommandPack BehaviourPack { get; set; }
        /// <summary>
        /// If object has default properties for its instance
        /// </summary>
        bool HasInstanceProperties { get; }
        /// <summary>
        /// If object references any resources
        /// </summary>
        bool ReferencesResources { get; }
    }
}
