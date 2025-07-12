using System;
using System.Collections.Generic;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourCommandsSequence : ITwinItem, ITwinAgentLab
    {
        /// <summary>
        /// Unknown key value which must be either 17 or 18
        /// </summary>
        public InstanceType Key { get; set; }
        /// <summary>
        /// Unknown index in global storage
        /// </summary>
        public Byte IndexInGlobalStorage { get; set; }
        /// <summary>
        /// Behaviour packs with their IDs
        /// </summary>
        public List<KeyValuePair<UInt16, ITwinBehaviourCommandPack>> BehaviourPacks { get; set; }
        /// <summary>
        /// Command chain
        /// </summary>
        public List<ITwinBehaviourCommand> Commands { get; set; }

        internal bool HasNext { get; set; }

        /// <summary>
        /// Output the sequence's text form
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="tabs"></param>
        public void WriteText(StreamWriter writer, Int32 tabs = 0);
        /// <summary>
        /// Interpret sequence's text form
        /// </summary>
        /// <param name="reader"></param>
        public void ReadText(StreamReader reader);
        public String ToString();

        /// <summary>
        /// Key value enum
        /// </summary>
        public enum InstanceType
        {
            Pickup = 0x11,
            Projectile = 0x12,
        }
        
        public enum GameReservedIds : ushort
        {
            // Pickup custom agent
            PUP_STATE_INACTIVE = 562,
            PUP_STATE_INVISIBLE = 564,
            PUP_STATE_SPAWN_IN_AIR = 566,
            PUP_STATE_SPAWN_FROM_CRATE = 568,
            PUP_STATE_IDLE = 570,
            PUP_STATE_EXCITE = 572,
            PUP_STATE_SUCK_IN = 574,
            PUP_STATE_COLLECT = 576,
            PUP_STATE_FLY_AWAY = 578,
            PUP_STATE_DESTROY = 580,

            // Projectile custom agent
            PRO_STATE_INACTIVE = 590,
            PRO_STATE_INVISIBLE = 592,
            PRO_STATE_LAUNCH = 594,
            PRO_STATE_TRAVEL = 596,
            PRO_STATE_IMPACT_CRATE = 598,
            PRO_STATE_IMPACT_CREATURE = 600,
            PRO_STATE_IMPACT_FURNITURE = 602,
            PRO_STATE_IMPACT_PLAYER = 604,
            PRO_STATE_IMPACT_SCENERY = 606,
            PRO_STATE_CREATE_DAMAGE = 608,
            PRO_STATE_DESTROY = 610,
            PRO_STATE_UNK1 = 612,
            PRO_STATE_UNK2 = 614,
        }
    }
}
