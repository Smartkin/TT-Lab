using System;
using System.IO;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.AssetData.Instance.Particle;

public class ParticleSystem : IDocumentModel
{
    public ParticleSystem()
    {
    }

    public ParticleSystem(TwinParticleSystem twinPs)
    {
        Version = twinPs.Version;
        Name = new String(twinPs.Name);
        Name = Name.Replace("\0", "");
        UnkByte1 = twinPs.UnkByte1;
        GenRate = twinPs.GenRate;
        MaxParticleCount = twinPs.MaxParticleCount;
        UnkUShort3 = twinPs.UnkUShort3;
        EmitterOverTime = twinPs.EmitterOverTime;
        EmitterOverTimeRandom = twinPs.EmitterOverTimeRandom;
        EmitterOffTime = twinPs.EmitterOffTime;
        EmitterOffTimeRandom = twinPs.EmitterOffTimeRandom;
        GenSort = twinPs.GenSort;
        UnkByte3 = twinPs.UnkByte3;
        TextureFilter = twinPs.TextureFilter;
        UnkByte5 = twinPs.UnkByte5;
        UnkFloat1 = twinPs.UnkFloat1;
        CutOnRadius = twinPs.CutOnRadius;
        CutOffRadius = twinPs.CutOffRadius;
        DrawCutOff = twinPs.DrawCutOff;
        UnkFloat5 = twinPs.UnkFloat5;
        UnkFloat6 = twinPs.UnkFloat6;
        Velocity = twinPs.Velocity;
        RandomEmit = CloneUtils.Clone(twinPs.RandomEmit);
        RandomStart = CloneUtils.Clone(twinPs.RandomStart);
        UnkFloat8 = twinPs.UnkFloat8;
        UnkFloat9 = twinPs.UnkFloat9;
        UnkFloat10 = twinPs.UnkFloat10;
        UnkFloat11 = twinPs.UnkFloat11;
        UnkFloat12 = twinPs.UnkFloat12;
        UnkFloat13 = twinPs.UnkFloat13;
        UnkFloat14 = twinPs.UnkFloat14;
        UnkFloat15 = twinPs.UnkFloat15;
        UnkFloat16 = twinPs.UnkFloat16;
        UnkFloat17 = twinPs.UnkFloat17;
        UnkFloat18 = twinPs.UnkFloat18;
        UnkFloat19 = twinPs.UnkFloat19;
        Gravity = twinPs.Gravity;
        ParticleLifeTime = twinPs.ParticleLifeTime;
        UnkUShort8 = twinPs.UnkUShort8;
        UnkByte6 = twinPs.UnkByte6;
        UnkByte7 = twinPs.UnkByte7;
        UnkFloat22 = twinPs.UnkFloat22;
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
        UnkGradient1 = CloneUtils.CloneArray(twinPs.UnkGradient1);
        UnkGradient2 = CloneUtils.CloneArray(twinPs.UnkGradient2);
        TextureStart = CloneUtils.Clone(twinPs.TextureStart);
        TextureEnd = CloneUtils.Clone(twinPs.TextureEnd);
        Collision = CloneUtils.CloneArray(twinPs.Collision);
        CollisionNumSpheres = twinPs.CollisionNumSpheres;
        DrawFlag = twinPs.DrawFlag;
        ScaleFactor = twinPs.ScaleFactor;
        ParticleGhostsNum = twinPs.ParticleGhostsNum;
        GhostSeparation = twinPs.GhostSeparation;
        StarRadialPoints = twinPs.StarRadialPoints;
        StarRadiusRatio = twinPs.StarRadiusRatio;
        RampTime = twinPs.RampTime;
        TexturePage = twinPs.TexturePage;
        UnkVec3 = CloneUtils.Clone(twinPs.UnkVec3);
    }

    public void Write(BinaryWriter writer)
    {
        var twinPs = new TwinParticleSystem();
        twinPs.Version = Version;
        twinPs.Name = new Char[16];
        var nameIdx = 0;
        foreach (var c in Name.ToCharArray())
        {
            twinPs.Name[nameIdx++] = c;
            if (nameIdx >= 16)
            {
                break;
            }
        }
        while (nameIdx < 16)
        {
            twinPs.Name[nameIdx++] = '\0';
        }
        
        twinPs.UnkByte1 = UnkByte1;
        twinPs.GenRate = GenRate;
        twinPs.MaxParticleCount = MaxParticleCount;
        twinPs.UnkUShort3 = UnkUShort3;
        twinPs.EmitterOverTime = EmitterOverTime;
        twinPs.EmitterOverTimeRandom = EmitterOverTimeRandom;
        twinPs.EmitterOffTime = EmitterOffTime;
        twinPs.EmitterOffTimeRandom = EmitterOffTimeRandom;
        twinPs.GenSort = GenSort;
        twinPs.UnkByte3 = UnkByte3;
        twinPs.TextureFilter = TextureFilter;
        twinPs.UnkByte5 = UnkByte5;
        twinPs.UnkFloat1 = UnkFloat1;
        twinPs.CutOnRadius = CutOnRadius;
        twinPs.CutOffRadius = CutOffRadius;
        twinPs.DrawCutOff = DrawCutOff;
        twinPs.UnkFloat5 = UnkFloat5;
        twinPs.UnkFloat6 = UnkFloat6;
        twinPs.Velocity = Velocity;
        twinPs.RandomEmit = CloneUtils.Clone(RandomEmit);
        twinPs.RandomStart = CloneUtils.Clone(RandomStart);
        twinPs.UnkFloat8 = UnkFloat8;
        twinPs.UnkFloat9 = UnkFloat9;
        twinPs.UnkFloat10 = UnkFloat10;
        twinPs.UnkFloat11 = UnkFloat11;
        twinPs.UnkFloat12 = UnkFloat12;
        twinPs.UnkFloat13 = UnkFloat13;
        twinPs.UnkFloat14 = UnkFloat14;
        twinPs.UnkFloat15 = UnkFloat15;
        twinPs.UnkFloat16 = UnkFloat16;
        twinPs.UnkFloat17 = UnkFloat17;
        twinPs.UnkFloat18 = UnkFloat18;
        twinPs.UnkFloat19 = UnkFloat19;
        twinPs.Gravity = Gravity;
        twinPs.ParticleLifeTime = ParticleLifeTime;
        twinPs.UnkUShort8 = UnkUShort8;
        twinPs.UnkByte6 = UnkByte6;
        twinPs.UnkByte7 = UnkByte7;
        twinPs.UnkFloat22 = UnkFloat22;
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
        twinPs.UnkGradient1 = CloneUtils.CloneArray(UnkGradient1);
        twinPs.UnkGradient2 = CloneUtils.CloneArray(UnkGradient2);
        twinPs.TextureStart = CloneUtils.Clone(TextureStart);
        twinPs.TextureEnd = CloneUtils.Clone(TextureEnd);
        twinPs.Collision = CloneUtils.CloneArray(Collision);
        twinPs.CollisionNumSpheres = CollisionNumSpheres;
        twinPs.DrawFlag = DrawFlag;
        twinPs.ScaleFactor = ScaleFactor;
        twinPs.ParticleGhostsNum = ParticleGhostsNum;
        twinPs.GhostSeparation = GhostSeparation;
        twinPs.StarRadialPoints = StarRadialPoints;
        twinPs.StarRadiusRatio = StarRadiusRatio;
        twinPs.RampTime = RampTime;
        twinPs.TexturePage = TexturePage;
        twinPs.UnkVec3 = CloneUtils.Clone(UnkVec3);
        
        twinPs.Write(writer);
    }

    [Editable] [EditorReadOnly] public UInt32 Version { get; set; } = 0x1E;
    
    [Editable] [EditorParam(TextFieldViewModel.TextFieldStringLength, 16U)]
    public String Name { get; set; } = "Particle System";

    [Editable] public Byte UnkByte1 { get; set; }

    [Editable] public UInt16 GenRate { get; set; }

    [Editable] public UInt16 MaxParticleCount { get; set; }

    [Editable] public UInt16 UnkUShort3 { get; set; }

    [Editable] public UInt16 EmitterOverTime { get; set; }

    [Editable] public UInt16 EmitterOverTimeRandom { get; set; }

    [Editable] public UInt16 EmitterOffTime { get; set; }

    [Editable] public UInt16 EmitterOffTimeRandom { get; set; }

    [Editable] public Byte GenSort { get; set; }

    [Editable] public Byte UnkByte3 { get; set; }

    [Editable] public Byte TextureFilter { get; set; }

    [Editable] public Byte UnkByte5 { get; set; }

    [Editable] public Single UnkFloat1 { get; set; }

    [Editable] public Single CutOnRadius { get; set; }

    [Editable] public Single CutOffRadius { get; set; }

    [Editable] public Single DrawCutOff { get; set; }

    [Editable] public Single UnkFloat5 { get; set; }

    [Editable] public Single UnkFloat6 { get; set; }

    [Editable] public Single Velocity { get; set; }

    [Editable] public Vector3 RandomEmit { get; set; } = new();

    [Editable] public Vector3 RandomStart { get; set; } = new();

    [Editable] public Single UnkFloat8 { get; set; }

    [Editable] public Single UnkFloat9 { get; set; }

    [Editable] public Single UnkFloat10 { get; set; }

    [Editable] public Single UnkFloat11 { get; set; }

    [Editable] public Single UnkFloat12 { get; set; }

    [Editable] public Single UnkFloat13 { get; set; }

    [Editable] public Single UnkFloat14 { get; set; }

    [Editable] public Single UnkFloat15 { get; set; }

    [Editable] public Single UnkFloat16 { get; set; }

    [Editable] public Single UnkFloat17 { get; set; }

    [Editable] public Single UnkFloat18 { get; set; }

    [Editable] public Single UnkFloat19 { get; set; }

    [Editable] public Single Gravity { get; set; }

    [Editable] public Single ParticleLifeTime { get; set; }

    [Editable] public UInt16 UnkUShort8 { get; set; }

    [Editable] public Byte UnkByte6 { get; set; }

    [Editable] public Byte UnkByte7 { get; set; }

    [Editable] public Single UnkFloat22 { get; set; }

    [Editable] public Single JibberXFreq { get; set; }

    [Editable] public Single JibberXAmp { get; set; }

    [Editable] public Single JibberYFreq { get; set; }

    [Editable] public Single JibberYAmp { get; set; }

    [Editable] [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector4[] ColorGradients { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector2[] AlphaGradient { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] public Vector2 Distortion { get; set; } = new()
    {
        X = 0.125f,
        Y = 0.125f
    };

    [Editable] public Single MinSize { get; set; }

    [Editable] public Single MaxSize { get; set; }

    [Editable] [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector2[] SizeWidth { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector2[] SizeHeight { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] public Single MinRotation { get; set; }

    [Editable] public Single MaxRotation { get; set; }

    [Editable] [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector2[] Rotation { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector2[] UnkGradient1 { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector2[] UnkGradient2 { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] public Vector2 TextureStart { get; set; } = new();

    [Editable] public Vector2 TextureEnd { get; set; } = new();

    [Editable] [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Vector2[] Collision { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new()];

    [Editable] public Byte CollisionNumSpheres { get; set; }

    [Editable] public Byte DrawFlag { get; set; }

    [Editable] public Single ScaleFactor { get; set; }

    [Editable] public Int16 ParticleGhostsNum { get; set; }

    [Editable] public Single GhostSeparation { get; set; }

    [Editable] public Int16 StarRadialPoints { get; set; }

    [Editable] public Single StarRadiusRatio { get; set; }

    [Editable] public Single RampTime { get; set; }

    [Editable] [EditorParam(TextFieldViewModel.TextFieldNumberRange, new[] { 0, 2 })]
    public Int32 TexturePage { get; set; }

    [Editable] public Vector4 UnkVec3 { get; set; } = new();

    public string DocumentName => "Particle System";
}