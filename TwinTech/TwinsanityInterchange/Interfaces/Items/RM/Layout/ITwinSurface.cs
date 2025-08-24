using System;
using Twinsanity.TwinsanityInterchange.Common;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinSurface : ITwinItem
    {
        /// <summary>
        /// Collision mask
        /// </summary>
        SurfaceCollisionFlags CollisionMask { get; set; }
        /// <summary>
        /// Surface's ID
        /// </summary>
        SurfaceType SurfaceId { get; set; }
        /// <summary>
        /// Sound/SFX ID to play when stepped on
        /// </summary>
        UInt16 StepSoundId1 { get; set; }
        /// <summary>
        /// Sound/SFX ID to play when stepped on (2nd variant?)
        /// </summary>
        UInt16 StepSoundId2 { get; set; }
        /// <summary>
        /// Particle system ID to use when walking on/in
        /// </summary>
        UInt16 WalkOnParticleSystemId { get; set; }
        /// <summary>
        /// Particle system ID to use when walking on/in (2nd variant?)
        /// </summary>
        UInt16 WalkOnParticleSystemId2 { get; set; }
        /// <summary>
        /// Sound/SFX to play when landed on
        /// </summary>
        UInt16 LandSoundId1 { get; set; }
        /// <summary>
        /// Unknown ID
        /// </summary>
        UInt16 UnkId3 { get; set; }
        /// <summary>
        /// Particle system ID to use when landed on/in
        /// </summary>
        UInt16 LandOnParticleSystemId { get; set; }
        /// <summary>
        /// Sound/SFX to play when landed on (2nd variant?)
        /// </summary>
        UInt16 LandSoundId2 { get; set; }
        /// <summary>
        /// Unknown sound ID
        /// </summary>
        UInt16 UnkSoundId { get; set; }
        /// <summary>
        /// Float parameters. Slippiness, friction, etc.
        /// </summary>
        Single[] PhysicsParameters { get; set; }
        /// <summary>
        /// Unknown vector
        /// </summary>
        Vector4 UnkVec { get; set; }
        /// <summary>
        /// Unknown bounding box
        /// </summary>
        Vector4[] UnkBoundingBox { get; set; }
    }
}
