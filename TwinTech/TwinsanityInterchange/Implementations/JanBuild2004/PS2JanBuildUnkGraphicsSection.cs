using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.JanBuild2004;

public class PS2JanBuildUnkGraphicsSection : BaseTwinSection
{
    public override void Read(BinaryReader reader, int length)
    {
        if (length > 0)
        {
            // ComputeHash(reader.BaseStream);
            Int64 baseOffset = reader.BaseStream.Position;
            var magicNumber = reader.ReadUInt32();
            // if ((magicNumber >> 0x10) >= 2 || (magicNumber & 0xFFFF) != GetMagicNumber())
            // {
            //     throw new Exception("Invalid section!");
            // }
            UInt32 itemsCount = reader.ReadUInt32();
            UInt32 streamLength = reader.ReadUInt32();
            Record[] records = new Record[itemsCount];
            for (int i = 0; i < itemsCount; ++i)
            {
                Record record = new Record();
                record.Read(reader, 12);
                records[i] = record;
            }
            Items.Clear();
            for (int i = 0; i < itemsCount; ++i)
            {
                ITwinItem item;
                UInt32 mapperId = ProcessId(records[i].ItemId);
                if (idToClassDictionary.ContainsKey(mapperId))
                {
                    Type type = idToClassDictionary[mapperId];
                    item = (ITwinItem)Activator.CreateInstance(type);
                }
                else
                {
                    item = (ITwinItem)Activator.CreateInstance(defaultType);
                }
                reader.BaseStream.Position = records[i].Offset + baseOffset;
                item.SetID(records[i].ItemId);
                // item.ComputeHash(reader.BaseStream, records[i].Size);
                item.Read(reader, (Int32)records[i].Size);
                Items.Add(item);
            }

            var leftOverBytes = (Int32)(length - (reader.BaseStream.Position - baseOffset));
            if (leftOverBytes < 0)
            {
                leftOverBytes = 0;
            }
            extraData = reader.ReadBytes(leftOverBytes);
        }
        else
        {
            skip = true;
        }
    }
}