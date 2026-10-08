using System;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    /// <summary>
    /// A camera trigger: the box the player has to be in, how the camera controller blends the camera in and the two subtypes that
    /// give the point the camera looks at and where it stands. What the controller does with the values was worked out from the
    /// PAL executable (FUN_00274e08 applies a camera, FUN_00275cf0 and FUN_00274b20 switch to one, FUN_001434a0 accepts one,
    /// FUN_0026f3f0 runs the subtypes), the angles are 65536ths of a turn and the controller's three angle blenders are taken for
    /// the field of view, the pitch and the yaw by the values the game's levels give them (32°, 20° and 180°)
    /// </summary>
    public interface ITwinCamera : ITwinItem
    {
        TwinTrigger CamTrigger { get; set; }
        CameraFlags Flags { get; set; }
        CameraSwitches Switches { get; set; }
        /// <summary>
        /// Seconds the camera blends in over (2 when the controller is told to blend in without a camera)
        /// </summary>
        Single BlendTime { get; set; } // 10
        /// <summary>
        /// With <see cref="CameraFlags.GivesTargetBox"/>, the first corner of the box the follow camera's target takes in place of its
        /// own: the camera looks at the followed object's place plus the point halfway across the box, turned with the object unless
        /// <see cref="CameraFlags.TargetBoxUnturned"/> (FollowCameraTarget, (0, 1.6, 0) on many cameras). The tools' memory without the flag
        /// </summary>
        Vector4 TargetBoxMin { get; set; }
        /// <summary>
        /// The target box's other corner, see <see cref="TargetBoxMin"/>
        /// </summary>
        Vector4 TargetBoxMax { get; set; } // 42
        /// <summary>
        /// With <see cref="CameraFlags.FramesInstances"/>, the furthest the follow camera's target moves toward the middle of the camera
        /// trigger's instances. The tools' memory without the flag
        /// </summary>
        Single FramingDistance { get; set; }
        /// <summary>
        /// With <see cref="CameraFlags.FramesInstances"/>, the share of the way from the followed object to the middle of the camera
        /// trigger's instances the target moves (up to <see cref="FramingDistance"/>). The tools' memory without the flag
        /// </summary>
        Single FramingShare { get; set; } // 50
        /// <summary>
        /// The angle the first angle blender (probably the field of view) starts from with <see cref="CameraFlags.SetsFov"/>, or its
        /// value at the start of the camera's line, path or spline with <see cref="CameraFlags.ValuesAlongGeometry"/>
        /// </summary>
        UInt32 FovStart { get; set; }
        /// <summary>
        /// The angle the first angle blender goes to, or its value at the end of the geometry
        /// </summary>
        UInt32 FovEnd { get; set; }
        /// <summary>
        /// The angle the second angle blender (the pitch, 20° in the game's levels) starts from with <see cref="CameraFlags.SetsPitch"/>,
        /// or its value at the start of the geometry
        /// </summary>
        UInt32 PitchStart { get; set; }
        /// <summary>
        /// The angle the second angle blender goes to, or its value at the end of the geometry
        /// </summary>
        UInt32 PitchEnd { get; set; } // 66
        /// <summary>
        /// The angle the third angle blender (the yaw, half a turn in the game's levels) starts from with <see cref="CameraFlags.SetsYaw"/>,
        /// or its value at the start of the geometry
        /// </summary>
        UInt32 YawStart { get; set; }
        /// <summary>
        /// The angle the third angle blender goes to, or its value at the end of the geometry
        /// </summary>
        UInt32 YawEnd { get; set; } // 74
        /// <summary>
        /// The distance from the target the camera starts from with <see cref="CameraFlags.SetsDistance"/> (5, 10 and 4.5 in the
        /// game's levels, the controller's own is 10), or its distance at the start of the geometry
        /// </summary>
        Single DistanceStart { get; set; }
        /// <summary>
        /// The distance the camera goes to, or its distance at the end of the geometry
        /// </summary>
        Single DistanceEnd { get; set; }
        /// <summary>
        /// With <see cref="CameraFlags.SetsPositionFollowRate"/>, the rate the camera rig's point follower moves the camera's place to
        /// where the camera puts it (the share of the way a second), which the follow camera also moves at
        /// </summary>
        Single PositionFollowRate { get; set; }
        /// <summary>
        /// With <see cref="CameraFlags.SetsTargetFollowRate"/>, the rate the camera rig's point follower moves the point the camera
        /// looks at (the share of the way a second)
        /// </summary>
        Single TargetFollowRate { get; set; } // 90
        /// <summary>
        /// With <see cref="CameraFlags.SetsYawSpeed"/>, the yaw blender's speed (65536ths of a turn a second), slowed by the sine of
        /// how far it has left to turn. 0 in the game's levels, the scripts' SetCameraNodeValue sets it on the follow camera's own camera
        /// </summary>
        UInt32 YawSpeed { get; set; }
        /// <summary>
        /// With <see cref="CameraFlags.BlendsInFromYaw"/>, the yaw the yaw blender goes to instead of the start and end whenever the
        /// camera's yaw is at least as near it as <see cref="YawStart"/> (MainCamera::NearerBlendIn, every frame the camera is taken):
        /// a camera entered from that side keeps looking that way
        /// </summary>
        UInt32 BlendInYaw { get; set; } // 98
        /// <summary>
        /// With <see cref="CameraFlags.BlendsInFromPitch"/>, the pitch the pitch blender goes to instead of the start and end while the
        /// yaw is at least as near <see cref="BlendInYaw"/> as <see cref="YawStart"/>
        /// </summary>
        UInt32 BlendInPitch { get; set; }
        /// <summary>
        /// With <see cref="CameraFlags.BlendsInFromDistance"/>, the distance the camera goes to instead of the start and end while the
        /// yaw is at least as near <see cref="BlendInYaw"/> as <see cref="YawStart"/>
        /// </summary>
        Single BlendInDistance { get; set; } // 106
        CameraType TypeIndex1 { get; set; }
        CameraType TypeIndex2 { get; set; } // 114
        /// <summary>
        /// Cameras of different groups don't replace each other while both are nonzero (0 on most cameras, 1 to 5 on a few)
        /// </summary>
        Byte Group { get; set; } // 115
        /// <summary>
        /// The point the camera looks at for the player's position (the follow camera's target), none to look at the player
        /// </summary>
        CameraSubBase MainCamera1 { get; set; }
        /// <summary>
        /// Where the camera stands for the player's position, none to follow the player from behind
        /// </summary>
        CameraSubBase MainCamera2 { get; set; }

        enum CameraType
        {
            Null = 3,
            BossCamera = 0xA19,
            CameraPoint = 0x1C02,
            CameraLine = 0x1C03,
            CameraPath = 0x1C04,
            CameraSpline = 0x1C06,
            CameraSub1C09 = 0x1C09,
            CameraPoint2 = 0x1C0B,
            CameraSub1C0C = 0x1C0C,
            CameraLine2 = 0x1C0D,
            CameraZone = 0x1C0F,
        }

        /// <summary>
        /// The camera's flags word: what the follow camera (FollowCameraPositioner, FollowCameraTarget) and the camera controller take
        /// from the camera. Bits 1 and 14 are set on cameras of the game's levels, bit 14 is never read and bit 1 only by a switch
        /// back of the follow camera nothing starts
        /// </summary>
        [Flags]
        enum CameraFlags : UInt32
        {
            /// <summary>
            /// The second subtype gives its position at the controller's parameter instead of for the target's position
            /// </summary>
            SecondCameraAtParameter = 1 << 0,
            /// <summary>
            /// Lets the follow camera's switch back blend to its rig, which nothing starts
            /// </summary>
            Unused1 = 1 << 1,
            /// <summary>
            /// The pitch blender takes <see cref="PitchStart"/> and <see cref="PitchEnd"/>
            /// </summary>
            SetsPitch = 1 << 2,
            /// <summary>
            /// The distance takes <see cref="DistanceStart"/> and <see cref="DistanceEnd"/>
            /// </summary>
            SetsDistance = 1 << 3,
            /// <summary>
            /// The follow camera's probes stay on: it steers around walls (the follow camera's own camera always has it)
            /// </summary>
            Steers = 1 << 4,
            /// <summary>
            /// The camera cuts in instead of blending over <see cref="BlendTime"/>
            /// </summary>
            NoBlendIn = 1 << 5,
            /// <summary>
            /// The yaw blender takes <see cref="YawStart"/> and <see cref="YawEnd"/>
            /// </summary>
            SetsYaw = 1 << 6,
            /// <summary>
            /// The field of view's blender takes <see cref="FovStart"/> and <see cref="FovEnd"/>
            /// </summary>
            SetsFov = 1 << 7,
            /// <summary>
            /// The follow camera's target takes <see cref="TargetBoxMin"/> and <see cref="TargetBoxMax"/> as its box
            /// </summary>
            GivesTargetBox = 1 << 8,
            /// <summary>
            /// The follow camera's target moves toward the middle of the camera trigger's instances by <see cref="FramingShare"/>, up
            /// to <see cref="FramingDistance"/>
            /// </summary>
            FramesInstances = 1 << 9,
            /// <summary>
            /// The start and end values are the values at the start and the end of the camera's geometry, taken by how far along
            /// it the target is, instead of the two targets of a blend over time
            /// </summary>
            ValuesAlongGeometry = 1 << 10,
            /// <summary>
            /// The follow camera takes the camera's values even while it ignores cameras' values
            /// </summary>
            AlwaysTakesValues = 1 << 11,
            /// <summary>
            /// The rig's point follower moves the camera's place at <see cref="PositionFollowRate"/>
            /// </summary>
            SetsPositionFollowRate = 1 << 12,
            /// <summary>
            /// The rig's point follower moves the point the camera looks at at <see cref="TargetFollowRate"/>
            /// </summary>
            SetsTargetFollowRate = 1 << 13,
            Unused14 = 1 << 14,
            /// <summary>
            /// The yaw blender turns at <see cref="YawSpeed"/>
            /// </summary>
            SetsYawSpeed = 1 << 15,
            /// <summary>
            /// The yaw goes to <see cref="BlendInYaw"/> while it's at least as near it as <see cref="YawStart"/>
            /// </summary>
            BlendsInFromYaw = 1 << 16,
            /// <summary>
            /// The pitch goes to <see cref="BlendInPitch"/> while the yaw is at least as near <see cref="BlendInYaw"/> as
            /// <see cref="YawStart"/>
            /// </summary>
            BlendsInFromPitch = 1 << 17,
            /// <summary>
            /// The distance goes to <see cref="BlendInDistance"/> while the yaw is at least as near <see cref="BlendInYaw"/> as
            /// <see cref="YawStart"/>
            /// </summary>
            BlendsInFromDistance = 1 << 18,
            /// <summary>
            /// The follow camera only turns to look at the target
            /// </summary>
            OnlyLooksAtTarget = 1 << 19,
            /// <summary>
            /// The follow camera keeps its height and looks at the target
            /// </summary>
            KeepsHeight = 1 << 20,
            /// <summary>
            /// The follow camera doesn't move
            /// </summary>
            HoldsStill = 1 << 21,
            /// <summary>
            /// The follow camera tilts toward the way the target faces
            /// </summary>
            Tilts = 1 << 22,
            /// <summary>
            /// The follow camera's probes are off
            /// </summary>
            NoProbes = 1 << 23,
            /// <summary>
            /// The camera cuts in when the camera it replaces has this as well
            /// </summary>
            CutsFromSameKind = 1 << 24,
            /// <summary>
            /// The follow camera's target blends to the camera's point even when it's near
            /// </summary>
            BlendsWhenNear = 1 << 25,
            /// <summary>
            /// The camera is only accepted while another camera is running
            /// </summary>
            NeedsRunningCamera = 1 << 26,
            /// <summary>
            /// The camera is taken whatever the character does, without it a new camera is only taken on foot while the character is
            /// on the ground or the follow camera restarted (FUN_001434a0)
            /// </summary>
            IgnoresPlayerState = 1 << 27,
            /// <summary>
            /// The target box's point isn't turned with the followed object
            /// </summary>
            TargetBoxUnturned = 1 << 28,
            /// <summary>
            /// The follow camera blends its place along the line from where the blend started
            /// </summary>
            BlendsAlongLine = 1 << 29,
            /// <summary>
            /// The follow camera doesn't check its view of the target
            /// </summary>
            SkipsViewCheck = 1 << 30,
            /// <summary>
            /// The follow camera adds its own extra yaw (not the camera's) to the yaw
            /// </summary>
            AddsExtraYaw = 1U << 31
        }

        /// <summary>
        /// The camera's second flags word, 0 on nearly every camera of the game's levels
        /// </summary>
        [Flags]
        enum CameraSwitches : UInt16
        {
            /// <summary>
            /// A camera for the character's death: the follow camera only takes it once the character died, keeps it as its second
            /// camera and switches to it while the character is dead (FUN_001434a0)
            /// </summary>
            SecondSlot = 1 << 0,
            /// <summary>
            /// With <see cref="CameraFlags.NoProbes"/>, the probes stay on while the follow camera ignores cameras' values
            /// </summary>
            KeepsProbesWhileIgnoring = 1 << 1,
            /// <summary>
            /// Becoming the camera resets the controller
            /// </summary>
            ResetsController = 1 << 2
        }

        /// <summary>
        /// The flags a spline camera's game reads (CameraSplineCamera::Read), the other bits of its word are leftovers of the tools
        /// (15 or 0xCDCD in the retail data)
        /// </summary>
        [Flags]
        enum SplineCameraFlags : UInt16
        {
            /// <summary>
            /// Takes <see cref="CameraSubBase.Offset"/> as the offset along the curve instead of the samples' keys'
            /// </summary>
            TakesOffset = 1 << 0
        }
    }
}
