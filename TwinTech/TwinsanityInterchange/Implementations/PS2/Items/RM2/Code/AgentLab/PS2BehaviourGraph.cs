using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab
{
    public class PS2BehaviourGraph : TwinBehaviourWrapper, ITwinBehaviourGraph
    {
        private static AgentLabDefs AgentLabDefs = null;

        public static AgentLabDefs GetAgentLabDefs()
        {
            if (AgentLabDefs == null)
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                using FileStream stream = new(Path.Combine(Path.GetDirectoryName(path), @"AgentLabDefsPS2.json"), FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(stream);
                JsonSerializerOptions options = new()
                {
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                AgentLabDefs = JsonSerializer.Deserialize<AgentLabDefs>(reader.ReadToEnd(), options);
            }
            return AgentLabDefs;
        }

        public String Name { get; set; }
        public Int32 StartState { get; set; }
        public List<ITwinBehaviourState> ScriptStates { get; set; }

        public PS2BehaviourGraph()
        {
            ScriptStates = new List<ITwinBehaviourState>();
        }

        public override int GetLength()
        {
            var totalLen = 4 + Name.Length + 8;
            totalLen += ScriptStates.Sum(s => s.GetLength());
            totalLen += base.GetLength();
            return totalLen;
        }

        public override String GetName()
        {
            return Name;
        }

        public override void Decompile(StreamWriter writer, int tabs = 0)
        {
            StringUtils.WriteLineTabulated(writer, $"[StartFrom(State_{StartState})]", tabs);
            StringUtils.WriteLineTabulated(writer, $"[Priority({Priority})]", tabs);
            StringUtils.WriteLineTabulated(writer, $"behaviour {Name} {{", tabs);
            foreach (var state in ScriptStates)
            {
                state.Decompile(writer, tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, length);
            var NameLen = reader.ReadInt32();
            Name = new String(reader.ReadChars(NameLen));
            var statesAmt = reader.ReadInt32();
            StartState = reader.ReadInt32();
            ScriptStates.Clear();
            for (int i = 0; i < statesAmt; ++i)
            {
                PS2BehaviourState state = new();
                state.Index = i;
                state.Read(reader, length);
                ScriptStates.Add(state);
            }
            foreach (var state in ScriptStates)
            {
                state.Bodies.Clear();
                var bodiesAmt = state.Bitfield & 0x1F;
                for (var i = 0; i < bodiesAmt; ++i)
                {
                    PS2BehaviourStateBody stateBody = new();
                    state.Bodies.Add(stateBody);
                    stateBody.Read(reader, length);
                }
            }
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(Name.Length);
            writer.Write(Name.ToCharArray());
            writer.Write(ScriptStates.Count);
            writer.Write(StartState);
            foreach (var state in ScriptStates)
            {
                state.HasNext = !ScriptStates.Last().Equals(state);
                state.Write(writer);
            }
            foreach (var state in ScriptStates)
            {
                foreach (var body in state.Bodies)
                {
                    body.HasNext = !state.Bodies.Last().Equals(body);
                    body.Write(writer);
                }
            }
        }

        public void WriteText(StreamWriter writer, Int32 tabs = 0)
        {
            StringUtils.WriteLineTabulated(writer, $"Script({Name}) {"{"}", tabs);
            StringUtils.WriteLineTabulated(writer, $"StartState = {StartState}", tabs + 1);
            StringUtils.WriteLineTabulated(writer, $"Priority = {Priority}", tabs + 1);
            int i = 0;
            foreach (var state in ScriptStates)
            {
                state.WriteText(writer, i, tabs + 1);
                ++i;
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs + 0);
            writer.Flush();
        }

        public void ReadText(StreamReader reader)
        {
            String line = "";
            ScriptStates.Clear();
            while (!line.StartsWith("Script"))
            {
                line = reader.ReadLine().Trim();
            }
            Name = StringUtils.GetStringInBetween(line, "(", ")");
            while (!line.EndsWith("{"))
            {
                line = reader.ReadLine().Trim();
            }
            while (!line.EndsWith("}"))
            {
                line = reader.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("StartState"))
                {
                    StartState = Int32.Parse(StringUtils.GetStringAfter(line, "="));
                }
                if (line.StartsWith("Priority"))
                {
                    Priority = Byte.Parse(StringUtils.GetStringAfter(line, "="));
                }
                if (line.StartsWith("State_"))
                {
                    String arg = StringUtils.GetStringInBetween(line, "(", ")");
                    Int32 index = int.Parse(StringUtils.GetStringInBetween(line, "_", "("));
                    while (ScriptStates.Count <= index)
                    {
                        ScriptStates.Add(new PS2BehaviourState());
                    }
                    var state = ScriptStates[index];
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        state.BehaviourIndexOrSlot = -1;
                    }
                    else
                    {
                        state.BehaviourIndexOrSlot = short.Parse(StringUtils.GetStringInBetween(line, "(", ")"));
                    }
                    while (!line.EndsWith("{"))
                    {
                        line = reader.ReadLine().Trim();
                    }
                    state.ReadText(reader);

                }
            }
        }
        public override String ToString()
        {
            using MemoryStream stream = new();
            StreamWriter writer = new(stream);
            StreamReader reader = new(stream);
            WriteText(writer);
            writer.Flush();
            stream.Position = 0;
            return reader.ReadToEnd();
        }
    }
}
