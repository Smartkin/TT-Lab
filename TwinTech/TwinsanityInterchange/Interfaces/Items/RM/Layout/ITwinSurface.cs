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
        /// The volume scales of the sounds of kinds 0, 4, 5, 1 and 2 (shared), 3 (-1 leaves the volume as it is), then 5 values
        /// the tools kept of which the game only reads the second, the friction the player and rigid bodies get on the surface
        /// (0.05 on ice, 1 on most). See <see cref="SurfacePhysics"/>.
        /// </summary>
        Single[] PhysicsParameters { get; set; }
        /// <summary>(0, 0, 0, 1) on every retail surface, never read</summary>
        Vector4 UnusedVector { get; set; }
        /// <summary>
        /// Two vectors handed to the player standing on a surface with <see cref="SurfaceCollisionFlags.SendsContactMessage"/> and to
        /// rigid bodies landing on the surface when the second's X isn't 0, whose bit 3 the player also checks. Leftover memory in
        /// the retail data.
        /// </summary>
        Vector4[] ContactMessage { get; set; }
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
        /// <summary>1000000 on most surfaces, 5 and 2 on the slippy ones, 120 on liquids and deadly surfaces. Never read.</summary>
        public const Int32 Unread1 = 5;
        /// <summary>What the player and rigid bodies grip the surface with: 1 on most, 0.7 on metal and rock, 0.05 on ice, 0 on the AI walls</summary>
        public const Int32 Friction = 6;
        /// <summary>1 on most, 0.5 on soft grounds, 0.1 on liquids and deadly surfaces. Never read.</summary>
        public const Int32 Unread2 = 7;
        /// <summary>35 or 45 on the slippy surfaces, 0 elsewhere. Never read.</summary>
        public const Int32 Unread3 = 8;
        /// <summary>0.98 or 0.99 on the slippy surfaces, 0 elsewhere. Never read.</summary>
        public const Int32 Unread4 = 9;
        public const Int32 Count = 10;
    }
}
