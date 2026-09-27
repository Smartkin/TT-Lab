using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics
{
    public class XboxAnyModel : BaseTwinItem, ITwinModel
    {
        public List<ITwinSubModel> SubModels { get; set; }

        public XboxAnyModel()
        {
            SubModels = new();
        }

        public override Int32 GetLength()
        {
            return 4 + SubModels.Sum(s => s.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            int amount = reader.ReadInt32();
            SubModels.Clear();
            for (int i = 0; i < amount; ++i)
            {
                XboxSubModel model = new XboxSubModel();
                model.Read(reader, length);
                SubModels.Add(model);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(SubModels.Count);
            foreach (ITwinSerializable e in SubModels)
            {
                e.Write(writer);
            }
        }

        public override String GetName()
        {
            return $"Model {id:X}";
        }
    }
}
