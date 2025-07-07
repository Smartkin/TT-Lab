using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab
{
    public class XboxBehaviourState : ITwinBehaviourState
    {
        public UInt16 Bitfield { get; set; }
        public Int16 BehaviourIndexOrSlot { get; set; }
        public Boolean SkipsFirstStateBody { get; set; }
        public Boolean UsesObjectSlot { get; set; }
        public bool NoneBlocking { get; set; }
        public TwinBehaviourControlPacket ControlPacket { get; set; }
        public List<ITwinBehaviourStateBody> Bodies { get; set; }

        bool ITwinBehaviourState.HasNext { get; set; }

        public XboxBehaviourState()
        {
            Bodies = new List<ITwinBehaviourStateBody>(0x1F);
        }

        public int GetLength()
        {
            return 4 + (ControlPacket != null ? ControlPacket.GetLength() : 0) + Bodies.Sum(body => body.GetLength());
        }

        public void Compile()
        {
            return;
        }

        public void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0)
        {
            throw new NotImplementedException();
        }

        public void Read(BinaryReader reader, int length)
        {
            Bitfield = reader.ReadUInt16();
            BehaviourIndexOrSlot = reader.ReadInt16();
            if ((Bitfield & 0x4000) != 0)
            {
                ControlPacket = new TwinBehaviourControlPacket();
                ControlPacket.Read(reader, length);
            }
        }

        public void Read(BinaryReader reader, int length, IList<ITwinBehaviourState> scriptStates)
        {
            Read(reader, length);
            var hasNext = (Bitfield & 0x8000) != 0;
            if (hasNext)
            {
                var state = new XboxBehaviourState();
                scriptStates.Add(state);
                state.Read(reader, length, scriptStates);
            }
        }

        public void Write(BinaryWriter writer)
        {
            UInt16 newBitfield = (UInt16)Bodies.Count;
            if (ControlPacket != null)
            {
                newBitfield |= 0x4000;
            }
            ITwinBehaviourState downCast = this;
            if (downCast.HasNext)
            {
                newBitfield |= 0x8000;
            }
            newBitfield |= Bitfield;
            writer.Write(newBitfield);
            writer.Write(BehaviourIndexOrSlot);
            ControlPacket?.Write(writer);
        }
        public void WriteText(StreamWriter writer, Int32 i, Int32 tabs = 0)
        {
            if (BehaviourIndexOrSlot != -1)
            {
                StringUtils.WriteLineTabulated(writer, $"State_{i}({BehaviourIndexOrSlot}) {"{"}", tabs);
                writer.WriteLine();
            }
            else
            {
                StringUtils.WriteLineTabulated(writer, $"State_{i}() {"{"}", tabs);
                writer.WriteLine();
            }
            if (ControlPacket != null)
            {
                ControlPacket.WriteText(writer, tabs + 1);
            }
            foreach (var body in Bodies)
            {
                body.WriteText(writer, tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs);
            writer.WriteLine();
        }

        public void ReadText(StreamReader reader)
        {
            String line = "";
            ControlPacket = null;
            Bodies.Clear();
            while (!line.EndsWith("}"))
            {
                line = reader.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("ControlPacket"))
                {
                    ControlPacket = new TwinBehaviourControlPacket();
                    while (!line.EndsWith("{"))
                    {
                        line = reader.ReadLine().Trim();
                    }
                    ControlPacket.ReadText(reader);
                }
                if (line.StartsWith("Body"))
                {
                    XboxBehaviourStateBody body = new();
                    while (!line.EndsWith("{"))
                    {
                        line = reader.ReadLine().Trim();
                    }
                    body.ReadText(reader);
                    Bodies.Add(body);
                }
            }
        }
    }
}
