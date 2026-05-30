using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinInstance : ITwinItem
    {
        /// <summary>
        /// Instance's position in the chunk
        /// </summary>
        Vector4 Position { get; set; }
        /// <summary>
        /// Instance's rotation in the chunk in quaternion
        /// </summary>
        Vector4 Rotation { get; set; }
        /// <summary>
        /// Instance's X rotation <seealso cref="TwinIntegerRotation"/>
        /// </summary>
        TwinIntegerRotation RotationX { get; set; }
        /// <summary>
        /// Instance's Y rotation <seealso cref="TwinIntegerRotation"/>
        /// </summary>
        TwinIntegerRotation RotationY { get; set; }
        /// <summary>
        /// Instance's Z rotation <seealso cref="TwinIntegerRotation"/>
        /// </summary>
        TwinIntegerRotation RotationZ { get; set; }
        UInt32 InstancesRelated { get; set; }
        /// <summary>
        /// Referenced instances
        /// </summary>
        List<UInt16> Instances { get; set; }
        UInt32 PositionsRelated { get; set; }
        /// <summary>
        /// Referenced positions
        /// </summary>
        List<UInt16> Positions { get; set; }
        UInt32 PathsRelated { get; set; }
        /// <summary>
        /// Referenced paths
        /// </summary>
        List<UInt16> Paths { get; set; }
        /// <summary>
        /// The object to create instance from
        /// </summary>
        UInt16 ObjectId { get; set; }
        Int16 RefListIndex { get; set; }
        /// <summary>
        /// Script/Behaviour to execute on spawn
        /// </summary>
        UInt16 OnSpawnHeaderScriptID { get; set; }
        /// <summary>
        /// Instance state flags <seealso cref="Enumerations.Enums.InstanceState"/>
        /// </summary>
        UInt32 StateFlags { get; set; }
        /// <summary>
        /// Flag parameters
        /// </summary>
        List<UInt32> ParamList1 { get; set; }
        /// <summary>
        /// Float parameters
        /// </summary>
        List<Single> ParamList2 { get; set; }
        /// <summary>
        /// Integer parameters
        /// </summary>
        List<UInt32> ParamList3 { get; set; }
    }
}
