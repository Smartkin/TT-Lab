using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    public class TwinBehaviourStarter : TwinBehaviourWrapper
    {
        public List<TwinBehaviourAssigner> Assigners;

        public TwinBehaviourStarter()
        {
            Assigners = new List<TwinBehaviourAssigner>();
        }

        public override void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0)
        {
            var assignerResolvers = resolver as IStarterAssignerGlobalObjectIdResolversList;
            StringUtils.WriteLineTabulated(writer, "starter {", tabs);
            var index = 0;
            foreach (var assigner in Assigners)
            {
                var assignerResolver = assignerResolvers?.ResolverAssigner(index++);
                assigner.Decompile(assignerResolver, writer, tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public override int GetLength()
        {
            return base.GetLength() + 4 + Assigners.Count * (Constants.SIZE_UINT32 + Constants.SIZE_UINT32);
        }

        public override String GetName()
        {
            return $"Behaviour Starter {id:X}";
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, length);
            var amount = reader.ReadUInt32();
            for (var i = 0; i < amount; ++i)
            {
                var assigner = new TwinBehaviourAssigner();
                assigner.Read(reader, length);
                Assigners.Add(assigner);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(Assigners.Count);
            foreach (var assigner in Assigners)
            {
                assigner.Write(writer);
            }
        }
    }
}
