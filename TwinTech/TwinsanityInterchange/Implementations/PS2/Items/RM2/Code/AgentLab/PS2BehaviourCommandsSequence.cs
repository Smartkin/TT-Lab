using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab
{
    public class PS2BehaviourCommandsSequence : BaseTwinItem, ITwinBehaviourCommandsSequence
    {
        public ITwinBehaviourCommandsSequence.InstanceType Key { get; set; }
        public byte IndexInGlobalStorage { get; set; }
        public List<KeyValuePair<UInt16, ITwinBehaviourCommandPack>> BehaviourPacks { get; set; }
        public List<ITwinBehaviourCommand> Commands { get; set; }

        Boolean ITwinBehaviourCommandsSequence.HasNext { get; set; }

        public PS2BehaviourCommandsSequence()
        {
            BehaviourPacks = new List<KeyValuePair<UInt16, ITwinBehaviourCommandPack>>();
            Commands = new List<ITwinBehaviourCommand>();
        }

        public override int GetLength()
        {
            return 4 + BehaviourPacks.Sum(pair => pair.Value.GetLength()) + BehaviourPacks.Count * Constants.SIZE_UINT16 + Commands.Sum(com => com.GetLength());
        }

        public void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0)
        {
            StringUtils.WriteLineTabulated(writer, $"[GlobalIndex({IndexInGlobalStorage})]", tabs);
            StringUtils.WriteLineTabulated(writer, $"[InstanceType({Key})]", tabs);
            StringUtils.WriteLineTabulated(writer, $"library BehaviourLibrary_{id} {{", tabs);

            foreach (var packPair in BehaviourPacks)
            {
                StringUtils.WriteLineTabulated(writer, $"behaviour {(GameReservedIds)packPair.Key} {{", tabs + 1);
                packPair.Value.Decompile(resolver, writer, tabs + 2);
                StringUtils.WriteLineTabulated(writer, "}", tabs + 1);
                writer.WriteLine();
            }
            
            foreach (var cmd in Commands)
            {
                cmd.Decompile(resolver, writer, tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public override void Read(BinaryReader reader, int length)
        {
            var header = reader.ReadInt32();
            Key = (ITwinBehaviourCommandsSequence.InstanceType)(header & 0xFF);
            IndexInGlobalStorage = (Byte)(header >> 8 & 0xFF);
            BehaviourPacks.Clear();
            var packs = (Byte)(header >> 16 & 0xFF);
            for (var i = 0; i < packs; ++i)
            {
                var pack = new PS2BehaviourCommandPack();
                pack.Read(reader, length);
                var scriptId = reader.ReadUInt16();
                var pair = new KeyValuePair<UInt16, ITwinBehaviourCommandPack>(scriptId, pack);
                BehaviourPacks.Add(pair);
            }
            Commands.Clear();
            var com = new PS2BehaviourCommand();
            Commands.Add(com);
            com.Read(reader, length, Commands);
        }

        public override void Write(BinaryWriter writer)
        {
            var newHeader = (Int32)(BehaviourPacks.Count << 16) | (IndexInGlobalStorage << 8) | (Int32)(Key);
            writer.Write(newHeader);
            foreach (var pair in BehaviourPacks)
            {
                pair.Value.Write(writer);
                writer.Write(pair.Key);
            }
            foreach (var com in Commands)
            {
                com.HasNext = !Commands.Last().Equals(com);
                com.Write(writer);
            }
        }

        public void WriteText(StreamWriter writer, Int32 tabs = 0)
        {
            StringUtils.WriteLineTabulated(writer, "@PS2 Sequence", tabs);
            StringUtils.WriteLineTabulated(writer, $"BehaviourCommandsSequence() {"{"}", tabs);
            StringUtils.WriteLineTabulated(writer, $"Key = {Key}", tabs + 1);
            StringUtils.WriteLineTabulated(writer, $"IndexInGlobalStorage = {IndexInGlobalStorage}", tabs + 1);
            foreach (var packPair in BehaviourPacks)
            {
                StringUtils.WriteLineTabulated(writer, $"Pack({(GameReservedIds)packPair.Key}) {{", tabs + 1);
                packPair.Value.WriteText(writer, tabs + 2);
                StringUtils.WriteLineTabulated(writer, "}", tabs + 1);
            }
            foreach (var cmd in Commands)
            {
                cmd.WriteText(writer, tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public void ReadText(StreamReader reader)
        {
            String line = reader.ReadLine().Trim();
            BehaviourPacks.Clear();
            Commands.Clear();
            Debug.Assert(line == "@PS2 Sequence", "Trying to parse PS2 commands sequence as different version");
            while (!line.StartsWith("BehaviourCommandsSequence"))
            {
                line = reader.ReadLine().Trim();
            }
            StringUtils.GetStringInBetween(line, "(", ")");
            while (!line.EndsWith("{"))
            {
                line = reader.ReadLine().Trim();
            }

            while (!line.EndsWith("}"))
            {
                line = reader.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(line) || line == "}")
                {
                    continue;
                }

                if (line.StartsWith("Key"))
                {
                    Key = Enum.Parse<ITwinBehaviourCommandsSequence.InstanceType>(StringUtils.GetStringAfter(line, "=").Trim());
                    continue;
                }

                if (line.StartsWith("IndexInGlobalStorage"))
                {
                    IndexInGlobalStorage = Byte.Parse(StringUtils.GetStringAfter(line, "=").Trim());
                    continue;
                }
                
                if (line.StartsWith("Pack"))
                {
                    UInt16 arg = ushort.Parse(StringUtils.GetStringInBetween(line, "(", ")"));
                    PS2BehaviourCommandPack pack = new();
                    BehaviourPacks.Add(new KeyValuePair<UInt16, ITwinBehaviourCommandPack>(arg, pack));
                    while (!line.EndsWith("{"))
                    {
                        line = reader.ReadLine().Trim();
                    }
                    pack.ReadText(reader);
                }
                else
                {
                    PS2BehaviourCommand cmd = new();
                    Commands.Add(cmd);
                    cmd.ReadText(line);
                }
            }
        }

        public override String ToString()
        {
            using MemoryStream stream = new();
            using StreamWriter writer = new(stream);
            using StreamReader reader = new(stream);
            WriteText(writer);
            writer.Flush();
            stream.Position = 0;
            return reader.ReadToEnd();
        }

        public override String GetName()
        {
            return $"Behaviour Commands Sequence {id:X}";
        }

        public enum GameReservedIds : ushort
        {
            // Pickup custom agent
            PUP_STATE_INACTIVE = 562,
            PUP_STATE_INVISIBLE = 564,
            PUP_STATE_SPAWN_IN_AIR = 566,
            PUP_STATE_SPAWN_FROM_CRATE = 568,
            PUP_STATE_IDLE = 570,
            PUP_STATE_EXCITE = 572,
            PUP_STATE_SUCK_IN = 574,
            PUP_STATE_COLLECT = 576,
            PUP_STATE_FLY_AWAY = 578,
            PUP_STATE_DESTROY = 580,

            // Projectile custom agent
            PRO_STATE_INACTIVE = 590,
            PRO_STATE_INVISIBLE = 592,
            PRO_STATE_LAUNCH = 594,
            PRO_STATE_TRAVEL = 596,
            PRO_STATE_IMPACT_CRATE = 598,
            PRO_STATE_IMPACT_CREATURE = 600,
            PRO_STATE_IMPACT_FURNITURE = 602,
            PRO_STATE_IMPACT_PLAYER = 604,
            PRO_STATE_IMPACT_SCENERY = 606,
            PRO_STATE_CREATE_DAMAGE = 608,
            PRO_STATE_DESTROY = 610,
            PRO_STATE_UNK1 = 612,
            PRO_STATE_UNK2 = 614,
        }
    }
}
