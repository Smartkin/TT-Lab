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

public class ParticleSystemInstance : IDocumentModel
{
    public ParticleSystemInstance()
    {
    }

    public ParticleSystemInstance(TwinParticleEmitter twinInst)
    {
        Version = twinInst.Version;
        Position = CloneUtils.Clone(twinInst.Position);
        GravityRotX = twinInst.GravityRotX;
        GravityRotY = twinInst.GravityRotY;
        EmitRotX = twinInst.EmitRotX;
        EmitRotY = twinInst.EmitRotY;
        UnkShort5 = twinInst.UnkShort5;
        Offset = twinInst.Offset;
        Name = new String(twinInst.Name);
        Name = Name.Replace("\0", "");
        SwitchType = twinInst.SwitchType;
        SwitchId = twinInst.SwitchId;
        SwitchValue = twinInst.SwitchValue;
        UnkShort6 = twinInst.UnkShort6;
        UnkShort7 = twinInst.UnkShort7;
        PlaneOffset = twinInst.PlaneOffset;
        BounceFactor = twinInst.BounceFactor;
        GroupId = twinInst.GroupId;
    }

    public void Write(BinaryWriter writer)
    {
        var twinInst = new TwinParticleEmitter();
        twinInst.Version = Version;
        twinInst.Position = CloneUtils.Clone(Position);
        twinInst.Name = new Char[16];
        var nameIdx = 0;
        foreach (var c in Name.ToCharArray())
        {
            twinInst.Name[nameIdx++] = c;
            if (nameIdx >= 16)
            {
                break;
            }
        }
        while (nameIdx < 16)
        {
            twinInst.Name[nameIdx++] = '\0';
        }
        
        twinInst.GravityRotX = GravityRotX;
        twinInst.GravityRotY = GravityRotY;
        twinInst.EmitRotX = EmitRotX;
        twinInst.EmitRotY = EmitRotY;
        twinInst.UnkShort5 = UnkShort5;
        twinInst.Offset = Offset;
        twinInst.SwitchType = SwitchType;
        twinInst.SwitchId = SwitchId;
        twinInst.SwitchValue = SwitchValue;
        twinInst.UnkShort6 = UnkShort6;
        twinInst.UnkShort7 = UnkShort7;
        twinInst.PlaneOffset = PlaneOffset;
        twinInst.BounceFactor = BounceFactor;
        twinInst.GroupId = GroupId;
        
        twinInst.Write(writer);
    }

    [Editable] [EditorReadOnly] public UInt32 Version { get; set; } = 0x1E;
    [Editable] public Vector3 Position { get; set; } = new();
    [Editable] public Int16 GravityRotX { get; set; }
    [Editable] public Int16 GravityRotY { get; set; }
    [Editable] public Int16 EmitRotX { get; set; }
    [Editable] public Int16 EmitRotY { get; set; }
    [Editable] public Int16 UnkShort5 { get; set; }
    [Editable] public Int32 Offset { get; set; }

    [Editable]
    [EditorParam(TextFieldViewModel.TextFieldStringLength, 16U)]
    public string Name { get; set; } = "Particle Inst";

    [Editable] public Int32 SwitchType { get; set; }
    [Editable] public Int32 SwitchId { get; set; }
    [Editable] public Single SwitchValue { get; set; }
    [Editable] public Int16 UnkShort6 { get; set; }
    [Editable] public Int16 UnkShort7 { get; set; }
    [Editable] public Single PlaneOffset { get; set; }
    [Editable] public Single BounceFactor { get; set; }
    [Editable] public Int16 GroupId { get; set; }

    public string DocumentName => "Particle System Instance";
}