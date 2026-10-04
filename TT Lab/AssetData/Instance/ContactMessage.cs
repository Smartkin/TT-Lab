using System;
using System.IO;
using Newtonsoft.Json;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.AssetData.Instance;

/// <summary>
/// What touching a collision surface does to the agent it reaches (the game's ContactMessage, <see cref="TwinContactMessage"/>)
/// </summary>
[JsonObject]
public class ContactMessage : IDocumentModel
{
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Where the contact was, its W the push of a physical contact. 0 on every retail surface")]
    public Vector4 Point { get; set; } = new(0, 0, 0, 0);

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The kinds of hit the contact is, which the scripts' conditions test (each kind's tooltip says which): the deadly surfaces are " +
                     "FallingThrough, Electric, GenericHit, Burning and Sinking (lava), Water and Sinking (drowning), water Water. Rigid bodies " +
                     "touching the level's collision are told of a surface with any kind whatever its flags")]
    public Enums.ContactKinds Kinds { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The hit points the contact takes (100 on the deadly surfaces). At 0 the agent's script never hears of it")]
    public Byte Damage { get; set; }

    /// <summary>
    /// The bytes after the damage, the tools' memory
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public Byte[] Leftover { get; set; } = new Byte[TwinContactMessage.LeftoverSize];

    public string DocumentName => "Contact message";

    public ContactMessage()
    {
    }

    public ContactMessage(TwinContactMessage message)
    {
        Point = CloneUtils.Clone(message.Point);
        Kinds = (Enums.ContactKinds)message.Kinds;
        Damage = message.Damage;
        Leftover = CloneUtils.CloneArray(message.Leftover);
    }

    public void Write(BinaryWriter writer)
    {
        Point.Write(writer);
        writer.Write((UInt32)Kinds);
        writer.Write(Damage);
        writer.Write(Leftover, 0, TwinContactMessage.LeftoverSize);
    }
}
