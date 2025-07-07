using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab
{
    public class XboxBehaviourCommandPack : ITwinBehaviourCommandPack
    {
        public List<ITwinBehaviourCommand> Commands { get; set; }

        public XboxBehaviourCommandPack()
        {
            Commands = new List<ITwinBehaviourCommand>();
        }

        public int GetLength()
        {
            return 4 + Commands.Sum(com => com.GetLength());
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
            var amt = reader.ReadInt32();
            Commands.Clear();
            for (var i = 0; i < amt; ++i)
            {
                var com = new XboxBehaviourCommand();
                Commands.Add(com);
                com.Read(reader, length);
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Commands.Count);
            foreach (var com in Commands)
            {
                com.HasNext = !Commands.Last().Equals(com);
                com.Write(writer);
            }
        }
        public void WriteText(StreamWriter writer, Int32 tabs = 0)
        {
            writer.WriteLine("@Xbox Pack");
            foreach (var cmd in Commands)
            {
                cmd.WriteText(writer, tabs);
            }
        }
        public bool ReadText(StreamReader reader)
        {
            String line = reader.ReadLine().Trim();
            Debug.Assert(line == "@Xbox Pack", "Attepting to parse XBox command pack as a different version");
            while (!line.EndsWith("}"))
            {
                line = reader.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                XboxBehaviourCommand cmd = new();
                cmd.ReadText(line);
                Commands.Add(cmd);
            }
            return true;
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
    }
}
