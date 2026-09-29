using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;
using Twinsanity.Libraries;
using Twinsanity.AgentLab;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab
{
    public class PS2BehaviourState : ITwinBehaviourState
    {
        public UInt16 Bitfield { get; set; }
        public Int16 BehaviourIndexOrSlot { get; set; }
        public Boolean HasCompletionBody { get; set; }
        public Boolean UsesObjectSlot { get; set; }
        public Boolean Interrupting { get; set; }
        public TwinBehaviourControlPacket ControlPacket { get; set; }
        public List<ITwinBehaviourStateBody> Bodies { get; set; }
        internal int Index { get; set; }

        bool ITwinBehaviourState.HasNext { get; set; }

        protected virtual PS2BehaviourState CreateState()
        {
            return new PS2BehaviourState();
        }

        protected virtual PS2BehaviourStateBody CreateStateBody()
        {
            return new PS2BehaviourStateBody();
        }

        public PS2BehaviourState()
        {
            Bodies = new List<ITwinBehaviourStateBody>(0x1F);
            BehaviourIndexOrSlot = -1;
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
            var stateResolver = resolver as IStateResolver;
            ControlPacket?.Decompile(resolver, writer, tabs);

            writer.WriteLine();

            if (ControlPacket != null)
            {
                StringUtils.WriteLineTabulated(writer, $"[ControlPacket({ControlPacket.Name})]", tabs);
            }
            if (UsesObjectSlot)
            {
                StringUtils.WriteLineTabulated(writer, $"[UseObjectSlot({(ITwinBehaviourState.ObjectBehaviourSlots)BehaviourIndexOrSlot})]", tabs);
            }
            if (Interrupting)
            {
                StringUtils.WriteLineTabulated(writer, "[Interrupting]", tabs);
            }

            for (var i = 0; i < Bodies.Count; i++)
            {
                Bodies[i].IsCompletionBody = HasCompletionBody && i == 0;
            }

            // A completion block marks the first body, the attribute is only for a first body that can't be written as one
            var firstIsCompletionBlock = Bodies.Count > 0 && (Bodies[0].Condition == null || Bodies[0].Condition.IsPlainNext);
            if (HasCompletionBody && !firstIsCompletionBlock)
            {
                StringUtils.WriteLineTabulated(writer, "[SkipFirstBody]", tabs);
            }

            var behaviourIndex = stateResolver?.ResolveBehaviour() ?? BehaviourIndexOrSlot.ToString();
            if (BehaviourIndexOrSlot == -1)
            {
                behaviourIndex = null;
            }
            
            if (behaviourIndex != null && !UsesObjectSlot)
            {
                // A behaviour's name is written as it is, a URI or an index as a string
                var reference = AgentLabLexer.IsIdentifier(behaviourIndex) && !AgentLabLexer.IsReservedKeyword(behaviourIndex) ? behaviourIndex : $"\"{behaviourIndex}\"";
                StringUtils.WriteLineTabulated(writer, $"state State_{Index}({reference}) {{", tabs);
            }
            else
            {
                StringUtils.WriteLineTabulated(writer, $"state State_{Index}() {{", tabs);
            }

            foreach (var body in Bodies)
            {
                body.Decompile(resolver, writer, tabs + 1);
            }
            
            StringUtils.WriteLineTabulated(writer, "}", tabs);
            writer.WriteLine();
        }

        public void Read(BinaryReader reader, int length)
        {
            Bitfield = reader.ReadUInt16();
            HasCompletionBody = (Bitfield & 0x400) != 0;
            Interrupting = (Bitfield & 0x800) != 0;
            UsesObjectSlot = (Bitfield & 0x1000) != 0;
            BehaviourIndexOrSlot = reader.ReadInt16();
            if ((Bitfield & 0x4000) != 0)
            {
                ControlPacket = new TwinBehaviourControlPacket();
                ControlPacket.PacketIndex = Index;
                ControlPacket.Read(reader, length);
            }
        }

        public void Read(BinaryReader reader, int length, IList<ITwinBehaviourState> scriptStates)
        {
            Read(reader, length);
            var hasNext = (Bitfield & 0x8000) != 0;
            if (hasNext)
            {
                var state = CreateState();
                scriptStates.Add(state);
                state.Read(reader, length, scriptStates);
            }
        }
        
        public void Write(BinaryWriter writer)
        {
            UInt16 newBitfield = (UInt16)(Bodies.Count & 0x1F);
            if (HasCompletionBody)
            {
                newBitfield |= 0x400;
            }
            if (Interrupting)
            {
                newBitfield |= 0x800;
            }
            if (UsesObjectSlot)
            {
                newBitfield |= 0x1000;
            }
            if (ControlPacket != null)
            {
                newBitfield |= 0x4000;
            }
            ITwinBehaviourState downCast = this;
            if (downCast.HasNext)
            {
                newBitfield |= 0x8000;
            }
            Bitfield = newBitfield;
            writer.Write(newBitfield);
            writer.Write(BehaviourIndexOrSlot);
            ControlPacket?.Write(writer);
        }
        public void WriteText(StreamWriter writer, Int32 i, Int32 tabs = 0)
        {
            if (BehaviourIndexOrSlot != -1)
            {
                StringUtils.WriteLineTabulated(writer, $"State_{i}({BehaviourIndexOrSlot}) {{", tabs);
                writer.WriteLine();
            }
            else
            {
                StringUtils.WriteLineTabulated(writer, $"State_{i}() {"{"}", tabs);
                writer.WriteLine();
            }
            if (UsesObjectSlot)
            {
                StringUtils.WriteLineTabulated(writer, $"uses_object_slot = {UsesObjectSlot}", tabs + 1);
            }
            if (Interrupting)
            {
                StringUtils.WriteLineTabulated(writer, "interrupting", tabs + 1);
            }
            if (HasCompletionBody)
            {
                StringUtils.WriteLineTabulated(writer, "has_completion_body", tabs + 1);
            }
            ControlPacket?.WriteText(writer, tabs + 1);
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
                    while (!line.EndsWith("}"))
                    {
                        line = reader.ReadLine().Trim();
                    }
                    line = reader.ReadLine().Trim();
                }
                if (line.StartsWith("Body"))
                {
                    var body = CreateStateBody();
                    while (!line.EndsWith("{"))
                    {
                        line = reader.ReadLine().Trim();
                    }
                    body.ReadText(reader);
                    Bodies.Add(body);
                }
                if (line.StartsWith("uses_object_slot"))
                {
                    UsesObjectSlot = Boolean.Parse(StringUtils.GetStringAfter(line, "=").Trim());
                }
                if (line.StartsWith("interrupting"))
                {
                    Interrupting = true;
                }
                if (line.StartsWith("has_completion_body"))
                {
                    HasCompletionBody = true;
                }
            }
        }
    }
}
