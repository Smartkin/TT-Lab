using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code
{
    public interface ITwinOGI : ITwinItem
    {
        /// <summary>
        /// Header information
        /// </summary>
        enum HeaderInfo
        {
            JOINT_AMOUNT = 0,
            EXIT_POINT_AMOUNT = 1,
            REACT_JOINT_AMOUNT = 2,
            RIGID_MODELS_AMOUNT = 5,
            HAS_SKIN = 6,
            HAS_BLEND_SKIN = 7,
            COLLISIONS_AMOUNT = 8,
        }
        /// <summary>
        /// Skeleton joints
        /// </summary>
        List<TwinJoint> Joints { get; set; }
        /// <summary>
        /// The joint IDs the game binds for animations and scripts to find joints by: every ID below it is looked up among the joints.
        /// The tools wrote how many joints have an ID, which leaves the higher IDs of models whose IDs have gaps unbound
        /// </summary>
        Byte JointIdCount { get; set; }
        /// <summary>
        /// Also known as point of interests(POI)
        /// </summary>
        List<TwinExitPoint> ExitPoints { get; set; }
        /// <summary>
        /// Default bounding box for collision detection
        /// </summary>
        Vector4[] BoundingBox { get; set; }
        /// <summary>
        /// Unique index for each joint to build the armature
        /// </summary>
        List<Byte> JointIndices { get; set; }
        /// <summary>
        /// Rigid models that are linked to the joints
        /// </summary>
        List<UInt32> RigidModelIds { get; set; }
        /// <summary>
        /// Inverse bind matrices that form the default T-pose of the skeleton for animations
        /// </summary>
        List<Matrix4> SkinInverseBindMatrices { get; set; }
        /// <summary>
        /// Skinned model ID
        /// </summary>
        UInt32 SkinID { get; set; }
        /// <summary>
        /// Facial model ID
        /// </summary>
        UInt32 BlendSkinID { get; set; }
        /// <summary>
        /// Collision builder for multiple bounding boxes for more precise collision detection
        /// </summary>
        List<TwinCollisionHull> CollisionHulls { get; set; }
        /// <summary>
        /// Which joint links to which bounding box <seealso cref="Collisions"/>
        /// </summary>
        List<Byte> CollisionJointIndices { get; set; }
    }
}
