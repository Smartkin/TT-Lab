using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.AssetData.Instance.Particle;

/// <summary>
/// A particle system, see <see cref="TwinParticleSystem"/> for what the game does with each value
/// </summary>
public class ParticleSystem : IDocumentModel
{
    public ParticleSystem()
    {
    }

    public ParticleSystem(TwinParticleSystem twinPs)
    {
        Version = twinPs.Version;
        (Name, NameLeftover) = ParticleNames.Split(twinPs.Name);
        GenRate = twinPs.GenRate;
        MaxParticleCount = twinPs.MaxParticleCount;
        TimingOffset = twinPs.TimingOffset;
        EmitterOverTime = twinPs.EmitterOverTime;
        EmitterOverTimeRandom = twinPs.EmitterOverTimeRandom;
        EmitterOffTime = twinPs.EmitterOffTime;
        EmitterOffTimeRandom = twinPs.EmitterOffTimeRandom;
        GenSort = (TwinParticleSystem.GenSortType)twinPs.GenSort;
        GenCode = twinPs.GenCode;
        BlendMode = twinPs.BlendMode;
        UnusedByte = twinPs.UnusedByte;
        UnusedFloat1 = twinPs.UnusedFloat1;
        CutOnRadius = twinPs.CutOnRadius;
        CutOffRadius = twinPs.CutOffRadius;
        DrawCutOff = twinPs.DrawCutOff;
        UnusedFloat5 = twinPs.UnusedFloat5;
        UnusedFloat6 = twinPs.UnusedFloat6;
        Velocity = twinPs.Velocity;
        RandomEmit = CloneUtils.Clone(twinPs.RandomEmit);
        RandomStart = CloneUtils.Clone(twinPs.RandomStart);
        StartRandomScale = CloneUtils.Clone(twinPs.StartRandomScale);
        StartBase = CloneUtils.Clone(twinPs.StartBase);
        VelocityRandomScale = CloneUtils.Clone(twinPs.VelocityRandomScale);
        VelocityBase = CloneUtils.Clone(twinPs.VelocityBase);
        Gravity = twinPs.Gravity;
        ParticleLifeTime = twinPs.ParticleLifeTime;
        TextureFrameCount = twinPs.TextureFrameCount;
        TextureFrameStart = twinPs.TextureFrameStart;
        TextureFrameHold = twinPs.TextureFrameHold;
        TextureFrameRate = twinPs.TextureFrameRate;
        JibberXFreq = twinPs.JibberXFreq;
        JibberXAmp = twinPs.JibberXAmp;
        JibberYFreq = twinPs.JibberYFreq;
        JibberYAmp = twinPs.JibberYAmp;
        ColorGradients = CloneUtils.CloneArray(twinPs.ColorGradients);
        AlphaGradient = CloneUtils.CloneArray(twinPs.AlphaGradient);
        Distortion = CloneUtils.Clone(twinPs.Distortion);
        MinSize = twinPs.MinSize;
        MaxSize = twinPs.MaxSize;
        SizeWidth = CloneUtils.CloneArray(twinPs.SizeWidth);
        SizeHeight = CloneUtils.CloneArray(twinPs.SizeHeight);
        MinRotation = twinPs.MinRotation;
        MaxRotation = twinPs.MaxRotation;
        Rotation = CloneUtils.CloneArray(twinPs.Rotation);
        UnusedGradient1 = CloneUtils.CloneArray(twinPs.UnusedGradient1);
        UnusedGradient2 = CloneUtils.CloneArray(twinPs.UnusedGradient2);
        TextureStart = CloneUtils.Clone(twinPs.TextureStart);
        TextureEnd = CloneUtils.Clone(twinPs.TextureEnd);
        CollisionRadius = CloneUtils.CloneArray(twinPs.CollisionRadius);
        CollisionNumSpheres = twinPs.CollisionNumSpheres;
        DrawFlag = twinPs.DrawFlag;
        ScaleFactor = twinPs.ScaleFactor;
        ParticleGhostsNum = twinPs.ParticleGhostsNum;
        GhostSeparation = twinPs.GhostSeparation;
        StarRadialPoints = twinPs.StarRadialPoints;
        StarRadiusRatio = twinPs.StarRadiusRatio;
        RampTime = twinPs.RampTime;
        TexturePage = twinPs.TexturePage;
        BoundingExtents = CloneUtils.Clone(twinPs.BoundingExtents);
    }

    /// <summary>
    /// How many particles the emitter has around at once, the way the game works it out when it loads the system
    /// </summary>
    public int ComputeMaxParticleCount()
    {
        return TwinParticleSystem.ComputeMaxParticleCount(GenRate, ParticleLifeTime, EmitterOverTime, EmitterOverTimeRandom, EmitterOffTime, ParticleGhostsNum);
    }

    public void Write(BinaryWriter writer)
    {
        var twinPs = new TwinParticleSystem();
        twinPs.Version = Version;
        twinPs.Name = ParticleNames.Join(Name, NameLeftover);
        twinPs.GenRate = GenRate;
        // The game works it out again when it loads the system, the tools stored the same
        MaxParticleCount = (UInt16)Math.Min(ComputeMaxParticleCount(), UInt16.MaxValue);
        twinPs.MaxParticleCount = MaxParticleCount;
        twinPs.TimingOffset = TimingOffset;
        twinPs.EmitterOverTime = EmitterOverTime;
        twinPs.EmitterOverTimeRandom = EmitterOverTimeRandom;
        twinPs.EmitterOffTime = EmitterOffTime;
        twinPs.EmitterOffTimeRandom = EmitterOffTimeRandom;
        twinPs.GenSort = (Byte)GenSort;
        twinPs.GenCode = GenCode;
        twinPs.BlendMode = BlendMode;
        twinPs.UnusedByte = UnusedByte;
        twinPs.UnusedFloat1 = UnusedFloat1;
        twinPs.CutOnRadius = CutOnRadius;
        twinPs.CutOffRadius = CutOffRadius;
        twinPs.DrawCutOff = DrawCutOff;
        twinPs.UnusedFloat5 = UnusedFloat5;
        twinPs.UnusedFloat6 = UnusedFloat6;
        twinPs.Velocity = Velocity;
        twinPs.RandomEmit = CloneUtils.Clone(RandomEmit);
        twinPs.RandomStart = CloneUtils.Clone(RandomStart);
        twinPs.StartRandomScale = CloneUtils.Clone(StartRandomScale);
        twinPs.StartBase = CloneUtils.Clone(StartBase);
        twinPs.VelocityRandomScale = CloneUtils.Clone(VelocityRandomScale);
        twinPs.VelocityBase = CloneUtils.Clone(VelocityBase);
        twinPs.Gravity = Gravity;
        twinPs.ParticleLifeTime = ParticleLifeTime;
        twinPs.TextureFrameCount = TextureFrameCount;
        twinPs.TextureFrameStart = TextureFrameStart;
        twinPs.TextureFrameHold = TextureFrameHold;
        twinPs.TextureFrameRate = TextureFrameRate;
        twinPs.JibberXFreq = JibberXFreq;
        twinPs.JibberXAmp = JibberXAmp;
        twinPs.JibberYFreq = JibberYFreq;
        twinPs.JibberYAmp = JibberYAmp;
        twinPs.ColorGradients = CloneUtils.CloneArray(ColorGradients);
        twinPs.AlphaGradient = CloneUtils.CloneArray(AlphaGradient);
        twinPs.Distortion = CloneUtils.Clone(Distortion);
        twinPs.MinSize = MinSize;
        twinPs.MaxSize = MaxSize;
        twinPs.SizeWidth = CloneUtils.CloneArray(SizeWidth);
        twinPs.SizeHeight = CloneUtils.CloneArray(SizeHeight);
        twinPs.MinRotation = MinRotation;
        twinPs.MaxRotation = MaxRotation;
        twinPs.Rotation = CloneUtils.CloneArray(Rotation);
        twinPs.UnusedGradient1 = CloneUtils.CloneArray(UnusedGradient1);
        twinPs.UnusedGradient2 = CloneUtils.CloneArray(UnusedGradient2);
        twinPs.TextureStart = CloneUtils.Clone(TextureStart);
        twinPs.TextureEnd = CloneUtils.Clone(TextureEnd);
        twinPs.CollisionRadius = CloneUtils.CloneArray(CollisionRadius);
        twinPs.CollisionNumSpheres = CollisionNumSpheres;
        twinPs.DrawFlag = DrawFlag;
        twinPs.ScaleFactor = ScaleFactor;
        twinPs.ParticleGhostsNum = ParticleGhostsNum;
        twinPs.GhostSeparation = GhostSeparation;
        twinPs.StarRadialPoints = StarRadialPoints;
        twinPs.StarRadiusRatio = StarRadiusRatio;
        twinPs.RampTime = RampTime;
        twinPs.TexturePage = TexturePage;
        twinPs.BoundingExtents = CloneUtils.Clone(BoundingExtents);

        twinPs.Write(writer);
    }

    [Editable] [EditorReadOnly] public UInt32 Version { get; set; } = 0x1E;

    [Editable(EditorDescType = typeof(ParticleSystemNameEditorDesc), Hint = "What emitters play the system by, its own in its version of the game")]
    [EditorParam(TextFieldViewModel.TextFieldStringLength, 16U)]
    public String Name { get; set; } = "NEW_SYSTEM";

    // What the tools left in the name's buffer after its NUL, written back so the file stays the same
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public String? NameLeftover { get; set; }

    // Per frame when positive, one every that many frames when negative
    [Editable(Caption = "Gen Rate (per frame, -N = every N frames)")] public Int16 GenRate { get; set; } = 1;

    // The game works it out from the rate, the timing, the lifetime and the ghosts when it loads the system
    [Editable] [EditorReadOnly] public UInt16 MaxParticleCount { get; set; }

    [Editable(Caption = "Timing Offset (frames)")] public UInt16 TimingOffset { get; set; }

    [Editable(Caption = "On Time (frames)")] public UInt16 EmitterOverTime { get; set; } = 1;

    [Editable(Caption = "On Time Random (frames)")] public UInt16 EmitterOverTimeRandom { get; set; }

    [Editable(Caption = "Off Time (frames)")] public UInt16 EmitterOffTime { get; set; }

    [Editable(Caption = "Off Time Random (frames)")] public UInt16 EmitterOffTimeRandom { get; set; }

    [Editable] public TwinParticleSystem.GenSortType GenSort { get; set; }

    // 0 none, 1 velocity XZ = 2 × start, 2 velocity XZ = -start, 3 velocity XZ = 4 × start, 4 velocity XZ = 16 × start, 5 pulled in with a
    // random lifetime, 6 velocity = 5.4 × start
    [Editable(Caption = "Gen Code (velocity rule)")] public Byte GenCode { get; set; }

    // Which of the page's shaders draws the particles, 7 draws distorting hexagons (see ParticleBlendModes)
    [Editable(EditorDescType = typeof(ParticleBlendModeEditorDesc), Hint = "How the particles are drawn over what's behind them, hover a choice for what the game does with it")]
    public Byte BlendMode { get; set; }

    [Editable] public Byte UnusedByte { get; set; }

    [Editable] public Single UnusedFloat1 { get; set; } = 25.0f;

    [Editable(Caption = "Cut On Radius (camera at least)")] public Single CutOnRadius { get; set; }

    [Editable(Caption = "Cut Off Radius (camera at most, 0 any)")] public Single CutOffRadius { get; set; } = 25.0f;

    [Editable(Caption = "Draw Cut Off (camera at most)")] public Single DrawCutOff { get; set; } = 9999.9f;

    [Editable] public Single UnusedFloat5 { get; set; }

    [Editable] public Single UnusedFloat6 { get; set; } = 0.5f;

    // Along the emitter's up, or along the radius of the radial sorts
    [Editable] public Single Velocity { get; set; }

    // Box sorts: ± velocity per axis. Radial sorts: X ± speed, Y ± turn, Z ± tilt in 65536ths of a turn
    [Editable] public Vector3 RandomEmit { get; set; } = new();

    // Box sorts: ± start per axis. Radial sorts: X radius, Y turn, Z tilt from the up in 65536ths of a turn
    [Editable] public Vector3 RandomStart { get; set; } = new();

    // The Ranges sorts (2 to 5): start = random × scale + base, the random 0 to 2^31
    [Editable(Caption = "Start Random Scale (Ranges sorts)")] public Vector3 StartRandomScale { get; set; } = new();

    [Editable(Caption = "Start Base (Ranges sorts)")] public Vector3 StartBase { get; set; } = new();

    [Editable(Caption = "Velocity Random Scale (Ranges sorts)")] public Vector3 VelocityRandomScale { get; set; } = new();

    [Editable(Caption = "Velocity Base (Ranges sorts)")] public Vector3 VelocityBase { get; set; } = new();

    // A particle is at start + velocity × t + gravity × t² along the gravity up, so negative falls
    [Editable(Caption = "Gravity (× t², negative falls)")] public Single Gravity { get; set; }

    [Editable(Caption = "Particle Life Time (seconds)")] public Single ParticleLifeTime { get; set; } = 1.0f;

    // Probably a texture animation, the game works the rate out and then never reads them
    [Editable(Caption = "Texture Frame Count (never read by the game)")] public UInt16 TextureFrameCount { get; set; }

    [Editable(Caption = "Texture Frame Start (never read by the game)")] public Byte TextureFrameStart { get; set; }

    [Editable(Caption = "Texture Frame Hold (never read by the game)")] public Byte TextureFrameHold { get; set; } = 1;

    [Editable] [EditorReadOnly] public Single TextureFrameRate { get; set; }

    [Editable(Caption = "Jibber X Freq (cycles per life)")] public Single JibberXFreq { get; set; }

    [Editable(Caption = "Jibber X Amp (ten thousandths)")] public Single JibberXAmp { get; set; }

    [Editable(Caption = "Jibber Y Freq (cycles per life)")] public Single JibberYFreq { get; set; }

    [Editable(Caption = "Jibber Y Amp (ten thousandths)")] public Single JibberYAmp { get; set; }

    // Curves are keys of a time in the particle's life and a value, they end at the first key at 1
    [Editable(Caption = "Color (128 leaves the texture as is)", EditorDescType = typeof(ParticleGradientEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector4[] ColorGradients { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable(Caption = "Alpha", EditorDescType = typeof(ParticleCurveEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector2[] AlphaGradient { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    // Only the Distortion mode's: the hexagon's rim shows what's behind it moved by its offset from the middle times X / 16384 and -Y / 4096
    // in the game's screen units, fading out to nothing 40 units from the camera
    [Editable(Caption = "Distortion (Distortion mode)", Hint = "A positive X pulls what the hexagon shows in towards its middle sideways (magnifying), a positive Y pushes it out up and down, negative values the other way. Both fade out to nothing 40 units away from the camera")]
    public Vector2 Distortion { get; set; } = new()
    {
        X = 0.125f,
        Y = 0.125f
    };

    // The tools' ranges for the curves, the game doesn't read them
    [Editable(Caption = "Min Size (editor range)")] public Single MinSize { get; set; }

    [Editable(Caption = "Max Size (editor range)")] public Single MaxSize { get; set; } = 500.0f;

    [Editable(Caption = "Width (ten thousandths)", EditorDescType = typeof(ParticleCurveEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector2[] SizeWidth { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable(Caption = "Height (ten thousandths)", EditorDescType = typeof(ParticleCurveEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector2[] SizeHeight { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable(Caption = "Min Rotation (editor range)")] public Single MinRotation { get; set; } = -360.0f;

    [Editable(Caption = "Max Rotation (editor range)")] public Single MaxRotation { get; set; } = 360.0f;

    [Editable(Caption = "Angle (65536ths of a turn)", EditorDescType = typeof(ParticleCurveEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector2[] Rotation { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable(Caption = "Unused Gradient 1", EditorDescType = typeof(ParticleCurveEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector2[] UnusedGradient1 { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable(Caption = "Unused Gradient 2", EditorDescType = typeof(ParticleCurveEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector2[] UnusedGradient2 { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    // Pixels of the page plus 2^19, the game keeps the low 10 bits of the pixel (see ParticleTextureRect). Edited with the page
    [Editable] [EditorHidden] public Vector2 TextureStart { get; set; } = new();

    [Editable] [EditorHidden] public Vector2 TextureEnd { get; set; } = new();

    [Editable(Caption = "Collision Radius", EditorDescType = typeof(ParticleCurveEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public Vector2[] CollisionRadius { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] public Byte CollisionNumSpheres { get; set; }

    // The list the game draws the system from: only list 0 and, for the Distortion mode, list 2 are drawn
    [Editable(Caption = "Draw List", EditorDescType = typeof(ParticleDrawListEditorDesc), Hint = "The list the game draws the system from. List 0 is drawn every frame, list 1 never, list 2 only with the Distortion blend mode (which always goes in it)")]
    public Byte DrawFlag { get; set; }

    [Editable(Caption = "Scale Factor (super scale)")] public Single ScaleFactor { get; set; }

    [Editable] public Int16 ParticleGhostsNum { get; set; }

    [Editable(Caption = "Ghost Separation (seconds)")] public Single GhostSeparation { get; set; }

    [Editable(Caption = "Star Radial Points (Star sort)")] public Int16 StarRadialPoints { get; set; } = 5;

    [Editable(Caption = "Star Radius Ratio (Star sort)")] public Single StarRadiusRatio { get; set; } = 0.5f;

    [Editable(Caption = "Ramp Time (radial sorts, seconds)")] public Single RampTime { get; set; }

    // Which of the default chunk's texture pages the picture is on, edited together with the rectangle it takes from it
    [Editable(Caption = "Texture", EditorDescType = typeof(ParticleTextureEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top,
        Hint = "Click one of the game's pictures, outlined on the page, drag the rectangle or its corners, or drag elsewhere on the page to take another part of it")]
    public Int32 TexturePage { get; set; }

    // Half size of the box the particles stay in, for culling
    [Editable] public Vector4 BoundingExtents { get; set; } = new() { X = 10, Y = 10, Z = 10 };

    public string DocumentName => "Particle System";
}
