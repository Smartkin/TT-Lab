using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.AssetData.Instance.Particle;

/// <summary>
/// An emitter placed in the chunk, see <see cref="TwinParticleEmitter"/>. Rotations are in 65536ths of a turn: the tilt turns about Z
/// (the up leans towards -X), then the yaw about Y, then the roll about X
/// </summary>
public class ParticleSystemInstance : IDocumentModel
{
    public ParticleSystemInstance()
    {
    }

    public ParticleSystemInstance(TwinParticleEmitter twinInst)
    {
        Version = twinInst.Version;
        Position = CloneUtils.Clone(twinInst.Position);
        GravityTilt = twinInst.GravityTilt;
        GravityYaw = twinInst.GravityYaw;
        EmitTilt = twinInst.EmitTilt;
        EmitYaw = twinInst.EmitYaw;
        EmitRoll = twinInst.EmitRoll;
        TimingOffset = twinInst.TimingOffset;
        (Name, NameLeftover) = ParticleNames.Split(twinInst.Name);
        SwitchType = twinInst.SwitchType;
        SwitchId = twinInst.SwitchId;
        SwitchValue = twinInst.SwitchValue;
        UnusedShort = twinInst.UnusedShort;
        BouncePlaneAngle = twinInst.BouncePlaneAngle;
        PlaneOffset = twinInst.PlaneOffset;
        BounceFactor = twinInst.BounceFactor;
        GroupId = twinInst.GroupId;
    }

    public void Write(BinaryWriter writer)
    {
        var twinInst = new TwinParticleEmitter();
        twinInst.Version = Version;
        twinInst.Position = CloneUtils.Clone(Position);
        twinInst.Name = ParticleNames.Join(Name, NameLeftover);
        twinInst.GravityTilt = GravityTilt;
        twinInst.GravityYaw = GravityYaw;
        twinInst.EmitTilt = EmitTilt;
        twinInst.EmitYaw = EmitYaw;
        twinInst.EmitRoll = EmitRoll;
        twinInst.TimingOffset = TimingOffset;
        twinInst.SwitchType = SwitchType;
        twinInst.SwitchId = SwitchId;
        twinInst.SwitchValue = SwitchValue;
        twinInst.UnusedShort = UnusedShort;
        twinInst.BouncePlaneAngle = BouncePlaneAngle;
        twinInst.PlaneOffset = PlaneOffset;
        twinInst.BounceFactor = BounceFactor;
        twinInst.GroupId = GroupId;

        twinInst.Write(writer);
    }

    [Editable] [EditorReadOnly] public UInt32 Version { get; set; } = 0x1E;
    [Editable] public Vector3 Position { get; set; } = new();

    // Turns the space the particles move and fall in, the emit rotation with it
    [Editable(Caption = "Gravity Tilt (degrees, about Z)", EditorDescType = typeof(AngleEditorDesc))] public Int16 GravityTilt { get; set; }
    [Editable(Caption = "Gravity Yaw (degrees, about Y)", EditorDescType = typeof(AngleEditorDesc))] public Int16 GravityYaw { get; set; }

    // Turns the direction the particles are emitted along, within the gravity space
    [Editable(Caption = "Emit Tilt (degrees, about Z)", EditorDescType = typeof(AngleEditorDesc))] public Int16 EmitTilt { get; set; }
    [Editable(Caption = "Emit Yaw (degrees, about Y)", EditorDescType = typeof(AngleEditorDesc))] public Int16 EmitYaw { get; set; }
    [Editable(Caption = "Emit Roll (degrees, about X)", EditorDescType = typeof(AngleEditorDesc))] public Int16 EmitRoll { get; set; }

    // Shifts the on and off cycle against the game's frame counter, and starts a rotor's sweep
    [Editable(Caption = "Timing Offset (frames)")] public Int32 TimingOffset { get; set; }

    // Name of the particle system it plays, the chunk's own or the default chunk's
    [Editable(Caption = "Particle System", EditorDescType = typeof(ParticleSystemEditorDesc))]
    [EditorParam(TextFieldViewModel.TextFieldStringLength, 16U)]
    [EditorParam(TextFieldViewModel.TextFieldAsciiOnly, true)]
    public string Name { get; set; } = "Particle Inst";

    // What the tools left in the name's buffer after its NUL, written back so the file stays the same
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? NameLeftover { get; set; }

    // The switches and group aren't read by the retail game
    [Editable] [EditorHidden] public Int32 SwitchType { get; set; }
    [Editable] [EditorHidden] public Int32 SwitchId { get; set; } = -1;
    [Editable] [EditorHidden] public Single SwitchValue { get; set; }
    [Editable] [EditorHidden] public Int16 UnusedShort { get; set; }

    // The Bounce sorts' plane: its height (or the vertical plane's distance) relative to the emitter, the vertical plane's angle about Y
    [Editable(Caption = "Bounce Plane Angle (degrees, BounceXZ)", EditorDescType = typeof(AngleEditorDesc))]
    [EditorLinkedField(typeof(BounceRead), nameof(Name))]
    public Int16 BouncePlaneAngle { get; set; }
    [Editable(Caption = "Plane Offset (Bounce sorts)")]
    [EditorLinkedField(typeof(BounceRead), nameof(Name))]
    public Single PlaneOffset { get; set; }
    [Editable(Caption = "Bounce Factor")]
    [EditorLinkedField(typeof(BounceRead), nameof(Name))]
    public Single BounceFactor { get; set; } = 0.9f;
    [Editable] [EditorHidden] public Int16 GroupId { get; set; }

    public string DocumentName => "Particle System Instance";

    private sealed class BounceRead : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode changed) => FollowPlayedSystem(listener);

        public void Linked(PropertyNode listener, PropertyNode changed) => FollowPlayedSystem(listener);
    }

    // The bounce values are read while the system the emitter plays bounces (GenParticle_Bounce, GenParticle_BounceXZ), the angle only
    // by the vertical plane's; an emitter whose system isn't found has nothing grayed out
    internal static void FollowPlayedSystem(PropertyNode field)
    {
        if (field.Target is not ParticleSystemInstance emitter || field.Name is not (nameof(BouncePlaneAngle) or nameof(PlaneOffset) or nameof(BounceFactor)))
        {
            return;
        }

        var sort = ParticlesOf(field)?.FindSystem(emitter.Name)?.System.GenSort;
        field.IsReadOnly = sort != null && (field.Name == nameof(BouncePlaneAngle)
            ? sort != TwinParticleSystem.GenSortType.BounceXZ
            : sort is not (TwinParticleSystem.GenSortType.Bounce or TwinParticleSystem.GenSortType.BounceXZ));
    }

    private static ParticleData? ParticlesOf(PropertyNode node)
    {
        for (var parent = node.Parent; parent != null; parent = parent.Parent)
        {
            if (parent.Target is ParticleData particles)
            {
                return particles;
            }
        }

        return null;
    }
}
