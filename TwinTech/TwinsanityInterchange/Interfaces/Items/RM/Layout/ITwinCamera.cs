using System;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    /// <summary>
    /// A camera trigger: the box the player has to be in, how the camera controller blends the camera in and the two subtypes that
    /// give the camera's position. What the controller does with the values was worked out from the PAL executable (FUN_00274e08
    /// applies a camera, FUN_00275cf0 and FUN_00274b20 switch to one, FUN_001434a0 accepts one, FUN_0026f3f0 runs the subtypes),
    /// the angles are 65536ths of a turn and the controller's three angle blenders are taken for the field of view, the pitch and
    /// the yaw by the values the game's levels give them (32°, 20° and 180°)
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
        /// Leftover memory of the tools (pointers, strings), never read
        /// </summary>
        Vector4 LeftoverVector1 { get; set; }
        /// <summary>
        /// Leftover memory of the tools, never read
        /// </summary>
        Vector4 LeftoverVector2 { get; set; } // 42
        /// <summary>
        /// Leftover memory of the tools, never read
        /// </summary>
        Single LeftoverFloat1 { get; set; }
        /// <summary>
        /// Leftover memory of the tools, never read
        /// </summary>
        Single LeftoverFloat2 { get; set; } // 50
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
        /// Handed to the second subtype when <see cref="CameraFlags.PassesValueToCamera2"/> is set, and kept by the controller
        /// </summary>
        Single Camera2Value { get; set; }
        /// <summary>
        /// Handed to the first subtype when <see cref="CameraFlags.PassesValueToCamera1"/> is set
        /// </summary>
        Single Camera1Value { get; set; } // 90
        /// <summary>
        /// An angle given to the yaw blender with <see cref="CameraFlags.SetsYawExtra"/>, 0 in the game's levels
        /// </summary>
        UInt32 YawExtra { get; set; }
        /// <summary>
        /// The yaw the camera blends in from instead of the start and end with <see cref="CameraFlags.BlendsInFromYaw"/>
        /// </summary>
        UInt32 BlendInYaw { get; set; } // 98
        /// <summary>
        /// The pitch the camera blends in from instead of the start and end with <see cref="CameraFlags.BlendsInFromPitch"/>
        /// </summary>
        UInt32 BlendInPitch { get; set; }
        /// <summary>
        /// The distance the camera blends in from instead of the start and end with <see cref="CameraFlags.BlendsInFromDistance"/>
        /// </summary>
        Single BlendInDistance { get; set; } // 106
        CameraType TypeIndex1 { get; set; }
        CameraType TypeIndex2 { get; set; } // 114
        /// <summary>
        /// Cameras of different groups don't replace each other while both are nonzero (0 on most cameras, 1 to 5 on a few)
        /// </summary>
        Byte Group { get; set; } // 115
        CameraSubBase MainCamera1 { get; set; }
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
        /// The camera's flags word. The bits without a name (1, 8, 9, 14, 25, 28) are set on cameras of the game's levels and never
        /// read, the ones named Controller are copied into the camera controller's own flags where their effect wasn't traced
        /// </summary>
        [Flags]
        enum CameraFlags : UInt32
        {
            /// <summary>
            /// The second subtype gives its position at the controller's parameter instead of for the target's position
            /// </summary>
            SecondCameraAtParameter = 1 << 0,
            /// <summary>
            /// The pitch blender takes <see cref="PitchStart"/> and <see cref="PitchEnd"/>
            /// </summary>
            SetsPitch = 1 << 2,
            /// <summary>
            /// The distance takes <see cref="DistanceStart"/> and <see cref="DistanceEnd"/>
            /// </summary>
            SetsDistance = 1 << 3,
            Controller4 = 1 << 4,
            /// <summary>
            /// The camera cuts in instead of blending over <see cref="BlendTime"/>
            /// </summary>
            NoBlendIn = 1 << 5,
            /// <summary>
            /// The yaw blender takes <see cref="YawStart"/> and <see cref="YawEnd"/>
            /// </summary>
            SetsYaw = 1 << 6,
            /// <summary>
            /// The first angle blender takes <see cref="FovStart"/> and <see cref="FovEnd"/>
            /// </summary>
            SetsFov = 1 << 7,
            /// <summary>
            /// The start and end values are the values at the start and the end of the camera's geometry, taken by how far along
            /// it the target is, instead of the two targets of a blend over time
            /// </summary>
            ValuesAlongGeometry = 1 << 10,
            Controller11 = 1 << 11,
            /// <summary>
            /// Hands <see cref="Camera2Value"/> to the second subtype
            /// </summary>
            PassesValueToCamera2 = 1 << 12,
            /// <summary>
            /// Hands <see cref="Camera1Value"/> to the first subtype
            /// </summary>
            PassesValueToCamera1 = 1 << 13,
            /// <summary>
            /// Gives the yaw blender <see cref="YawExtra"/>
            /// </summary>
            SetsYawExtra = 1 << 15,
            /// <summary>
            /// While blending in the yaw comes from <see cref="BlendInYaw"/>
            /// </summary>
            BlendsInFromYaw = 1 << 16,
            /// <summary>
            /// While blending in the pitch comes from <see cref="BlendInPitch"/>
            /// </summary>
            BlendsInFromPitch = 1 << 17,
            /// <summary>
            /// While blending in the distance comes from <see cref="BlendInDistance"/>
            /// </summary>
            BlendsInFromDistance = 1 << 18,
            Controller19 = 1 << 19,
            Controller20 = 1 << 20,
            Controller21 = 1 << 21,
            Controller22 = 1 << 22,
            Controller23 = 1 << 23,
            /// <summary>
            /// The camera cuts in when the camera it replaces has this as well
            /// </summary>
            CutsFromSameKind = 1 << 24,
            /// <summary>
            /// The camera is only accepted while another camera is running
            /// </summary>
            NeedsRunningCamera = 1 << 26,
            /// <summary>
            /// The camera is accepted whatever the player's state, without it the player has to be in a state the check
            /// doesn't tell (FUN_001434a0)
            /// </summary>
            IgnoresPlayerState = 1 << 27,
            Controller29 = 1 << 29,
            Controller30 = 1 << 30,
            Controller31 = 1U << 31
        }

        /// <summary>
        /// The camera's second flags word, 0 on nearly every camera of the game's levels
        /// </summary>
        [Flags]
        enum CameraSwitches : UInt16
        {
            /// <summary>
            /// The camera is kept as the player's second camera when the controller allows one (FUN_001434a0)
            /// </summary>
            SecondSlot = 1 << 0,
            Controller1 = 1 << 1,
            /// <summary>
            /// Becoming the camera resets the controller
            /// </summary>
            ResetsController = 1 << 2
        }
    }
}
