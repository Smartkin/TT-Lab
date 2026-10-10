using System;
using System.ComponentModel;

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
        /// An instance's state flags, which its agent applies (BasicAgent::ApplyState and the classes' own): bits 0-3, 8, 9 and 15-17
        /// are the agent's own states, bits 10-14 stop the characters' attacks of a movement mode (body slam, slide, spin, twin slam,
        /// thrown Cortex: <c>1 &lt;&lt; mode</c>), and on crates 10 lets falling crates land on it and 11 makes it unbreakable, bit 5
        /// gives the instance a node keeping its previous transform, bits 6-7 its persistent flag and where it's kept, bit 18 places it
        /// on the ground below when created (creatures and characters), bit 4 makes what stands on it ride along (its instance's flag
        /// 14, read by the characters' solver and rigid bodies). Scripts test any bit with SoftFlagSet(n) (CheckInstanceFlagSet): bits
        /// 19-31 are theirs alone, the retail creatures', drones', bats' and penguins' scripts take them as options of their instances.
        /// </summary>
        [Flags]
        public enum InstanceState : UInt32
        {
            // The members' descriptions document them, the inspector shows them as the check boxes' tooltips
#pragma warning disable CS1591
            [Description("Its agent puts the instance to sleep when it applies its state, else wakes it: an instance asleep isn't drawn and hit " +
                         "searches leave it out")]
            Deactivated = 1 << 0,
            [Description("Ray casts' queries of instances and the characters' collision find it")]
            CollisionActive = 1 << 1,
            [Description("Drawn while it's awake, its shadow needs it too")]
            Visible = 1 << 2,
            [Description("Casts its shadow (its shadow node's shapes) while it's visible and in a scenery cell drawn this frame, every Crash " +
                         "instance has it")]
            ShadowActive = 1 << 3,
            [Description("What stands on it rides along (its instance's flag 14, which dynamic scenery always has): characters standing on its " +
                         "collision hulls move with it while its matrix isn't scaled, rigid bodies landing on it ride its movement (with " +
                         "TracksMovement). Scripts can test it with SoftFlagSet(4)")]
            PlayableCharacterCanMoveAlong = 1 << 4,
            [Description("The instance keeps its transform of the previous frame (dynamic scenery always does), which what stands on it moves by")]
            TracksMovement = 1 << 5,
            [Description("The instance gets a slot among its chunk's persistent flags, set by scripts (SetPersistentFlag) and the movies it played, " +
                         "and tested by CheckPersistentFlagCondition")]
            SyncCrossChunkState = 1 << 6,
            [Description("The persistent flag is kept in the chunk's own store, which every instance's flag goes to, instead of the level's")]
            PersistentFlagInChunkStore = 1 << 7,
            [Description("Hits reach it: its agent hears attacks (unless its part is invulnerable), and the scripts' HitInstancesInBoxes and the " +
                         "Humiliskate's and the Rollerbrawl's hits find it. Scripts switch it")]
            ReceiveOnTriggerSignals = 1 << 8,
            [Description("The agent hits back the characters attacking it")]
            CanDamageCharacter = 1 << 9,
            [Description("Stops the characters' body slam; on a crate, crates falling on it land on it instead of slamming it")]
            SolidToBodySlam = 1 << 10,
            [Description("Stops the characters' slide, which doesn't crush it either; a crate with it is unbreakable")]
            SolidToSlide = 1 << 11,
            [Description("Stops the characters' spin")]
            SolidToSpin = 1 << 12,
            [Description("Stops the tied characters' slam (Crash and Cortex tied together)")]
            SolidToTwinSlam = 1 << 13,
            [Description("Stops Cortex thrown by Crash")]
            SolidToThrownCortex = 1 << 14,
            [Description("The characters' target lock can pick it")]
            Targettable = 1 << 15,
            [Description("The agent always hits back the characters attacking it, and its script never hears of attacks or contacts")]
            CanAlwaysDamageCharacter = 1 << 16,
            [Description("Projectiles hitting it bounce back")]
            BulletsWillBounceBack = 1 << 17,
            [Description("Placed on the ground when created: a ray cast from 1 above the instance to 7 below it, the instance goes where it hits " +
                         "plus its float property 5")]
            SnapToGround = 1 << 18,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(19) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown4 = 1 << 19,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(20) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown5 = 1 << 20,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(21) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown6 = 1 << 21,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(22) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown7 = 1 << 22,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(23) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown8 = 1 << 23,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(24) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown9 = 1 << 24,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(25) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown10 = 1 << 25,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(26) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown11 = 1 << 26,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(27) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown12 = 1 << 27,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(28) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown13 = 1 << 28,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(29) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown14 = 1 << 29,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(30) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown15 = 1 << 30,
            [Description("The scripts' own: no engine code reads it, SoftFlagSet(31) tests it. The retail creatures', drones', bats' and penguins' " +
                         "scripts take bits 19 to 31 as options of their instances")]
            Unknown16 = 1U << 31,
#pragma warning restore CS1591
        }

        /// <summary>
        /// The bits of a trigger's header the game reads: which messages the trigger sends and whether it gets polled
        /// </summary>
        [Flags]
        public enum TriggerFlags : UInt32
        {
            // The members' descriptions document them, the inspector shows them as the check boxes' tooltips
#pragma warning disable CS1591
            [Description("Sends its second message to what enters the box when the first isn't sent")]
            OnEnter = 1 << 8,
            [Description("Sends its third message to what stays in the box at every check, and to what enters it when neither of the first two is " +
                         "sent")]
            OnStay = 1 << 9,
            [Description("Sends its fourth message when something leaves the box")]
            OnExit = 1 << 10,
            [Description("Sends its first message to what enters the box until anything has been inside it at a check (until the trigger is reset)")]
            OnEnterOnce = 1 << 11,
            [Description("The trigger's node never checks its box, no retail trigger has it")]
            NotPolled = 1 << 12
#pragma warning restore CS1591
        }
        /// <summary>
        /// The types of objects that set a trigger off, a bit each (made the kinds of their agents' nodes; bit 9 is a kind no object
        /// type has)
        /// </summary>
        [Flags]
        public enum TriggerActivatorObjects
        {
            // The members' descriptions document them, the inspector shows them as the check boxes' tooltips
#pragma warning disable CS1591
            [Description("The playable characters set it off: a trigger of only this bit is checked by the player itself every frame unless it " +
                         "messages what leaves (one of no bits by every playable character), with other bits the trigger looks for them every " +
                         "CheckInterval seconds. Cameras need it")]
            PlayableCharacter = 1 << 0,
            [Description("Pickups set it off: the trigger looks for their instances every CheckInterval seconds")]
            Pickups = 1 << 1,
            [Description("Crates set it off: the trigger looks for their instances every CheckInterval seconds")]
            Crates = 1 << 2,
            [Description("Creatures set it off: the trigger looks for their instances every CheckInterval seconds")]
            Creatures = 1 << 3,
            [Description("Generic objects set it off: the trigger looks for their instances every CheckInterval seconds")]
            GenericObjects = 1 << 4,
            [Description("Grabbables set it off: the trigger looks for their instances every CheckInterval seconds")]
            Grabbables = 1 << 5,
            [Description("Pay gates set it off: the trigger looks for their instances every CheckInterval seconds")]
            PayGates = 1 << 6,
            [Description("Graples set it off: the trigger looks for their instances every CheckInterval seconds")]
            Graples = 1 << 7,
            [Description("Projectiles set it off: the trigger looks for their instances every CheckInterval seconds")]
            Projectiles = 1 << 8
#pragma warning restore CS1591
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
            // No retail material has these types: bits no other type has, the game only compares the keys to skip loading the programs again
            ScreenCopy = 0x20000,
            UnlitClothDeformation2 = 0x80000,
            WaveDeformation = 0x100000,
            UiShader = 0x10000000,
        }

        /// <summary>
        /// An AI position's flags (the game's AiPosition): route searches can ask for some and rule some out (the nearest point search with
        /// flags, GetShortRoute's), the scripts' conditions test the ones of the route's step (SubPathPointFlags56 none of 1, 2, 4 and 6)
        /// and SetNearestPointFlags switches bits 0-2 of the position nearest the agent. Bits 1, 2 and 4 are set on positions of the
        /// retail levels
        /// </summary>
        [Flags]
        public enum AiPositionFlags : UInt16
        {
            // The members' descriptions document them, the inspector shows them as the check boxes' tooltips
#pragma warning disable CS1591
            [Description("No route goes through the position (the path finder's step cost), the scripts' SubPathPointFlag0 tests it")]
            Blocked = 1 << 0,
            [Description("The scripts' NodeIsAirborne condition tests it on the route's step")]
            Airborne = 1 << 1,
            [Description("SetFocusPositionToNearestPoint takes a position with it however far it is (others within 30 units), the scripts' " +
                         "SubPathPointFlag2 conditions test it")]
            Flag2 = 1 << 2,
            [Description("Only searches asking for flags or ruling them out read it")]
            Flag3 = 1 << 3,
            [Description("SetFocusPositionToNearestPoint never takes a position with it, the scripts' SubPathPointFlag4 conditions test it")]
            Flag4 = 1 << 4,
            [Description("The scripts' SubPathPointFlag5 conditions test it")]
            Flag5 = 1 << 5,
            [Description("The scripts' SubPathPointFlag6 conditions test it")]
            Flag6 = 1 << 6,
#pragma warning restore CS1591
        }

        /// <summary>
        /// An AI path's flags (the game's AiPath): which routes may take it (a route request's bits each rule out the paths with one, bits
        /// 18 and 19 only while bit 17 isn't set) and what the scripts' conditions find on the path to the route's step
        /// </summary>
        [Flags]
        public enum AiPathFlags : UInt16
        {
            // The members' descriptions document them, the inspector shows them as the check boxes' tooltips
#pragma warning disable CS1591
            [Description("Set by the tools together with bit 1 (on a sixth of the retail paths), never read")]
            Flag0 = 1 << 0,
            [Description("Set by the tools together with bit 0, never read")]
            Flag1 = 1 << 1,
            [Description("Crossing it takes a jump (EdgeNeedsJump), requests with bit 17 rule it out")]
            NeedsJump = 1 << 2,
            [Description("Crossing it takes a long jump (EdgeNeedsLongJump), requests with bit 19 rule it out")]
            NeedsLongJump = 1 << 3,
            [Description("Crossing it takes a high jump (EdgeNeedsHighJump), requests with bit 18 rule it out")]
            NeedsHighJump = 1 << 4,
            [Description("Crossing it takes flying (EdgeNeedsFlying), requests with bit 20 rule it out; with bit 24 only paths with one of bits 5-8 " +
                         "are taken")]
            Flag5 = 1 << 5,
            [Description("Requests with bit 21 rule it out")]
            Flag6 = 1 << 6,
            [Description("Requests with bit 22 rule it out")]
            Flag7 = 1 << 7,
            [Description("Requests with bit 23 rule it out, the scripts' PathSegmentFlag0 condition tests it")]
            Flag8 = 1 << 8,
#pragma warning restore CS1591
        }

        /// <summary>
        /// A collision surface's flags, from the PAL executable. The ray casts test one bit each: the player's probes bit 4, the camera's
        /// bit 5, objects' ground and movement checks (CanMoveForwards, SnapToGround) bit 6, lines of sight (CanSeePlayer,
        /// ClearLineOfSightToFocus) bit 7. Bits 12-19 are set on every surface by the surface's constructor and never read.
        /// </summary>
        [Flags]
        public enum SurfaceCollisionFlags : UInt32
        {
            // The members' descriptions document them, the inspector shows them as the check boxes' tooltips
#pragma warning disable CS1591
            [Description("The tools' tag of the slightly slippy surfaces, never read: the friction does the sliding")]
            SlightlySlippy = 1 << 0,
            [Description("The tools' tag of the medium slippy surfaces, never read")]
            MediumSlippy = 1 << 1,
            [Description("Set on the liquids and the deadly surfaces, never read")]
            Hazard = 1 << 2,
            [Description("One of the tools' tags (bits 0-3), never read")]
            Unknown4 = 1 << 3,
            [Description("The characters touch it and their ray casts hit it: their collision, ground search and move probes, the Humiliskate's, " +
                         "the Rollerbrawl's and the graples' casts")]
            SolidToPlayerProbes = 1 << 4,
            [Description("The camera's ray casts hit it and it keeps the camera out of instances' hulls")]
            BlocksCamera = 1 << 5,
            [Description("Ground for objects: their ray casts hit it, rigid bodies rest on it and instances snap to it")]
            SolidToObjects = 1 << 6,
            [Description("Lines of sight stop at it (CanSeePlayer, ClearLineOfSightToFocus, PlayerVisible)")]
            BlocksLineOfSight = 1 << 7,
            [Description("The agents of rigid bodies touching it get the surface's contact message (set on every deadly surface)")]
            SendsContactMessageToObjects = 1 << 8,
            [Description("The player standing on it gets the surface's contact message (the deadly surfaces' kill)")]
            SendsContactMessageToPlayer = 1 << 9,
            [Description("The Rollerbrawl's ball packs snow on rolling over it (other ground wears it off)")]
            Sticky = 1 << 10,
            [Description("Soft: Nina clings to and slides down upright walls of it she jumps into (CharacterAgent::ClingToWall), and the " +
                         "Humiliskate's and the Rollerbrawl's skid trails are laid on it (LaySkidMark). Footprints go by the surface's ID")]
            LeavesFootprints = 1 << 11,
            Default12 = 1 << 12,
            Default13 = 1 << 13,
            Default14 = 1 << 14,
            Default15 = 1 << 15,
            Default16 = 1 << 16,
            Default17 = 1 << 17,
            Default18 = 1 << 18,
            Default19 = 1 << 19,
            [Description("The player's body collides with it, surfaces without it (water, the camera and AI walls) are passed through")]
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
#pragma warning restore CS1591
        }

        /// <summary>
        /// The kinds of hit a contact message is (<see cref="Common.TwinContactMessage.Kinds"/>), named after what sends them in retail: the
        /// surfaces, the engine and the scripts' CreateDamage. Nothing but the scripts' conditions reads them, besides the drowned and the
        /// Rollerbrawl's snow. Every bit is declared, the inspector's check boxes go by the bits' order
        /// </summary>
        [Flags]
        public enum ContactKinds : UInt32
        {
            // The members' descriptions document them, the inspector shows them as the check boxes' tooltips
#pragma warning disable CS1591
            Unused0 = 1 << 0,
            [Description("Explosions (crates, bombs, mines, grenades), the tiki monsters' fireballs and the mantraps' snaps. Scripts test it with ObjectContextFlag1")]
            Explosion = 1 << 1,
            [Description("The fall-through death surfaces. Scripts test it with ObjectContextFlag2")]
            FallingThrough = 1 << 2,
            [Description("Particles with collision spheres touching a character (1 hit point), Dingodile's flames and lava, which melts the Rollerbrawl's " +
                         "snow instead of hurting while it has any. Scripts test it, or Kind22, with ObjectContextFlags3or22")]
            Burning = 1 << 3,
            [Description("The iceteroids' hits. Nothing tests it")]
            Iceteroid = 1 << 4,
            [Description("Missiles, rocks and the beetles' bombs. Scripts test it with HitBySpinHitbox")]
            Projectile = 1 << 5,
            Unused6 = 1 << 6,
            [Description("The electric death surfaces, the ant prods, N. Gin's driver and Cortex's lasers. Scripts test it with HitByCortexBolt")]
            Electric = 1 << 7,
            Unused8 = 1 << 8,
            Unused9 = 1 << 9,
            [Description("The instant death surfaces, agents hitting back (1 hit point), a crushed character (with Crush) and CreateDamage with its " +
                         "flag 1. Nothing tests it")]
            GenericHit = 1 << 10,
            [Description("A character crushed (with GenericHit, 100 hit points). Nothing tests it")]
            Crush = 1 << 11,
            Unused12 = 1 << 12,
            Unused13 = 1 << 13,
            Unused14 = 1 << 14,
            [Description("The piranha plants' bites. Nothing tests it")]
            Bite = 1 << 15,
            [Description("The characters' spin, spin punch and knee slide. Scripts test it with HitByPunch")]
            Spin = 1 << 16,
            [Description("Crash's flying and stomp kicks. Scripts test it with ObjectContextFlag17")]
            Kick = 1 << 17,
            [Description("Scripts test it with HitByBodySlam2, nothing in retail sends it")]
            Kind18 = 1 << 18,
            [Description("Cortex's down blast and super laser and the mecha's rockets (with Electric), Crash's stomp kick (with Kick). Scripts test " +
                         "it with ObjectContextFlag19")]
            Heavy = 1 << 19,
            Unused20 = 1 << 20,
            Unused21 = 1 << 21,
            [Description("Scripts test it, or Burning, with ObjectContextFlags3or22, nothing in retail sends it")]
            Kind22 = 1 << 22,
            [Description("The lava and drowning surfaces (with Burning or Water). Nothing tests it")]
            Sinking = 1 << 23,
            [Description("The characters' knee drop landing, Crash's super knee drop's too. Scripts test it with HitByBodySlamHitbox")]
            KneeDrop = 1 << 24,
            [Description("Water and the drowning surfaces: a dead character with it isn't moved any more (drowned). Scripts test it with " +
                         "ObjectContextFlag25")]
            Water = 1 << 25,
            Unused26 = 1 << 26,
            Unused27 = 1 << 27,
            Unused28 = 1 << 28,
            Unused29 = 1 << 29,
            Unused30 = 1 << 30,
            Unused31 = 1U << 31,
#pragma warning restore CS1591
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
