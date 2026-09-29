using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    /// <summary>
    /// An instance template of a layout (the startup chunk's crates, pickups and grass): the object it makes an instance of, the
    /// starters it runs and the instance properties. The PAL executable reads templates into a table nothing looks up again, so
    /// every value is the tools' (LoadInstanceTemplate 0x26ddb8, FUN_00263978)
    /// </summary>
    public interface ITwinTemplate : ITwinItem
    {
        String Name { get; set; }
        UInt16 ObjectId { get; set; }
        /// <summary>
        /// The tools' copy of the object's <see cref="Code.ITwinObject.SubType"/>
        /// </summary>
        Byte ObjectSubType { get; set; }
        /// <summary>
        /// The tools' copy of the object's type
        /// </summary>
        Byte ObjectType { get; set; }
        /// <summary>
        /// IDs of the behaviour starters (the ID before their graph's) the template runs
        /// </summary>
        List<UInt16> BehaviourStarters { get; set; }
        /// <summary>
        /// The tools' list header of <see cref="BehaviourStarters"/>: its capacity, the amount on every retail template
        /// </summary>
        UInt32 BehaviourListCapacity { get; set; }
        /// <summary>
        /// The tools' list header of <see cref="BehaviourStarters"/>: its growth step, 10 on every retail template
        /// </summary>
        UInt32 BehaviourListGrowth { get; set; }
        /// <summary>
        /// The tools' copy of the object's exit point amount
        /// </summary>
        Byte ObjectExitPoints { get; set; }
        /// <summary>
        /// The tools' copy of the object's react joint amount
        /// </summary>
        Byte ObjectReactJoints { get; set; }
        /// <summary>
        /// The instance state flags of the instances the template makes, laid out like an object's
        /// </summary>
        Enums.InstanceState InstanceStateFlags { get; set; }
        List<UInt32> Flags { get; set; }
        List<Single> Floats { get; set; }
        List<UInt32> Ints { get; set; }
    }
}
