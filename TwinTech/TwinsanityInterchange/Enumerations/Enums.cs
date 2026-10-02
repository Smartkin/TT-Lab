using System;

namespace Twinsanity.TwinsanityInterchange.Enumerations
{
    public static class Enums
    {
        public enum Layouts
        {
            LAYER_1,
            LAYER_2,
            LAYER_3,
            LAYER_4,
            LAYER_5,
            LAYER_6,
            LAYER_7,
            LAYER_8
        }

        public enum ObjectType
        {
            PLAYABLE_CHARACTER,
            PICKUP,
            CRATE,
            CREATURE,
            GENERIC_OBJECT,
            GRABBABLE,
            PAY_GATE,
            GRAPLE,
            PROJECTILE
        }

        public enum LodType
        {
            FULL = 0x1001,
            COMPRESSED = 0x1002
        }

        /// <summary>
        /// An instance's state flags. What the PAL executable reads of them: bits 0-3 and 8-17 are the object's own states, bit 4 sets
        /// a context bit, bit 5 gives the instance a node keeping its previous transform, bits 6-7 its persistent flag and where it's
        /// kept, bit 18 places it on the ground below when created. Bits 19-31 are never read by the engine, scripts can still test
        /// any bit with CheckInstanceFlagSet.
        /// </summary>
        [Flags]
        public enum InstanceState : UInt32
        {
            Deactivated = 1 << 0,
            CollisionActive = 1 << 1,
            Visible = 1 << 2,
            ShadowActive = 1 << 3,
            PlayableCharacterCanMoveAlong = 1 << 4,
            /// <summary>
            /// The instance keeps its transform of the previous frame (dynamic scenery always does), which what stands on it moves by
            /// </summary>
            TracksMovement = 1 << 5,
            /// <summary>
            /// The instance gets a slot among its chunk's persistent flags, set by scripts (SetPersistentFlag) and the movies it
            /// played, and tested by CheckPersistentFlagCondition
            /// </summary>
            SyncCrossChunkState = 1 << 6,
            /// <summary>
            /// The persistent flag is kept in the chunk's own store, which every instance's flag goes to, instead of the level's
            /// </summary>
            PersistentFlagInChunkStore = 1 << 7,
            ReceiveOnTriggerSignals = 1 << 8,
            CanDamageCharacter = 1 << 9,
            SolidToBodySlam = 1 << 10,
            SolidToSlide = 1 << 11,
            SolidToSpin = 1 << 12,
            SolidToTwinSlam = 1 << 13,
            SolidToThrownCortex = 1 << 14,
            Targettable = 1 << 15,
            CanAlwaysDamageCharacter = 1 << 16,
            BulletsWillBounceBack = 1 << 17,
            /// <summary>
            /// Placed on the ground when created: a ray cast from 1 above the instance to 7 below it, the instance goes where it hits
            /// plus its float property 5
            /// </summary>
            SnapToGround = 1 << 18,
            Unknown4 = 1 << 19,
            Unknown5 = 1 << 20,
            Unknown6 = 1 << 21,
            Unknown7 = 1 << 22,
            Unknown8 = 1 << 23,
            Unknown9 = 1 << 24,
            Unknown10 = 1 << 25,
            Unknown11 = 1 << 26,
            Unknown12 = 1 << 27,
            Unknown13 = 1 << 28,
            Unknown14 = 1 << 29,
            Unknown15 = 1 << 30,
            Unknown16 = 1U << 31,
        }

        /// <summary>
        /// The bits of a trigger's header the game reads: which messages the trigger sends and whether it gets polled
        /// </summary>
        [Flags]
        public enum TriggerFlags : UInt32
        {
            /// <summary>
            /// Sends its second message every time something enters the box
            /// </summary>
            OnEnter = 1 << 8,
            /// <summary>
            /// Sends its third message while something stays in the box
            /// </summary>
            OnStay = 1 << 9,
            /// <summary>
            /// Sends its fourth message when something leaves the box
            /// </summary>
            OnExit = 1 << 10,
            /// <summary>
            /// Sends its first message the first time something enters the box
            /// </summary>
            OnEnterOnce = 1 << 11,
            /// <summary>
            /// The trigger's node never checks its box, no retail trigger has it
            /// </summary>
            NotPolled = 1 << 12
        }
        [Flags]
        public enum TriggerActivatorObjects
        {
            PlayableCharacter = 1 << 0,
            Pickups = 1 << 1,
            Crates = 1 << 2,
            Creatures = 1 << 3,
            GenericObjects = 1 << 4,
            Grabbables = 1 << 5,
            PayGates = 1 << 6,
            Graples = 1 << 7,
            Projectiles = 1 << 8
        }
        
        // public enum Type
        // {
        //     StandardUnlit = 1,
        //     StandardLit = 2,
        //     LitSkinnedModel = 4,
        //     UnlitSkydome = 10,
        //     ColorOnly = 11,
        //     LitEnvironmentMap = 12,
        //     UiShader = 13,
        //     LitMetallic = 15,
        //     LitReflectionSurface = 16,
        //     SHADER_17 = 17,
        //     Particle = 18,
        //     Decal = 19,
        //     SHADER_20 = 20,
        //     UnlitGlossy = 21,
        //     UnlitEnvironmentMap = 22,
        //     UnlitClothDeformation = 23,
        //     SHADER_25 = 25,
        //     UnlitClothDeformation2 = 26,
        //     UnlitBillboard = 27,
        //     SHADER_30 = 30,
        //     SHADER_31 = 31,
        //     SHADER_32 = 32,
        // }

        [Flags]
        public enum AppliedShaders : UInt64
        {
            LitSkinnedModel = 0x2,
            StandardLit = 0x4,
            StandardUnlit = 0x8,
            UnlitBillboard = StandardUnlit,
            UnlitSkydome = 0x10,
            ColorOnly = 0x20,
            LitEnvironmentMap = 0x40,
            LitMetallic = 0x200,
            LitReflectionSurface = 0x400,
            Particle = 0x1000,
            Decal = 0x2000,
            UnlitGlossy = 0x4000,
            UnlitEnvironmentMap = 0x8000,
            UnlitClothDeformation = 0x10000,
            UnlitClothDeformation2 = 0x80000,
            UiShader = 0x10000000,
        }

        /// <summary>
        /// An AI position's flags (the game's AiPosition): route searches can ask for some and rule some out (the nearest point search with
        /// flags), the scripts' conditions test the ones of the route's step. Bits 1, 2 and 4 are set on positions of the retail levels
        /// </summary>
        [Flags]
        public enum AiPositionFlags : UInt16
        {
            /// <summary>No route goes through the position (the path finder's step cost)</summary>
            Blocked = 1 << 0,
            /// <summary>The scripts' NodeIsAirborne condition tests it on the route's step</summary>
            Airborne = 1 << 1,
            Flag2 = 1 << 2,
            Flag3 = 1 << 3,
            /// <summary>The scripts' SubPathPointFlag4 conditions test it</summary>
            Flag4 = 1 << 4,
            /// <summary>The scripts' SubPathPointFlag5 conditions test it</summary>
            Flag5 = 1 << 5,
            /// <summary>The scripts' SubPathPointFlag6 conditions test it</summary>
            Flag6 = 1 << 6,
        }

        /// <summary>
        /// An AI path's flags (the game's AiPath): which routes may take it (a route request's bits each rule out the paths with one) and what
        /// the scripts' conditions find on the path to the route's step
        /// </summary>
        [Flags]
        public enum AiPathFlags : UInt16
        {
            Flag0 = 1 << 0,
            Flag1 = 1 << 1,
            /// <summary>Crossing it takes a jump (EdgeNeedsJump), requests with bit 17 rule it out</summary>
            NeedsJump = 1 << 2,
            /// <summary>Crossing it takes a long jump (EdgeNeedsLongJump), requests with bit 19 rule it out</summary>
            NeedsLongJump = 1 << 3,
            /// <summary>Crossing it takes a high jump (EdgeNeedsHighJump), requests with bit 18 rule it out</summary>
            NeedsHighJump = 1 << 4,
            /// <summary>Requests with bit 20 rule it out; with bit 24 only paths with one of bits 5-8 are taken</summary>
            Flag5 = 1 << 5,
            /// <summary>Requests with bit 21 rule it out</summary>
            Flag6 = 1 << 6,
            /// <summary>Requests with bit 22 rule it out</summary>
            Flag7 = 1 << 7,
            /// <summary>Requests with bit 23 rule it out, the scripts' PathSegmentFlag0 condition tests it</summary>
            Flag8 = 1 << 8,
        }

        /// <summary>
        /// A collision surface's flags, from the PAL executable. The ray casts test one bit each: the player's probes bit 4, the camera's
        /// bit 5, objects' ground and movement checks (CanMoveForwards, SnapToGround) bit 6, lines of sight (CanSeePlayer,
        /// ClearLineOfSightToFocus) bit 7. Bits 12-19 are set on every surface by the surface's constructor and never read.
        /// </summary>
        [Flags]
        public enum SurfaceCollisionFlags : UInt32
        {
            /// <summary>The tools' tag of the slightly slippy surfaces, never read: the friction does the sliding</summary>
            SlightlySlippy = 1 << 0,
            /// <summary>The tools' tag of the medium slippy surfaces, never read</summary>
            MediumSlippy = 1 << 1,
            /// <summary>Set on the liquids and the deadly surfaces, never read</summary>
            Hazard = 1 << 2,
            Unknown4 = 1 << 3,
            /// <summary>The player's ray casts hit it (ground probes, footprints, the spin's reach)</summary>
            SolidToPlayerProbes = 1 << 4,
            /// <summary>The camera's ray casts hit it and it keeps the camera out of instances' hulls</summary>
            BlocksCamera = 1 << 5,
            /// <summary>Ground for objects: their ray casts hit it, rigid bodies rest on it and instances snap to it</summary>
            SolidToObjects = 1 << 6,
            /// <summary>Lines of sight stop at it (CanSeePlayer, ClearLineOfSightToFocus, PlayerVisible)</summary>
            BlocksLineOfSight = 1 << 7,
            /// <summary>The agents of rigid bodies touching it get the surface's contact message (set on every deadly surface)</summary>
            SendsContactMessageToObjects = 1 << 8,
            /// <summary>The player standing on it gets the surface's contact message (the deadly surfaces' kill)</summary>
            SendsContactMessageToPlayer = 1 << 9,
            /// <summary>Slows the player down like the sticky snow</summary>
            Sticky = 1 << 10,
            /// <summary>The player's steps leave footprints on it</summary>
            LeavesFootprints = 1 << 11,
            Default12 = 1 << 12,
            Default13 = 1 << 13,
            Default14 = 1 << 14,
            Default15 = 1 << 15,
            Default16 = 1 << 16,
            Default17 = 1 << 17,
            Default18 = 1 << 18,
            Default19 = 1 << 19,
            /// <summary>The player's body collides with it, surfaces without it (water, the camera and AI walls) are passed through</summary>
            SolidToPlayer = 1 << 20,
            Unknown22 = 1 << 21,
            Unknown23 = 1 << 22,
            Unknown24 = 1 << 23,
            Unknown25 = 1 << 24,
            Unknown26 = 1 << 25,
            Unknown27 = 1 << 26,
            Unknown28 = 1 << 27,
            Unknown29 = 1 << 28,
            Unknown30 = 1 << 29,
            Unknown31 = 1 << 30,
            Unknown32 = 1U << 31,
        }

        public enum SurfaceType : UInt16
        {
            SURF_DEFAULT = 0,
            SURF_GENERIC_SLIGHTLY_SLIPPY = 1,
            SURF_GENERIC_MEDIUM_SLIPPY = 2,
            SURF_LAVA = 3,
            SURF_GENERIC_INSTANT_DEATH = 4,
            SURF_FALL_THRU_DEATH = 5,
            SURF_NORMAL_GRASS = 6,
            SURF_SLIPPY_METAL = 7,
            SURF_NORMAL_WOOD = 8,
            SURF_NORMAL_METAL = 9,
            SURF_NORMAL_SAND = 10,
            SURF_NORMAL_MUD = 11,
            SURF_NORMAL_WATER = 12,
            SURF_NORMAL_ROCK = 13,
            SURF_SLIPPY_ROCK = 14,
            SURF_NORMAL_SNOW = 15,
            SURF_STICKY_SNOW = 16,
            SURF_ICE = 17,
            SURF_GLASS_WALL = 18,
            SURF_HACK_RAIL = 19,
            SURF_CAMERA_BLOCKING = 20,
            SURF_NORMAL_STONE_TILES = 21,
            SURF_ICE_LOW_SLIPPY = 22,
            SURF_DROWNING_PLANE = 23,
            SURF_BLOCK_PLAYER = 24,
            SURF_GENERIC_MEDIUM_SLIPPY_RIGID_ONLY = 25,
            SURF_NONSOLID_ELECTRIC_DEATH = 26,
            SURF_BLOCK_AI_ONLY = 27,
            // After this custom surfaces start. Game supports a hard limit of 128 collision surfaces
            SURF_CUSTOM_1 = 28,
            SURF_CUSTOM_2,
            SURF_CUSTOM_3,
            SURF_CUSTOM_4,
            SURF_CUSTOM_5,
            SURF_CUSTOM_6,
            SURF_CUSTOM_7,
            SURF_CUSTOM_8,
            SURF_CUSTOM_9,
            SURF_CUSTOM_10,
            SURF_CUSTOM_11,
            SURF_CUSTOM_12,
            SURF_CUSTOM_13,
            SURF_CUSTOM_14,
            SURF_CUSTOM_15,
            SURF_CUSTOM_16,
            SURF_CUSTOM_17,
            SURF_CUSTOM_18,
            SURF_CUSTOM_19,
            SURF_CUSTOM_20,
            SURF_CUSTOM_21,
            SURF_CUSTOM_22,
            SURF_CUSTOM_23,
            SURF_CUSTOM_24,
            SURF_CUSTOM_25,
            SURF_CUSTOM_26,
            SURF_CUSTOM_27,
            SURF_CUSTOM_28,
            SURF_CUSTOM_29,
            SURF_CUSTOM_30,
            SURF_CUSTOM_31,
            SURF_CUSTOM_32,
            SURF_CUSTOM_33,
            SURF_CUSTOM_34,
            SURF_CUSTOM_35,
            SURF_CUSTOM_36,
            SURF_CUSTOM_37,
            SURF_CUSTOM_38,
            SURF_CUSTOM_39,
            SURF_CUSTOM_40,
            SURF_CUSTOM_41,
            SURF_CUSTOM_42,
            SURF_CUSTOM_43,
            SURF_CUSTOM_44,
            SURF_CUSTOM_45,
            SURF_CUSTOM_46,
            SURF_CUSTOM_47,
            SURF_CUSTOM_48,
            SURF_CUSTOM_49,
            SURF_CUSTOM_50,
            SURF_CUSTOM_51,
            SURF_CUSTOM_52,
            SURF_CUSTOM_53,
            SURF_CUSTOM_54,
            SURF_CUSTOM_55,
            SURF_CUSTOM_56,
            SURF_CUSTOM_57,
            SURF_CUSTOM_58,
            SURF_CUSTOM_59,
            SURF_CUSTOM_60,
            SURF_CUSTOM_61,
            SURF_CUSTOM_62,
            SURF_CUSTOM_63,
            SURF_CUSTOM_64,
            SURF_CUSTOM_65,
            SURF_CUSTOM_66,
            SURF_CUSTOM_67,
            SURF_CUSTOM_68,
            SURF_CUSTOM_69,
            SURF_CUSTOM_70,
            SURF_CUSTOM_71,
            SURF_CUSTOM_72,
            SURF_CUSTOM_73,
            SURF_CUSTOM_74,
            SURF_CUSTOM_75,
            SURF_CUSTOM_76,
            SURF_CUSTOM_77,
            SURF_CUSTOM_78,
            SURF_CUSTOM_79,
            SURF_CUSTOM_80,
            SURF_CUSTOM_81,
            SURF_CUSTOM_82,
            SURF_CUSTOM_83,
            SURF_CUSTOM_84,
            SURF_CUSTOM_85,
            SURF_CUSTOM_86,
            SURF_CUSTOM_87,
            SURF_CUSTOM_88,
            SURF_CUSTOM_89,
            SURF_CUSTOM_90,
            SURF_CUSTOM_91,
            SURF_CUSTOM_92,
            SURF_CUSTOM_93,
            SURF_CUSTOM_94,
            SURF_CUSTOM_95,
            SURF_CUSTOM_96,
            SURF_CUSTOM_97,
            SURF_CUSTOM_98,
            SURF_CUSTOM_99,
            SURF_CUSTOM_100,
        }
    }
}
