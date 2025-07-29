using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab
{
    public class PS2BehaviourCommand : ITwinBehaviourCommand
    {
        public UInt32 Bitfield { get; set; }
        public UInt16 CommandIndex { get; set; }
        public List<UInt32> Arguments { get;
            set; }
        public AgentLabVersion Version { get => AgentLabVersion.PS2; }

        Boolean ITwinBehaviourCommand.HasNext { get; set; }

        public UInt32 CommandSize
        {
            get
            {
                return GetCommandSize(CommandIndex);
            }
        }

        public PS2BehaviourCommand()
        {
            Arguments = new List<UInt32>();
        }

        public int GetLength()
        {
            return 4 + (Arguments?.Count ?? 0) * 4;
        }

        public void Compile()
        {
            return;
        }

        public void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0)
        {
            WriteText(writer, tabs);
        }

        public void Read(BinaryReader reader, int length)
        {
            Bitfield = reader.ReadUInt32();
            CommandIndex = (UInt16)(Bitfield & 0xFFFF);
            Arguments.Clear();
            if (CommandSize - 0xC > 0)
            {
                var args = (CommandSize - 0xC) / 4;
                for (int i = 0; i < args; ++i)
                {
                    Arguments.Add(reader.ReadUInt32());
                }
            }
        }

        public void Read(BinaryReader reader, int length, IList<ITwinBehaviourCommand> commands)
        {
            Read(reader, length);
            var hasNext = (Bitfield & 0x1000000) != 0;
            if (hasNext)
            {
                var com = new PS2BehaviourCommand();
                commands.Add(com);
                com.Read(reader, length, commands);
            }
        }

        public void Write(BinaryWriter writer)
        {
            UInt32 newBitfield = CommandIndex;
            ITwinBehaviourCommand downCast = this;
            if (downCast.HasNext)
            {
                newBitfield |= 0x1000000;
            }
            // In reality all Bitfields should be obsoleted and only used to construct
            // the member variables during Read only, but for unresearched stuff we need
            // to carry over the unknown bits to preserve consistency
            newBitfield |= Bitfield & 0xFEFF0000;
            writer.Write(newBitfield);
            foreach (UInt32 arg in Arguments)
            {
                writer.Write(arg);
            }
        }

        public void WriteText(StreamWriter writer, Int32 tabs = 0)
        {
            AgentLabDefs defs = PS2BehaviourGraph.GetAgentLabDefs();
            StringUtils.WriteTabulated(writer, $"{MapCommand(CommandIndex, defs)}(", tabs);
            for (Int32 i = 0; i < Arguments.Count; ++i)
            {
                writer.Write($"{ToStringArgument(Arguments[i], i, CommandIndex, defs)}");
                if (i < Arguments.Count - 1)
                {
                    writer.Write(", ");
                }
            }
            writer.WriteLine(");");
        }

        private string MapCommand(UInt32 index, AgentLabDefs defs)
        {
            string str_index = index.ToString();
            if (defs.CommandMap.ContainsKey(str_index))
            {
                return defs.CommandMap[str_index].Name;
            }
            else
            {
                return $"AUnknown_{str_index}";
            }
        }

        private string ToStringArgument(UInt32 arg, Int32 pos, UInt32 index, AgentLabDefs defs)
        {
            string type = "hex";
            string str_index = index.ToString();
            if (defs.CommandMap.ContainsKey(str_index.ToString()))
            {
                List<string> types = defs.CommandMap[str_index].Arguments;
                if (pos < types.Count)
                {
                    type = types[pos].ToLower();
                }
            }
            return type switch
            {
                "int32" => BitConverter.ToInt32(BitConverter.GetBytes(arg), 0).ToString(),
                "int16" => BitConverter.ToInt16(BitConverter.GetBytes(0xFFFF & arg), 0).ToString(),
                "uint32" => arg.ToString(),
                "uint16" => (0xFFFF & arg).ToString(),
                "byte" => (0xFF & arg).ToString(),
                "single" => BitConverter.ToSingle(BitConverter.GetBytes(arg), 0).ToString(CultureInfo.InvariantCulture),
                _ => "0x" + arg.ToString("X8"),
            };
        }

        private UInt32 ToArgumentString(string arg, Int32 pos, UInt32 index, AgentLabDefs defs)
        {
            String type = "hex";
            if (!arg.StartsWith("0x"))
            {
                if (pos < defs.CommandMap[index.ToString()].Arguments.Count)
                {
                    type = defs.CommandMap[index.ToString()].Arguments[pos];
                }
            }
            switch (type)
            {
                case "int32":
                    {
                        Int32 val = Convert.ToInt32(arg);
                        return BitConverter.ToUInt32(BitConverter.GetBytes(val), 0);
                    }
                case "int16":
                    {
                        Int16 val = Convert.ToInt16(arg);
                        return BitConverter.ToUInt32(BitConverter.GetBytes(val), 0);
                    }
                case "uint32":
                    {
                        UInt32 val = Convert.ToUInt32(arg);
                        return BitConverter.ToUInt32(BitConverter.GetBytes(val), 0);
                    }
                case "uint16":
                    {
                        UInt16 val = Convert.ToUInt16(arg);
                        return BitConverter.ToUInt16(BitConverter.GetBytes(val), 0);
                    }
                case "byte":
                    {
                        Byte val = Convert.ToByte(arg);
                        return BitConverter.ToUInt32(new byte[1] { val }, 0);
                    }
                case "single":
                    {
                        Single val = Convert.ToSingle(arg, CultureInfo.InvariantCulture);
                        return BitConverter.ToUInt32(BitConverter.GetBytes(val), 0);
                    }
                case "hex":
                default:
                    return Convert.ToUInt32(arg[2..], 16);
            }
        }

        public void ReadText(String line)
        {
            AgentLabDefs defs = PS2BehaviourGraph.GetAgentLabDefs();
            Arguments.Clear();
            Debug.Assert(line.StartsWith($"@{Version}"), "Trying to parse PS2 command as a different version");
            if (line.StartsWith($"@{Version} ById_"))
            {
                CommandIndex = ushort.Parse(StringUtils.GetStringInBetween(line, "ById_", "("));
            }
            else
            {
                String cmd_Name = StringUtils.GetStringAfter(StringUtils.GetStringBefore(line, "("), $"@{Version}").Trim();
                CommandIndex = ushort.Parse(defs.CommandMap.FirstOrDefault(x => x.Value.Name == cmd_Name).Key);
            }
            String[] args = StringUtils.GetStringInBetween(line, "(", ")").Split(',');
            for (Int32 i = 0; i < args.Length; ++i)
            {
                if (string.IsNullOrWhiteSpace(args[i]))
                {
                    continue;
                }
                Arguments.Add(ToArgumentString(args[i].Trim(), i, CommandIndex, defs));
            }
        }

        public static UInt32 GetCommandSize(UInt16 index)
        {
            if (commandSizeMap == null)
            {
                commandSizeMap = new UInt32[1024];
                var commandSizes = PS2BehaviourGraph.GetAgentLabDefs().CommandSizes;
                Debug.Assert(commandSizes.Count == 1024, "Command sizes table must contain 1024 entries");
                for (Int32 i = 0; i < commandSizes.Count; i++)
                {
                    commandSizeMap[i] = Convert.ToUInt32(commandSizes[i][2..], 16);
                }
            }
            if (index < 0 || index >= commandSizeMap.Length)
            {
                return 0;
            }
            return commandSizeMap[index];
        }

        static UInt32[] commandSizeMap = null;

    }
}
