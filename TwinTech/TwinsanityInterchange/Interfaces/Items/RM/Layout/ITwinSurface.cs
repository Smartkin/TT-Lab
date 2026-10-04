using System;
using Twinsanity.TwinsanityInterchange.Common;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    /// <summary>
    /// A collision surface: what the collision's triangles are made of. Objects touching it play its sounds and particles by
    /// contact kind (the game's kinds 0 to 5: impact, step 1, step 2, land, hard impact, scrape) with the kind's volume scale,
    /// the player's own footsteps come from the character's sound table by surface ID instead. Verified in the PAL executable.
    /// </summary>
    public interface ITwinSurface : ITwinItem
    {
        SurfaceCollisionFlags CollisionMask { get; set; }
        SurfaceType SurfaceId { get; set; }
        /// <summary>Kind 1's sound, scripts play it with DoSound</summary>
        UInt16 StepSoundId1 { get; set; }
        /// <summary>Kind 2's sound, scripts play it with DoSound</summary>
        UInt16 StepSoundId2 { get; set; }
        /// <summary>Kind 0's particle system: objects landing on the surface and touching water</summary>
        UInt16 ImpactParticleSystemId { get; set; }
        /// <summary>Kind 4's particle system: objects landing hard on the surface</summary>
        UInt16 HardImpactParticleSystemId { get; set; }
        /// <summary>Kind 0's sound: objects landing on the surface and touching water</summary>
        UInt16 ImpactSoundId { get; set; }
        /// <summary>Kind 4's sound: objects landing hard on the surface</summary>
        UInt16 HardImpactSoundId { get; set; }
        /// <summary>Kinds 1 and 2's particle system</summary>
        UInt16 StepParticleSystemId { get; set; }
        /// <summary>Kind 3's sound, scripts play it with DoSound (on sand it also throws up sand)</summary>
        UInt16 LandSoundId { get; set; }
        /// <summary>Kind 5's sound: objects scraping along the surface</summary>
        UInt16 ScrapeSoundId { get; set; }
        /// <summary>
        /// The volume scales of the sounds of kinds 0, 4, 5, 1 and 2 (shared), 3 (-1 leaves the volume as it is), then how fast the
        /// characters on it get to their steered velocity, the friction, what rigid bodies' bounce is scaled by, and how hard and
        /// from which slope it pulls the characters downhill. See <see cref="SurfacePhysics"/>.
        /// </summary>
        Single[] PhysicsParameters { get; set; }
        /// <summary>
        /// The flow: a velocity along the ground (X and Z) the surface carries the characters on it along at (AccelerateOnSurface adds it
        /// to where they're steered). (0, 0, 0, 1) on every retail surface
        /// </summary>
        Vector4 UnusedVector { get; set; }
        /// <summary>
        /// What touching the surface does: handed to the player standing on a surface with
        /// <see cref="SurfaceCollisionFlags.SendsContactMessageToPlayer"/> and to the agents of rigid bodies touching one with
        /// <see cref="SurfaceCollisionFlags.SendsContactMessageToObjects"/> (DynamicBody::SurfaceContact). The deadly surfaces deal
        /// 100 hit points of their kind of hit
        /// </summary>
        TwinContactMessage ContactMessage { get; set; }
    }

    /// <summary>
    /// Indexes into <see cref="ITwinSurface.PhysicsParameters"/>.
    /// </summary>
    public static class SurfacePhysics
    {
        public const Int32 ImpactSoundVolume = 0;
        public const Int32 HardImpactSoundVolume = 1;
        public const Int32 ScrapeSoundVolume = 2;
        public const Int32 StepSoundVolume = 3;
        public const Int32 LandSoundVolume = 4;
        /// <summary>
        /// How fast a character standing on the surface gets to its steered velocity plus the flow, in units a second per second
        /// (AccelerateOnSurface): 1000000 on most surfaces, 5 and 2 on the slippy ones, 120 on liquids and deadly surfaces. A held
        /// crouch slide's time runs 3.5 over it slower
        /// </summary>
        public const Int32 Unread5 = 5;
        /// <summary>
        /// How hard rigid bodies grip the surface (with their own friction) and how fast a character who left it steers in the air
        /// (10 + 40 times it a second): 1 on most, 0.7 on metal and rock, 0.05 on ice, 0 on the AI walls
        /// </summary>
        public const Int32 Friction = 6;
        /// <summary>What rigid bodies' restitution (their bounce) is multiplied by on the surface: 1 on most, 0.5 on soft grounds, 0.1 on liquids and deadly surfaces</summary>
        public const Int32 Restitution = 7;
        /// <summary>
        /// How hard ground steeper than <see cref="Unread9"/> pulls a character downhill, in units a second per second, all of it from
        /// 30 degrees on (AccelerateOnSurface): 35 or 45 on the slippy surfaces, 0 elsewhere
        /// </summary>
        public const Int32 Unread8 = 8;
        /// <summary>The ground's normal's Y below which the downhill pull works: 0.98 or 0.99 on the slippy surfaces, 0 elsewhere</summary>
        public const Int32 Unread9 = 9;
        public const Int32 Count = 10;
    }
}
