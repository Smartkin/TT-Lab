using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    /// <summary>
    /// An object instance of a layout (the game's ObjectInstance, ReadInstance)
    /// </summary>
    public interface ITwinInstance : ITwinItem
    {
        /// <summary>
        /// Where the instance is in the chunk
        /// </summary>
        Vector4 Position { get; set; }
        /// <summary>
        /// The instance's turn about X, in 65536ths of a turn. The game reads the three words as tagged values and turns the whole
        /// signed word into an angle (GetRotationXYZ): the retail words are the angles sign extended
        /// </summary>
        Int32 RotationX { get; set; }
        Int32 RotationY { get; set; }
        Int32 RotationZ { get; set; }
        /// <summary>
        /// The instances of its layout the instance names, by their index (the scripts' linked objects)
        /// </summary>
        List<UInt16> Instances { get; set; }
        /// <summary>
        /// The room the tools' list header gives the instance list to grow by (10), kept and never read
        /// </summary>
        UInt32 InstancesGrowth { get; set; }
        /// <summary>
        /// The positions of its layout the instance names, by their index
        /// </summary>
        List<UInt16> Positions { get; set; }
        UInt32 PositionsGrowth { get; set; }
        /// <summary>
        /// The paths of its layout the instance names, by their index
        /// </summary>
        List<UInt16> Paths { get; set; }
        UInt32 PathsGrowth { get; set; }
        /// <summary>
        /// The object the instance is made of
        /// </summary>
        UInt16 ObjectId { get; set; }
        /// <summary>
        /// The instance's index among its behaviour starter's receivers, -1 none
        /// </summary>
        Int16 RefListIndex { get; set; }
        /// <summary>
        /// The behaviour starter the instance's spawn runs (its graph's ID minus 1), 0xFFFF none
        /// </summary>
        UInt16 SpawnScriptId { get; set; }
        Enums.InstanceState StateFlags { get; set; }
        /// <summary>
        /// The instance's tagged values (<see cref="Common.AgentLab.TaggedValue"/>): an int, an angle or a float, or an instance
        /// property's index; the tools left some raw words. The class of its object's type keeps as many as it has room for in its
        /// property holder, the rest in extras
        /// </summary>
        List<UInt32> TaggedProperties { get; set; }
        List<Single> FloatProperties { get; set; }
        List<Int32> IntProperties { get; set; }
    }
}
