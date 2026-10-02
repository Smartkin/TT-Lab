using System;
using System.Collections.Generic;
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
        /// All existing types of the object
        /// </summary>
        enum ObjectType
        {
            Character,
            Pickup,
            Crate,
            Creature,
            /// <summary>
            /// aka Furniture, internal name by Twinsanity's engine
            /// </summary>
            GenericObject,
            /// <summary>
            /// aka ChiChiGrass
            /// </summary>
            Grabbable,
            /// <summary>
            /// Unused by all the objects included in the game
            /// </summary>
            PayGate,
            /// <summary>
            /// aka Nina's Hand/Foofie in Evolution
            /// </summary>
            Graple,
            Projectile
        }
        /// <summary>
        /// Object's type
        /// </summary>
        ObjectType Type { get; set; }
        /// <summary>
        /// Unknown type value. Used when creating object's instance
        /// </summary>
        /// <summary>
        /// Bits 12-19 of the header, 1 on nearly every object, 16 and 17 pick a pickup's node
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
