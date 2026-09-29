using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Particles
{
    /// <summary>
    /// A particle system: how an emitter makes its particles and what they look like. Worked out from the PAL executable's
    /// loader (ReadParticleSystem 0x1b8670), generators (the table at 0x2e7890 by <see cref="GenSort"/>), per frame update
    /// (0x1b7818) and render table (0x1b0d28); the game keeps it in a 0x350 byte struct whose offsets the fields note.
    /// Every system of the retail game is version 0x1E
    /// </summary>
    public class TwinParticleSystem : ITwinSerializable
    {
        /// <summary>
        /// Generator kinds, the index into the game's generator table
        /// </summary>
        public enum GenSortType : byte
        {
            /// <summary>
            /// Starts within a box of <see cref="RandomStart"/> around the emitter and flies along the emitter's up at
            /// <see cref="Velocity"/> plus a random <see cref="RandomEmit"/> per axis, all rotated by the emitter's rotation
            /// </summary>
            Box = 0,
            /// <summary>
            /// The same as <see cref="Box"/>
            /// </summary>
            Box2 = 1,
            /// <summary>
            /// Start and velocity are random × <see cref="StartRandomScale"/> + <see cref="StartBase"/> per axis (unrotated)
            /// </summary>
            Ranges = 2,
            /// <summary>
            /// A leftover: starts on a line at a fixed angle, Y and its speed from the range fields
            /// </summary>
            Line = 3,
            /// <summary>
            /// A leftover: reuses the slot's last position, Y and its speed from the range fields
            /// </summary>
            Reuse = 4,
            /// <summary>
            /// <see cref="Ranges"/> with the lifetime scaled by a random 1 to 2.43
            /// </summary>
            RangesRandomLife = 5,
            /// <summary>
            /// On a ring or cone around the emitter's up: tilted from the up by <see cref="RandomStart"/>.Z ± <see cref="RandomEmit"/>.Z
            /// and turned around it by RandomStart.Y ± RandomEmit.Y (65536ths of a turn), RandomStart.X out, speed
            /// <see cref="Velocity"/> ± RandomEmit.X along that direction, the radius growing over <see cref="RampTime"/>
            /// </summary>
            Radial = 6,
            /// <summary>
            /// <see cref="Radial"/> with the angles sweeping: every particle turns them by RandomEmit.Y and .Z from the
            /// timing offset times them
            /// </summary>
            RadialRotor = 7,
            /// <summary>
            /// Starts within an ellipsoid of <see cref="RandomStart"/> radii, velocity like <see cref="Box"/>
            /// </summary>
            Spheroid = 8,
            /// <summary>
            /// <see cref="Box"/> bouncing off the plane <see cref="TwinParticleEmitter.PlaneOffset"/> below or above the
            /// emitter with <see cref="TwinParticleEmitter.BounceFactor"/>
            /// </summary>
            Bounce = 9,
            /// <summary>
            /// <see cref="Box"/> bouncing off a vertical plane turned by <see cref="TwinParticleEmitter.BouncePlaneAngle"/>
            /// </summary>
            BounceXZ = 10,
            /// <summary>
            /// <see cref="Radial"/> spread evenly over the sphere's area (the tilt by uniform sine), the angles the same
            /// </summary>
            Sphere = 11,
            /// <summary>
            /// <see cref="Radial"/> with the radius shaped into a star of <see cref="StarRadialPoints"/> points, in between
            /// them <see cref="StarRadiusRatio"/> of it
            /// </summary>
            Star = 12
        }

        public UInt32 Version;
        /// <summary>
        /// 0x00, emitters name the system they play
        /// </summary>
        public Char[] Name;
        /// <summary>
        /// 0x12, particles made per frame when positive, one every that many frames when negative, none when 0
        /// </summary>
        public Int16 GenRate;
        /// <summary>
        /// 0x14, how many particles can be around at once. The game works it out again when it loads the system
        /// (<see cref="ComputeMaxParticleCount"/>), the tools stored the same
        /// </summary>
        public UInt16 MaxParticleCount;
        /// <summary>
        /// 0x16, frames the emitter's on and off cycle is shifted by against the game's frame counter, and where
        /// <see cref="GenSortType.RadialRotor"/> starts its sweep
        /// </summary>
        public UInt16 TimingOffset;
        /// <summary>
        /// 0x18, frames the emitter is on for
        /// </summary>
        public UInt16 EmitterOverTime;
        /// <summary>
        /// 0x1a, 1 to this many frames added to the on time when it isn't 0
        /// </summary>
        public UInt16 EmitterOverTimeRandom;
        /// <summary>
        /// 0x1c, frames the emitter is off for between on times, 0 keeps it on
        /// </summary>
        public UInt16 EmitterOffTime;
        /// <summary>
        /// 0x1e, 1 to this many frames added to the off time when it isn't 0
        /// </summary>
        public UInt16 EmitterOffTimeRandom;
        /// <summary>
        /// 0x20, the generator (<see cref="GenSortType"/>)
        /// </summary>
        public Byte GenSort;
        /// <summary>
        /// 0x21, a rule applied to each particle after the generator: 0 none, 1 velocity XZ = 2 × start XZ, 2 velocity XZ = -start XZ,
        /// 3 velocity XZ = 4 × start XZ, 4 velocity XZ = 16 × start XZ, 5 velocity XZ -= 0.6 × start XZ, Y -= 0.4 × |start XZ| and
        /// the lifetime × a random 1 to 2.43, 6 velocity = 5.4 × start. Always 0 in the game's data
        /// </summary>
        public Byte GenCode;
        /// <summary>
        /// 0x22, which of the texture page's shaders draws the particles (0 to 3), 7 draws untextured hexagons that distort
        /// what's behind them by <see cref="Distortion"/> (12 per block instead of 32, always drawn "super early")
        /// </summary>
        public Byte BlendMode;
        /// <summary>
        /// 0x23, never read by the game, 0 in its data
        /// </summary>
        public Byte UnusedByte;
        /// <summary>
        /// 0x24, never read by the game (25 or 40000 in its data, version 0x20 files get 25)
        /// </summary>
        public Single UnusedFloat1;
        /// <summary>
        /// 0x28, the emitter only runs while the camera is at least this far away
        /// </summary>
        public Single CutOnRadius;
        /// <summary>
        /// 0x2c, the emitter only runs while the camera is at most this far away, 0 for any distance
        /// </summary>
        public Single CutOffRadius;
        /// <summary>
        /// 0x30, the emitter's particles are only drawn while the camera is closer than this
        /// </summary>
        public Single DrawCutOff;
        /// <summary>
        /// 0x34, never read by the game, 0 in its data
        /// </summary>
        public Single UnusedFloat5;
        /// <summary>
        /// 0x38, never read by the game, 0.5 in its data
        /// </summary>
        public Single UnusedFloat6;
        /// <summary>
        /// 0x3c, speed along the emitter's up (box sorts) or along the radius (radial sorts), negative goes down or in
        /// </summary>
        public Single Velocity;
        /// <summary>
        /// 0x40, box sorts: ± this much random velocity per axis. Radial sorts: X ± speed, Y ± turn around the up, Z ± tilt
        /// from the up, angles in 65536ths of a turn (the rotor's steps per particle)
        /// </summary>
        public Vector3 RandomEmit;
        /// <summary>
        /// 0x4c, box sorts: particles start ± this far from the emitter per axis (the ellipsoid's radii for <see cref="GenSortType.Spheroid"/>).
        /// Radial sorts: X how far out they start, Y the turn around the up and Z the tilt from it in 65536ths of a turn
        /// </summary>
        public Vector3 RandomStart;
        /// <summary>
        /// 0x58, <see cref="GenSortType.Ranges"/> and 3 to 5: start = random × this + <see cref="StartBase"/> per axis, the
        /// random being 0 to 2^31
        /// </summary>
        public Vector3 StartRandomScale;
        /// <summary>
        /// 0x64, see <see cref="StartRandomScale"/>
        /// </summary>
        public Vector3 StartBase;
        /// <summary>
        /// 0x70, <see cref="GenSortType.Ranges"/> and 3 to 5: velocity = random × this + <see cref="VelocityBase"/> per axis
        /// </summary>
        public Vector3 VelocityRandomScale;
        /// <summary>
        /// 0x7c, see <see cref="VelocityRandomScale"/>
        /// </summary>
        public Vector3 VelocityBase;
        /// <summary>
        /// 0x88, along the emitter's gravity up: a particle is at start + velocity × t + gravity × t² (the acceleration is
        /// twice this), so negative values fall
        /// </summary>
        public Single Gravity;
        /// <summary>
        /// 0x8c, seconds a particle lives, its curves run over it
        /// </summary>
        public Single ParticleLifeTime;
        /// <summary>
        /// 0x90, with <see cref="TextureFrameStart"/>, <see cref="TextureFrameHold"/> and <see cref="TextureFrameRate"/>
        /// probably a texture animation: the game works the rate out as count × 60 / hold when a level loads and then never
        /// reads any of them. 16 frames held for 3 in the game's explosions, fires and smoke, 0 elsewhere
        /// </summary>
        public UInt16 TextureFrameCount;
        /// <summary>
        /// 0x92, see <see cref="TextureFrameCount"/> (1 in the game's data)
        /// </summary>
        public Byte TextureFrameStart;
        /// <summary>
        /// 0x93, see <see cref="TextureFrameCount"/> (3 in the game's data, 0 counts as 1)
        /// </summary>
        public Byte TextureFrameHold;
        /// <summary>
        /// 0x94, see <see cref="TextureFrameCount"/>, written over when a level loads
        /// </summary>
        public Single TextureFrameRate;
        /// <summary>
        /// 0x98, sideways wobble: cycles over the particle's life
        /// </summary>
        public Single JibberXFreq;
        /// <summary>
        /// 0x9c, sideways wobble in ten thousandths of a unit
        /// </summary>
        public Single JibberXAmp;
        /// <summary>
        /// 0xa0, up and down wobble: cycles over the particle's life
        /// </summary>
        public Single JibberYFreq;
        /// <summary>
        /// 0xa4, up and down wobble in ten thousandths of a unit
        /// </summary>
        public Single JibberYAmp;
        /// <summary>
        /// 0xa8, keys of the time in the life (0 to 1) and red, green, blue 0 to 255, 128 leaves the texture as it is
        /// </summary>
        public Vector4[] ColorGradients;
        /// <summary>
        /// 0x128, keys of the time in the life and the alpha 0 to 255. The game divides the first key's value by the
        /// lifetime when a level loads
        /// </summary>
        public Vector2[] AlphaGradient;
        /// <summary>
        /// 0x168, how much <see cref="BlendMode"/> 7 particles distort what's behind them sideways and up
        /// </summary>
        public Vector2 Distortion;
        /// <summary>
        /// 0x170, the tools' range for the size curves, never read by the game
        /// </summary>
        public Single MinSize;
        /// <summary>
        /// 0x174, the tools' range for the size curves, the game only uses it for the bounds of old versions
        /// </summary>
        public Single MaxSize;
        /// <summary>
        /// 0x178, keys of the time in the life and the width in ten thousandths of a unit
        /// </summary>
        public Vector2[] SizeWidth;
        /// <summary>
        /// 0x1b8, keys of the time in the life and the height in ten thousandths of a unit
        /// </summary>
        public Vector2[] SizeHeight;
        /// <summary>
        /// 0x1f8, the tools' range for the rotation curve, never read by the game
        /// </summary>
        public Single MinRotation;
        /// <summary>
        /// 0x1fc, the tools' range for the rotation curve, never read by the game
        /// </summary>
        public Single MaxRotation;
        /// <summary>
        /// 0x200, keys of the time in the life and the angle in 65536ths of a turn
        /// </summary>
        public Vector2[] Rotation;
        /// <summary>
        /// 0x240, never read by the game, the empty curve in every system of its data
        /// </summary>
        public Vector2[] UnusedGradient1;
        /// <summary>
        /// 0x280, never read by the game, the empty curve in every system of its data
        /// </summary>
        public Vector2[] UnusedGradient2;
        /// <summary>
        /// 0x2c0, the texture rectangle's top left in pixels of the page. The game adds 2^19 and keeps the low 10 bits of
        /// the pixel, so its data stores them plus 2^19 (2^18 in a few systems)
        /// </summary>
        public Vector2 TextureStart;
        /// <summary>
        /// 0x2c8, the texture rectangle's bottom right, see <see cref="TextureStart"/>
        /// </summary>
        public Vector2 TextureEnd;
        /// <summary>
        /// 0x2d4, keys of the time in a collision sphere's life and its radius
        /// </summary>
        public Vector2[] CollisionRadius;
        /// <summary>
        /// 0x314, spheres that fly along the emitter's up at <see cref="Velocity"/> under gravity, spawned evenly over the
        /// lifetime, that things collide with
        /// </summary>
        public Byte CollisionNumSpheres;
        /// <summary>
        /// 0x315, the render list: 0 before the fog, 1 after it, 2 before everything ("super early")
        /// </summary>
        public Byte DrawFlag;
        private Int32 padAmount;
        /// <summary>
        /// 0x334, "super scale", handed to the culling program with <see cref="BoundingExtents"/>
        /// </summary>
        public Single ScaleFactor;
        /// <summary>
        /// 0x31e, copies of every particle made this many frames apart (<see cref="GhostSeparation"/>) that trail it
        /// </summary>
        public Int16 ParticleGhostsNum;
        /// <summary>
        /// 0x324, seconds between a particle and each of its ghosts
        /// </summary>
        public Single GhostSeparation;
        /// <summary>
        /// 0x320, points of a <see cref="GenSortType.Star"/>
        /// </summary>
        public Int16 StarRadialPoints;
        /// <summary>
        /// 0x32c, how far out a star's particles start between its points, as a fraction of the radius
        /// </summary>
        public Single StarRadiusRatio;
        /// <summary>
        /// 0x328, seconds a radial system's radius takes to grow from 0 after its emitter starts, 0 for none
        /// </summary>
        public Single RampTime;
        /// <summary>
        /// 0x330, which of the default chunk's three texture pages the particles are drawn with
        /// </summary>
        public Int32 TexturePage;
        /// <summary>
        /// 0x340, half size of the box around the emitter its particles stay in, for culling. Worked out from the speed,
        /// lifetime, start and size for older versions, 10 for the sorts that aren't boxes
        /// </summary>
        public Vector4 BoundingExtents;

        private readonly Dictionary<UInt32, Int32> versionSizeMap = new();
        public TwinParticleSystem()
        {
            Name = new Char[16];
            RandomEmit = new Vector3();
            RandomStart = new Vector3();
            StartRandomScale = new Vector3();
            StartBase = new Vector3();
            VelocityRandomScale = new Vector3();
            VelocityBase = new Vector3();
            ColorGradients = new Vector4[8];
            AlphaGradient = new Vector2[8];
            SizeWidth = new Vector2[8];
            SizeHeight = new Vector2[8];
            Rotation = new Vector2[8];
            UnusedGradient1 = new Vector2[8];
            UnusedGradient2 = new Vector2[8];
            CollisionRadius = new Vector2[8];
            BoundingExtents = new Vector4();
            Version = 0x1E;
        }
        public TwinParticleSystem(UInt32 ver) : this()
        {
            Version = ver;
        }

        public Int32 GetLength()
        {
            return versionSizeMap[Version];
        }

        public void Compile()
        {
            return;
        }

        /// <summary>
        /// How many particles the emitter has around at once, the way the game works it out when it loads the system: the
        /// ones made over one lifetime of frames with the on and off times, at least 1, times the ghosts and the particle
        /// </summary>
        public static Int32 ComputeMaxParticleCount(Int16 genRate, Single lifeTime, UInt16 onTime, UInt16 onTimeRandom, UInt16 offTime, Int16 ghosts)
        {
            var frames = (Int32)(lifeTime * 60.0f);
            var count = 0;
            var made = 0;
            var repeats = 1;
            var onLeft = offTime != 0 ? onTime + onTimeRandom : frames;
            for (var frame = 0; frame < frames; frame++)
            {
                if (onLeft == 0)
                {
                    repeats--;
                    if (repeats == 0)
                    {
                        made = 0;
                        onLeft = onTime + onTimeRandom;
                    }

                    continue;
                }

                if (genRate < 0)
                {
                    if (made % -genRate == 0)
                    {
                        count++;
                    }
                }
                else
                {
                    count += genRate;
                }

                onLeft--;
                made++;
                if (onLeft == 0)
                {
                    repeats = offTime;
                }
            }

            return Math.Max(count, 1) * (ghosts + 1);
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var basePos = reader.BaseStream.Position;

            Name = reader.ReadChars(16);
            if (Version == 0x20)
            {
                reader.ReadByte();
                TexturePage = reader.ReadSByte();
            }
            GenRate = reader.ReadInt16();
            MaxParticleCount = reader.ReadUInt16();
            TimingOffset = reader.ReadUInt16();
            EmitterOverTime = reader.ReadUInt16();
            EmitterOverTimeRandom = reader.ReadUInt16();
            EmitterOffTime = reader.ReadUInt16();
            EmitterOffTimeRandom = reader.ReadUInt16();
            GenSort = reader.ReadByte();
            GenCode = reader.ReadByte();
            BlendMode = reader.ReadByte();
            UnusedByte = reader.ReadByte();
            UnusedFloat1 = reader.ReadSingle();
            if (Version == 0x20)
            {
                UnusedFloat1 = 25;
                if (BlendMode > 3)
                {
                    BlendMode -= 4;
                }
            }
            CutOnRadius = 0;
            CutOffRadius = 25;
            if (Version >= 0x6)
            {
                CutOnRadius = reader.ReadSingle();
                CutOffRadius = reader.ReadSingle();
            }
            DrawCutOff = 0;
            if (Version >= 0xA)
            {
                DrawCutOff = reader.ReadSingle();
                if (DrawCutOff <= 2)
                {
                    DrawCutOff = 999999.875f;
                }
            }
            if (Version <= 0x16 || Version == 0x20)
            {
                UnusedFloat5 = 0;
            }
            else
            {
                UnusedFloat5 = reader.ReadSingle();
            }
            if (Version < 0x18 || Version == 0x20)
            {
                UnusedFloat6 = 0.5f;
            }
            else
            {
                UnusedFloat6 = reader.ReadSingle();
            }
            if (Version < 0x7)
            {
                reader.ReadBytes(8);
            }
            Velocity = reader.ReadSingle();
            RandomEmit.Read(reader, Constants.SIZE_VECTOR3);
            if (Version < 0x12)
            {
                reader.ReadBytes(0xC);
            }
            RandomStart.Read(reader, Constants.SIZE_VECTOR3);
            if (Version < 0x12)
            {
                reader.ReadBytes(0xC);
            }
            // Versions 0x12 and 0x13 stored half the tilt of radial systems
            if (Version is 0x12 or 0x13 && GenSort == (Byte)GenSortType.Radial)
            {
                RandomEmit.Z += RandomEmit.Z;
                RandomStart.Z += RandomStart.Z;
            }
            StartRandomScale.Read(reader, Constants.SIZE_VECTOR3);
            StartBase.Read(reader, Constants.SIZE_VECTOR3);
            VelocityRandomScale.Read(reader, Constants.SIZE_VECTOR3);
            VelocityBase.Read(reader, Constants.SIZE_VECTOR3);
            Gravity = reader.ReadSingle();
            ParticleLifeTime = reader.ReadSingle();
            TextureFrameCount = reader.ReadUInt16();
            TextureFrameStart = reader.ReadByte();
            TextureFrameHold = reader.ReadByte();
            TextureFrameRate = reader.ReadSingle();
            JibberXFreq = reader.ReadSingle();
            JibberXAmp = reader.ReadSingle();
            JibberYFreq = reader.ReadSingle();
            JibberYAmp = reader.ReadSingle();
            for (var i = 0; i < 8; ++i)
            {
                ColorGradients[i] = new Vector4();
                ColorGradients[i].Read(reader, Constants.SIZE_VECTOR4);
            }
            for (var i = 0; i < 8; ++i)
            {
                AlphaGradient[i] = new Vector2();
                AlphaGradient[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            Distortion = new Vector2
            {
                X = 0.125f,
                Y = 0.125f
            };
            if (Version >= 0x15)
            {
                Distortion.Read(reader, Constants.SIZE_VECTOR2);
            }
            MinSize = reader.ReadSingle();
            MaxSize = reader.ReadSingle();
            for (var i = 0; i < 8; ++i)
            {
                SizeWidth[i] = new Vector2();
                SizeWidth[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            for (var i = 0; i < 8; ++i)
            {
                SizeHeight[i] = new Vector2();
                SizeHeight[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            MinRotation = reader.ReadSingle();
            MaxRotation = reader.ReadSingle();
            for (var i = 0; i < 8; ++i)
            {
                Rotation[i] = new Vector2();
                Rotation[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnusedGradient1[i] = new Vector2();
                UnusedGradient1[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnusedGradient2[i] = new Vector2();
                UnusedGradient2[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            TextureStart = new Vector2();
            TextureStart.Read(reader, Constants.SIZE_VECTOR2);
            TextureEnd = new Vector2();
            TextureEnd.Read(reader, Constants.SIZE_VECTOR2);
            if (Version == 0x20)
            {
                reader.ReadBytes(4);
            }
            if (Version >= 0x3)
            {
                for (var i = 0; i < 8; ++i)
                {
                    CollisionRadius[i] = new Vector2();
                    CollisionRadius[i].Read(reader, Constants.SIZE_VECTOR2);
                }
                CollisionNumSpheres = reader.ReadByte();
            }
            if (Version >= 0x11)
            {
                DrawFlag = reader.ReadByte();
            }
            if (BlendMode == 0x7)
            {
                DrawFlag = 2;
            }
            if (Version == 0x20)
            {
                reader.ReadBytes(6);
            }
            if (Version > 0x16 && Version < 0x1D && Version != 0x20)
            {
                padAmount = reader.ReadInt32();
                reader.ReadBytes(padAmount * 24);
            }
            else
            {
                if (Version == 0x20)
                {
                    // 4 records of 12 bytes, the first starting with the scale
                    ScaleFactor = reader.ReadSingle();
                    reader.ReadBytes(44);
                }
            }
            ParticleGhostsNum = 0;
            GhostSeparation = 0;
            if (Version >= 0x10)
            {
                if (Version == 0x20)
                {
                    ParticleGhostsNum = reader.ReadInt16();
                    reader.ReadBytes(2);
                }
                else
                {
                    ParticleGhostsNum = (Int16)reader.ReadInt32();
                }
                GhostSeparation = reader.ReadSingle();
            }
            StarRadialPoints = 5;
            StarRadiusRatio = 0.5f;
            if (Version >= 0x19 && Version != 0x20)
            {
                StarRadialPoints = (Int16)reader.ReadInt32();
                StarRadiusRatio = reader.ReadSingle();
            }
            RampTime = 0;
            if (Version >= 0x1A && Version != 0x20)
            {
                RampTime = reader.ReadSingle();
            }
            if (Version != 0x20)
            {
                if (Version > 0x1A)
                {
                    TexturePage = reader.ReadInt32();
                }
                if (Version > 0x1B)
                {
                    ScaleFactor = reader.ReadSingle();
                }
            }
            if (Version >= 0x1E)
            {
                BoundingExtents.Read(reader, Constants.SIZE_VECTOR4);
            }
            else
            {
                ComputeBoundingExtents();
            }
            var sizePos = reader.BaseStream.Position;
            versionSizeMap[Version] = (Int32)(sizePos - basePos);
        }

        /// <summary>
        /// The bounds the game works out for versions without them: how far a box system's particles get in a lifetime, 10 for
        /// the other sorts
        /// </summary>
        public void ComputeBoundingExtents()
        {
            BoundingExtents.X = 10;
            BoundingExtents.Y = 10;
            BoundingExtents.Z = 10;
            BoundingExtents.W = 0;
            if (GenSort == 0)
            {
                var size = MaxSize * 0.0001f;
                BoundingExtents.X = ((Velocity + RandomEmit.X) * ParticleLifeTime + RandomStart.X + size) * 0.75f;
                BoundingExtents.Y = ((Velocity + RandomEmit.Y) * ParticleLifeTime + RandomStart.Y + size) * 0.75f;
                BoundingExtents.Z = ((Velocity + RandomEmit.Z) * ParticleLifeTime + RandomStart.Z + size) * 0.75f;
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name, 0, 16);
            if (Version == 0x20)
            {
                writer.Write((Byte)0);
                writer.Write((SByte)TexturePage);
            }
            writer.Write(GenRate);
            writer.Write(MaxParticleCount);
            writer.Write(TimingOffset);
            writer.Write(EmitterOverTime);
            writer.Write(EmitterOverTimeRandom);
            writer.Write(EmitterOffTime);
            writer.Write(EmitterOffTimeRandom);
            writer.Write(GenSort);
            writer.Write(GenCode);
            writer.Write(BlendMode);
            writer.Write(UnusedByte);
            writer.Write(UnusedFloat1);
            if (Version >= 0x6)
            {
                writer.Write(CutOnRadius);
                writer.Write(CutOffRadius);
            }
            if (Version >= 0xA)
            {
                writer.Write(DrawCutOff);
            }
            if (!(Version <= 0x16 || Version == 0x20))
            {
                writer.Write(UnusedFloat5);
            }
            if (!(Version < 0x18 || Version == 0x20))
            {
                writer.Write(UnusedFloat6);
            }
            if (Version < 0x7)
            {
                writer.Write(0);
                writer.Write(0);
            }
            writer.Write(Velocity);
            RandomEmit.Write(writer);
            if (Version < 0x12)
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }
            RandomStart.Write(writer);
            if (Version < 0x12)
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }
            StartRandomScale.Write(writer);
            StartBase.Write(writer);
            VelocityRandomScale.Write(writer);
            VelocityBase.Write(writer);
            writer.Write(Gravity);
            writer.Write(ParticleLifeTime);
            writer.Write(TextureFrameCount);
            writer.Write(TextureFrameStart);
            writer.Write(TextureFrameHold);
            writer.Write(TextureFrameRate);
            writer.Write(JibberXFreq);
            writer.Write(JibberXAmp);
            writer.Write(JibberYFreq);
            writer.Write(JibberYAmp);
            for (var i = 0; i < 8; ++i)
            {
                ColorGradients[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                AlphaGradient[i].Write(writer);
            }
            if (Version >= 0x15)
            {
                Distortion.Write(writer);
            }
            writer.Write(MinSize);
            writer.Write(MaxSize);
            for (var i = 0; i < 8; ++i)
            {
                SizeWidth[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                SizeHeight[i].Write(writer);
            }
            writer.Write(MinRotation);
            writer.Write(MaxRotation);
            for (var i = 0; i < 8; ++i)
            {
                Rotation[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnusedGradient1[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnusedGradient2[i].Write(writer);
            }
            TextureStart.Write(writer);
            TextureEnd.Write(writer);
            if (Version == 0x20)
            {
                writer.Write(0);
            }
            if (Version >= 0x3)
            {
                for (var i = 0; i < 8; ++i)
                {
                    CollisionRadius[i].Write(writer);
                }
                writer.Write(CollisionNumSpheres);
            }
            if (Version >= 0x11)
            {
                writer.Write(DrawFlag);
            }
            if (Version == 0x20)
            {
                writer.Write(0);
                writer.Write((Byte)0);
                writer.Write((Byte)0);
            }
            if (Version > 0x16 && Version < 0x1D && Version != 0x20)
            {
                writer.Write(padAmount);
                for (var i = 0; i < padAmount * 6; ++i)
                {
                    writer.Write(0);
                }
            }
            else
            {
                if (Version == 0x20)
                {
                    writer.Write(ScaleFactor);
                    for (var i = 0; i < 11; ++i)
                    {
                        writer.Write(0);
                    }
                }
            }
            if (Version >= 0x10)
            {
                if (Version == 0x20)
                {
                    writer.Write(ParticleGhostsNum);
                    writer.Write((Byte)0);
                    writer.Write((Byte)0);
                }
                else
                {
                    writer.Write((Int32)ParticleGhostsNum);
                }
                writer.Write(GhostSeparation);
            }
            if (Version >= 0x19 && Version != 0x20)
            {
                writer.Write((Int32)StarRadialPoints);
                writer.Write(StarRadiusRatio);
            }
            if (Version >= 0x1A && Version != 0x20)
            {
                writer.Write(RampTime);
            }
            if (Version != 0x20)
            {
                if (Version > 0x1A)
                {
                    writer.Write(TexturePage);
                }
                if (Version > 0x1B)
                {
                    writer.Write(ScaleFactor);
                }
            }
            if (Version >= 0x1E)
            {
                BoundingExtents.Write(writer);
            }
        }
    }
}
